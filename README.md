# RabbitMQ target for NLog

**Configuration**:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<!-- 
  This file needs to be put in the application directory. Make sure to set 
  'Copy to Output Directory' option in Visual Studio.
  -->
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">

	<extensions>
		<add assembly="NLog.Targets.RabbitMQ" />
	</extensions>

	<targets>
		<target name="rmq" type="RabbitMQ" 
				username="guest" 
				password="guest" 
				hostname="localhost" 
				exchange="app-logging"
				port="5672"
				topic="{0}"
				vhost="/"
				appid="NLog.RabbitMQ.DemoApp"
				/>
	</targets>

	<rules>
		<logger name="*" minLevel="Trace" appendTo="rmq"/>
	</rules>

</nlog>
```