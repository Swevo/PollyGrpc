# PollyGrpc

[![NuGet](https://img.shields.io/nuget/v/PollyGrpc.svg)](https://www.nuget.org/packages/PollyGrpc)
[![NuGet Downloads](https://img.shields.io/nuget/dt/PollyGrpc.svg)](https://www.nuget.org/packages/PollyGrpc)
[![CI](https://github.com/Swevo/PollyGrpc/actions/workflows/build.yml/badge.svg)](https://github.com/Swevo/PollyGrpc/actions)

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

## Related packages

| Package | Downloads | Description |
|---|---|---|
| [PollyMediatR](https://www.nuget.org/packages/PollyMediatR) | [![Downloads](https://img.shields.io/nuget/dt/PollyMediatR.svg)](https://www.nuget.org/packages/PollyMediatR) | Polly v8 resilience for MediatR |
| [PollyRedis](https://www.nuget.org/packages/PollyRedis) | [![Downloads](https://img.shields.io/nuget/dt/PollyRedis.svg)](https://www.nuget.org/packages/PollyRedis) | Polly v8 resilience for StackExchange.Redis |
| [PollyNpgsql](https://www.nuget.org/packages/PollyNpgsql) | [![Downloads](https://img.shields.io/nuget/dt/PollyNpgsql.svg)](https://www.nuget.org/packages/PollyNpgsql) | Polly v8 resilience for Npgsql (PostgreSQL) with PostgresTransientErrors predicate |
| [PollySqlClient](https://www.nuget.org/packages/PollySqlClient) | [![Downloads](https://img.shields.io/nuget/dt/PollySqlClient.svg)](https://www.nuget.org/packages/PollySqlClient) | Polly v8 resilience for SQL Server and Azure SQL with SqlServerTransientErrors predicate |
| [PollyCosmosDb](https://www.nuget.org/packages/PollyCosmosDb) | [![Downloads](https://img.shields.io/nuget/dt/PollyCosmosDb.svg)](https://www.nuget.org/packages/PollyCosmosDb) | Polly v8 resilience for Azure Cosmos DB with CosmosTransientErrors predicate |
| [PollyAzureServiceBus](https://www.nuget.org/packages/PollyAzureServiceBus) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureServiceBus.svg)](https://www.nuget.org/packages/PollyAzureServiceBus) | Polly v8 resilience for Azure Service Bus |
| [PollyAzureBlob](https://www.nuget.org/packages/PollyAzureBlob) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureBlob.svg)](https://www.nuget.org/packages/PollyAzureBlob) | Polly v8 resilience for Azure Blob Storage |
| [PollyEFCore](https://www.nuget.org/packages/PollyEFCore) | [![Downloads](https://img.shields.io/nuget/dt/PollyEFCore.svg)](https://www.nuget.org/packages/PollyEFCore) | Polly v8 resilience for Entity Framework Core |
| [PollyDapper](https://www.nuget.org/packages/PollyDapper) | [![Downloads](https://img.shields.io/nuget/dt/PollyDapper.svg)](https://www.nuget.org/packages/PollyDapper) | Polly v8 resilience for Dapper |
| [PollyMongo](https://www.nuget.org/packages/PollyMongo) | [![Downloads](https://img.shields.io/nuget/dt/PollyMongo.svg)](https://www.nuget.org/packages/PollyMongo) | Polly v8 resilience for MongoDB.Driver |
| [PollyOpenAI](https://www.nuget.org/packages/PollyOpenAI) | [![Downloads](https://img.shields.io/nuget/dt/PollyOpenAI.svg)](https://www.nuget.org/packages/PollyOpenAI) | Polly v8 resilience for OpenAI and Azure OpenAI |
| [PollyHealthChecks](https://www.nuget.org/packages/PollyHealthChecks) | [![Downloads](https://img.shields.io/nuget/dt/PollyHealthChecks.svg)](https://www.nuget.org/packages/PollyHealthChecks) | ASP.NET Core health checks for Polly v8 circuit breakers |
| [PollyBackoff](https://www.nuget.org/packages/PollyBackoff) | [![Downloads](https://img.shields.io/nuget/dt/PollyBackoff.svg)](https://www.nuget.org/packages/PollyBackoff) | Jitter, linear & custom backoff for Polly v8 retry |

---

| [PollyRabbitMQ](https://www.nuget.org/packages/PollyRabbitMQ) | Polly v8 resilience for RabbitMQ.Client channels |

## License

MIT