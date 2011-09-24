using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NLog.Common;
using RabbitMQ.Client;
using RabbitMQ.Client.Framing.v0_9_1;

namespace NLog.Targets
{
	/// <summary>
	/// A RabbitMQ-target for NLog. See https://github.com/haf/NLog.RabbitMQ for documentation in Readme.md.
	/// </summary>
	[Target("RabbitMQ")]
	public class RabbitMQ : TargetWithLayout
	{
		private IConnection _Connection;
		private IModel _Model;
		private readonly Encoding _Encoding = Encoding.UTF8;
		private readonly DateTime _Epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0);
		private readonly Queue<Tuple<byte[], IBasicProperties, string>> _UnsentMessages
			= new Queue<Tuple<byte[], IBasicProperties, string>>(512);

		#region Properties

		private string _VHost = "/";

		/// <summary>
		/// 	Gets or sets the virtual host to publish to.
		/// </summary>
		public string VHost
		{
			get { return _VHost; }
			set { if (value != null) _VHost = value; }
		}

		private string _UserName = "guest";

		/// <summary>
		/// 	Gets or sets the username to use for
		/// 	authentication with the message broker. The default
		/// 	is 'guest'
		/// </summary>
		public string UserName
		{
			get { return _UserName; }
			set { _UserName = value; }
		}

		private string _Password = "guest";

		/// <summary>
		/// 	Gets or sets the password to use for
		/// 	authentication with the message broker.
		/// 	The default is 'guest'
		/// </summary>
		public string Password
		{
			get { return _Password; }
			set { _Password = value; }
		}

		private ushort _Port = 5672;

		/// <summary>
		/// 	Gets or sets the port to use
		/// 	for connections to the message broker (this is the broker's
		/// 	listening port).
		/// 	The default is '5672'.
		/// </summary>
		public ushort Port
		{
			get { return _Port; }
			set { _Port = value; }
		}

		private string _Topic = "{0}";

		///<summary>
		///	Gets or sets the routing key (aka. topic) with which
		///	to send messages. Defaults to {0}, which in the end is 'error' for log.Error("..."), and
		///	so on. An example could be setting this property to 'ApplicationType.MyApp.Web.{0}'.
		///	The default is '{0}'.
		///</summary>
		public string Topic
		{
			get { return _Topic; }
			set { _Topic = value; }
		}

		private IProtocol _Protocol = Protocols.DefaultProtocol;

		/// <summary>
		/// 	Gets or sets the AMQP protocol (version) to use
		/// 	for communications with the RabbitMQ broker. The default 
		/// 	is the RabbitMQ.Client-library's default protocol.
		/// </summary>
		public IProtocol Protocol
		{
			get { return _Protocol; }
			set { if (value != null) _Protocol = value; }
		}

		private string _HostName = "localhost";

		/// <summary>
		/// 	Gets or sets the host name of the broker to log to.
		/// </summary>
		/// <remarks>
		/// 	Default is 'localhost'
		/// </remarks>
		public string HostName
		{
			get { return _HostName; }
			set { if (value != null) _HostName = value; }
		}

		private string _Exchange = "app-logging";

		/// <summary>
		/// 	Gets or sets the exchange to bind the logger output to.
		/// </summary>
		/// <remarks>
		/// 	Default is 'log4net-logging'
		/// </remarks>
		public string Exchange
		{
			get { return _Exchange; }
			set { if (value != null) _Exchange = value; }
		}

		/// <summary>
		/// 	Gets or sets the application id to specify when sending. Defaults to null,
		/// 	and then IBasicProperties.AppId will be the name of the logger instead.
		/// </summary>
		public string AppId { get; set; }

		private int _MaxBuffer = 10240;

		/// <summary>
		/// Gets or sets the maximum number of messages to save in the case
		/// that the RabbitMQ instance goes down. Must be >= 1. Defaults to 10240.
		/// </summary>
		public int MaxBuffer
		{
			get { return _MaxBuffer; }
			set { if (value > 0) _MaxBuffer = value; }
		}

		private ushort _heartBeatSeconds = 3;

		/// <summary>
		/// Gets or sets the number of heartbeat seconds to have for the RabbitMQ connection.
		/// If the heartbeat times out, then the connection is closed (logically) and then
		/// re-opened the next time a log message comes along.
		/// </summary>
		public ushort HeartBeatSeconds
		{
			get { return _heartBeatSeconds; }
			set {  _heartBeatSeconds = value; }
		}


		#endregion

		protected override void Write(AsyncLogEventInfo logEvent)
		{
			var basicProperties = GetBasicProperties(logEvent);
			var message = GetMessage(logEvent);
			var routingKey = string.Format(_Topic, logEvent.LogEvent.Level.Name);

			if (_Model == null || !_Model.IsOpen)
				StartConnection();

			if (_Model == null || !_Model.IsOpen)
			{
				AddUnsent(routingKey, basicProperties, message);
				return;
			}

			try
			{
				CheckUnsent();
				Publish(message, basicProperties, routingKey);
				return;
			}
			catch (IOException e)
			{
				InternalLogger.Error("Could not send to RabbitMQ instance! {0}", e.ToString());
			}
			catch (ObjectDisposedException e)
			{
				InternalLogger.Error("Could not write data to the network stream! {0}", e.ToString());
			}

			AddUnsent(routingKey, basicProperties, message);
			ShutdownAmqp(_Connection, new ShutdownEventArgs(ShutdownInitiator.Application,
				Constants.ChannelError, "Could not talk to RabbitMQ instance"));

		}

		private void AddUnsent(string routingKey, IBasicProperties basicProperties, byte[] message)
		{
			if (_UnsentMessages.Count < _MaxBuffer)
				_UnsentMessages.Enqueue(Tuple.Create(message, basicProperties, routingKey));
			else
				InternalLogger.Warn("MaxBuffer {0} filled. Ignoring message.", _MaxBuffer);
		}

		private void CheckUnsent()
		{
			// using a queue so that removing and publishing is a single operation
			while (_UnsentMessages.Count > 0)
			{
				var tuple = _UnsentMessages.Dequeue();
				InternalLogger.Info("publishing unsent message: {0}.", tuple);
				Publish(tuple.Item1, tuple.Item2, tuple.Item3);
			}
		}

		private void Publish(byte[] bytes, IBasicProperties basicProperties, string routingKey)
		{
			_Model.BasicPublish(_Exchange,
			                    routingKey,
			                    true, false, basicProperties,
			                    bytes);
		}

		private byte[] GetMessage(AsyncLogEventInfo logEvent)
		{
			return _Encoding.GetBytes(Layout.Render(logEvent.LogEvent));
		}


		private IBasicProperties GetBasicProperties(AsyncLogEventInfo loggingEvent)
		{
			var @event = loggingEvent.LogEvent;
			
			var basicProperties = new BasicProperties();
			basicProperties.ContentEncoding = "utf8";
			basicProperties.ContentType = "text/plain";
			basicProperties.AppId = AppId ?? @event.LoggerName;

			basicProperties.Timestamp = new AmqpTimestamp(
				Convert.ToInt64((@event.TimeStamp - _Epoch).TotalSeconds));

			// support Validated User-ID (see http://www.rabbitmq.com/extensions.html)
			basicProperties.UserId = UserName;

			return basicProperties;
		}

		protected override void InitializeTarget()
		{
			base.InitializeTarget();

			StartConnection();
		}

		/// <summary>
		/// Never throws
		/// </summary>
		[MethodImpl(MethodImplOptions.Synchronized)]
		private void StartConnection()
		{
			try
			{
				_Connection = GetConnectionFac().CreateConnection();
				_Connection.ConnectionShutdown += ShutdownAmqp;

				try { _Model = _Connection.CreateModel(); }
				catch (Exception e)
				{
					InternalLogger.Error("could not create model", e);
				}

				if (_Model != null)
					_Model.ExchangeDeclare(_Exchange, ExchangeType.Topic);
			}
			catch (Exception e)
			{
				InternalLogger.Error("could not connect to Rabbit instance", e);
			}
		}

		private ConnectionFactory GetConnectionFac()
		{
			return new ConnectionFactory
			{
				HostName = HostName,
				VirtualHost = VHost,
				UserName = UserName,
				Password = Password,
				RequestedHeartbeat = HeartBeatSeconds,
				Port = Port
			};
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		private void ShutdownAmqp(IConnection connection, ShutdownEventArgs reason)
		{
			// I can't make this NOT hang when RMQ goes down
			// and then a log message is sent...

			try
			{
				if (_Model != null && _Model.IsOpen 
					&& reason.ReplyCode != Constants.ChannelError
					&& reason.ReplyCode != Constants.ConnectionForced)
					_Model.Abort(); //_Model.Close();
			}
			catch (Exception e)
			{
				InternalLogger.Error("could not close model", e);
			}

			try
			{
				if (connection != null && connection.IsOpen)
				{
					connection.ConnectionShutdown -= ShutdownAmqp;
					connection.Close(reason.ReplyCode, reason.ReplyText, 1000);
					connection.Abort(1000); // you get 2 seconds to shut down!
				}
			}
			catch (Exception e)
			{
				InternalLogger.Error("could not close connection", e);
			}
		}

		// Dispose calls CloseTarget!

		protected override void CloseTarget()
		{
			ShutdownAmqp(_Connection,
			             new ShutdownEventArgs(ShutdownInitiator.Application, Constants.ReplySuccess, "closing appender"));
			
			base.CloseTarget();
		}
	}
}