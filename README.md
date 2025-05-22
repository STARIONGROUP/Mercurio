# Mercurio
A library to make RabbitMQ integration in .NET microservices seamless

## Introduction
Mercurio provides RabbitMQ integration with common implementation of Messaging Client. This common implementation of messaging client reduce the need of code duplication on microservices implementation that requires to interacts with RabbitMQ.  
It also eases the setup of ConnectionFactory and also to register multiple ConnectionFactory setup, using the _IRabbitMqConnectionProvider_.

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
    .WithDefaultJsonMessageSerializer();

var serviceProvider = serviceCollection.BuildServiceProvider();
var connectionProvider = serviceProvider.GetRequiredService<IRabbitMqConnectionProvider>();
var primaryConnection = await  connectionProvider.GetConnectionAsync("Primary");
```

### Message Serialization
The _MessageClientService_ requires to serialize and deserialize messages sent on RabbitMQ. For that, two interfaces are declared: IMessageDeserializerService and IMessageSerializerService.  
A JSON basic implementation is already provided and can be registered using the _.WithDefaultJsonMessageSerializer()_ extension method on the IServiceCollection.  
This implementation provides basic JSON serialization using the System.Text.Json library.

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
