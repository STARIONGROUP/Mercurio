![mercurio](https://raw.githubusercontent.com/STARIONGROUP/Mercurio/master/mercurio-logo-strapline.png)
# Mercurio
A library to make RabbitMQ integration in .NET microservices seamless

## Introduction
Mercurio provides RabbitMQ integration with common implementation of Messaging Client. This common implementation of messaging client reduce the need of code duplication on microservices implementation that requires to interacts with RabbitMQ.  
It also eases the setup of ConnectionFactory and also to register multiple ConnectionFactory setup, using the _IRabbitMqConnectionProvider_.

Mercurio follows RabbitMQ best practices by encouraging connection reuse and channel pooling per named connection. 
Instead of creating new connections for every operation—which is inefficient and discouraged by RabbitMQ—it provides a shared, 
thread-safe mechanism to lease and reuse channels from a limited pool tied to a single registered connection.
This improves performance, resource management, and aligns with RabbitMQ's recommendations for production-grade messaging systems.

See [channel reuse and connection management](https://github.com/STARIONGROUP/Mercurio/wiki/channel) for more details on the implementation.

## Examples
### Dependency Injection Registration
```csharp
var serviceCollection = new ServiceCollection();

serviceCollection.AddRabbitMqConnectionProvider()
    .WithRabbitMqConnectionFactoryAsync("Primary", _ =>
    {
        var connectionFactory = new ConnectionFactory()
        {
            HostName = "localhost",
            Port =  5432
        };

        return Task.FromResult(connectionFactory);
    })
    .WithRabbitMqConnectionFactory("Secondary", _ =>
    {
        var connectionFactory = new ConnectionFactory()
        {
            HostName = "localhost",
            Port =  5433
        };

        return connectionFactory;
    })
    .WithSerialization();

var serviceProvider = serviceCollection.BuildServiceProvider();
var connectionProvider = serviceProvider.GetRequiredService<IRabbitMqConnectionProvider>();
var primaryConnection = await  connectionProvider.GetConnectionAsync("Primary");
```

### 📦 Message Serialization

Mercurio uses a flexible and extensible serialization system that supports multiple formats and allows format-specific resolution at runtime.

By default, Mercurio registers **System.Text.Json**-based serializers and deserializers. However, you can register custom implementations or support additional formats like **MessagePack**.

Serialization is configured using `.WithSerialization()` in your service registration:

```csharp
services
    .AddRabbitMqConnectionProvider()
    .WithSerialization(builder => builder
        .UseDefaultJson() // Registers JsonMessageSerializerService that uses System.Text.Json as default serializer
        .UseMessagePack<YourMessagePackSerializer>(asDefault: false)); // Optional additional format
```

Under the hood, serialization is format-aware:

* Each format (e.g., `Json`, `MessagePack`) is keyed by `SupportedSerializationFormat` and is transported in the 'content type' header.
* A central `SerializationProviderService` handles resolution of serializers and deserializers.
* The **default format** (usually `Json`) is mapped to the special key `Unspecified`.

The following interfaces drive the system:

* `IMessageSerializerService` – used to serialize outgoing messages.
* `IMessageDeserializerService` – used to deserialize incoming messages.
* `ISerializationProviderService` – allows resolving serializers and deserializers for a given format.

All registered services are added via `IServiceCollection` using Microsoft.Extensions.DependencyInjection’s [keyed services](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-usage#keyed-service-registration).

#### 🔧 Default Behavior

If `.WithSerialization()` is called without configuration, Mercurio will:

* Register `JsonMessageSerializerService` for both serializer and deserializer interfaces.
* Use `SupportedSerializationFormat.Json` as the default.
* Register a fallback mapping under `SupportedSerializationFormat.Unspecified`.

#### 🔄 Format Resolution

Serialization and deserialization for a given format can be resolved at runtime via:

```csharp
var serializer = serializationProvider.ResolveSerializer(SupportedSerializationFormat.Json);
var deserializer = serializationProvider.ResolveDeserializer(SupportedSerializationFormat.MessagePack);
```

### MessageClientService
A base implementation of a _MessageClientService_ is available. It defines base behavior to push and listen after messages on queue and exchange.  
Following example expects to have a connection registered, the service registered as _IMessageClientBaseService_ into the service collection and will use Direct Exchange.

#### Push Message
```csharp
var messageClientService = serviceProvider.GetRequiredService<IMessageClientBaseService>();
var exchangeConfiguration = new DirectExchangeConfiguration("DirectQueue", "AnExchange", "SomeRouting");
await messageClientService.PushAsync("RegisteredConnection","A message to be sent",exchangeConfiguration);
```

#### Listen After Message
```csharp
var messageClientService = serviceProvider.GetRequiredService<IMessageClientBaseService>();
var exchangeConfiguration = new DirectExchangeConfiguration("DirectQueue", "AnExchange", "SomeRouting");
var messageObservable = await messageClientService.ListenAsync<string>("RegisteredConnection", exchangeConfiguration);
messageObservable.Subscribe(message => Console.WriteLine(message));
```

## Integration Tests
Before running any integration tests, it is required to have a running instance of RabbitMQ.  
Please use this following command to run it :
```sh
docker run -d --name mercurio -p 15672:15672 -p 5672:5672 rabbitmq:4-management 
```

## Code Quality

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=STARIONGROUP_Mercurio&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=STARIONGROUP_Mercurio)
[![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=STARIONGROUP_Mercurio&metric=code_smells)](https://sonarcloud.io/summary/new_code?id=STARIONGROUP_Mercurio)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=STARIONGROUP_Mercurio&metric=coverage)](https://sonarcloud.io/summary/new_code?id=STARIONGROUP_Mercurio)
[![Duplicated Lines (%)](https://sonarcloud.io/api/project_badges/measure?project=STARIONGROUP_Mercurio&metric=duplicated_lines_density)](https://sonarcloud.io/summary/new_code?id=STARIONGROUP_Mercurio)
[![Lines of Code](https://sonarcloud.io/api/project_badges/measure?project=STARIONGROUP_Mercurio&metric=ncloc)](https://sonarcloud.io/summary/new_code?id=STARIONGROUP_Mercurio)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=STARIONGROUP_Mercurio&metric=sqale_rating)](https://sonarcloud.io/summary/new_code?id=STARIONGROUP_Mercurio)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=STARIONGROUP_Mercurio&metric=reliability_rating)](https://sonarcloud.io/summary/new_code?id=STARIONGROUP_Mercurio)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=STARIONGROUP_Mercurio&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=STARIONGROUP_Mercurio)
[![Technical Debt](https://sonarcloud.io/api/project_badges/measure?project=STARIONGROUP_Mercurio&metric=sqale_index)](https://sonarcloud.io/summary/new_code?id=STARIONGROUP_Mercurio)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=STARIONGROUP_Mercurio&metric=vulnerabilities)](https://sonarcloud.io/summary/new_code?id=STARIONGROUP_Mercurio)

## Nuget
The Mercurio library is released as NuGet package and available from [nuget.org](https://www.nuget.org/packages?q=mercurio).  
![NuGet Badge](https://img.shields.io/nuget/v/Mercurio)

## Software Bill of Materials (SBOM)

As part of our commitment to security and transparency, this project includes a Software Bill of Materials (SBOM) in the associated NuGet packages. The SBOM provides a detailed inventory of the components and dependencies included in the package, allowing you to track and verify the software components, their licenses, and versions.

**Why SBOM?**

- **Improved Transparency**: Gain insight into the open-source and third-party components included in this package.
- **Security Assurance**: By providing an SBOM, we enable users to more easily track vulnerabilities associated with the included components.
- **Compliance**: SBOMs help ensure compliance with licensing requirements and make it easier to audit the project's dependencies.

You can find the SBOM in the NuGet package itself, which is automatically generated and embedded during the build process.
