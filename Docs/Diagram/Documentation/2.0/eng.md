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