# RabbitMQ target for NLog

The RabbitMQ target writes asynchronously to a RabbitMQ instance.

##Configuration:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
	  xmlns:haf="https://github.com/haf/NLog.RabbitMQ/tree/master/src/schemas/NLog.RabbitMQ.xsd"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
	  internalLogToConsole="true">

	<extensions>
		<add assembly="NLog.Targets.RabbitMQ" />
	</extensions>

	<targets>
		<!-- when http://nlog.codeplex.com/workitem/6491 is fixed, then xsi:type="haf:RabbitMQ" instead;
			 these are the defaults (except 'topic' and 'appid'): 
		-->
		<target name="RabbitMQTarget"
				xsi:type="RabbitMQ"
				username="guest" 
				password="guest" 
				hostname="localhost" 
				exchange="app-logging"
				port="5672"
				topic="DemoApp.Logging.{0}"
				vhost="/"
				appid="NLog.RabbitMQ.DemoApp"
				maxBuffer="10240"
				heartBeatSeconds="3"
				layout="${longdate}|${level:uppercase=true}|${logger}|${message}"
				/>
	</targets>

	<rules>
		<logger name="*" minlevel="Trace" writeTo="RabbitMQTarget"/>
	</rules>

</nlog>
```

**Recommendation - async wrapper target**

Make the targets tag look like this: `<targets async="true"> ... </targets>` so that
a failure of communication with RabbitMQ doesn't slow the application down. With this configuration
an overloaded message broker will have 10000 messages buffered in the logging application
before messages start being discarded. A downed message broker will have its messages
in the *inner* target (i.e. RabbitMQ-target), not in the async buffer (as the RabbitMQ-target
will not block which is what AsyncWrapperTarget buffers upon).

##Important - shutting it down!

Because NLog doesn't expose a single method for shutting everything down (but loads automatically by static properties - the loggers' first invocation to the framework) - you need to add this code to the exit of your application!

```csharp
var allTargets = LogManager.Configuration.AllTargets;

foreach (var target in allTargets)
	target.Dispose();
```

For an example of how to do this with WPF see the demo.

##Configuration schema

See https://github.com/haf/NLog.RabbitMQ/blob/master/src/schemas/NLog.RabbitMQ.xsd