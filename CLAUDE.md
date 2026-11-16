# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Mercurio is a .NET library (published to NuGet as `Mercurio` and `Mercurio.Hosting`) that wraps `RabbitMQ.Client` 7.x: named connection registration, pooled channels, push/listen messaging with Rx observables, RPC, pluggable serialization and OpenTelemetry-style tracing via `ActivitySource`.

## Commands

```sh
dotnet restore Mercurio.sln
dotnet build Mercurio.sln --no-restore

# All tests of one project (there is no need to run Mercurio.Core.Tests, see below)
dotnet test Mercurio.Tests/Mercurio.Tests.csproj
dotnet test Mercurio.Hosting.Tests/Mercurio.Hosting.Tests.csproj

# One fixture or one test
dotnet test Mercurio.Tests/Mercurio.Tests.csproj --filter "FullyQualifiedName~MessageClientServiceTestFixture"
dotnet test Mercurio.Tests/Mercurio.Tests.csproj --filter "Name=<TestMethodName>"

# Skip the tests marked [Category("Integration")]
dotnet test Mercurio.Tests/Mercurio.Tests.csproj --filter "TestCategory!=Integration"

# What the release workflow runs
dotnet pack -c Release -o ReleaseBuilds Mercurio.sln
```

**Docker must be running for the test projects.** `RabbitMqContainerSetupFixture` is an NUnit `[SetUpFixture]` that starts a `rabbitmq:4.2.0-alpine` Testcontainer once per test assembly, so even a filtered run starts the container. Fixtures take host and port from `RabbitMqContainerSetupFixture.RabbitMqContainer`, not from a fixed local broker.

CI (`.github/workflows/CodeQuality.yml`) builds with `/p:ContinuousIntegrationBuild=true`, runs the two test projects with coverlet and feeds SonarCloud. There is no separate lint step; style is enforced by review and by `Mercurio.sln.DotSettings` (ReSharper/Rider).

## Projects

- `Mercurio` – the core library. Targets `net10.0;net9.0;net8.0;netstandard2.1` with `LangVersion` 12 and `Nullable` disabled, so new code must compile on netstandard2.1 and must not use nullable annotations.
- `Mercurio.Hosting` – same targets; adds `BackgroundService` based wrappers and health checks on top of the core.
- `Mercurio.Tests`, `Mercurio.Hosting.Tests` – NUnit 4 + Moq, `net10.0` only. Test classes are named `*TestFixture` and mirror the folder of the type under test. `Mercurio` exposes its internals to `Mercurio.Tests`.
- `Mercurio.Core.Tests` – contains no tests. It only holds `RabbitMqContainerSetupFixture.cs`, which the two test projects include as a linked `<Compile>` item. Edit the file there.

## Architecture

**Connections and channel pool (`Provider/RabbitMqConnectionProvider`, internal, singleton).** `AddRabbitMqConnectionProvider()` registers the provider; each `WithRabbitMqConnectionFactory[Async](name, ...)` adds an `IConnectionFactoryConfiguration` singleton that the provider collects through `IEnumerable<>` injection. Everything downstream is addressed by that connection *name*. One `IConnection` is cached per name and recreated when it is no longer open. Channels are never created directly by callers: they are leased with `LeaseChannelAsync`, which returns a `ChannelLease` whose disposal gives the channel back through `ICanReleaseChannel`. A per-connection `SemaphoreSlim` sized by `PoolSize` bounds the outstanding leases, so a lease that is not disposed permanently consumes a slot. Channel creation is retried with Polly exponential backoff (`RetryPolicyConfiguration`).

Channels with publisher confirmations live in a second pool keyed `"<name>:confirmations"`, because confirmations can only be enabled when a channel is created. `IChannel` does not expose that setting, so the provider tracks those channels in `confirmationChannels` to release each one into the pool it came from. Both pools share the one semaphore of the connection.

**Messaging (`Messaging/`).** `IMessageClientService` → abstract `MessageClientBaseService` (lease helpers, trace context propagation through the `traceparent`/`tracestate` headers) → `MessageClientService` (push, listen) → `RpcClientService<TResponse>` and `RpcServerService`. The public methods validate arguments synchronously and delegate to a `*InternalAsync` method, so invalid arguments throw immediately rather than from the returned task. Keep that split when adding operations.

Error handling differs per operation and is part of the contract:
- `PushAsync` is fire and forget: operational failures are logged and swallowed.
- `PushWithConfirmationAsync` returns `false` on any operational failure (logged), and a collection push stops at the first message that is not acknowledged.
- `ListenAsync` returns a cold `IObservable<T>`; the consumer is only attached on subscription, and failures (deserialization as `InvalidMessageException`, channel shutdown, callback exceptions) arrive through `OnError`, which ends the subscription.

**Exchange configurations (`Model/`).** An `IExchangeConfiguration` (`Default`, `Direct`, `Fanout`, `Topic`, all deriving from `BaseExchangeConfiguration`) both describes the queue/exchange/routing key and declares them: `EnsureQueueAndExchangeAreDeclaredAsync(channel, isDeclareForPush)` is called before every publish and consume. A new exchange type is a new subclass, not a change to the message client.

**Serialization (`Serializer/`, `Configuration/SerializationConfiguration/`).** `WithSerialization()` registers serializers and deserializers as keyed services, keyed by `SupportedSerializationFormat`; the default format is additionally registered under `Unspecified`. `SerializationProviderService` resolves them at runtime from the message's `ContentType` property, which is how the format travels with a message. `System.Text.Json` (`JsonMessageSerializerService`) is the default.

**Hosting (`Mercurio.Hosting`).** `MessagingBackgroundService` is the abstract base for a consuming service. Subclasses implement `InitializeAsync`, which must set `ConnectionName` (otherwise `StartAsync` throws) and registers listeners with `RegisterListener` / `RegisterAsyncListener`. Those subscribe with `SubscribeSafe*` from `ObservableExtensions` and, when the observable errors, dispose the subscription and re-run the observable factory, which is what gives listeners auto-recovery. `PushMessage(s)` enqueue onto an in-memory queue drained by `ExecuteAsync`; the `*Async` and `*WithConfirmationAsync` variants bypass the queue so the caller can await the result. `RpcServerBackgroundService` is the equivalent for RPC servers.

## Conventions

From `.github/CONTRIBUTING.md`, and consistently applied in the code:

- `using` directives go *inside* the namespace; block scoped namespaces.
- Always `this.` for instance members; no `_` prefix on members.
- Braces on every block, 4 spaces, no `#region`.
- `var` unless the type is not obvious; C# type aliases (`string`, `int`).
- Every file starts with the Starion Group Apache 2.0 copyright header, and every member, including private ones, has XML documentation (`GenerateDocumentationFile` is on).
- Guard logging calls with `this.Logger.IsEnabled(LogLevel.X)`.

Branch from `development` and open pull requests against it; never commit directly to `development` or `master`.

## Releases

Package versions use NuGet Central Package Management: every version is a `PackageVersion` in `Directory.Packages.props`, and a `PackageReference` in a csproj file must not carry a `Version` attribute (restore fails with NU1008 if it does). Adding a package means adding both the `PackageVersion` and the `PackageReference`.

`<Version>` and `<PackageReleaseNotes>` are maintained by hand in both `Mercurio.csproj` and `Mercurio.Hosting.csproj` and are kept in step. The manually triggered `release.yml` workflow builds the GitHub release notes from those `PackageReleaseNotes` elements, updates `CITATION.cff`, packs, pushes to NuGet and tags. Use the `update-package-release-notes` skill in `.claude/skills` when package references or versions change.
