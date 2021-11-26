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
## What's the Protocol?
What's the Protocol? Lots of people probably will answer "TCP" or "UDP". But to build a network application, only TCP or UDP is not enough. TCP and UDP are transport-layer protocols. It's far from enough to enable talking between two endpoints in the network if you only define transport-layer protocol. You need to define your application level protocol to convert your received binary data to the requests which your application can understand.

## The Built-in Command Line Protocol
- The command line protocol is a widely used protocols, lots of protocols like Telnet, SMTP, POP3 and FTP protocols are base on command line protocol etc. If you do not have a custom protocol, then SuperSocket will use command line protocol by default, which can simplify the development of this kind of protocols.

- The command line protocol defines each request must be ended with a carriage return "\r\n".

- If you use the command line protocol in SuperSocket, all requests will to translated into StringRequestInfo instances.

- StringRequestInfo is defined like this:
	```c#
	public class StringRequestInfo
	{
			public string Key { get; }

			public string Body { get; }

			public string[] Parameters { get; }

			/*
			Other properties and methods
			*/
	}
	```

- Because the built-in command line protocol in SuperSocket uses a space to split request key and parameters, So when the client sends the data below to the server:
	```
	"LOGIN kerry 123456" + NewLine
	```
- the SuperSocket server will receive a StringRequestInfo instance, the properties of the request info instance will be:
	```
	Key: "LOGIN"
	Body: "kerry 123456";
	Parameters: ["kerry", "123456"]
	```

- If you have defined a Command with name "LOGIN", the command's ExecuteCommand method will be excuted with the StringRequestInfo instance as parameter:
	```c#
	public class LOGIN : CommandBase<AppSession, StringRequestInfo>
	{
			public override void ExecuteCommand(AppSession session, StringRequestInfo requestInfo)
			{
					//Implement your business logic
			}
	}
	```

## Customize the Command Line Protocol
- Some users might have different request format, for instance:
	```
	"LOGIN:kerry,12345" + NewLine
	```

- The request's key is separated with body by the char ':', and the parameters are separated by the char ','. This kind of request can be supported easily, just extend the command line protocol like the below code:
	```c#
	public class YourServer : AppServer<YourSession>
	{
			public YourServer()
					: base(new CommandLineReceiveFilterFactory(Encoding.Default, new BasicRequestInfoParser(":", ",")))
			{

			}
	}
	```

- If you want to customize the request format much deeper, you can implement a RequestInfoParser class base the interface IRequestInfoParser, and then pass in your own RequestInfoParser instance when instantiate the CommandLineReceiveFilterFactory instance:
	```c#
	public class YourServer : AppServer<YourSession>
	{
			public YourServer()
					: base(new CommandLineReceiveFilterFactory(Encoding.Default, new YourRequestInfoParser()))
			{

			}
	}
	```

## Text Encoding
- The default encoding of the command line protocol is Ascii, but you can change it in the configuration by setting the "textEncoding" attribute of the server node:
	```xml
	<server name="TelnetServer"
				textEncoding="UTF-8"
				serverType="YourAppServer, YourAssembly"
				ip="Any" port="2020">
	</server>
	```

# 7. The Built-in Common Format Protocol Implementation Templates
> Keywords: Protocol Tools, Custom Protocol, TerminatorReceiveFilter, CountSpliterReceiveFilter, FixedSizeReceiveFilter, BeginEndMarkReceiveFilter, FixedHeaderReceiveFilter

- After reading the previous document, you probably find implementing your own protocol using SuperSocket is not easy for you. To make this job easier, SuperSocket provides some common protocol tools, which you can use to build your own protocol easily and fast:

	- TerminatorReceiveFilter (SuperSocket.SocketBase.Protocol.TerminatorReceiveFilter, SuperSocket.SocketBase)
	- CountSpliterReceiveFilter (SuperSocket.Facility.Protocol.CountSpliterReceiveFilter, SuperSocket.Facility)
	- FixedSizeReceiveFilter (SuperSocket.Facility.Protocol.FixedSizeReceiveFilter, SuperSocket.Facility)
	- BeginEndMarkReceiveFilter (SuperSocket.Facility.Protocol.BeginEndMarkReceiveFilter, SuperSocket.Facility)
	- FixedHeaderReceiveFilter (SuperSocket.Facility.Protocol.FixedHeaderReceiveFilter, SuperSocket.Facility)

## TerminatorReceiveFilter - Terminator Protocol
- Similar with command line protocol, some protocols use a terminator to identify a request. For example, one protocol uses two chars "##" as terminator, then you can use the class "TerminatorReceiveFilterFactory":
	```c#
	/// <summary>
	/// TerminatorProtocolServer
	/// Each request end with the terminator "##"
	/// ECHO Your message##
	/// </summary>
	public class TerminatorProtocolServer : AppServer
	{
			public TerminatorProtocolServer()
					: base(new TerminatorReceiveFilterFactory("##"))
			{

			}
	}

	```

- The default RequestInfo is StringRequestInfo, you also can create your own RequestInfo class, but it requires a bit more work:

- Implement your ReceiveFilter base on TerminatorReceiveFilter:
	```c#
	public class YourReceiveFilter : TerminatorReceiveFilter<YourRequestInfo>
	{
			//More code
	}
	```

- Implement your ReceiveFilterFactory which can create your request filter instances:
	```c#
	public class YourReceiveFilterFactory : IReceiveFilterFactory<YourRequestInfo>
	{
			//More code
	}
	```
- And then use the request filter factory in your AppServer.

## CountSpliterReceiveFilter - Fixed Number Split Parts with Separator Protocol
- Some protocols defines their requests look like in the format of "#part1#part2#part3#part4#part5#part6#part7#". There are 7 parts in one request and all parts are separated by char '#'. This kind protocol's implementing also is quite easy:
	```c#
	/// <summary>
	/// Your protocol likes like the format below:
	/// #part1#part2#part3#part4#part5#part6#part7#
	/// </summary>
	public class CountSpliterAppServer : AppServer
	{
			public CountSpliterAppServer()
					: base(new CountSpliterReceiveFilterFactory((byte)'#', 8)) // 7 parts but 8 separators
			{

			}
	}
	```

- You also can customize your protocol deeper using the classes below:
	```
	CountSpliterReceiveFilter<TRequestInfo>
	CountSpliterReceiveFilterFactory<TReceiveFilter>
	CountSpliterReceiveFilterFactory<TReceiveFilter, TRequestInfo>

	```
## FixedSizeReceiveFilter - Fixed Size Request Protocol
- In this kind protocol, the size of all requests are same. If your each request is 9 characters string like "KILL BILL", what you should do is implementing a ReceiveFilter like the code below:
	```c#
	class MyReceiveFilter : FixedSizeReceiveFilter<StringRequestInfo>
	{
			public MyReceiveFilter()
					: base(9) //pass in the fixed request size
			{

			}

			protected override StringRequestInfo ProcessMatchedRequest(byte[] buffer, int offset, int length, bool toBeCopied)
			{
					//TODO: construct the request info instance from the parsed data and then return
			}
	}
	```

- Then use the receive filter in your AppServer class:
	```c#
	public class MyAppServer : AppServer
	{
			public MyAppServer()
					: base(new DefaultReceiveFilterFactory<MyReceiveFilter, StringRequestInfo>()) //using default receive filter factory
			{

			}
	}
	```

## BeginEndMarkReceiveFilter - The Protocol with Begin and End Mark
- Every message in this protocol have fixed begin mark and end mark. For example, I have a protocol all messages are in the format "!xxxxxxxxxxxxxx$". In this case "!" is begin mark and the "$" is end mark, so my receive filter looks like:
	```c#
	class MyReceiveFilter : BeginEndMarkReceiveFilter<StringRequestInfo>
	{
		//Both begin mark and end mark can be two or more bytes
		private readonly static byte[] BeginMark = new byte[] { (byte)'!' };
		private readonly static byte[] EndMark = new byte[] { (byte)'$' };

		public MyReceiveFilter()
				: base(BeginMark, EndMark) //pass in the begin mark and end mark
		{

		}

		protected override StringRequestInfo ProcessMatchedRequest(byte[] readBuffer, int offset, int length)
		{
				//TODO: construct the request info instance from the parsed data and then return
		}
	}
	```
	
- Then use the receive filter in your AppServer class:
	```c#
	public class MyAppServer : AppServer
	{
		public MyAppServer()
				: base(new DefaultReceiveFilterFactory<MyReceiveFilter, StringRequestInfo>()) //using default receive filter factory
		{

		}
	}
	```

## FixedHeaderReceiveFilter - Fixed Header with Body Length Protocol
- This kind protocol defines each request has two parts, the first part contains some basic information of this request include the length of the second part. We usually call the first part is header and the second part is body.

- For example, we have a protocol like that: the header contains 6 bytes, the first 4 bytes represent the request's name, the last 2 bytes represent the length of the body:

		/// +-------+---+-------------------------------+
		/// |request| l |                               |
		/// | name  | e |    request body               |
		/// |  (4)  | n |                               |
		/// |       |(2)|                               |
		/// +-------+---+-------------------------------+

- Using SuperSocket, you can implement this kind protocol easily:
	```c#
	class MyReceiveFilter : FixedHeaderReceiveFilter<BinaryRequestInfo>
	{
		public MyReceiveFilter()
				: base(6)
		{

		}

		protected override int GetBodyLengthFromHeader(byte[] header, int offset, int length)
		{
				return (int)header[offset + 4] * 256 + (int)header[offset + 5];
		}

		protected override BinaryRequestInfo ResolveRequestInfo(ArraySegment<byte> header, byte[] bodyBuffer, int offset, int length)
		{
				return new BinaryRequestInfo(Encoding.UTF8.GetString(header.Array, header.Offset, 4), bodyBuffer.CloneRange(offset, length));
		}
	}
	```

- You need to implement your own request filter base on FixedHeaderReceiveFilter.
	- The number 6 passed into the parent class's constructor means the size of the request header;
	- The method "GetBodyLengthFromHeader(...)" you should override returns the length of the body according the received header;
	- the method "ResolveRequestInfo(....)" you should override returns the RequestInfo instance according the received header and body.
- Then you can build a receive filter factory or use the default receive factory to use this receive filter in SuperSocket.

# 8. Implement Your Own Communication Protocol with IRequestInfo, IReceiveFilter and etc
> Keywords: Protocol Customization, IRequestInfo, IReceiveFilter, ReceiveFilterFactory

## Why do you want to use Your Own Communication Protocol?
- The communication protocol is used for converting your received binary data to the requests which your application can understand. SuperSocket provides a built-in communication protocol "Command Line Protocol" which defines each request must be ended with a carriage return "\r\n".

- But some applications cannot use "Command Line Protocol" for many different reasons. In this case, you need to implement your own communication protocol using the tools below:
	- RequestInfo
	- ReceiveFilter
	- ReceiveFilterFactory
	- AppServer and AppSession

## The RequestInfo
- RequestInfo is the entity class which represents a request from the client. Each request of client should be instantiated as a RequestInfo. The RequestInfo class must implement the interface IRequestInfo which only have a property named "Key" in string type:
	```c#
	public interface IRequestInfo
	{
			string Key { get; }
	}
	```

- Talked in the previous documentation, The request info class StringRequestInfo is used in SuperSocket command line protocol.

- You also can implement your own RequestInfo class as your application requirement. For instance, if all of your requests must have a DeviceID field, you can define a property for it in the RequestInfo class:
	```c#
	public class MyRequestInfo : IRequestInfo
	{
			public string Key { get; set; }

			public int DeviceId { get; set; }

			/*
			// Other properties
			*/
	}
	```

- SuperSocket also provides another request info class "BinaryRequestInfo" used for binary protocol:
	```c#
	public class BinaryRequestInfo
	{
			public string Key { get; }

			public byte[] Body { get; }
	}
	```
- You can use BinaryRequestInfo directly if it can satisfy your requirement.

## The ReceiveFilter
- The ReceiveFilteris used for converting received binary data to your request info instances.

- To implement a ReceiveFilter, you need to implement the interface IReceiveFilter:
	```c#
	public interface IReceiveFilter<TRequestInfo>
			where TRequestInfo : IRequestInfo
	{
			/// <summary>
			/// Filters received data of the specific session into request info.
			/// </summary>
			/// <param name="readBuffer">The read buffer.</param>
			/// <param name="offset">The offset of the current received data in this read buffer.</param>
			/// <param name="length">The length of the current received data.</param>
			/// <param name="toBeCopied">if set to <c>true</c> [to be copied].</param>
			/// <param name="rest">The rest, the length of the data which hasn't been parsed.</param>
			/// <returns></returns>
			TRequestInfo Filter(byte[] readBuffer, int offset, int length, bool toBeCopied, out int rest);

			/// <summary>
			/// Gets the size of the left buffer.
			/// </summary>
			/// <value>
			/// The size of the left buffer.
			/// </value>
			int LeftBufferSize { get; }

			/// <summary>
			/// Gets the next receive filter.
			/// </summary>
			IReceiveFilter<TRequestInfo> NextReceiveFilter { get; }

			/// <summary>
			/// Resets this instance to initial state.
			/// </summary>
			void Reset();
	}
	```
	- TRequestInfo: the type parameter "TRequestInfo" is the request info class you want to use in the application
	- LeftBufferSize: the data size which is cached in this request filter;
	- NextReceiveFilter: the request filter which will be used when next piece of binary data is received;
	- Reset(): resets this instance to initial state;
	- Filter(....): the filter method is executed when a piece of binary data is received by SuperSocket, the received data locates in the parameter readBuffer. Because the readBuffer is shared by all connections in the same appServer instance, so you need to load the received data from the position "offset"(method parameter) and with the size "length" (method parameter).
		```C#
		 TRequestInfo Filter(byte[] readBuffer, int offset, int length, bool toBeCopied, out int rest);
		```
		- readBuffer: the receiving buffer, the received data is stored in this array
		- offset: the received data's start position in the readBuffer
		- length: the length of the received data
		- toBeCopied: indicate whether should create a copy of the readBuffer instead of use it directly when we want to cache data in it
		- rest: it's a output parameter, it should be set to be the remaining received data size after you find a full request
- There are many cases you need to handle:
	- If you find a full request from the received data, your must return a request info instance of your request info type.
	- If you haven't find a full request, you just return NULL.
	- If you have find a full request from the received data, but the received data not only contain one request, set the remaining data size to the output parameter "rest". SuperSocket will examine the output parameter "rest", if it is bigger than 0, the Filter method will be executed again with the parameters "offset" and "length" adjusted.

## The ReceiveFilterFactory
- The ReceiveFilterFactory is used for creating receive filter for each session. To define you receive filter factory class, you must implement the interface IReceiveFilterFactory. The type parameter "TRequestInfo" is the request info class you want to use in the application
	```c#
	/// <summary>
	/// Receive filter factory interface
	/// </summary>
	/// <typeparam name="TRequestInfo">The type of the request info.</typeparam>
	public interface IReceiveFilterFactory<TRequestInfo> : IReceiveFilterFactory
			where TRequestInfo : IRequestInfo
	{
			/// <summary>
			/// Creates the receive filter.
			/// </summary>
			/// <param name="appServer">The app server.</param>
			/// <param name="appSession">The app session.</param>
			/// <param name="remoteEndPoint">The remote end point.</param>
			/// <returns>
			/// the new created request filer assosiated with this socketSession
			/// </returns>
			IReceiveFilter<TRequestInfo> CreateFilter(IAppServer appServer, IAppSession appSession, IPEndPoint remoteEndPoint);
	}
	```
- You also can use the default receive filter factory
	```C#
	DefaultReceiveFilterFactory<TReceiveFilter, TRequestInfo>
	```
	, it will return the TReceiveFilter instance which is instantiated by the non-parameter constructor of class TReceiveFilter when method CreateFilter is invoked.

## Work together with AppSession and AppServer
- Now, you have RequestInfo, ReceiveFilter and ReceiveFilterFactory, but you haven't started use them. If you want to make them available in your application, you need to define your AppSession and AppServer using your created RequestInfo, ReceiveFilter and ReceiveFilterFactory.

	- Set RequestInfo for AppSession
		```C#
		public class YourSession : AppSession<YourSession, YourRequestInfo>
		{
				//More code...
		}
		```

	- Set RequestInfo and ReceiveFilterFactory for AppServer
		```c#
		public class YourAppServer : AppServer<YourSession, YourRequestInfo>
		{
				public YourAppServer()
						: base(new YourReceiveFilterFactory())
				{

				}
		}
		```

- After finish these two things, your custom communication protocol should work now.

# 9. Command and Command Loader
> Keywords: Command, Command Loader, Multiple Command Assemblies

## Command
- Command in SuperSocket is designed to handle the requests coming from the clients, it play an important role in the business logic processing.

- Command class must implement the basic command interface below:
	```c#
	public interface ICommand<TAppSession, TRequestInfo> : ICommand
			where TRequestInfo : IRequestInfo
			where TAppSession : IAppSession
	{

			void ExecuteCommand(TAppSession session, TRequestInfo requestInfo);
	}

	public interface ICommand
	{
			string Name { get; }
	}
	```

- The request processing code should be placed in the method "ExecuteCommand(TAppSession session, TRequestInfo requestInfo)" and the property "Name" is used for matching the received requestInfo. When a requestInfo instance is received, SuperSocket will look for the command which has the responsibility to handle it by matching the requestInfo's Key and the command's name.

- For a instance, if we receive a requestInfo like below:
	```
	Key: "ADD"
	Body: "1 2"
	```

- Then SuperSocket will looking a command whose name is "ADD". If we have a command defined below:
	```c#
	public class ADD : StringCommandBase
	{
		public override void ExecuteCommand(AppSession session, StringRequestInfo requestInfo)
		{
			session.Send((int.Parse(requestInfo[0] + int.Parse(requestInfo[1])).ToString()));
		}
	}
	```

- Then this command will be found, because the StringCommandBase instance's name is filled by the class's name.

- But in some cases, the requestInfo's Key cannot be used as a class name. For example:
	```
	Key: "01"
	Body: "1 2"
	```

- To make your ADD command work, you need to override the name attribute of the command class:
	```
	public class ADD : StringCommandBase
	{
		public override string Name
		{
			get { return "01"; }
		}

		public override void ExecuteCommand(AppSession session, StringRequestInfo requestInfo)
		{
				session.Send((int.Parse(requestInfo[0] + int.Parse(requestInfo[1])).ToString()));
		}
	}
	```

## Command assemblies definition
- Yes, it uses reflection to find public classes who implement the basic command interface, but it only look for from the assembly where your AppServer class is defined.

- For example, your AppServer is defined in the assembly GameServer.dll, but your command ADD is defined in the assembly BasicModules.dll:
	```
	GameServer.dll
			+ MyGameServer.cs
	```
	```
	BasicModules.dll
			+ ADD.cs
	```
- By default, the command "ADD" cannot be loaded into the game server instance. If you want to load the command, you should add the assembly BasicModules.dll into command assemblies in the configuration:
	```xml
	<?xml version="1.0" encoding="utf-8" ?>
	<configuration>
			<configSections>
					<section name="superSocket" type="SuperSocket.SocketEngine.Configuration.SocketServiceConfig, SuperSocket.SocketEngine"/>
			</configSections>
			<appSettings>
					<add key="ServiceName" value="BroardcastService"/>
			</appSettings>
			<superSocket>
					<servers>
							<server name="SampleServer"
											serverType="GameServer.MyGameServer, GameServer"
											ip="Any" port="2012">
								<commandAssemblies>
									<add assembly="BasicModules"></add>
								</commandAssemblies>
							</server>
					</servers>
			</superSocket>
			<startup>
					<supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.0" />
			</startup>
	</configuration>
	```
- You also can add more command assemblies in the configuration.

## Command Loader
- In some cases, you prefer some special logic operations to return commands instead of by automatically reflection, you should implement a command loader by yourself:
	```c#
	public interface ICommandLoader<TCommand>
	```
- And then configure your server to use the command loader:
	```xml
	<superSocket>
			<servers>
				<server name="SampleServer"
								serverType="GameServer.MyGameServer, GameServer"
								ip="Any" port="2012"
								commandLoader="MyCommandLoader">
				</server>
			</servers>
			<commandLoaders>
					<add name="MyCommandLoader"
						type="GameServer.MyCommandLoader, GameServer" />
			</commandLoaders>
	</superSocket>
	```

# 10. Get the Connected Event and Closed Event of a Connection

> Keywords: Session, Connected Event, Closed Event

## AppSession's virtual methods OnSessionStarted() and OnSessionClosed(CloseReason reason)
- You can override the base virtual methods OnSessionStarted() and OnSessionClosed(CloseReason reason) to do some business operations when a new session connects or a session drops:
	```c#
	public class TelnetSession : AppSession<TelnetSession>
	{
			protected override void OnSessionStarted()
			{
					this.Send("Welcome to SuperSocket Telnet Server");
					//add your business operations
			}

			protected override void OnSessionClosed(CloseReason reason)
			{
					//add your business operations
			}
	}
	```

## AppServer's event NewSessionConnected and event SessionClosed
- Subscribe event:
	```c#
	appServer.NewSessionConnected += new SessionHandler<AppSession>(appServer_NewSessionConnected);
	appServer.SessionClosed += new SessionHandler<AppSession, CloseReason>(appServer_SessionClosed);
	```
- Define event handling method:
	```c#
	static void appServer_SessionClosed(AppSession session, CloseReason reason)
	{
			Console.WriteLine("A session is closed for {0}.", reason);
	}

	static void appServer_NewSessionConnected(AppSession session)
	{
			session.Send("Welcome to SuperSocket Telnet Server");
	}
	```

# 11. Push Data to Clients from Server Initiative
> Keywords: Server Push, Send Data, Get Session

## Send Data to Client by Session Object
- It was said before, AppSession represents a logic socket connection, connection based operations should be defined in this class. The AppSession also wraps the sending data method of the socket. You can use the method "Send(...)" of AppSession to send data to client:
	```c#
	session.Send(data, 0, data.Length);
	or
	session.Send("Welcome to use SuperSocket!");
	```

## Get Session by SessionID
- As mentioned in previous part, if you have got the connection's session instance, then you can send data to the client by the "Send(..)" method. But in some cases, you cannot get the session instance you want directly.

- SuperSocket provide a API to allow you get a session by session ID from the AppServer's session container.
	```c#
	var session = appServer.GetSessionByID(sessionID);
	
	if(session != null)
    session.Send(data, 0, data.Length);
	```
	
- What is the SessionID?

	SessionID is a property of the AppSession class which is used for identifying a Session. In a SuperSocket TCP server, the SessionID is a GUID string which is assigned as soon as the session is created. If you don't use UdpRequestInfo in a SuperSocket UDP server, the SessionID will be consist of the remote endpoint's IP and port. If you use UdpRequestInfo in a SuperSocket UDP server, the value of SessionID is passed from the client.

## Get All Connected Sessions
- You also can get all connected sessions from the AppServer instances and then push data to all clients:
	```c#
	foreach(var session in appServer.GetAllSessions())
	{
			session.Send(data, 0, data.Length);
	} 
	```  

- If you enable session snapshot, the sessions get from method AppServer.GetAllSessions() are not updated realtime. They are all the connected sessions of the AppServer in the time when the last snapshot is taken.

## Get Sessions by Criteria
- If you have a custom property "CompanyId" in your AppSession, and you want get all connected session whose CompanyId are equal with your specific value, the methd of AppServer "GetSession(...)" should be useful:
	```c#
	var sessions = appServer.GetSessions(s => s.CompanyId == companyId);
	foreach(var s in sessions)
	{
			s.Send(data, 0, data.Length);
	}
	```
- Same as the method "GetAllSessions(...)", if you enable session snapshot, the sessions also come from snapshot.

# 12. Extend Server Configuration
> Keywords: Configuration, Custom Configuration, Extend Configuration

- When you implement your socket server by SuperSocket, it is unavoidable to define some parameters in configuration file.The SuperSocket provides a very easy way to store the parameters in your configuration file and then read and use them in AppServer.

- Please take a look at the following configuration code:
	```xml
	<server name="FlashPolicyServer"
					serverType="SuperSocket.Facility.PolicyServer.FlashPolicyServer, SuperSocket.Facility"
					ip="Any" port="843"
					receiveBufferSize="32"
					maxConnectionNumber="100"
					clearIdleSession="true"
					policyFile="Policy\flash.xml">
	</server>
	```

- In above server configuration,the attribute "policyFile" is not defined in SuperSocket, but you also can read it in your AppServer class:
	```c#
	public class YourAppServer : AppServer
	{
			private string m_PolicyFile;

			protected override bool Setup(IRootConfig rootConfig, IServerConfig config)
			{  
					m_PolicyFile = config.Options.GetValue("policyFile");

					if (string.IsNullOrEmpty(m_PolicyFile))
					{
							if(Logger.IsErrorEnabled)
									Logger.Error("Configuration option policyFile is required!");
							return false;
					}

					return true;
			}
	}
	```

- Not only we can add customized attributes in server node, we also can add the customized child configuration like below:
	```xml
	<server name="SuperWebSocket"
					serverTypeName="SuperWebSocket"
					ip="Any" port="2011" mode="Tcp">
			<subProtocols>
					<!--Your configuration-->
			</subProtocols>
	</server>
	```

- A configuration element type is required:
	```c#
	/// <summary>
	/// SubProtocol configuration
	/// </summary>
	public class SubProtocolConfig : ConfigurationElement
	{
			//Configuration attributes
	}
	/// <summary>
	/// SubProtocol configuation collection
	/// </summary>
	[ConfigurationCollection(typeof(SubProtocolConfig))]
	public class SubProtocolConfigCollection : ConfigurationElementCollection
	{
			//Configuration attributes
	}
	```

- Then you can read the child configuration node in your AppServer:
	```c#
	public class YourAppServer : AppServer
	{
		private SubProtocolConfigCollection m_SubProtocols;

		protected override bool Setup(IRootConfig rootConfig, IServerConfig config)
		{  
			m_SubProtocols = config.GetChildConfig<SubProtocolConfigCollection>("subProtocols");

			if (m_SubProtocols == null)
			{
					if(Logger.IsErrorEnabled)
							Logger.Error("The child configuration node 'subProtocols' is required!");
					return false;
			}

			return true;
		}
	}
	```

# 13. Command Filter
> Keywords: Command Filter, Global Command Filter, CommandFilterAttribute, Command

- The Command Filter feature in SuperSocket looks like Action Filter in ASP.NET MVC, you can use it to intercept execution of Command, the Command Filter will be invoked before or after a command execution.

- Command Filter class must inherit from Attribute CommandFilterAttribute:
	```c#
	/// <summary>
	/// Command filter attribute
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public abstract class CommandFilterAttribute : Attribute
	{
		/// <summary>
		/// Gets or sets the execution order.
		/// </summary>
		/// <value>
		/// The order.
		/// </value>
		public int Order { get; set; }

		/// <summary>
		/// Called when [command executing].
		/// </summary>
		/// <param name="commandContext">The command context.</param>
		public abstract void OnCommandExecuting(CommandExecutingContext commandContext);

		/// <summary>
		/// Called when [command executed].
		/// </summary>
		/// <param name="commandContext">The command context.</param>
		public abstract void OnCommandExecuted(CommandExecutingContext commandContext);
	}
	```
	- There are two methods you should implement for your command filter:
		- OnCommandExecuting: This method is called before the execution of the Command;
		- OnCommandExecuted: This method is called after the execution of the Command;
		- Order: You also can set the Order property of the command filter to control the executing order

- The following code defines a Command Filter LogTimeCommandFilterAttribute for recording the command execution time if the time is longer than 5 seconds:
	```c#
	public class LogTimeCommandFilter : CommandFilterAttribute
	{
		public override void OnCommandExecuting(CommandExecutingContext commandContext)
		{
			commandContext.Session.Items["StartTime"] = DateTime.Now;
		}

		public override void OnCommandExecuted(CommandExecutingContext commandContext)
		{
			var session = commandContext.Session;
			var startTime = session.Items.GetValue<DateTime>("StartTime");
			var ts = DateTime.Now.Subtract(startTime);

			if (ts.TotalSeconds > 5 && session.Logger.IsInfoEnabled)
			{
					session.Logger.InfoFormat("A command '{0}' took {1} seconds!", commandContext.CurrentCommand.Name, ts.ToString());
			}
		}
	}
	```

- And then apply the command filter to the command "QUERY" by adding attribute:
	```c#
	[LogTimeCommandFilter]
	public class QUERY : StringCommandBase<TestSession>
	{
			public override void ExecuteCommand(TestSession session, StringCommandInfo commandData)
			{
					//Your code
			}
	}
	```

- If you want to apply this command filter to all commands, you should add this Command Filter Attribute to your AppServer class like the following code:
	```c#
	[LogTimeCommandFilter]
	public class TestServer : AppServer<TestSession>
	{

	}
	```

- You also can cancel the command's execution by setting the commandContext's Cancel property to be true:
	```c#
	public class LoggedInValidationFilter : CommandFilterAttribute
	{
		public override void OnCommandExecuting(CommandExecutingContext commandContext)
		{
			var session = commandContext.Session as MyAppSession;

			//If the session is not logged in, cancel the executing of the command
			if (!session.IsLoggedIn)
					commandContext.Cancel = true;
		}

		public override void OnCommandExecuted(CommandExecutingContext commandContext)
		{

		}
	}
	```

# 14. Connection Filter
> Keywords: Connection Filter, Session Filter, Allow Connect

- Connection Filter in SuperSocket is the interface which is used for filtering client connections. By connection filter, you can allow or disallow the client connections from the specified source.

- Connection Filter interface is defined like below:
	```c#
	/// <summary>
	/// The basic interface of connection filter
	/// </summary>
	public interface IConnectionFilter
	{
		/// <summary>
		/// Initializes the connection filter
		/// </summary>
		/// <param name="name">The name.</param>
		/// <param name="appServer">The app server.</param>
		/// <returns></returns>
		bool Initialize(string name, IAppServer appServer);

		/// <summary>
		/// Gets the name of the filter.
		/// </summary>
		string Name { get; }

		/// <summary>
		/// Whether allows the connect according the remote endpoint
		/// </summary>
		/// <param name="remoteAddress">The remote address.</param>
		/// <returns></returns>
		bool AllowConnect(IPEndPoint remoteAddress);
	}
	```
	- bool Initialize(string name, IAppServer appServer);
		- This method is used to initialize the connection filter, name is the name of Filter.
	- string Name {get;}
		- Return Filter name
	- bool AllowConnect (IPEndPoint remoteAddress);
		- This method requires the client to achieve the endpoint to determine whether to allow connection to the server.

- The following code implemented a connection filter which only allow connection from the specific ip range:
	```c#
	public class IPConnectionFilter : IConnectionFilter
	{
		private Tuple<long, long>[] m_IpRanges;

		public bool Initialize(string name, IAppServer appServer)
		{
			Name = name;

			var ipRange = appServer.Config.Options.GetValue("ipRange");

			string[] ipRangeArray;

			if (string.IsNullOrEmpty(ipRange)
					|| (ipRangeArray = ipRange.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)).Length <= 0)
			{
					throw new ArgumentException("The ipRange doesn't exist in configuration!");
			}

			m_IpRanges = new Tuple<long, long>[ipRangeArray.Length];

			for (int i = 0; i < ipRangeArray.Length; i++)
			{
					var range = ipRangeArray[i];
					m_IpRanges[i] = GenerateIpRange(range);
			}

			return true;
		}

		private Tuple<long, long> GenerateIpRange(string range)
		{
			var ipArray = range.Split(new char[] { '-' }, StringSplitOptions.RemoveEmptyEntries);

			if(ipArray.Length != 2)
					throw new ArgumentException("Invalid ipRange exist in configuration!");

			return new Tuple<long, long>(ConvertIpToLong(ipArray[0]), ConvertIpToLong(ipArray[1]));
		}

		private long ConvertIpToLong(string ip)
		{
			var points = ip.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

			if(points.Length != 4)
					throw new ArgumentException("Invalid ipRange exist in configuration!");

			long value = 0;
			long unit = 1;

			for (int i = points.Length - 1; i >= 0; i--)
			{
					value += unit * points[i].ToInt32();
					unit *= 256;
			}

			return value;
		}

		public string Name { get; private set; }

		public bool AllowConnect(IPEndPoint remoteAddress)
		{
			var ip = remoteAddress.Address.ToString();
			var ipValue = ConvertIpToLong(ip);

			for (var i = 0; i < m_IpRanges.Length; i++)
			{
				var range = m_IpRanges[i];

				if (ipValue > range.Item2)
						return false;

				if (ipValue < range.Item1)
						return false;
			}

			return true;
		}
	}
	```

- Then you need to update the configuration file to use this connection filter:
	1. add configuration node "connectionFilters";
		```xml
		<connectionFilters>
			<add name="IpRangeFilter"
					type="SuperSocket.QuickStart.ConnectionFilter.IPConnectionFilter, SuperSocket.QuickStart.ConnectionFilter" />
		</connectionFilters>
		```
			
	2. add configuration attributes for server instance;
		```xml
		<server name="EchoServer"
						serverTypeName="EchoService" ip="Any" port="2012"
						connectionFilter="IpRangeFilter"
						ipRange="127.0.1.0-127.0.1.255">
		</server>
		```
	3. the finally configuration should look like;
		```xml
		<?xml version="1.0" encoding="utf-8" ?>
		<configuration>
				<configSections>
						<section name="superSocket" type="SuperSocket.SocketEngine.Configuration.SocketServiceConfig, SuperSocket.SocketEngine"/>
				</configSections>
				<appSettings>
						<add key="ServiceName" value="EchoService"/>
				</appSettings>
				<superSocket>
						<servers>
								<server name="EchoServer"
										serverTypeName="EchoService"
										ip="Any" port="2012"
										connectionFilter="IpRangeFilter"
										ipRange="127.0.1.0-127.0.1.255">
								</server>
						</servers>
					<serverTypes>
							<add name="EchoService"
										type="SuperSocket.QuickStart.EchoService.EchoServer, SuperSocket.QuickStart.EchoService" />
					</serverTypes>
					<connectionFilters>
							<add name="IpRangeFilter"
										type="SuperSocket.QuickStart.ConnectionFilter.IPConnectionFilter, SuperSocket.QuickStart.ConnectionFilter" />
					</connectionFilters>
				</superSocket>
				<startup>
						<supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.0" />
				</startup>
		</configuration>
		```

# 15. Multiple Listeners
> Keywords: Multiple Listeners, Multiple Port, Multiple Endpoints, Multiple Listeners Configuration, IP, Port

## Single listener
- In the configuration below, you can configure the server instance's listening IP and port:
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

## Multiple listeners
- You can add a child configuration node "listeners" to add more listening ip/port pairs:
	```xml
	<superSocket>
		<servers>
			<server name="EchoServer" serverTypeName="EchoService">
				<listeners>
					<add ip="127.0.0.2" port="2012" />
					<add ip="IPv6Any" port="2012" />
				</listeners>
			</server>
		</servers>
		<serverTypes>
			<add name="EchoService"
					type="SuperSocket.QuickStart.EchoService.EchoServer, SuperSocket.QuickStart.EchoService" />
		</serverTypes>
	</superSocket>
	```
	- In this case, the server instance "EchoServer" will listen two local endpoints. It is very similar with that a website can has many bindings in IIS.
- You also can set different options for the different listeners:
	```xml
	<superSocket>
		<servers>
			<server name="EchoServer" serverTypeName="EchoService">
				<certificate filePath="localhost.pfx" password="supersocket"></certificate>
				<listeners>
						<add ip="Any" port="80" />
						<add ip="Any" port="443" security="tls" />
				</listeners>
			</server>
		</servers>
		<serverTypes>
			<add name="EchoService"
					type="SuperSocket.QuickStart.EchoService.EchoServer, SuperSocket.QuickStart.EchoService" />
		</serverTypes>
	</superSocket>
	```

# 16. Multiple Server Instances
> Keywords: Multiple Server Instances, Multiple Server Configuration, Server Dispatch, Isolation

## SuperSocket support running multiple server instances in the same process
- The multiple server instances can be in same server type:
	```xml
	<superSocket>
		<servers>
			<server name="EchoServerA" serverTypeName="EchoService">
						<listeners>
							<add ip="Any" port="80" />
						</listeners>
			</server>
			<server name="EchoServerB" serverTypeName="EchoService" security="tls">
					<certificate filePath="localhost.pfx" password="supersocket"></certificate>
					<listeners>
							<add ip="Any" port="443" />
					</listeners>
			</server>
		</servers>
		<serverTypes>
			<add name="EchoService"
					type="SuperSocket.QuickStart.EchoService.EchoServer, SuperSocket.QuickStart.EchoService" />
		</serverTypes>
	</superSocket>
	```

- They are also can be in different server types:
	```xml
	<superSocket>
		<servers>
			<server name="ServerA"
							serverTypeName="MyAppServerA"
							ip="Any" port="2012">
			</server>
			<server name="ServerB"
							serverTypeName="MyAppServerB"
							ip="Any" port="2013">
			</server>
		</servers>
		<serverTypes>
			<add name="MyAppServerA"
					type="SuperSocket.QuickStart.MultipleAppServer.MyAppServerA, SuperSocket.QuickStart.MultipleAppServer" />
			<add name="MyAppServerB"
					type="SuperSocket.QuickStart.MultipleAppServer.MyAppServerB, SuperSocket.QuickStart.MultipleAppServer" />
		</serverTypes>
	</superSocket>
	```

## Isolation level of the server instances
- As mentioned before, there is a configuration attribute in the SuperSocket root configuration:
```xml
<superSocket isolation="AppDomain">//None, AppDomain, Process
    ....
</superSocket>
```

- If the isolation level is 'None' (default value), these app server instances will share the same process and the same AppDomain. So they can access each other easily. (We'll discuss it later)
- But if the isolation level is 'AppDomain', SuperSocket will create one AppDomain for each server instance and they will be run in the different AppDomains.
- But if the isolation level is 'Process', SuperSocket will create one Process for each server instance and they will be run in the different Processes.

- The picture below demonstrate how isolation model works:
	![image](https://raw.githubusercontent.com/kerryjiang/SuperSocket.Document/v1.6/images/isolationmodel.jpg)

## Process level isolation
- If you want to use Process level isolation, beyond the configuration, you need to include an executable assembly "SuperSocket.Agent.exe" into your project output, which is provided by SuperSocket.

- After you start your SuperSocket, you will find more processes of SuperSocket:
	```
	SuperSocket.SocketService.exe
	SuperSocket.Agent.exe
	SuperSocket.Agent.exe
	```

## Interactions among the multiple server instances
- As described in the previous section, if the isolation is 'None', the interactions among the multiple server instances is very easy.
- For example, they can access each other by name using Bootstap provided by SuperSocket:
	```c#
	interface IDespatchServer
	{
			void DispatchMessage(string sessionKey, string message);
	}

	public class MyAppServerB : AppServer, IDespatchServer
	{
			public void DispatchMessage(string sessionKey, string message)
			{
					var session = GetAppSessionByID(sessionKey);
					if (session == null)
							return;

					session.Send(message);
			}
	}

	public class MyAppServerA : AppServer
	{
			private IDespatchServer m_DespatchServer;

			protected override void OnStartup()
			{
					m_DespatchServer = this.Bootstrap.GetServerByName("ServerB") as IDespatchServer;
					base.OnStartup();
			}

			internal void DespatchMessage(string targetSessionKey, string message)
			{
					m_DespatchServer.DispatchMessage(targetSessionKey, message);
			}
	}
	```

- The above sample give you a demonstration about how dispatch a message from one server instance to a session of the other server instance.

## Control server instances independent
- By default, all server instances in a SuperSocket server will be started and stopped together. Is there a way to start/stop a server instance and don't affect other server instances? Of course, the answer is yes and SuperSocket provide more options:

	1. SuperSocket control script

		SuperSocket also provide two control scripts in SuperSocket.SocketService project:

		- supersocket.cmd - for Windows
			```
			supersocket list
			supersocket start FTPServer
			supersocket stop FTPServer
			```

		- supersocket.sh - for Linux/Unix
			```
			./supersocket list
			./supersocket start FTPServer
			./supersocket stop FTPServer
			```

	2. SuperSocket ServerManager
		
		You can use the ServerManager client application to control the server instances by GUI. Please read the documentation to learn how to setup ServerManager. Document of SuperSocket ServerManager

		![image](https://raw.githubusercontent.com/kerryjiang/SuperSocket.Document/v1.6/images/servermanagercontrol.jpg)

# 17. Implement Your Commands by Dynamic Language
> Keywords: Dynamic Language, IronPython, IronRuby, Script, Dynamic Commands

## Enable dynamic language for your SuperSocket
- There are many steps:
	1. Add DLR (dynamic language runtime) configuration section;
		
		- Section definition:
			```xml
			<section name="microsoft.scripting" requirePermission="false"
					type="Microsoft.Scripting.Hosting.Configuration.Section, Microsoft.Scripting"/>
			```		 

		- Section content:
			```xml
			<microsoft.scripting>
					<languages>
							<language extensions=".py" displayName="IronPython"
									type="IronPython.Runtime.PythonContext, IronPython"
									names="IronPython;Python;py"/>
					</languages>
			</microsoft.scripting>
			```

	2. Add command loader for DLR;
		```xml
		<SuperSocket>
				......
				<commandLoaders>
						<add name="dynamicCommandLoader" type="SuperSocket.Dlr.DynamicCommandLoader, SuperSocket.Dlr"/>
				</commandLoaders>
		</superSocket>
		```

	3. Use the command loader for your server instances:
		```xml
		<servers>
			<server name="IronPythonServer"
					serverTypeName="IronPythonService"
					ip="Any" port="2012"
					maxConnectionNumber="50"
					commandLoader="dynamicCommandLoader">
			</server>
		</servers>
		```

- The full configuration file will be:
	```xml
	<?xml version="1.0"?>
	<configuration>
		<configSections>
			<section name="superSocket" type="SuperSocket.SocketEngine.Configuration.SocketServiceConfig, SuperSocket.SocketEngine" />
			<section name="microsoft.scripting" requirePermission="false"
							type="Microsoft.Scripting.Hosting.Configuration.Section, Microsoft.Scripting"/>
		</configSections>
		<appSettings>
			<add key="ServiceName" value="SupperSocketService" />
		</appSettings>
		<connectionStrings/>
		<superSocket>
			<servers>
				<server name="IronPythonServer"
						serverTypeName="IronPythonService"
						ip="Any" port="2012"
						maxConnectionNumber="50"
						commandLoader="dynamicCommandLoader">
				</server>
			</servers>
			<serverTypes>
				<add name="IronPythonService"
				type="SuperSocket.QuickStart.IronSocketServer.DynamicAppServer, SuperSocket.QuickStart.IronSocketServer" />
			</serverTypes>
			<commandLoaders>
					<add name="dynamicCommandLoader" type="SuperSocket.Dlr.DynamicCommandLoader, SuperSocket.Dlr"/>
			</commandLoaders>
		</superSocket>
		<microsoft.scripting>
			<languages>
				<language extensions=".py" displayName="IronPython"
							type="IronPython.Runtime.PythonContext, IronPython"
							names="IronPython;Python;py"/>
			</languages>
		</microsoft.scripting>
		<startup>
			<supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.0" />
		</startup>
	</configuration>
	```

## Ensure all required assemblies exist in the working directory
- The files below are required: * Microsoft.Dynamic.dll * Microsoft.Scripting.dll * IronPython.dll * SuperSocket.Dlr.dll

## Implement your commands
- Now, if we have a command line protocol SuperSocket server instance "IronPythonServer", and we want to create a "ADD" command in Python for adding two integers and then send back the result to client, we should the following steps:

	1. Create a python script file named as "ADD.py" with the content below:
		```c#
		def execute(session, request):
				session.Send(str(int(request[0]) + int(request[1])))
		```
	2. Put this file into the sub directory "Command" of the working directory
		```
		WorkRoot -> Command -> ADD.py
		```
	3. Start the server, and verify the function by telnet
		```
		telnet 127.0.0.1 2012
		Client: ADD 100 150
		Server: 250
		```
- You can find we put the file ADD.py in root of the Command folder, therefore SuperSocket allow all server instances load it. If you want to only allow the server instance "IronPythonServer" to use it, you should put this file in the sub directory "IronPythonServer" of the command folter:
	```
	WorkRoot -> Command -> IronPythonServer -> ADD.py
	```

## Updating of the dynamic commands
- The SuperSocket checks the updates of the command folder in the interval of 5 minutes. So if you have any command updates including adding, updating or removing, SuperSocket will adopt your changes within 5 minutes.

## Add command filter for dynamic commands
- Because we cannot add CLR attribute for Python file or function easily like C#, so you need yo add the extra method "getFilters()" to return command filters to CLR runtime.

- ADD.py
	```
	def getFilters():
			return [LogTimeCommandFilter(), LoggedInValidationFilter()]

	def execute(session, request):
			session.Send(str(int(request[0]) + int(request[1])))
	```

# 18. Logging in SuperSocket
> Keywords: Logging, log4net, Logging API, Logging Customization, LogFactory

## The logging system in SuperSocket
- The logging system is enabled automatically when the SuperSocket boostrap is starting, so you needn't create your own logging tool by yourself. You'd better to use the logging function in SuperSocket.

- By default, the SuperSocket uses log4net as it's logging framework. So if you are familiar with log4net, it will be very easy for you to use and customize the logging function in SuperSocket.

- SuperSocket also provides the basic log4net configuration files log4net.config/log4net.unix.config, you should put the log configuration file into the sub directory "Config" of the root of the running application. The log4net config define the loggers and appenders to category all logs into the 4 rolling files in the folder named "Logs":
	- info.log
	- debug.log
	- err.log
	- perf.log
- You also can customize the config according your logging requirements.

- Because of the loose couple with the log4net, you need to reference the file log4net.dll manually by yourself (please use the one provided by SuperSocket).

## The logging API
- The logging in SuperSocket is very easy, you can log information in the most places of your code. Both of the basic class of AppServer and AppSession have the Logger property which can be used directly for logging.

- The code below demonstrates the logging API:

	1. 
		```c#
		/// <summary>
		/// PolicyServer base class
		/// </summary>
		public abstract class PolicyServer : AppServer<PolicySession, BinaryRequestInfo>
		{
				......

				/// <summary>
				/// Setups the specified root config.
				/// </summary>
				/// <param name="rootConfig">The root config.</param>
				/// <param name="config">The config.</param>
				/// <returns></returns>
				protected override bool Setup(IRootConfig rootConfig, IServerConfig config)
				{
						m_PolicyFile = config.Options.GetValue("policyFile");

						if (string.IsNullOrEmpty(m_PolicyFile))
						{
								if(Logger.IsErrorEnabled)
										Logger.Error("Configuration option policyFile is required!");
								return false;
						}

						return true;
				}

				......
		}
		```
	
	2. 
		```c#
		public class RemoteProcessSession : AppSession<RemoteProcessSession>
		{
				protected override void HandleUnknownRequest(StringRequestInfo requestInfo)
				{
						Logger.Error("Unknow request");
				}
		}
		```

## Extend your logger
- SuperSocket allow you to customize your logger by yourself. For example, if you want to save your business operations log into another file than the default SuperSocket logging files, then you can define a new logger in your log4net configuration (assume you are using log4net by default):
	```xml
	<appender name="myBusinessAppender">
			<!--Your appender details-->
	</appender>
	<logger name="MyBusiness" additivity="false">
		<level value="ALL" />
		<appender-ref ref="myBusinessAppender" />
	</logger>
	```

	and then create this logger instance in your code:
	```c#
	var myLogger = server.LogFactory.GetLog("MyBusiness");
	```

## Use other logging framework than log4net
- SuperSocket supports you implement your own log factory from the interface:
	```c#
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;

	namespace SuperSocket.SocketBase.Logging
	{
			/// <summary>
					/// LogFactory Interface
			/// </summary>
			public interface ILogFactory
			{
					/// <summary>
					/// Gets the log by name.
					/// </summary>
					/// <param name="name">The name.</param>
					/// <returns></returns>
					ILog GetLog(string name);
			}
	}
	```

- The interfaces ILogFactory and ILog are defined in SuperSocket.

- After you implement your own log factory, then you can enable it in the configuration:
	```xml
	<superSocket logFactory="ConsoleLogFactory">
		<servers>
			<server name="EchoServer" serverTypeName="EchoService">
				<listeners>
					<add ip="Any" port="80" />
				</listeners>
			</server>
		</servers>
		<serverTypes>
			<add name="EchoService"
					type="SuperSocket.QuickStart.EchoService.EchoServer, SuperSocket.QuickStart.EchoService" />
		</serverTypes>
		<logFactories>
			<add name="ConsoleLogFactory"
					type="SuperSocket.SocketBase.Logging.ConsoleLogFactory, SuperSocket.SocketBase" />
		</logFactories>
	</superSocket>
	```
- There is a log factory implemented for Enterprise Library Logging Application Block in SuperSocket Extensions: http://supersocketext.codeplex.com/

# 19. The Built in Flash Silverlight Policy Server in SuperSocket
> Keywords: Policy File, Policy Server, Silverlight, Flash, Cross Domain, clientaccesspolicy

- SuperSocket contains a built-in Socket Policy Server for both Flash and Silverlight client. And it's implementation code is included in the assembly SuperSocket.Facility.dll. Thus, to enable the policy server, you need to make sure SuperSocket.Facility.dll exist in SuperSocket run directory firstly, and then add the policy server's configuration node in configuration file, like the following code.

- Flash Policy Server:
	```xml
	<?xml version="1.0"?>
	<configuration>
			<configSections>
					<section name="superSocket" type="SuperSocket.SocketEngine.Configuration.SocketServiceConfig, SuperSocket.SocketEngine" />
			</configSections>
			<appSettings>
					<add key="ServiceName" value="SupperSocketService" />
			</appSettings>
			<superSocket>
					<servers>
							<server name="FlashPolicyServer"
											serverType="SuperSocket.Facility.PolicyServer.FlashPolicyServer, SuperSocket.Facility"
											ip="Any" port="843"
											receiveBufferSize="32"
											maxConnectionNumber="100"
											policyFile="Policy\flash.xml"
											clearIdleSession="true">
							</server>
					</servers>
			</superSocket>
			<startup>
					<supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.0" />
			</startup>
	</configuration>
	```

- Silverlight Policy Server:
	```xml
	<?xml version="1.0"?>
	<configuration>
			<configSections>
					<section name="superSocket" type="SuperSocket.SocketEngine.Configuration.SocketServiceConfig, SuperSocket.SocketEngine" />
			</configSections>
			<appSettings>
					<add key="ServiceName" value="SupperSocketService" />
			</appSettings>
			<superSocket>
					<servers>
							<server name="SilverlightPolicyServer"
											serverType="SuperSocket.Facility.PolicyServer.SilverlightPolicyServer, SuperSocket.Facility"
											ip="Any" port="943"
											receiveBufferSize="32"
											maxConnectionNumber="100"
											policyFile="Policy\silverlight.xml"
											clearIdleSession="true">
							</server>
					</servers>
			</superSocket>
			<startup>
					<supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.0" />
			</startup>
	</configuration>
	```
- Note: the policyFile property in server node is your policy file stored path.

# 20. Enable TLS/SSL trasnferring layer encryption in SuperSocket
> Keywords: TLS, SSL, Certificate, X509 Certificate, Local Certificate Store

## SuperSocket supports the transport layer encryption (TLS/SSL)
- SuperSocket has automatically support for TLS/SSL. You needn't change any code to let your socket server support TLS/SSL.

## To enable TLS/SSL for your SuperSocket server, you should prepare a certificate at first.
- There are two ways to provide the certificate:
	- a X509 certificate file with private key
		- for testing purpose you can generate a certificate file by the CertificateCreator in SuperSocket(http://supersocket.codeplex.com/releases/view/59311)
		- in production environment, you should purchase a certificate from a certificate authority
	- a certificate in local certificate store

## Enable TLS/SSL by a certificate file
- You should change the configuration file to use the certificate file following the below steps:
	1. set security attribute for the server node;
	2. add the certificate node in server node as child.

- The configuration should look like:
	```xml
	<server name="EchoServer"
					serverTypeName="EchoService"
					ip="Any" port="443"
					security="tls">
			<certificate filePath="localhost.pfx" password="supersocket"></certificate>
	</server>
	```
	- Note: the password of the certificate node is the private key of the certificate file.

- There is one more option named "keyStorageFlags" for certificate loading:
	```xml
	<certificate filePath="localhost.pfx"
							password="supersocket"
							keyStorageFlags="UserKeySet"></certificate>
	```
- You can read the MSDN article below for more information about this option: http://msdn.microsoft.com/zh-cn/library/system.security.cryptography.x509certificates.x509keystorageflags(v=vs.110).aspx

## Enable TLS/SSL by a certificate in local certificate store
- You also can use a certificate in your local certificate store without a physical file. The thumbprint of the certificate you want to use is required:
	```xml
	<server name="EchoServer"
					serverTypeName="EchoService"
					ip="Any" port="443"
					security="tls">
			<certificate storeName="My" thumbprint="f42585bceed2cb049ef4a3c6d0ad572a6699f6f3"></certificate>
	</server>
	```

- Other optional options:

	- storeLocation - CurrentUser, LocalMachine
		```xml
		<certificate storeName="My"
								thumbprint="f42585bceed2cb049ef4a3c6d0ad572a6699f6f3">
								storeLocation="LocalMachine"
		</certificate>
		```

- You also can only apply TLS/SSL for one listener of the appserver instance
	```xml
	<server name="EchoServer" serverTypeName="EchoService" maxConnectionNumber="10000">
			<certificate storeName="My" thumbprint="f42585bceed2cb049ef4a3c6d0ad572a6699f6f3"></certificate>
			<listeners>
				<add ip="Any" port="80" />
				<add ip="Any" port="443" security="tls" />
			</listeners>
	</server>
	```

## Client certificate validation
- In TLS/SSL communications, the client side certificate is not a must, but some systems require much higher security guarantee. This feature allow you to validate the client side certificate from the server side.

- At first, to enable the client certificate validation, you should add the attribute "clientCertificateRequired" in the certificate node of the configuration:
	```xml
	<certificate storeName="My"
							storeLocation="LocalMachine"
							clientCertificateRequired="true"
							thumbprint="f42585bceed2cb049ef4a3c6d0ad572a6699f6f3"/>
	```

- Then you can override the AppServer's method "ValidateClientCertificate(...)" the implement your validation logic:
	```c#
	protected override bool ValidateClientCertificate(YourSession session, object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
	{
		//Check sslPolicyErrors

		//Check certificate

		//Return checking result
		return true;
	}
	```

# 21. Run SuperSocket in Windows Azure
> Keywords: Windows Azure, WorkRole, InstanceEndpoints

## What is Windows Azure?
- Windows Azure is Microsoft's cloud computing platform! Microsoft's Windows Azure provides on-demand computing power and storage capacity to host, scale and manage applications on the Internet to developers by it's data center.

- The application running on Windows Azure has high reliability and scalability. SuperSocket based server program can easily run on Windows Azure platform.

## SuperSocket Configuration
- The configuration file (app.config) which is used for Windows Azure hosting should be same with the standalone SuperSocket application.
	```xml
	<?xml version="1.0" encoding="utf-8" ?>
	<configuration>
		<configSections>
			<section name="superSocket" type="SuperSocket.SocketEngine.Configuration.SocketServiceConfig, SuperSocket.SocketEngine"/>
		</configSections>
		<superSocket>
			<servers>
				<server name="RemoteProcessServer"
						serverTypeName="remoteProcess"
						ip="Any" port="2012" />
			</servers>
			<serverTypes>
				<add name="remoteProcess"
				type="SuperSocket.QuickStart.RemoteProcessService.RemoteProcessServer, SuperSocket.QuickStart.RemoteProcessService" />
			</serverTypes>
		</superSocket>
		<system.diagnostics>
			<trace>
				<listeners>
					<add type="Microsoft.WindowsAzure.Diagnostics.DiagnosticMonitorTraceListener, Microsoft.WindowsAzure.Diagnostics, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"
					name="AzureDiagnostics">
					</add>
				</listeners>
			</trace>
		</system.diagnostics>
	</configuration>
	```

## Add SuperSocket start code in the WorkRole project
- Same with the other normal SuperSocket application, the startup code should be written in application entry point which is OnStart() method in Windows Azure WorkRole project:
	```c#
	public override bool OnStart()
	{
			// Set the maximum number of concurrent connections 
			ServicePointManager.DefaultConnectionLimit = 100;

			// For information on handling configuration changes
			// see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

			m_Bootstrap = BootstrapFactory.CreateBootstrap();

			if (!m_Bootstrap.Initialize())
			{
					Trace.WriteLine("Failed to initialize SuperSocket!", "Error");
					return false;
			}

			var result = m_Bootstrap.Start();

			switch (result)
			{
					case (StartResult.None):
							Trace.WriteLine("No server is configured, please check you configuration!");
							return false;

					case (StartResult.Success):
							Trace.WriteLine("The server has been started!");
							break;

					case (StartResult.Failed):
							Trace.WriteLine("Failed to start SuperSocket server! Please check error log for more information!");
							return false;

					case (StartResult.PartialSuccess):
							Trace.WriteLine("Some server instances were started successfully, but the others failed to start! Please check error log for more information!");
							break;
			}

			return base.OnStart();
	}
	```
## Configure Input Endpoint and then Use it
- Because of Windows Azure's internal network infrastructure, you cannot listen the ip/port you configured directly. In this case, you need to configure input endpoint in Windows Azure project:
	![image](https://raw.githubusercontent.com/kerryjiang/SuperSocket.Document/v1.6/images/windowsazure.jpg)

- The endpoint's naming rule is "AppServerName_ConfiguredListenPort". For example, we have a server named "RemoteProcessServer" configured like below:
	```xml
	<server name="RemoteProcessServer"
						serverTypeName="remoteProcess"
						ip="Any" port="2012" />
	```

- Then, we should create an endpoint with the name "RemoteProcessServer_2012".

- In the code, we can get the input endpoint's real port by programming:
	```c#
	var instanceEndpoint = RoleEnvironment.CurrentRoleInstance.InstanceEndpoints[serverConfig.Name + "_" + serverConfig.Port].Port;
	```

- But you needn't do it for SuperSocket by yourself, because the listen endpoint replacement has been implemented within the SuperSocket. What you should do is passing in the input endpoint dictionary when initialize the bootstrap, the final code should look like the code below:
	```c#
	public override bool OnStart()
	{
			// Set the maximum number of concurrent connections 
			ServicePointManager.DefaultConnectionLimit = 100;

			// For information on handling configuration changes
			// see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

			m_Bootstrap = BootstrapFactory.CreateBootstrap();

			if (!m_Bootstrap.Initialize(RoleEnvironment.CurrentRoleInstance.InstanceEndpoints.ToDictionary(p => p.Key, p => p.Value.IPEndpoint)))
			{
					Trace.WriteLine("Failed to initialize SuperSocket!", "Error");
					return false;
			}

			var result = m_Bootstrap.Start();

			switch (result)
			{
					case (StartResult.None):
							Trace.WriteLine("No server is configured, please check you configuration!");
							return false;

					case (StartResult.Success):
							Trace.WriteLine("The server has been started!");
							break;

					case (StartResult.Failed):
							Trace.WriteLine("Failed to start SuperSocket server! Please check error log for more information!");
							return false;

					case (StartResult.PartialSuccess):
							Trace.WriteLine("Some server instances were started successfully, but the others failed to start! Please check error log for more information!");
							break;
			}

			return base.OnStart();
	}
	```
- Finally, you can try to start your Windows Azure instance now.

# 22. Run SuperSocket in Linux/Unix
> Keywords: Mono, Linux, Unix, Mono Service, Cross Platform

## SuperSocket supports cross-platform compatibility (Unix/Linux) of .NET applications by Mono (Mono 2.10 or later version)
- As the Unix/Linux has different file path format with Windows, SuperSocket provides a different log4net configuration file (/Solution Items/log4net.unix.config) for Unix/Linux systems.

- Therefore, you need to include this file to your project in subdirectory "Config" of output directory.

- In Unix/Linux operating system, SuperSocket also can run as a console application or a service (Mono Service) like it in Windows.

- Console:
	```
	mono SuperSocket.SocketService.exe
	```

- Mono Service:
	```
	mono-service -l:supersocket.lock -m:supersocket.log [-d:<workdir>] SuperSocket.SocketService.exe
	```
- The parameter -d:<workdir> is required if the current directory is not the root of your application where the file SuperSocket.SocketService.exe locates.

# 23. SuperSocket ServerManager
> Keywords: ServerManager, Management, Management Client, SuperSocket Monitoring

## What's the SuperSocket ServerManager?
- SuperSocket ServerManager is a component of SuperSocket which allow you to manage and monitor your SuperSocket server applications from a client application with GUI.

## Setup ServerManager in the server side
- Actually, the ServerManager is an independent AppServer of SuperSocket. To let it work, please ensure these assemblies below exist in your working directory at first:
	- SuperSocket.ServerManager.dll (compile from the source code directory "Management\Server")
	- SuperSocket.WebSocket.dll (compile from the source code directory "Protocols\WebSocket")

- Then you should configure a server instance for this AppServer together with the server instances which you want to manage and monitor:
	```xml
	<superSocket isolation="Process">
			<servers>
				<server name="ServerA"
								serverTypeName="SampleServer"
								ip="Any" port="2012">
					<commandAssemblies>
						<add assembly="SuperSocket.QuickStart.SampleServer.CommandAssemblyA"></add>
						<add assembly="SuperSocket.QuickStart.SampleServer.CommandAssemblyB"></add>
					</commandAssemblies>
				</server>
				<server name="ServerB"
								serverTypeName="SampleServer"
								ip="Any" port="2013">
					<commandAssemblies>
						<add assembly="SuperSocket.QuickStart.SampleServer.CommandAssemblyB"></add>
						<add assembly="SuperSocket.QuickStart.SampleServer.CommandAssemblyC"></add>
					</commandAssemblies>
				</server>
				<server name="ManagementServer"
								serverType="SuperSocket.ServerManager.ManagementServer, SuperSocket.ServerManager">
					<listeners>
						<add ip="Any" port="4502" />
					</listeners>
					<users>
						<user name="kerry" password="123456"/>
					</users>
				</server>
			</servers>
			<serverTypes>
				<add name="SampleServer"
						type="SuperSocket.QuickStart.ServerManagerSample.SampleServer, SuperSocket.QuickStart.ServerManagerSample" />
			</serverTypes>
	</superSocket>
	```

- In above configuration, the ServerA and ServerB are your normal server instances. Additionally, you should add a server with "SuperSocket.ServerManager.ManagementServer, SuperSocket.ServerManager" as it's server type. As you see, the child node "users" defines the username/password which are allowed to connect to the ServerManager.

- If you want Silverlight client to connect this ServerManager, you should add a policy server in the configuration:
	```xml
	<server name="SilverlightPolicyServer"
						serverType="SuperSocket.Facility.PolicyServer.SilverlightPolicyServer, SuperSocket.Facility"
						ip="Any" port="943"
						receiveBufferSize="32"
						maxConnectionNumber="10"
						policyFile="Config\Silverlight.config"
						clearIdleSession="true">
	</server>
	```

- At the same time, you'd better add the policy server's name into the ServerManager's excludedServers list:
	```
	excludedServers="SilverlightPolicyServer"
	```

- Usually, you needn't care about the status of the policy server. After you add this configuration attribute, the Silverlight policy server will be hided in your ServerManager client.

## SuperSocket ServerManager Client
- SuperSocket ServerManager now has two kinds of clients, Silverlight Client and WPF client. The code of both locates in the source code directory "Management", you can build them by yourself.

- We also provide an online Silverlight client, which can be used directly:

	http://servermanager.supersocket.net/

	When you want to connect a SuperSocket server from the client, you need fill these information below:

	![image](https://raw.githubusercontent.com/kerryjiang/SuperSocket.Document/v1.6/images/servermanagerconfig.jpg)

	```
	Name: A identity of the server in your client;
	URI: the SuperSocket ServerManager's listening endpoint, it is a websocket uri (start with "ws://" or "wss://", because we use websocket protocol between client and server);
	User Name: the username which is configured in the SuperSocket ServerManager's users child node; 
	Password: the password which is configured in the SuperSocket ServerManager's users child node; 
	```

- After the connection is established, you will see the SuperSocket server's status.

	![image](https://raw.githubusercontent.com/kerryjiang/SuperSocket.Document/v1.6/images/servermanagershow.jpg)

- You also can start and stop the server instances within the client:

	![image](https://raw.githubusercontent.com/kerryjiang/SuperSocket.Document/v1.6/images/servermanagercontrol.jpg)

## Security Consideration
- For security reasons, you can enable the TLS/SSL trasnferring layer encryption for your ServerManager instance, please read the document below, then you will know how to do it:

	Enable TLS/SSL trasnferring layer encryption in SuperSocket

- After you enable TLS/SSL for the server side, you should use a secure websocket uri to connect the server:

	wss://***

# 24. New Features and Breaking Changes
> Keywords: SuperSocket 1.6, New Features, Breaking Changes, textEncoding, defaultCulture, Process Isolation, SuperSocket.Agent.exe

## The new configuration attribute "textEncoding"
- In the SuperSocket before 1.6, when you send a text message via session object, the default encoding to convert the text message to binary data which can be sent over socket is UTF8. You can change it by assigning a new encoding to the session's Charset property.

- Now in SuperSocket 1.6, you can define the default text encoding in the configuration. The new attribute "textEncoding" has been added into the server configuration node:
	```xml
	<superSocket>
			<servers>
				<server name="TelnetServer"
						textEncoding="UTF-8"
						serverType="YourAppServer, YourAssembly"
						ip="Any" port="2020">
				</server>
			</servers>
	</superSocket>
	```

## The new configuration attribute "defaultCulture"
- This new added feature is only for the .Net framework 4.5 and above. It allows you to set default culture for all the threads, no matter how the threads are created, by programming or come from thread pool.

- The new configuration attribute "defaultCulture" can be added in the root node or the server node, so you can configure the default culture for all the server instances or for each server instance separately:
	```xml
	<superSocket defaultCulture="en-US">
			<servers>
				<server name="TelnetServerA"
						serverType="YourAppServer, YourAssembly"
						ip="Any" port="2020">
				</server>
				<server name="TelnetServerB"
						defaultCulture="zn-CN"
						serverType="YourAppServer, YourAssembly"
						ip="Any" port="2021">
				</server>
				<server name="TelnetServerC"
						defaultCulture="zn-TW"
						serverType="YourAppServer, YourAssembly"
						ip="Any" port="2021">
				</server>
			</servers>
	</superSocket>
	```

## Process level isolation
- In SuperSocket 1.5, we added AppDomain level isolation, you can run your multiple appserver instances in the their own AppDomain. This feature provides higher level security and resource isolation and bring more benefits to the users who run SuperSocket as multiple hosting service.

- In SuperSocket 1.6, we introduce much higher level isolation "Process". If you configure the SuperSocket in this kind isolation, you appserver instances will run in the separated processes. The configuration looks like that:
	```xml
	<superSocket isolation="Process">
			<servers>
				<server name="TelnetServerA"
						serverType="YourAppServer, YourAssembly"
						ip="Any" port="2020">
				</server>
				<server name="TelnetServerB"
						serverType="YourAppServer, YourAssembly"
						ip="Any" port="2021">
				</server>
			</servers>
	</superSocket>
	```

- Beyond the configuration, you need to include an executable assembly "SuperSocket.Agent.exe" into your project output, which is provided by SuperSocket.

- After you start your SuperSocket, you will find more processes of SuperSocket:
	```
	SuperSocket.SocketService.exe
	SuperSocket.Agent.exe
	SuperSocket.Agent.exe
	```

## Changes about the performance data collecting
- The API collecting performance data has been changed, two virtual method have been changed:
	```c#
	protected virtual void UpdateServerSummary(ServerSummary serverSummary);

	protected virtual void OnServerSummaryCollected(NodeSummary nodeSummary, ServerSummary serverSummary)
	```

	To:
	```c#
	protected virtual void UpdateServerStatus(StatusInfoCollection serverStatus)

	protected virtual void OnServerStatusCollected(StatusInfoCollection nodeStatus, StatusInfoCollection serverStatus)
	```
- The classes ServerSummary and NodeSummary have been removed. Now you should use the class StatusInfoCollection.

## The new configuration attribute "storeLocation" for the certificate node
- You can specific the store location of the certificate which you want to load:
	```xml
	<certificate storeName="My" storeLocation="LocalMachine" thumbprint="f42585bceed2cb049ef4a3c6d0ad572a6699f6f3"/>
	```

## Start a connection from server initiatively
- You can connect a remote endpoint from the server side initiatively, the following network communications after the connection is established are same with the connections started from clients.
	```c#
	var activeConnector = appServer as IActiveConnector;
	var task = activeConnector.ActiveConnect(remoteEndPoint);
	task.ContinueWith(
						t => Logger.InfoFormat("Client connected, SessionID: {0}", t.Result.Session.SessionID),
						TaskContinuationOptions.OnlyOnRanToCompletion);
	```

## SuperSocket ServerManager
- Document of SuperSocket ServerManager

## Client certificate validation
- In TLS/SSL communications, the client side certificate is not a must, but some systems require much higher security guarantee. So some users asked the feature validating client certificate from the server side. Now in SuperSocket 1.6, this feature has been added.

- At first, to enable the client certificate validation, you should add the attribute "clientCertificateRequired" in the certificate node of the configuration:
	```xml
	<certificate storeName="My"
							storeLocation="LocalMachine"
							clientCertificateRequired="true"
							thumbprint="f42585bceed2cb049ef4a3c6d0ad572a6699f6f3"/>
	```						 

- Then you can override the AppServer's method "ValidateClientCertificate(...)" the implement your validation logic:
	```c#
	protected override bool ValidateClientCertificate(YourSession session, object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
	{
		//Check sslPolicyErrors

		//Check certificate

		//Return checking result
		return true;
	}
	```