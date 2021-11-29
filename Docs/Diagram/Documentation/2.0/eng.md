# Contents
1. A Telnet Example
2. Start SuperSocket by Configuration
3. Some basic Concepts in SuperSocket
4. The Built-in Command Line PipelineFilter
5. Implement your PipelineFilter
6. Command and Command Filter
7. Extend Your AppSession and SuperSocketService
8. Get the Connected Event and Closed Event of a Connection
9. WebSocket Server
10. Multiple Listeners
11. Multiple Server Instances
12. Enable Transport Layer Security in SuperSocket
13. Integrate with ASP.Net Core Website and ABP Framework
14. UDP Support in SuperSocket

# 1. A Telnet Example
> Keywords: Telnet, Console Project, References

## Prerequisites
- Make sure you have installed the latest .NET Core SDK (3.0 or above version)

## Create a Console project and add references of SuperSocket (targets to netcoreapp3.0)
	
```
dotnet new console
dotnet add package SuperSocket.Server --version 2.0.0-*
```

## Using the namespaces of SuperSocket and other namespaces you might need
```c#
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SuperSocket;
using SuperSocket.ProtoBase;
```

## Write the code about SuperSocket host startup in the Main method
```c#	
static async Task Main(string[] args)
{
	var host = ...;
	...
	await host.RunAsync();
}
```

## Create the host
```c#
var host = SuperSocketHostBuilder.Create<StringPackageInfo, CommandLinePipelineFilter>();
```

- Create the SuperSocket host with the package type and the pipeline filter type.

## Register package handler which processes incoming data
```c#
.UsePackageHandler(async (session, package) =>
{
	var result = 0;

	switch (package.Key.ToUpper())
	{
		case ("ADD"):
			result = package.Parameters
				.Select(p => int.Parse(p))
				.Sum();
			break;

		case ("SUB"):
			result = package.Parameters
				.Select(p => int.Parse(p))
				.Aggregate((x, y) => x - y);
			break;

		case ("MULT"):
			result = package.Parameters
				.Select(p => int.Parse(p))
				.Aggregate((x, y) => x * y);
			break;
	}

	await session.SendAsync(Encoding.UTF8.GetBytes(result.ToString() + "\r\n"));
})
```
- Send the received text back to the clinet.

## Configure logging
```c#
.ConfigureLogging((hostCtx, loggingBuilder) =>
{
	loggingBuilder.AddConsole();
})
```

- Enable the console output only. You also can register your own thirdparty logging library over here.

## Configure server's information like name and listening endpoint adn build the host
```c#
.ConfigureSuperSocket(options =>
{
	options.Name = "Echo Server";
	options.Listeners = new [] {
		new ListenOptions
		{
			Ip = "Any",
			Port = 4040
		}
	};
}).Build();
```

## Start the host
- await host.RunAsync();

# 2. Start SuperSocket by Configuration
> Keywords: Start by Configuration, Configuration

## The configuration file of SuperSocket
- Same as Asp.Net Core, SuperSocket uses the JSON configuration file appsettings.json. We just need leave the file in root of the application folder and make sure it can be copied to the output directory of he build.
	```
	appsettings.json
	appsettings.Development.json // for Development environment
	appsettings.Production.json // for Production environment
	```

## How to Start Server with the configuration file
- Actually, you don't need do anything else except writing the normal startup code.
	```c#
	var host = SuperSocketHostBuilder.Create<StringPackageInfo, CommandLinePipelineFilter>()
			.UsePackageHandler(async (s, p) =>
			{
					// handle packages
			})
			.ConfigureLogging((hostCtx, loggingBuilder) =>
			{
					loggingBuilder.AddConsole();
			})
			.Build();

	await host.RunAsync();
	```

## The format of the configuration file (appsettings.json)
- It is a sample:
	```json
	{
			"serverOptions": {
					"name": "GameMsgServer",
					"listeners": [
							{
									"ip": "Any",
									"port": "2020"
							},
							{
									"ip": "192.168.3.1",
									"port": "2020"
							}
					]
			}
	}
	```

- Options:
	- name: the name of the server;
	- maxPackageLength: max allowed package size in the server; 4M by default;
	- receiveBufferSize: the size of the receiving buffer; 4k by default;
	- sendBufferSize: the size of the sending buffer; 4k by default;
	- receiveTimeout: the timeout for receiving; in milliseconds;
	- sendTimeout: the timeout for sending; in milliseconds;
	- listeners: the listener enpoints of this server;
	- listeners/*/ip: the listener's listening IP; Any: any ipv4 ip addresses, IPv6Any: any ipv6 ip addresses, other actual IP addresses;
	- listeners/*/port: the listener's listening port;
	- listeners/*/backLog: the maximum length of the pending connections queue;
	- listeners/*/noDelay: specifies whether the stream Socket is using the Nagle algorithm;
	- listeners/*/security: None/Ssl3/Tls11/Tls12/Tls13; the TLS protocol version the communication uses;
	- listeners/*/certificateOptions: the options of the certificate which will be used for the TLS encryption/decryption;

# 3. Some basic Concepts in SuperSocket
> Keywords: basic concepts

## Package Type
- The package type represents the data structure of the package we receive from the other end.

- The type TextPackageInfo(SuperSocket.ProtoBase.TextPackageInfo,SuperSocket.ProtoBase) is the simplest defined package type within SuperSocket. It only standards for a text string.
	```c#
	public class TextPackageInfo
	{
			public string Text { get; set; }
	}
	```

- But we usually have more complex network package structure than that. For instance, the package type is defined below tells us their package has two fields, Sequence and Body.
	```c#
	public class MyPackage
	{
			public short Sequence { get; set; }

			public string Body { get; set; }
	}
	```

- Some packages may have a special field which can tell which kind of package it is. We call this field "key" here. This key field also tells which kind logic should handle the pakcgae. It is very common scenario in the design of network applications. In this case, you just need implement the interface IKeyedPackageInfo for your package type if the key is an integer:
	```c#
	public class MyPackage : IKeyedPackageInfo<int>
	{
			public int Key { get; set; }

			public short Sequence { get; set; }

			public string Body { get; set; }
	}
	```

- In the following parts of the document, we will explain how to define commands to handle different kinds of packages according the value in the key field.

## PipelineFilter Type
- This kind type plays an important role in the network protocol decoding. It defines how we decode IO stream into packages which can be understanded by the application.

- These are the basic interfaces for PipelineFilter. At least one PipelineFilter type which implement this interface is required in the system.
	```c#
	public interface IPipelineFilter
	{
			void Reset();

			object Context { get; set; }        
	}

	public interface IPipelineFilter<TPackageInfo> : IPipelineFilter
			where TPackageInfo : class
	{

			IPackageDecoder<TPackageInfo> Decoder { get; set; }

			TPackageInfo Filter(ref SequenceReader<byte> reader);

			IPipelineFilter<TPackageInfo> NextFilter { get; }

	}
	```
	- CommandLinePipelineFilter (SuperSocket.ProtoBase.CommandLinePipelineFilter, SuperSocket.ProtoBase) is one of our most common PipelineFilter templates. We use it in documents and samples very often.

## Create the SuperSocket Host with Package Type and PipelineFilter Type
- After you define your package info type and the pipeline filter type, you should be able to create a SuperSocket host by SuperSocketHostBuilder.
	```c#
	var host = SuperSocketHostBuilder.Create<StringPackageInfo, CommandLinePipelineFilter>();
	```

- In some cases, you may need implement IPipelineFilterFactory to get full control of creating of PipelineFilter.
	```c#
	public class MyFilterFactory : PipelineFilterFactoryBase<TextPackageInfo>
	{
			protected override IPipelineFilter<TPackageInfo> CreateCore(object client)
			{
					return new FixedSizePipelineFilter(10);
			}
	}
	```

- Then you will need to use the PipelineFilterFactory after your create the SuperSocket host:
	```c#
	var host = SuperSocketHostBuilder.Create<StringPackageInfo>();
	host.UsePipelineFilterFactory<MyFilterFactory>();
	```

# 4.The Built-in Command Line PipelineFilter
> Keywords: Command Line, Protocol, StringRequestInfo, Text Encoding

## What's the Protocol?
- What's the Protocol? Lots of people probably will answer "TCP" or "UDP". But to build a network application, only TCP or UDP is not enough. TCP and UDP are transport-layer protocols. It's far from enough to enable talking between two endpoints in the network if you only define transport-layer protocol. You need to define your application level protocol to convert your received binary data to the requests which your application can understand.

## The Built-in Command Line PipelineFilter
- The command line protocol is a widely used protocols, lots of protocols like Telnet, SMTP, POP3 and FTP protocols are base on command line protocol etc. The CommandLinePipelineFilter is a PipelineFilter which was designed for command line protocol.

- The command line protocol defines each request must be ended with a carriage return "\r\n".

- If you use the command line protocol in SuperSocket, all requests will to translated into StringRequestInfo instances.

- StringRequestInfo is defined like this:
	```c#
	public class StringPackageInfo : IKeyedPackageInfo<string>, IStringPackage
	{
			public string Key { get; set; }

			public string Body { get; set; }

			public string[] Parameters { get; set; }
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
	public class LOGIN : IAsyncCommand<StringPackageInfo>
	{
			public async ValueTask ExecuteAsync(IAppSession session, StringPackageInfo package)
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
	public class CustomPackageDecoder : IPackageDecoder<StringPackageInfo>
	{
			public StringPackageInfo Decode(ref ReadOnlySequence<byte> buffer, object context)
			{
					var text = buffer.GetString(new UTF8Encoding(false));
					var parts = text.Split(':', 2);

					return new StringPackageInfo
					{
							Key = parts[0],
							Body = text,
							Parameters = parts[1].Split(',')
					};
			}
	}

	// register the custom package decoder through the host builder

	builder.UsePackageDecoder<CustomPackageDecoder>();
	```

# 5. Implement your PipelineFilter
- Keywords: PipelineFilter

## The Built-in PipelineFilter Templates
- After reading the previous document, you probably find implementing your own protocol using SuperSocket is not easy for you. But actually, you don't need implement PipelineFilter by yourself from scratch, because SuperSocket provides already many built-in PipelineFilter templates (base classes) which almost cover 90% of the cases and simplify your development work significantly. Even if the templates are not fitting your requirement, developing a PipelineFilter completely by yourself should be easy as well.

- SuperSocket povides these built-in PipelineFilter templates:
	- TerminatorPipelineFilter (SuperSocket.ProtoBase.TerminatorPipelineFilter, SuperSocket.ProtoBase)
	- TerminatorTextPipelineFilter (SuperSocket.ProtoBase.TerminatorTextPipelineFilter, SuperSocket.ProtoBase)
	- LinePipelineFilter (SuperSocket.ProtoBase.LinePipelineFilter, SuperSocket.ProtoBase)
	- CommandLinePipelineFilter (SuperSocket.ProtoBase.CommandLinePipelineFilter, SuperSocket.ProtoBase)
	- BeginEndMarkPipelineFilter (SuperSocket.ProtoBase.BeginEndMarkPipelineFilter, SuperSocket.ProtoBase)
	- FixedSizePipelineFilter (SuperSocket.ProtoBase.FixedSizePipelineFilter, SuperSocket.ProtoBase)
	- FixedHeaderPipelineFilter (SuperSocket.ProtoBase.FixedHeaderPipelineFilter, SuperSocket.ProtoBase)

## How to Implement PipelineFilter base on the built-in Templates
- FixedHeaderPipelineFilter - Fixed Header with Body Length Protocol

- This kind protocol defines each request has two parts, the first part contains some basic information of this request including the length of the second part. We usually call the first part is header and the second part is body.

- For example, we have a protocol like that: the header contains 3 bytes, the first byte represent the request's type, the next 2 bytes represent the length of the body. And the body is a text string.
	```
	/// +-------+---+-------------------------------+
	/// |request| l |                               |
	/// | type  | e |    request body               |
	/// |  (1)  | n |                               |
	/// |       |(2)|                               |
	/// +-------+---+-------------------------------+
	```

- According the protocol specification, we can design the package type like the code below:
	```c#
	public class MyPackage
	{
			public byte Key { get; set; }

			public string Body { get; set; }
	}
	```

- Next step is to design the PipelineFilter:
	```c#
	public class MyPipelineFilter : FixedHeaderPipelineFilter<MyPackage>
	{
			public MyPipelineFilter()
					: base(3) // because the header size is 3, so pass 3 to the parent class's constructor
			{

			}

			// get body length from the header
			protected override int GetBodyLengthFromHeader(ref ReadOnlySequence<byte> buffer)
			{
					var reader = new SequenceReader<byte>(buffer);
					reader.Advance(1); // skip the first byte
					reader.TryReadBigEndian(out short len);
					return len;
			}

			protected override MyPackage DecodePackage(ref ReadOnlySequence<byte> buffer)
			{
					var package = new MyPackage();

					var reader = new SequenceReader<byte>(buffer);

					reader.TryRead(out byte packageKey);
					package.Key = packageKey;            
					reader.Advance(2); // skip the length             
					package.Body = reader.ReadString();

					return package;
			}
	}
	```

- Finally, you can use the Package type and the PipelineFilter type to build the host:
	```c#
	var host = SuperSocketHostBuilder.Create<MyPackage, MyPipelineFilter>()
			.UsePackageHandler(async (s, p) =>
			{
					// handle your package over here
			}).Build();
	```

- You also can get more flexiblity by moving the code about package decoding from PipelineFilter to package decoder:
	```c#
	public class MyPackageDecoder : IPackageDecoder<MyPackage>
	{
			public MyPackage Decode(ref ReadOnlySequence<byte> buffer, object context)
			{
					var package = new MyPackage();

					var reader = new SequenceReader<byte>(buffer);

					reader.TryRead(out byte packageKey);
					package.Key = packageKey;            
					reader.Advance(2); // skip the length             
					package.Body = reader.ReadString();

					return package;
			}
	}
	```

- Use the method UsePackageDecoder of the host builder to enable it in SuperSocket:
	```c#
	builder.UsePackageDecoder<MyPackageDecoder>();
	````

# 6. Command and Command Filter
> Keywords: Command, Command Filter

## Command
- Command in SuperSocket is designed to handle the requests coming from the clients, it play an important role in the business logic processing.

- Command class must implement the basic command interface below, choose Sync Command or Async Commnad as your need:
	```c#
	// Sync Command
	public interface ICommand<TAppSession, TPackageInfo>
			where TAppSession : IAppSession
	{
			void Execute(TAppSession session, TPackageInfo package);
	}

	// Async Command
	public interface IAsyncCommand<TAppSession, TPackageInfo> : ICommand
			where TAppSession : IAppSession
	{
			ValueTask ExecuteAsync(TAppSession session, TPackageInfo package);
	}
	```

- The request package processing code should be placed in the method "Execute" or "ExecuteAsync". Every command has its own metadata which includes Name and Key.

- Name: human friendly name of the command; Key: the object we use to match package's key;

- We can define command's metadata (Name='ShowVoltage', Key=0x03) by attribute on the command class:
	```c#
	[Command(Key = 0x03)]
	public class ShowVoltage : IAsyncCommand<StringPackageInfo>
	{
			public async ValueTask ExecuteAsync(IAppSession session, StringPackageInfo package)
			{
					...
			}
	}
	```

- By default, command's Name and Key would be same as its class name if there is no command metadata attribute defined for the command class.

- The metadata value "Key" is used for matching the received package. When a package instance is received, SuperSocket will look for the command which has the responsibility to handle it by matching the package's Key and the command's Key.

- But, there is a prerequisite that the package type must implement the interface IKeyedPackageInfo (TKey may be any primitive type like int, string, short or byte), like StringPackageInfo:
	```c#
	public class StringPackageInfo : IKeyedPackageInfo<string>
	```

- For instance, if we receive a package like below:
	```
	Key: "ADD"
	Body: "1 2"
	```

- Then SuperSocket will looking for a command whose key is "ADD". If we have a command defined below:
	```c#
	public class ADD : IAsyncCommand<string, StringPackageInfo>
	{
		public async ValueTask ExecuteAsync(IAppSession session, StringPackageInfo package)
		{
			var result = package.Parameters
					.Select(p => int.Parse(p))
					.Sum();

			await session.SendAsync(Encoding.UTF8.GetBytes(result.ToString() + "\r\n"));
		}
	}
	```

- This command will be found after you register it.

## Register command
```c#
hostBuilder.UseCommand((commandOptions) =>
{
		// register commands one by one
		commandOptions.AddCommand<ADD>();
		//commandOptions.AddCommand<MULT>();
		//commandOptions.AddCommand<SUB>();

		// register all commands in one aassembly
		//commandOptions.AddCommandAssembly(typeof(SUB).GetTypeInfo().Assembly);
}
```

## Command Filter
- The Command Filter in SuperSocket works like Action Filter in ASP.NET MVC, you can use it to intercept execution of Command。 The Command Filter will be invoked before or after the command executes.

- Synchronous CommandFilter:
	```c#
	public class HelloCommandFilterAttribute : CommandFilterAttribute
	{
			public override void OnCommandExecuted(CommandExecutingContext commandContext)
			{
					Console.WriteLine("Hello");
			}

			public override bool OnCommandExecuting(CommandExecutingContext commandContext)
			{
					Console.WriteLine("Bye bye");
					return true;
			}
	}
	```

- Asynchronous CommandFilter:
	```c#
	public class AsyncHelloCommandFilterAttribute  : AsyncCommandFilterAttribute
	{
			public override async ValueTask OnCommandExecutedAsync(CommandExecutingContext commandContext)
			{
					Console.WriteLine("Hello");
					await Task.Delay(0);
			}

			public override async ValueTask<bool> OnCommandExecutingAsync(CommandExecutingContext commandContext)
			{
					Console.WriteLine("Bye bye");
					await Task.Delay(0);
					return true;
			}
	}
	```

- Apply CommandFilter to Command:
	```c#
	[AsyncHelloCommandFilter]
	[HelloCommandFilter]
	class COUNTDOWN : IAsyncCommand<StringPackageInfo>
	{
			//...
	}
	```

## Global Command Filter
- Global command filter is just one command filter which is applied to all the commands.

- Register global command filter:
	```c#
	hostBuilder.UseCommand((commandOptions) =>
	{
			commandOptions.AddCommand<COUNT>();
			commandOptions.AddCommand<COUNTDOWN>();

			// register global command filter
			commandOptions.AddGlobalCommandFilter<HitCountCommandFilterAttribute>();
	}
	```

## Register Commands from the Configuration File
- You can add command assemblies under the node "commands/assemblies". At the meanwhile, please make sure the command assembly files are copied into the working directory of the application.

- It is the sample of the configuration:
	```json
	{
		"serverOptions": {
			"name": "GameMsgServer",
			"listeners": [
				{
					"ip": "Any",
					"port": "2020"
				},
				{
					"ip": "192.168.3.1",
					"port": "2020"
				}
			],
			"commands": {
				"assemblies": [
					{
						"name": "CommandAssemblyName"
					}
				]
			}
		}
	}
	```

- Beside that, you may need do one more thing. Because .NET Core application only look for assembly in its depedency tree (*.deps.json) by default when it does reflection. Your command assembly may not be able to be founed if you didn't add it as reference in the major project. To work around this problem, you should add a runtime config file "runtimeconfig.json" in the root of your major project.

- runtimeconfig.json
	```json
	{
		"runtimeOptions": {
			"Microsoft.NETCore.DotNetHostPolicy.SetAppPaths" : true            
		}
	}
	```

# 7. Extend Your AppSession and SuperSocketService
> Keywords: SuperSocketService, AppSession

## What is AppSession?
- AppSession represents a logic socket connection, connection based operations should be defined in this class. You can use the instance of this class to send data to clients, receive data from connection or close the connection.

## What is SuperSocketService?
- SuperSocketService stands for the service instance which listens all client connections, hosts all connections. Application level operations and logics can be defined in it.

## Extend your AppSession
1. You can override base AppSession's operations
	```c#
	public class TelnetSession : AppSession
	{
		protected override async ValueTask OnSessionConnectedAsync()
		{
			// do something right after the sesssion is connected
		}            

		protected override async ValueTask OnSessionClosedAsync(EventArgs e)
		{
			// do something right after the sesssion is closed
		}
	}
	```

- You also can add new properties for your session according your business requirement Let me create a AppSession which would be used in a game server:
	```c#
	public class PlayerSession ：AppSession
	{
		public int GameHallId { get; internal set; }

		public int RoomId { get; internal set; }
	}
	```

3. Adopt your own AppSession class in your SuperSocket service Register the application session type through builder:
	```c#
	builder.UseSession<MySession>();
	```

## Create your own SuperSocket service
1. Extend the SuperSocketService and override the methods if you want
	```c#
	public class GameService<TReceivePackageInfo> : SuperSocketService<TReceivePackageInfo>
			where TReceivePackageInfo : class
	{
		public GameService(IServiceProvider serviceProvider, IOptions<ServerOptions> serverOptions)
				: base(serviceProvider, serverOptions)
		{

		}

		protected override async ValueTask OnSessionConnectedAsync(IAppSession session)
		{
			// do something right after the sesssion is connected
			await base.OnSessionConnectedAsync(session);
		}

		protected override async ValueTask OnSessionClosedAsync(IAppSession session)
		{
			// do something right after the sesssion is closed
			await base.OnSessionClosedAsync(session);
		}

		protected override async ValueTask OnStartedAsync()
		{
			// do something right after the service is started
		}

		protected override async ValueTask OnStopAsync()
		{
			// do something right after the service is stopped
		}
	}
	```

2. Start to use your own service type Register the service type through builder:
	```c#
	builder.UseHostedService<GameService<TReceivePackageInfo>>();
	```

# 8. Get the Connected Event and Closed Event of a Connection
> Keywords: Connection, Session, Connected Event, Closed Event

## Register session open/close handlers by the method ConfigureSessionHandler of the host builder
```c#
builder.UseSessionHandler((s) =>
    {
        // things to do when the session just connects
    },
    (s, e) =>
    {
        // s: the session
        // e: the CloseEventArgs
        // e.Reason: the close reason
        // things to do after the session closes
    });
```

## Handle the session events by extending the application session
- Define your own application session type and handle the session events in the override methods:
	```c#
	public class MyAppSession : AppSession
	{
		protected override ValueTask OnSessionConnectedAsync()
		{
				// the logic after the session gets connected
		}

		protected override ValueTask OnSessionClosedAsync(CloseEventArgs e)
		{
				// the logic after the session gets closed
		}
	}
	```

- Enable your own application session with the host builder:
	```c#
	hostBuiler.UseSession<MyAppSession>();
	```

## Handle the session events by extending the SuperSocketService
- Define your own SuperSocket service type and override the session event handling methods:
	```c#
	public class GameService<TReceivePackageInfo> : SuperSocketService<TReceivePackageInfo>
			where TReceivePackageInfo : class
	{
		protected override ValueTask OnSessionConnectedAsync(IAppSession session)
		{
				// do something right after the sesssion gets connected
		}

		protected override ValueTask OnSessionClosedAsync(IAppSession session, CloseEventArgs e)
		{
				// do something right after the sesssion gets closed
		}
	}
	```

- Use your own SuperSocket service type when you create the host:
	```c#
	builder.UseHostedService<GameService<StringPackageInfo>>();
	```

# 9. WebSocket Server
> Keywords: WebSocket

## Create a WebSocket Server
- At first, you should reference the package SuperSocket.WebSocket.Server
	```
	dotnet add package SuperSocket.WebSocket.Server --version 2.0.0-*
	```

- Then add the using statement
	```c#
	using SuperSocket.WebSocket.Server;
	```

- Let's create the WebSocket server. This server just echo messages back to the client
	```c#
	var host = WebSocketHostBuilder.Create()
			.UseWebSocketMessageHandler(
					async (session, message) =>
					{
							await session.SendAsync(message.Message);
					}
			)
			.ConfigureAppConfiguration((hostCtx, configApp) =>
			{
					configApp.AddInMemoryCollection(new Dictionary<string, string>
					{
							{ "serverOptions:name", "TestServer" },
							{ "serverOptions:listeners:0:ip", "Any" },
							{ "serverOptions:listeners:0:port", "4040" }
					});
			})
			.ConfigureLogging((hostCtx, loggingBuilder) =>
			{
					loggingBuilder.AddConsole();
			})
			.Build();

	await host.RunAsync();
	```

## Define commands to handle messages
- Define at least one command
	```c#
	class ADD : IAsyncCommand<WebSocketSession, StringPackageInfo>
	{
		public async ValueTask ExecuteAsync(WebSocketSession session, StringPackageInfo package)
		{
			var result = package.Parameters
					.Select(p => int.Parse(p))
					.Sum();

			await session.SendAsync(result.ToString());
		}
	}
	```

- Register the command
	```c#
	builder
		.UseCommand<StringPackageInfo, StringPackageConverter>(commandOptions =>
		{
			// register commands one by one
			commandOptions.AddCommand<ADD>();
		});
	```

- The type parameter StringPackageConverter is the type which can convert WebSocketPackage to your application package.
	```c#
	class StringPackageConverter : IPackageMapper<WebSocketPackage, StringPackageInfo>
	{
		public StringPackageInfo Map(WebSocketPackage package)
		{
			var pack = new StringPackageInfo();
			var arr = package.Message.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
			pack.Key = arr[0];
			pack.Parameters = arr.Skip(1).ToArray();
			return pack;
		}
	}
	```

# 10. Multiple Listeners
- Keywords: Multiple Listeners, Multiple Port, Multiple Endpoints, Multiple Listeners Configuration, IP, Port

## Single listener
- In the configuration below, you can configure the server instance's listening IP and port:
	```json
	{
		"serverOptions": {
			"name": "EchoServer",
			"listeners": [
				{
					"ip": "Any",
					"port": "2020"
				}
			]
		}
	}
	```

## Multiple listeners
- You also can add more than one element under the configuration node "listeners":
	```json
	{
		"serverOptions": {
			"name": "EchoServer",
			"listeners": [
				{
					"ip": "Any",
					"port": "2020"
				},
				{
					"ip": "192.168.3.1",
					"port": "2020"
				}
			]
		}
	}
	```

- In this case, the server instance "EchoServer" will listen two local endpoints. It is very similar with that a website can have multiple bindings in IIS.

- You also can set different options for the different listeners:
	```json
	{
		"serverOptions": {
			"name": "EchoServer",
			"listeners": [
				{
					"ip": "Any",
					"port": 80
				},
				{
					"ip": "Any",
					"port": 443,
					"security": "Tls12",
					"certificateOptions": {
						"filePath": "supersocket.pfx",
						"password": "supersocket"
					}
				}
			]
		}
	}
	```

- In addition to define listeners in configuration, SuperSocket 2.0 also allow you to add listener programmatically:
	```c#
	var host = SuperSocketHostBuilder.Create<TextPackageInfo, LinePipelineFilter>(args)
			.ConfigureSuperSocket(options =>
			{
					options.AddListener(new ListenOptions
							{
									Ip = "Any",
									Port = 4040
							}
					);
			}).Build();

	await host.RunAsync();
	```

# 11. Multiple Server Instances
> Keywords: Multiple Server Instances, Multiple Server Configuration, Server Dispatch, Isolation

## SuperSocket support running multiple server instances in the same process
 - A generic host (.NET Core) can run multiple SuperSocket servver instances. Each of them is a HostedService within the host.

- You can leave the options of the multiple servers under the node "serverOptions" in the configuration:
	```json
	{
		"serverOptions": {
			"TestServer1": {
				"name": "TestServer1",
				"listeners": [
					{
						"ip": "Any",
						"port": 4040
					}
				]
			},
			"TestServer2": {
				"name": "TestServer2",
				"listeners": [                
					{
						"ip": "Any",
						"port": 4041
					}
				]
			}
		}
	}
	```

- And then you should tell which server configuration node each server should load. The "serverName1" and "serverName2" are the two servers' names which are defined in the configuration as the keys of two children of the node "serverOptions". SuperSocketServiceA and SuperSocketServiceB are the service types of these two server and they are not allowed to be the same type.
	```c#
	var hostBuilder = MultipleServerHostBuilder.Create()
		.AddServer<SuperSocketServiceA, TextPackageInfo, LinePipelineFilter>(builder =>
		{
			builder
			.ConfigureServerOptions((ctx, config) =>
			{
					return config.GetSection("serverName1");
			});
		})
		.AddServer<SuperSocketServiceB, TextPackageInfo, LinePipelineFilter>(builder =>
		{
			builder
			.ConfigureServerOptions((ctx, config) =>
			{
					return config.GetSection("serverName2");
			});
		})
	```
## Isolation of the server instances
- Different with the previous version of SuperSocket, multiple servers run as hosted service within one generic host. So they run in the same host and same process. If you want to run them in different processes or different servers, we recommend you to use Docker or other container technology.

# 12. Enable Transport Layer Security in SuperSocket
> Keywords: TLS, SSL, Certificate, X509 Certificate, Local Certificate Store

## SuperSocket supports the Transport Layer Security (TLS)
- SuperSocket has built-in support for TLS. You don't need make any change for your code to let your socket server support TLS.

## To enable TLS for your SuperSocket server, you should have a certificate in advance.
- There are two ways to provide the certificate:
	1. a X509 certificate file with private key (*.pfx)
		- for testing purpose you can generate a certificate file by this open source CertificateCreator(https://github.com/kerryjiang/CertificateCreator)
		- in production environment, you should purchase a certificate from a certificate authority
	2. a certificate in local certificate store

## Enable TLS with a certificate file
- You should update your configuration to use the certificate file following the below steps:
	1. set security attribute for the listener; This attribute is for the TLS protocols what the listener will support; The appilicable values include "Tls11", "Tls12", "Tls13" and so on; Multiple values should be seperated by comma, like "Tls11,Tls12,Tls13";
	2. add the certificate option node under the listener node;

- The configuration should look like:
	```json
	{
		"serverOptions": {
			"name": "TestServer",
			"listeners": [
				{
					"ip": "Any",
					"port": 4040,
					"security": "Tls12",
					"certificateOptions": {
						"filePath": "supersocket.pfx",
						"password": "supersocket"
					}
				}
			]
		}
	}
	```
	- Note: the password in the certificate options is the private key of the certificate file.

- There is one more option named "keyStorageFlags" for certificate loading:
	```json
	"certificateOptions": {
			"filePath": "supersocket.pfx",
			"password": "supersocket",
			"keyStorageFlags": "UserKeySet"
	}
	```

- You can read the MSDN article below for more information about this option: http://msdn.microsoft.com/zh-cn/library/system.security.cryptography.x509certificates.x509keystorageflags(v=vs.110).aspx

## Enable TLS with a certificate in your local certificate store
- You also can use a certificate in your local certificate store instead of a physical certificate file. The thumbprint of the certificate you want to use is required. If the storeName is not specified, the system will search the certificate from the "Root" store:
	```json
	"certificateOptions": {
		"storeName": "My",
		"thumbprint": "f42585bceed2cb049ef4a3c6d0ad572a6699f6f3"
	}
	```

- Other optional options:
	- storeLocation - CurrentUser, LocalMachine
		```json
		"certificateOptions": {
				"storeName": "My",
				"thumbprint": "‎f42585bceed2cb049ef4a3c6d0ad572a6699f6f3",
				"storeLocation": "LocalMachine"
		}
		```

## Client certificate validation
- In TLS communications, the client side certificate is not a must, but some systems require much higher security guarantee. This feature allow you to validate the client side certificate from the server side.

- At first, to enable the client certificate validation, you should add the attribute "clientCertificateRequired" in the certificate options node of the listener:
	```json
	"certificateOptions": {
			"filePath": "supersocket.pfx",
			"password": "supersocket",
			"clientCertificateRequired": true
	}
	```

- And then you should define you client certificate validation logic with the RemoteCertificateValidationCallback in the certificate options when you configure the server options:
	```c#
	var host = SuperSocketHostBuilder.Create<TextPackageInfo, LinePipelineFilter>(args)
			.ConfigureSuperSocket(options =>
			{
				foreach (var certOptions in options.Listeners.Where(l => l.CertificateOptions != null && l.CertificateOptions.ClientCertificateRequired))
				{
					certOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
				}
			}).Build();

	await host.RunAsync();
	```

# 13. Integrate with ASP.Net Core Website and ABP Framework
> Keywords: ASP.NET Core, ABP, Integrate

## Integrate with ASP.Net Core Website
- Yes, SuperSocket can run together with ASP.NET Core website side by side. What you should do are registering SuperSocket into the host builder of the ASP.NET Core and leaving the options in the configuration file or by code.

- In the Program class, add more lines of code for SuperSocket:
	```c#
	//don't forget the usings
	using SuperSocket;
	using SuperSocket.ProtoBase;

	public static IHostBuilder CreateHostBuilder(string[] args) =>
			Host.CreateDefaultBuilder(args)
					.ConfigureWebHostDefaults(webBuilder =>
					{
						webBuilder.UseStartup<Startup>();
					})
					.AsSuperSocketHostBuilder<TextPackageInfo, LinePipelineFilter>()
					.UsePackageHandler(async (s, p) =>
					{
						// echo message back to client
						await s.SendAsync(Encoding.UTF8.GetBytes(p.Text + "\r\n"));
					});
	```

- And leave server options in the configuration file "appsettings.json":
	```json
	{
		"Logging": {
			"LogLevel": {
			"Default": "Information",
			"Microsoft": "Warning",
			"Microsoft.Hosting.Lifetime": "Information"
			}
		},
		"serverOptions": {
			"name": "TestServer",
			"listeners": [
			{
				"ip": "Any",
				"port": 4040
			}
			]
		},
		"AllowedHosts": "*"
	}
	```

## Integrate with ABP Framework
- Coming soon...

# 14. UDP Support in SuperSocket
> Keywords: UDP

## Enable UDP in SuperSocket
- Beside TCP, SuperSocket can support UDP as well.

- First of all, you need add reference to the package SuperSocket.Udp.
	```
	dotnet add package SuperSocket.Udp --version 2.0.0-*
	```

- After you create your SuperSocket host builder like TCP, you just need enable UDP with one extra line of code:
	```
	hostBuilder.UseUdp();
	```

## Use your own Session Identifier
- For UDP SuperSocket server, the client IP address and port are used as session's identifier. But in some cases, you need use something like device id as session identifier. SuperSocket allows you to do it with IUdpSessionIdentifierProvider. You need implement this interface and then register it into the SuperSocket's host builder.

## Define your UdpSessionIdentifierProvider:
```c#
public class MySessionIdentifierProvider : IUdpSessionIdentifierProvider
{
    public string GetSessionIdentifier(IPEndPoint remoteEndPoint, ArraySegment<byte> data)
    {
        // take the device ID from the package data
        ....
        //return deviceID;
    }
}
```

## Register your UdpSessionIdentifierProvider:
```c#
hostBuilder.ConfigureServices((context, services) =>
{
    services.AddSingleton<IUdpSessionIdentifierProvider, MySessionIdentifierProvider>();                
})
```