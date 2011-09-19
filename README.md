# RabbitMQ target for NLog

**Configuration**:

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
		<!-- when http://nlog.codeplex.com/workitem/6491 is fixed, then xsi:type="haf:RabbitMQ" instead -->
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
				maxBuffer="2"
				heartBeatSeconds="3"
				/>
	</targets>

	<rules>
		<logger name="*" minlevel="Trace" writeTo="RabbitMQTarget"/>
	</rules>

</nlog>
```

**Important - shutting it down!**

Because NLog doesn't expose a single method for shutting everything down (but loads automatically by static properties - the loggers' first invocation to the framework) - you need to add this code to the exit of your application!

```csharp
var allTargets = LogManager.Configuration.AllTargets;

foreach (var target in allTargets)
	target.Dispose();
```

For an example of how to do this with WPF see the demo.