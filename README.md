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
    });

var serviceProvider = serviceCollection.BuildServiceProvider();
var connectionProvider = serviceProvider.GetRequiredService<IRabbitMqConnectionProvider>();
var primaryConnection = await  connectionProvider.GetConnectionAsync("Primary");
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