# PollyGrpc

[![NuGet](https://img.shields.io/nuget/v/PollyGrpc.svg)](https://www.nuget.org/packages/PollyGrpc)
[![NuGet Downloads](https://img.shields.io/nuget/dt/PollyGrpc.svg)](https://www.nuget.org/packages/PollyGrpc)
[![CI](https://github.com/Swevo/PollyGrpc/actions/workflows/build.yml/badge.svg)](https://github.com/Swevo/PollyGrpc/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**Polly v8 resilience for gRPC .NET** — retry, timeout, and circuit-breaker for any gRPC unary call, plus a built-in `GrpcTransientErrors` predicate covering the most common transient status codes. Works with any generated gRPC client, `GrpcChannel`, or `CallInvoker`.

```csharp
// Before
var reply = await client.SayHelloAsync(new HelloRequest { Name = "world" });

// After — automatic retry + timeout on every call
var resilient = channel.WithPolly(pipeline =>
    pipeline
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            ShouldHandle = GrpcTransientErrors.IsTransient, // built-in ✔
        })
        .AddTimeout(TimeSpan.FromSeconds(10)));

var reply = await resilient.ExecuteAsync(ct =>
    client.SayHelloAsync(new HelloRequest { Name = "world" }, cancellationToken: ct));
```

---

## Installation

```bash
dotnet add package PollyGrpc
```

Targets **net6.0**, **net8.0**, and **net9.0**.
Dependencies: `Polly.Core 8.*`, `Grpc.Net.Client 2.*`, `Microsoft.Extensions.DependencyInjection.Abstractions 8.*`

---

## GrpcTransientErrors — the key feature

Knowing which gRPC status codes are safe to retry is the hard part. `PollyGrpc` ships `GrpcTransientErrors.IsTransient` so you never have to look them up.

```csharp
new RetryStrategyOptions
{
    MaxRetryAttempts = 3,
    ShouldHandle = GrpcTransientErrors.IsTransient,
}
```

### Covered status codes

| Code | Name | Description |
|------|------|-------------|
| `4` | DeadlineExceeded | Request timed out before server could respond |
| `8` | ResourceExhausted | Quota or rate limit exceeded (like HTTP 429) |
| `10` | Aborted | Operation aborted — transaction conflict; safe to retry |
| `14` | Unavailable | Server temporarily unavailable — most common transient gRPC error |

> **Tip:** `StatusCode.Internal (13)` can also be transient (connection reset). If you see it in logs, extend the predicate:

```csharp
var myErrors = GrpcTransientErrors.StatusCodes.ToHashSet();
myErrors.Add(StatusCode.Internal);

new RetryStrategyOptions
{
    ShouldHandle = new PredicateBuilder()
        .Handle<RpcException>(ex => myErrors.Contains(ex.StatusCode))
}
```

---

## Quick start

### Approach 1 — ResilientGrpcChannel (simplest)

Wrap your existing `GrpcChannel` and pass any lambda that makes a gRPC call:

```csharp
using PollyGrpc;

var channel  = GrpcChannel.ForAddress("https://my-service:5001");
var resilient = channel.WithPolly(pipeline =>
    pipeline
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(200),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = GrpcTransientErrors.IsTransient,
        })
        .AddTimeout(TimeSpan.FromSeconds(10)));

var client = new Greeter.GreeterClient(channel);

// Unary call — pass AsyncUnaryCall directly
var reply = await resilient.ExecuteAsync(ct =>
    client.SayHelloAsync(new HelloRequest { Name = "world" }, cancellationToken: ct));

// Or pass .ResponseAsync explicitly
var reply2 = await resilient.ExecuteAsync(ct =>
    client.SayHelloAsync(new HelloRequest { Name = "world" }, cancellationToken: ct).ResponseAsync);
```

### Approach 2 — PollyClientInterceptor (for typed clients)

The interceptor approach integrates transparently at the gRPC channel level — no changes to call sites needed:

```csharp
var options = new PollyGrpcOptions
{
    MaxRetries = 3,
    BaseDelay  = TimeSpan.FromMilliseconds(200),
    CallTimeout = TimeSpan.FromSeconds(10),
    TransientStatusCodes = GrpcTransientErrors.StatusCodes.ToHashSet(),
};

var channel = GrpcChannel.ForAddress("https://my-service:5001",
    new GrpcChannelOptions
    {
        Interceptors = { new PollyClientInterceptor(options) }
    });

// All calls through this channel are automatically protected
var client = new Greeter.GreeterClient(channel);
var reply  = await client.SayHelloAsync(new HelloRequest { Name = "world" });
```

### Approach 3 — Dependency injection

```csharp
// Program.cs
builder.Services.AddPollyGrpc("https://my-service:5001", pipeline =>
    pipeline
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(200),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = GrpcTransientErrors.IsTransient,
        })
        .AddTimeout(TimeSpan.FromSeconds(10))
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            MinimumThroughput = 10,
            SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = TimeSpan.FromSeconds(15),
        }));

// Service
public class GreeterService(ResilientGrpcChannel resilient, GrpcChannel channel)
{
    private readonly Greeter.GreeterClient _client = new(channel);

    public Task<HelloReply> SayHelloAsync(string name, CancellationToken ct = default) =>
        resilient.ExecuteAsync(token =>
            _client.SayHelloAsync(new HelloRequest { Name = name }, cancellationToken: token), ct);
}
```

---

## ResilientGrpcChannel methods

| Method | Description |
|--------|-------------|
| `ExecuteAsync<T>(Func<CancellationToken, AsyncUnaryCall<T>>)` | Unary call — automatically disposes the call handle |
| `ExecuteAsync<T>(Func<CancellationToken, Task<T>>)` | Any async operation returning `T` |
| `ExecuteAsync(Func<CancellationToken, Task>)` | Any async operation with no return value |

---

## Pipeline order

```
[Timeout] → [Retry] → [Circuit Breaker] → [gRPC server]
```

```csharp
pipeline
    .AddTimeout(TimeSpan.FromSeconds(10))   // 1. Per-attempt deadline
    .AddRetry(retryOptions)                 // 2. Retry transient failures
    .AddCircuitBreaker(cbOptions)           // 3. Open circuit under load
```

---

## Related Packages

| Package | Downloads | Description |
|---|---|---|
| [PollyHealthChecks](https://www.nuget.org/packages/PollyHealthChecks) | [![Downloads](https://img.shields.io/nuget/dt/PollyHealthChecks.svg)](https://www.nuget.org/packages/PollyHealthChecks) | ASP.NET Core health checks for Polly v8 circuit breakers — expose circuit-breaker state (Closed, HalfOpen, Open, Isolated) as /health endpoint responses |
| [PollyBackoff](https://www.nuget.org/packages/PollyBackoff) | [![Downloads](https://img.shields.io/nuget/dt/PollyBackoff.svg)](https://www.nuget.org/packages/PollyBackoff) | Backoff delay strategies for Polly v8 resilience pipelines |
| [PollyEFCore](https://www.nuget.org/packages/PollyEFCore) | [![Downloads](https://img.shields.io/nuget/dt/PollyEFCore.svg)](https://www.nuget.org/packages/PollyEFCore) | Polly v8 resilience pipelines for Entity Framework Core — wrap every EF Core query and SaveChanges with retry, timeout and circuit-breaker via a single AddPollyResilience() call |
| [PollyRabbitMQ](https://www.nuget.org/packages/PollyRabbitMQ) | [![Downloads](https://img.shields.io/nuget/dt/PollyRabbitMQ.svg)](https://www.nuget.org/packages/PollyRabbitMQ) | Polly v8 resilience for RabbitMQ.Client v7+ — retry, circuit-breaker, and timeout for IChannel operations, with built-in RabbitMqTransientErrors predicate covering AlreadyClosedException, BrokerUnreachableException, OperationInterruptedException, and ConnectFailureException |
| [PollyMailKit](https://www.nuget.org/packages/PollyMailKit) | [![Downloads](https://img.shields.io/nuget/dt/PollyMailKit.svg)](https://www.nuget.org/packages/PollyMailKit) | Polly v8 resilience pipelines for MailKit — retry, timeout, and circuit-breaker for SmtpClient.SendAsync and any MailKit SMTP operation |
| [PollyMassTransit](https://www.nuget.org/packages/PollyMassTransit) | [![Downloads](https://img.shields.io/nuget/dt/PollyMassTransit.svg)](https://www.nuget.org/packages/PollyMassTransit) | Polly v8 resilience pipelines for MassTransit — retry, timeout, and circuit-breaker for IBus.Publish and ISendEndpointProvider.Send |
| [PollyNpgsql](https://www.nuget.org/packages/PollyNpgsql) | [![Downloads](https://img.shields.io/nuget/dt/PollyNpgsql.svg)](https://www.nuget.org/packages/PollyNpgsql) | Polly v8 resilience pipelines for Npgsql (PostgreSQL) — retry, timeout, and circuit-breaker for NpgsqlConnection queries and commands, plus a built-in PostgresTransientErrors predicate covering all common PostgreSQL transient SQLSTATE codes |
| [PollyOpenAI](https://www.nuget.org/packages/PollyOpenAI) | [![Downloads](https://img.shields.io/nuget/dt/PollyOpenAI.svg)](https://www.nuget.org/packages/PollyOpenAI) | Polly v8 resilience for OpenAI and Azure OpenAI API calls |
| [PollyAzureEventHub](https://www.nuget.org/packages/PollyAzureEventHub) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureEventHub.svg)](https://www.nuget.org/packages/PollyAzureEventHub) | Polly v8 resilience pipelines for Azure Event Hubs — retry, timeout, and circuit-breaker for EventHubProducerClient and EventHubConsumerClient |
| [PollyElasticsearch](https://www.nuget.org/packages/PollyElasticsearch) | [![Downloads](https://img.shields.io/nuget/dt/PollyElasticsearch.svg)](https://www.nuget.org/packages/PollyElasticsearch) | Polly v8 resilience pipelines for Elastic.Clients.Elasticsearch 8+ — retry, timeout, and circuit-breaker for any Elasticsearch operation, plus a built-in ElasticTransientErrors predicate covering rate limiting (429), service unavailability (503), gateway timeouts (504), and connection failures |
| [PollyHangfire](https://www.nuget.org/packages/PollyHangfire) | [![Downloads](https://img.shields.io/nuget/dt/PollyHangfire.svg)](https://www.nuget.org/packages/PollyHangfire) | Polly v8 resilience pipelines for Hangfire — retry, timeout, and circuit-breaker for IBackgroundJobClient.Enqueue and Schedule |
| [PollyCosmosDb](https://www.nuget.org/packages/PollyCosmosDb) | [![Downloads](https://img.shields.io/nuget/dt/PollyCosmosDb.svg)](https://www.nuget.org/packages/PollyCosmosDb) | Polly v8 resilience pipelines for Azure Cosmos DB — retry, timeout, and circuit-breaker for Container operations, plus a built-in CosmosTransientErrors predicate covering rate limiting (429), timeouts (408), partition failovers (410), and service unavailability (503) |
| [PollySendGrid](https://www.nuget.org/packages/PollySendGrid) | [![Downloads](https://img.shields.io/nuget/dt/PollySendGrid.svg)](https://www.nuget.org/packages/PollySendGrid) | Polly v8 resilience pipelines for SendGrid — retry, timeout, and circuit-breaker for ISendGridClient.SendEmailAsync |
| [PollyMongo](https://www.nuget.org/packages/PollyMongo) | [![Downloads](https://img.shields.io/nuget/dt/PollyMongo.svg)](https://www.nuget.org/packages/PollyMongo) | Polly v8 resilience pipelines for MongoDB.Driver — wrap Find, InsertOne, UpdateOne, DeleteOne and other IMongoCollection calls with retry, timeout, circuit-breaker, and more using a single ResilientMongoCollection decorator |
| [PollyDapper](https://www.nuget.org/packages/PollyDapper) | [![Downloads](https://img.shields.io/nuget/dt/PollyDapper.svg)](https://www.nuget.org/packages/PollyDapper) | Polly v8 resilience pipelines for Dapper — wrap QueryAsync, ExecuteAsync, and other Dapper calls with retry, timeout, circuit-breaker, and more using a single ResilientDbConnection decorator |
| [PollyMediatR](https://www.nuget.org/packages/PollyMediatR) | [![Downloads](https://img.shields.io/nuget/dt/PollyMediatR.svg)](https://www.nuget.org/packages/PollyMediatR) | Polly v8 resilience pipelines for MediatR — add retry, timeout, circuit-breaker, rate-limiting, hedging, and chaos engineering to any MediatR request handler with a single line of DI registration |
| [PollySqlClient](https://www.nuget.org/packages/PollySqlClient) | [![Downloads](https://img.shields.io/nuget/dt/PollySqlClient.svg)](https://www.nuget.org/packages/PollySqlClient) | Polly v8 resilience pipelines for Microsoft.Data.SqlClient (SQL Server and Azure SQL) — retry, timeout, and circuit-breaker for SqlConnection queries and commands, plus a built-in SqlServerTransientErrors predicate covering all common SQL Server and Azure SQL transient error numbers |
| [PollyAzureKeyVault](https://www.nuget.org/packages/PollyAzureKeyVault) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureKeyVault.svg)](https://www.nuget.org/packages/PollyAzureKeyVault) | Polly v8 resilience pipelines for Azure Key Vault — retry, timeout, and circuit-breaker for SecretClient, KeyClient, and CertificateClient |
| [PollyAzureQueueStorage](https://www.nuget.org/packages/PollyAzureQueueStorage) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureQueueStorage.svg)](https://www.nuget.org/packages/PollyAzureQueueStorage) | Polly v8 resilience pipelines for Azure Queue Storage — retry, timeout, and circuit-breaker for Azure.Storage.Queues QueueClient |
| [PollyRedis](https://www.nuget.org/packages/PollyRedis) | [![Downloads](https://img.shields.io/nuget/dt/PollyRedis.svg)](https://www.nuget.org/packages/PollyRedis) | Polly v8 resilience for StackExchange.Redis |
| [PollyAzureServiceBus](https://www.nuget.org/packages/PollyAzureServiceBus) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureServiceBus.svg)](https://www.nuget.org/packages/PollyAzureServiceBus) | Polly v8 resilience for Azure Service Bus — retry, circuit breaker, and timeout for sending and receiving messages |
| [PollyAzureBlob](https://www.nuget.org/packages/PollyAzureBlob) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureBlob.svg)](https://www.nuget.org/packages/PollyAzureBlob) | Polly v8 resilience pipelines for Azure Blob Storage — wrap BlobClient and BlobContainerClient operations with retry, timeout, circuit-breaker, and more using ResilientBlobClient and ResilientBlobContainerClient decorators |
| [PollyAzureTableStorage](https://www.nuget.org/packages/PollyAzureTableStorage) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureTableStorage.svg)](https://www.nuget.org/packages/PollyAzureTableStorage) | Polly v8 resilience pipelines for Azure Table Storage — retry, timeout, and circuit-breaker for Azure.Data.Tables TableClient |

## 💼 Need .NET consulting?

The author of this package is available for consulting on **Polly v8 resilience**, **Azure cloud architecture**, and **clean .NET design**.

**[→ solidqualitysolutions.com](https://www.solidqualitysolutions.com/)** · **[LinkedIn](https://www.linkedin.com/in/justbannister/)**
## License

MIT
