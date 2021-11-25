# Contents
1. Architecture Diagrams
2. A Telnet Example
3. Implement Your AppServer and AppSession
4. Start SuperSocket by Configuration
5. SuperSocket Basic Configuration
6. The Built1.in Command Line Protocol
7. The Built1.in Common Format Protocol Implementation Templates
8. Implement Your Own Communication Protocol with IRequestInfo, IReceiveFilter and etc
9. Command and Command Loader
10. Get the Connected Event and Closed Event of a Connection
11. Push Data to Clients from Server Initiative
12. Extend Server Configuration
13. Command Filter
14. Connection Filter
15. Multiple Listeners
16. Multiple Server Instances
17. Implement Your Commands by Dynamic Language
18. Logging in SuperSocket
19. The Built in Flash Silverlight Policy Server in SuperSocket
20. Enable TLS/SSL trasnferring layer encryption in SuperSocket
21. Run SuperSocket in Windows Azure
22. Run SuperSocket in Linux/Unix
23. SuperSocket ServerManager
24. New Features and Breaking Changes

# 1. Architecture Diagrams
> Keywords: Architecture Diagrams, Layers Diagram, Object Model Diagram, Request Handling Diagram

## SuperSocket Layers
![image](https://raw.githubusercontent.com/kerryjiang/SuperSocket.Document/v1.6/images/layermodel.jpg)

## SuperSocket Object Model
![image](https://raw.githubusercontent.com/kerryjiang/SuperSocket.Document/v1.6/images/objectmodel.jpg)

## SuperSocket Request Handling Model
![image](https://raw.githubusercontent.com/kerryjiang/SuperSocket.Document/v1.6/images/requesthandlingmodel.jpg)

## SuperSocket Isolation Model
![image](https://raw.githubusercontent.com/kerryjiang/SuperSocket.Document/v1.6/images/isolationmodel.jpg)

# 2. A Telnet Example
> Keywords: Telnet, Console Project, References

## Create a Console project and add references of SuperSocket
1. Create a "Console Application" project. After the project is created, you should change the target framework of this project from "Client Profile" to a full framework. Because this application will run as server and the SuperSocket is not compiled with "Client Profile".
2. Add SuperSocket's dll (SuperSocket.Common.dll, SuperSocket.SocketBase.dll, SuperSocket.SocketEngine.dll) in this project's reference
3. Add log4net.dll in this project's reference, because SuerSocket uses it as default logging framework
4. Include log4net.config which is provided by SuperSocket in the project folder "Config" and set it's Build Action to be "Content" and Copy to Output Directory to be "Copy if newer", because log4net require it

![image](https://raw.githubusercontent.com/kerryjiang/SuperSocket.Document/v1.6/images/telnetproject.jpg)

## Write the Start/Stop Server Code

```c#
static void Main(string[] args)
{
	Console.WriteLine("Press any key to start the server!");

	Console.ReadKey();
	Console.WriteLine();

	var appServer = new AppServer();

	//Setup the appServer
	if (!appServer.Setup(2012)) //Setup with listening port
	{
		Console.WriteLine("Failed to setup!");
		Console.ReadKey();
		return;
	}

	Console.WriteLine();

	//Try to start the appServer
	if (!appServer.Start())
	{
		Console.WriteLine("Failed to start!");
		Console.ReadKey();
		return;
	}

	Console.WriteLine("The server started successfully, press key 'q' to stop it!");

	while (Console.ReadKey().KeyChar != 'q')
	{
		Console.WriteLine();
		continue;
	}

	//Stop the appServer
	appServer.Stop();

	Console.WriteLine("The server was stopped!");
	Console.ReadKey();
}
```

## Handle the Incomming Connection
1. Register new session connected event handler
	```c#
	appServer.NewSessionConnected += new SessionHandler<AppSession>(appServer_NewSessionConnected);
	```

2. Send a welcome message to client in the handler
	```c#
	static void appServer_NewSessionConnected(AppSession session)
	{
			session.Send("Welcome to SuperSocket Telnet Server");
	}
	```

3. Test by telnet client
	1. open a telnet client
	2. type "telnet localhost 2012" ending with an "ENTER"
	3. you will get the message "Welcome to SuperSocket Telnet Server"

## Process Requests
1. Register request handler
	```c#
	appServer.NewRequestReceived += new RequestHandler<AppSession, StringRequestInfo>(appServer_NewRequestReceived);
	```

2. Implement request handler
	```c#
	static void appServer_NewRequestReceived(AppSession session, StringRequestInfo requestInfo)
	{
		switch (requestInfo.Key.ToUpper())
		{
			case("ECHO"):
					session.Send(requestInfo.Body);
					break;

			case ("ADD"):
				session.Send(requestInfo.Parameters.Select(p => Convert.ToInt32(p)).Sum().ToString());
				break;

			case ("MULT"):

				var result = 1;

				foreach (var factor in requestInfo.Parameters.Select(p => Convert.ToInt32(p)))
				{
					result *= factor;
				}

				session.Send(result.ToString());
				break;
		}
	}
	```
	- requestInfo.Key is the request command line's first segment delimited by space,  requestInfo.Parameters is the left segments delimited by space

3. Test by telnet client
- You can open a telnet client to verify the above code.
- After you connect the server,  
you can interact with server in this way  
(the message after "C:" stands for client's request,  
the message after "S:" stands for server's response):
	```
	C: ECHO ABCDEF
	S: ABCDEF
	C: ADD 1 2
	S: 3
	C: ADD 250 250
	S: 500
	C: MULT 2 8
	S: 16
	C: MULT 125 2
	S: 250
	```

## Usage of Command
- In the previous part, you have seen how to deal the client's request in SuperSocket. But at the meanwhile you probably have found a problem, if you have a complex business logic in your server, the switch case would be long and urgly and actually it doens't confront with the OOD. In this case, SuperSocket provides a command framework which allow define independ classes to deal the defferent kind requests.

- For a instance, you can define a class named "ADD" to process the requests with the requestInfo's key equals "ADD":
	```c#
	public class ADD : CommandBase<AppSession, StringRequestInfo>
	{
		public override void ExecuteCommand(AppSession session, StringRequestInfo requestInfo)
		{
			session.Send(requestInfo.Parameters.Select(p => Convert.ToInt32(p)).Sum().ToString());
		}
	}
	```

- and define a class named "MULT" to process the requests with the requestInfo's key equals "MULT":
	```c#
	public class MULT : CommandBase<AppSession, StringRequestInfo>
	{
		public override void ExecuteCommand(AppSession session, StringRequestInfo requestInfo)
		{
			var result = 1;

			foreach (var factor in requestInfo.Parameters.Select(p => Convert.ToInt32(p)))
			{
					result *= factor;
			}

			session.Send(result.ToString());
		}
	}
	```

- at the same time, you also need to remove the defined reqauest handler because request handler and command cannot work together:
	```c#
	//Remove this line
	appServer.NewRequestReceived += new RequestHandler<AppSession, StringRequestInfo>(appServer_NewRequestReceived);
	```

# 3. Implement Your AppServer and AppSession
> Keywords: AppServer, AppSession

## What is AppSession?
- AppSession represents a logic socket connection, connection based operations should be defined in this class. You can use the instance of this class to send data to tcp clients, receive data from connection or close the connection.

## What is AppServer?
- AppServer stands for the server instance which listens all clients' connections, hosts all tcp connections. Ideally, we can get any session which we want to find from the AppServer. Application level operations and logics should be defined in it.

## Create your AppSession
1. You can override base AppSession's operations
	```c#
	public class TelnetSession : AppSession<TelnetSession>
	{
		protected override void OnSessionStarted()
		{
			this.Send("Welcome to SuperSocket Telnet Server");
		}

		protected override void HandleUnknownRequest(StringRequestInfo requestInfo)
		{
			this.Send("Unknow request");
		}

		protected override void HandleException(Exception e)
		{
			this.Send("Application error: {0}", e.Message);
		}

		protected override void OnSessionClosed(CloseReason reason)
		{
			//add you logics which will be executed after the session is closed
			base.OnSessionClosed(reason);
		}
	}
	```
	- In above code, the server send the welcome message to the client immediately after the session is connected. The code also overrided the other methods of AppSession to process it's own logic.

2. You can add new properties for your session according your business requirement Let me create a AppSession which would be used in a game server:
	```c#
	public class PlayerSession ：AppSession<PlayerSession>
	{
		public int GameHallId { get; internal set; }

		public int RoomId { get; internal set; }
	}
	```

3. Relationship with Commands
	
	- In the first document, we talked about commands, now we revise this point here:
		```c#
		public class ECHO : CommandBase<AppSession, StringRequestInfo>
		{
			public override void ExecuteCommand(AppSession session, StringRequestInfo requestInfo)
			{
					session.Send(requestInfo.Body);
			}
		}
		```

	- In the command's code, you should have found the parent class pf ECHO is CommandBase<AppSession, StringRequestInfo>, which has a generic type parameter AppSession. Yes, it's the default AppSession. If you want to use your new AppSession, please pass your AppSession type as the parameter, or the server cannot discover the command:
		```c#
		public class ECHO : CommandBase<PlayerSession, StringRequestInfo>
		{
			public override void ExecuteCommand(PlayerSession session, StringRequestInfo requestInfo)
			{
					session.Send(requestInfo.Body);
			}
		}
		```

## Create your AppServer
1. Work with Session If you want to make available your AppSession, you must alter your AppServer to use it:
	```c#
	public class TelnetServer : AppServer<TelnetSession>
	{

	}
	```
	- Then the session TelnetSession will can be used in TelnetServer.

2. There are also many protected methods you can override
	```c#
	public class TelnetServer : AppServer<TelnetSession>
	{
			protected override bool Setup(IRootConfig rootConfig, IServerConfig config)
			{
					return base.Setup(rootConfig, config);
			}

			protected override void OnStartup()
			{
					base.OnStartup();
			}

			protected override void OnStopped()
			{
					base.OnStopped();
			}
	}
	```

## Benifits
- Imeplement your AppSession and AppServer allow you extend SuperSocket as your business requirement, you can hook session's connected and closed event, server instance's startup and stopped event. You can read your own customized configuration in AppServer's Setup() method. In summary it give you lots of abilities to build a socket server which is exact what you want very easily.

# 4. Start SuperSocket by Configuration
> Keywords: Start by Configuration, Configuration, Bootstrap, Windows Service

## Why Start Server by Configuration
1. Avoid hard coding
2. SuperSocket provides lots of useful configuration options
3. Take full use of the tools of SuperSocket

## How to Start Server by Configuration with Bootstrap
- SuperSocket configuration section SuperSocket uses .NET default configuration technology, a configuration section designed for SuperSocket:
	```xml
	<configSections>
			<section name="superSocket"
					type="SuperSocket.SocketEngine.Configuration.SocketServiceConfig, SuperSocket.SocketEngine" />
	</configSections>
	```

- Server instance configuration
	```xml
	<superSocket>
			<servers>
				<server name="TelnetServer"
						serverType="SuperSocket.QuickStart.TelnetServer_StartByConfig.TelnetServer, SuperSocket.QuickStart.TelnetServer_StartByConfig"
						ip="Any" port="2020">
				</server>
			</servers>
	</superSocket>
	```
	- Now, I explain the configuration of server node here:
		```
		name: the name of appServer instance
		serverType: the full name of the AppServer your want to run
		ip: listen ip
		port: listen port
		```
	- We'll have the full introduction about the configuration in next document.

- Start SuperSocket using BootStrap
	```c#
	static void Main(string[] args)
	{
			Console.WriteLine("Press any key to start the server!");

			Console.ReadKey();
			Console.WriteLine();

			var bootstrap = BootstrapFactory.CreateBootstrap();

			if (!bootstrap.Initialize())
			{
					Console.WriteLine("Failed to initialize!");
					Console.ReadKey();
					return;
			}

			var result = bootstrap.Start();

			Console.WriteLine("Start result: {0}!", result);

			if (result == StartResult.Failed)
			{
					Console.WriteLine("Failed to start!");
					Console.ReadKey();
					return;
			}

			Console.WriteLine("Press key 'q' to stop it!");

			while (Console.ReadKey().KeyChar != 'q')
			{
					Console.WriteLine();
					continue;
			}

			Console.WriteLine();

			//Stop the appServer
			bootstrap.Stop();

			Console.WriteLine("The server was stopped!");
			Console.ReadKey();
	}
	```
- Some configurations samples
- Server types nodes:
	```xml
	<superSocket>
			<servers>
				<server name="TelnetServer"
						serverTypeName="TelnetServer"
						ip="Any" port="2020">
				</server>
			</servers>
			<serverTypes>
					<add name="TelnetServer" type="SuperSocket.QuickStart.TelnetServer_StartByConfig.TelnetServer, SuperSocket.QuickStart.TelnetServer_StartByConfig"/>
			</serverTypes>
	</superSocket>
	```

- Multiple server instances:
	```xml
	<superSocket>
			<servers>
				<server name="TelnetServerA"
						serverTypeName="TelnetServer"
						ip="Any" port="2020">
				</server>
				<server name="TelnetServerB"
						serverTypeName="TelnetServer"
						ip="Any" port="2021">
				</server>
			</servers>
			<serverTypes>
					<add name="TelnetServer" type="SuperSocket.QuickStart.TelnetServer_StartByConfig.TelnetServer, SuperSocket.QuickStart.TelnetServer_StartByConfig"/>
			</serverTypes>
	</superSocket>
	```

## SuperSocket.SocketService.exe, the Running Container provided by SuperSocket
	- Use SuperSocket.SocketService.exe directly
	- make sure all assemblies required by your server are in the same directory with SuperSocket.SocketService.exe
	- put your SuperSocket configuration node in the file SuperSocket.SocketService.exe.config
	run "SuperSocket.SocketService.exe" directly, your defined server will run
	- Install SuperSocket.SocketService.exe as windows service

- You can install SuperSocket.SocketService.exe as windows service by running it with an extra command line parameter "-i":
	```		
	SuperSocket.SocketService.exe -i
	```
- The windows service name is defined in configuration file, you can change it as your requirement:
	```xml
	<appSettings>
			<add key="ServiceName" value="SuperSocketService" />
	</appSettings>
	```

- The service also can be uninstalled by the parameter "-u":
	```
	SuperSocket.SocketService.exe -u
	```

# 5. SuperSocket Basic Configuration
> Keywords: Basic Configuration, Configuration Documentation
## A Sample Configuration
```xml
<?xml version="1.0"?>
<configuration>
		<configSections>
				<section name="superSocket"
								type="SuperSocket.SocketEngine.Configuration.SocketServiceConfig, SuperSocket.SocketEngine" />
		</configSections>
		<appSettings>
				<add key="ServiceName" value="SupperSocketService" />
		</appSettings>
		<superSocket>
				<servers>
						<server name="TelnetServerA"
										serverTypeName="TelnetServer"
										ip="Any"
										port="2020">
						</server>
						<server name="TelnetServerB"
										serverTypeName="TelnetServer"
										ip="Any"
										port="2021">
						</server>
				</servers>
				<serverTypes>
						<add name="TelnetServer"
								type="SuperSocket.QuickStart.TelnetServer_StartByConfig.TelnetServer, SuperSocket.QuickStart.TelnetServer_StartByConfig"/>
				</serverTypes>
		</superSocket>
		<startup>
				<supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.0" />
		</startup>
</configuration>
```	

## Root Configuration
- The configuration node "superSocket" is the root of the SuperSocket configuration, it defines the global parameters of SuperSocket requires. Let me explain all the attributes of the root node:
	- maxWorkingThreads: maximum working threads count of .NET thread pool;
	- minWorkingThreads: minimum working threads count of .NET thread pool;
	- maxCompletionPortThreads: maximum completion threads count of .NET thread pool;
	- minCompletionPortThreads: minimum completion threads count of .NET thread pool;
	- disablePerformanceDataCollector: whether disable performance data collector;
	- performanceDataCollectInterval: performance data collecting interval (in seconds, default value: 60);
	- isolation: SuperSocket instances isolation level
		- None - no isolation
		- AppDomain - server instances will be isolated by AppDomains
		- Process - server instances will be isolated by processes
	- logFactory: the name of default logFactory, all log factories are defined in the child node "logFactories" which will be introduced in following documentation;
	- defaultCulture: default thread culture for the global application, only available in .Net 4.5;

## Servers Configuration
- In the root configuration node, there is child node named "servers", you can define one or many server configuration nodes in it which represent app server instances. The server instances can be same AppServer type, also can be different AppServer types. All server node's attributes:
	- name: the name of the server instance;
	- serverType: the full name the AppServer's type which you want to run;
	- serverTypeName: the name of the selected server types, all server types should be defined in serverTypes node which will be introduced in following documentation;
	- ip: the ip of the server instance listens. You can set an exact ip, you also can set the below values Any - all IPv4 address IPv6Any - all IPv6 address
	- port: the port of the server instance listens;
	- listenBacklog: the listen back log size;
	- mode: the socket server's running mode, Tcp (default) or Udp;
	- disabled: whether the server instance is disabled;
	- startupOrder: the server instance start order, the bootstrap will start all server instances order by this value;
	- sendTimeOut: sending data timeout;
	- sendingQueueSize: the sending queue's maximum size, the default value is 5;
	- maxConnectionNumber: maximum connection number the server instance allow to connect at the same time;
	- receiveBufferSize: receiving buffer size;
	- sendBufferSize: sending buffer size;
	- syncSend: sending data in sync mode, default value: false;
	- logCommand: whether log command execution record;
	- logBasicSessionActivity: whether log the session's basic activities like connected and closed;
	- clearIdleSession: true or false, whether clear idle sessions, default value is false;
	- clearIdleSessionInterval: the clearing timeout idle session interval, default value is 120, in seconds;
	- idleSessionTimeOut: The session timeout period; If the session's idle time exceeds the value, it will be closed in case of clearIdleSession is configured to be true; Default value is 300, in seconds;
	- security: Empty, Tls, Ssl3. The security option of the socket server, default value is empty;
	- maxRequestLength: The maximum allowed request length, default value is 1024;
	- textEncoding: The default text encoding in the server instance, default value is ASCII;
	- defaultCulture: default thread culture for this appserver instance, only available in .Net 4.5 and cannot be set if the isolation model is 'None';
	- disableSessionSnapshot: Indicate whether disable session snapshot, default value is false.
	- sessionSnapshotInterval: The interval of taking session snapshot, default value is 5, in seconds;
	- keepAliveTime: The interval of keeping alive, default value is 600, in seconds;
	- keepAliveInterval: The interval of retry after keep alive fail, default value is 60, in seconds;
	- certificate: it is a configuration element for X509Certificate which will be used in this server instance
	
		there are two usage:

		- one is load certificate from cert file
			```xml
			<certificate filePath="localhost.pfx" password="supersocket" />
			```
		- another one is load certificate from local certificate storage
			```xml
			<certificate storeName="My" storeLocation="LocalMachine" thumbprint="f42585bceed2cb049ef4a3c6d0ad572a6699f6f3"/>
			```
	- connectionFilter: the name of the connection filter you want to use for this server instance, multiple filters should be delimited by ',' or ';'. Connection filters should be defined in a child nodes of root node which will be introduced in the following documentation;
	- commandLoader: the name of the command loader you want to use for this server instance, multiple loaders should be delimited by ',' or ';'. Command loaders should be defined in a child nodes of root node which will be introduced in the following documentation;
	- logFactory: the log factory you want to use for this server instance. If you don't set it, the log factory defined in root configuration will be used;
	- listeners: it is an configuration element which is designed for supporting multiple listening ip/port pair in one server instance. The listeners node should contains one or more child nodes of listener whose attributes defined like below:
		```
		ip: the listening ip;
		port: the listening port;
		backlog: the listening back log size;
		security: the security mode (None/Default/Tls/Ssl/...);
		```

		for examples:
		```xml
		<server name="EchoServer" serverTypeName="EchoService">
			<listeners>
				<add ip="Any" port="2012" />
				<add ip="IPv6Any" port="2012" />
			</listeners>
		</server>
		```
	- receiveFilterFactory: the name of the receive filter factory you want to use it for this server instance;

## Server Types Configuration
- Server types node is a collection configuration node under the root. You are able to add one/more elements with element name "add" and attributes "name" and "type":
	```xml
	<serverTypes>
			<add name="TelnetServerType"
					type="SuperSocket.QuickStart.TelnetServer_StartByConfig.TelnetServer, SuperSocket.QuickStart.TelnetServer_StartByConfig"/>
	</serverTypes>
	```
- Because of the defined server type's name is "TelnetServerType", you can set the config attribute "serverTypeName" of the server instances you want to run as this type to be "TelnetServerType":
	```xml
	<server name="TelnetServerA"
					serverTypeName="TelnetServerType"
					ip="Any"
					port="2020">
	</server>
	```

## Log Factories Configuration
- Same as server type configuration, you also can define one or more log factories and use it in server, the only one difference is the log factory also can be set in root configuration:
	```xml
	<logFactories>
		<add name="ConsoleLogFactory"
				type="SuperSocket.SocketBase.Logging.ConsoleLogFactory, SuperSocket.SocketBase" />
	</logFactories>
	```
- Use it in root configuration:
	```xml
	<superSocket logFactory="ConsoleLogFactory">
			...
			...
	</superSocket>
	```
- Use it in server node:
	```xml
	<server name="TelnetServerA"
				logFactory="ConsoleLogFactory"
				ip="Any"
				port="2020">
	</server>
	```

## Configuration Intellisense
- SuperSocket provides online XSD (XML Schema Document) file to help your configuration. You just need to add 3 extra lines in your SuperSocket configuration section:
	```xml
	<superSocket xmlns="http://schema.supersocket.net/supersocket"
							xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
							xsi:schemaLocation="http://schema.supersocket.net/supersocket http://schema.supersocket.net/v1-6/supersocket.xsd">
		<!---->
	</superSocket>
	```
- Then you will get the intelligent auto completion function when you update the configuration:
![image](https://raw.githubusercontent.com/kerryjiang/SuperSocket.Document/v1.6/images/configinteli.jpg)

## SuperSocket Windows Service Configuration
- As you know, SuperSocket provides a running container "SuperSocket.SocketService.exe" which can run as a windows service.

- You can define the windows service's name by configuring:
	```xml
	<appSettings>
			<add key="ServiceName" value="SuperSocketService" />
	</appSettings>
	```

- There are some other configuration attributes for the windows service:
	```
	- ServiceDescription: the description of this windows service
	- ServicesDependedOn: the other windows service which this service depends on; this windows service will start after the depended windows service; multiple depended service should be separated by character ',' or ';'
	```

# 6. The Built-in Command Line Protocol
> Keywords: Command Line, Protocol, StringRequestInfo, Text Encoding






































































































# 7. The Built-in Common Format Protocol Implementation Templates
# 8. Implement Your Own Communication Protocol with IRequestInfo, IReceiveFilter and etc
# 9. Command and Command Loader
# 10. Get the Connected Event and Closed Event of a Connection
# 11. Push Data to Clients from Server Initiative
# 12. Extend Server Configuration
# 13. Command Filter
# 14. Connection Filter
# 15. Multiple Listeners
# 16. Multiple Server Instances
# 17. Implement Your Commands by Dynamic Language
# 18. Logging in SuperSocket
# 19. The Built in Flash Silverlight Policy Server in SuperSocket
# 20. Enable TLS/SSL trasnferring layer encryption in SuperSocket
# 21. Run SuperSocket in Windows Azure
# 22. Run SuperSocket in Linux/Unix
# 23. SuperSocket ServerManager
# 24. New Features and Breaking Changes