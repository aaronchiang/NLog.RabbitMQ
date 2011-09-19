using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NLog.Common;
using RabbitMQ.Client;
using RabbitMQ.Client.Framing.v0_9_1;

namespace NLog.Targets
{
	[Target("RabbitMQ")]
	public class RabbitMQ : Target
	{
		private IConnection _Connection;
		private IModel _Model;
		private readonly Encoding _Encoding = Encoding.UTF8;
		private readonly DateTime _Epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0);
		private readonly List<Tuple<byte[], IBasicProperties, string>> _UnsentMessages
			= new List<Tuple<byte[], IBasicProperties, string>>(512);

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

		#endregion

		protected override void Write(AsyncLogEventInfo logEvent)
		{
			var basicProperties = GetBasicProperties(logEvent);
			var message = GetMessage(logEvent);
			var routingKey = string.Format(_Topic, logEvent.LogEvent.Level.Name);

			if (_Model == null)
				StartConnection();

			if (_Model == null)
			{
				_UnsentMessages.Add(Tuple.Create(message, basicProperties, routingKey));
				return;
			}

			try
			{
				CheckUnsent();
				Publish(message, basicProperties, routingKey);
			}
			catch (IOException e)
			{
				InternalLogger.Error("Could not send to RabbitMQ instance! {0}", e.ToString());
				_UnsentMessages.Add(Tuple.Create(message, basicProperties, routingKey));
				ShutdownAmqp(_Connection, new ShutdownEventArgs(ShutdownInitiator.Application, Constants.ChannelError, "Could not talk to RabbitMQ instance"));
			}
		}

		private void CheckUnsent()
		{
			var count = _UnsentMessages.Count;

			for (var i = 0; i < count; i++)
			{
				var tuple = _UnsentMessages[i];
				InternalLogger.Info("publishing unsent message: {0}.", tuple);
				Publish(tuple.Item1, tuple.Item2, tuple.Item3);
			}

			if (count > 0) 
				_UnsentMessages.Clear();
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
			return _Encoding.GetBytes(logEvent.LogEvent.FormattedMessage);
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
			StartConnection();
		}

		/// <summary>
		/// Never throws
		/// </summary>
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
				RequestedHeartbeat = 60,
				Port = Port
			};
		}


		private void ShutdownAmqp(IConnection connection, ShutdownEventArgs reason)
		{
			try
			{
				if (connection != null)
				{
					connection.ConnectionShutdown -= ShutdownAmqp;
					connection.AutoClose = true;
				}

				if (_Model != null)
				{
					_Model.Close(Constants.ReplySuccess, "closing rabbitmq appender, shutting down logging");
					_Model.Dispose();
				}
			}
			catch (Exception e)
			{
				InternalLogger.Error("could not close model", e);
			}

			_Connection = null;
			_Model = null;
		}

		protected override void CloseTarget()
		{
			base.CloseTarget();

			ShutdownAmqp(_Connection,
						 new ShutdownEventArgs(ShutdownInitiator.Application, Constants.ReplySuccess, "closing appender"));
		}
	}
}