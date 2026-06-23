# PollyGrpc

[![NuGet](https://img.shields.io/nuget/v/PollyGrpc.svg)](https://www.nuget.org/packages/PollyGrpc)
[![NuGet Downloads](https://img.shields.io/nuget/dt/PollyGrpc.svg)](https://www.nuget.org/packages/PollyGrpc)
[![Build](https://github.com/Swevo/PollyGrpc/actions/workflows/build.yml/badge.svg)](https://github.com/Swevo/PollyGrpc/actions/workflows/build.yml)

**Polly v8 resilience for gRPC .NET clients** — retry, circuit breaker, and per-call timeout via a lightweight `Interceptor`, no protobuf changes required.

## Why PollyGrpc?

gRPC services can fail transiently (network blips, overloaded servers, rolling restarts). Without resilience, a single `Unavailable` or `ResourceExhausted` status crashes your call. PollyGrpc wraps every call in a Polly v8 pipeline so transient failures are retried automatically, persistent failures trip the circuit breaker, and runaway calls are cancelled by a configurable timeout.

| Feature | Built-in gRPC | PollyGrpc |
|---------|:---:|:---:|
| Automatic retry | ❌ | ✅ |
| Circuit breaker | ❌ | ✅ |
| Per-call timeout | ❌ | ✅ |
| Configurable transient codes | ❌ | ✅ |
| Works with `AddGrpcClient<T>()` | ❌ | ✅ |
| Works with raw `CallInvoker` | ❌ | ✅ |

## Installation

```bash
dotnet add package PollyGrpc
```

## Quick Start

### With `AddGrpcClient<T>()` (recommended)

```csharp
builder.Services
    .AddGrpcClient<Greeter.GreeterClient>(o =>
        o.Address = new Uri("https://my-grpc-service"))
    .AddPollyGrpcResilience(o =>
    {
        o.MaxRetries    = 3;
        o.BaseDelay     = TimeSpan.FromMilliseconds(200);
        o.CallTimeout   = TimeSpan.FromSeconds(10);
    });
```

### With a raw `CallInvoker`

```csharp
var channel = GrpcChannel.ForAddress("https://my-grpc-service");
var invoker = channel.CreateCallInvoker()
                     .WithPollyResilience(o => o.MaxRetries = 3);

var client = new Greeter.GreeterClient(invoker);
```

## Configuration

```csharp
var options = new PollyGrpcOptions
{
    // Retry
    MaxRetries = 3,                              // 0 = no retry
    BaseDelay  = TimeSpan.FromMilliseconds(200), // exponential base
    MaxDelay   = TimeSpan.FromSeconds(30),

    // Circuit breaker
    CircuitBreakerFailureRatio        = 0.5,
    CircuitBreakerMinimumThroughput   = 10,
    CircuitBreakerSamplingDuration    = TimeSpan.FromSeconds(30),
    CircuitBreakerBreakDuration       = TimeSpan.FromSeconds(5),

    // Timeout
    CallTimeout = TimeSpan.FromSeconds(10),

    // Which gRPC status codes are treated as transient (and therefore retried)
    TransientStatusCodes = new HashSet<StatusCode>
    {
        StatusCode.Unavailable,
        StatusCode.DeadlineExceeded,
        StatusCode.ResourceExhausted,
        StatusCode.Aborted,
        StatusCode.Internal,
    },
};
```

| Property | Default | Description |
|---|---|---|
| `MaxRetries` | `3` | Number of retry attempts (0 = disabled) |
| `BaseDelay` | `200 ms` | Base delay for exponential back-off with jitter |
| `MaxDelay` | `30 s` | Cap for exponential back-off delay |
| `CircuitBreakerFailureRatio` | `0.5` | Failure ratio to open the circuit |
| `CircuitBreakerMinimumThroughput` | `10` | Minimum calls before CB can open |
| `CircuitBreakerSamplingDuration` | `30 s` | Sliding window for failure ratio |
| `CircuitBreakerBreakDuration` | `5 s` | How long the circuit stays open |
| `CallTimeout` | `10 s` | Maximum time per call before `TimeoutRejectedException` |
| `TransientStatusCodes` | see above | `StatusCode` set that triggers retry/CB |

## Error Handling

Non-transient `RpcException`s (e.g. `NotFound`, `PermissionDenied`) are rethrown as-is. After all retries are exhausted the last exception is a `TransientRpcException` wrapping the original `RpcException`:

```csharp
try
{
    var reply = await client.SayHelloAsync(new HelloRequest { Name = "World" });
}
catch (TransientRpcException ex)
{
    // All retries failed
    Console.WriteLine($"gRPC status: {ex.StatusCode}");
    Console.WriteLine($"Original error: {ex.RpcException.Status.Detail}");
}
catch (BrokenCircuitException)
{
    // Circuit is open — fail fast without calling the server
}
catch (TimeoutRejectedException)
{
    // Call exceeded CallTimeout
}
```

## Resilience Pipeline Order

Calls flow through the pipeline in this order:

```
Retry → Circuit Breaker → Timeout → gRPC call
```

## Related Packages

| Package | Description |
|---|---|
| [PollyBackoff](https://www.nuget.org/packages/PollyBackoff) | Advanced back-off strategies with jitter |
| [PollyChaos](https://www.nuget.org/packages/PollyChaos) | Chaos engineering — inject faults in tests |
| [PollyMediatR](https://www.nuget.org/packages/PollyMediatR) | Polly pipeline behaviour for MediatR |
| [PollyEFCore](https://www.nuget.org/packages/PollyEFCore) | Resilient EF Core execution strategies |
| [PollyHealthChecks](https://www.nuget.org/packages/PollyHealthChecks) | Health check endpoints for Polly circuits |
| [PollyOpenAI](https://www.nuget.org/packages/PollyOpenAI) | Retry + rate-limit handling for OpenAI / Azure OpenAI |
| [PollyRedis](https://www.nuget.org/packages/PollyRedis) | Resilient StackExchange.Redis wrapper |
| [PollySignalR](https://www.nuget.org/packages/PollySignalR) | Reconnect policy for SignalR `HubConnection` |
| [PollyCaching](https://www.nuget.org/packages/PollyCaching) | Distributed cache with Polly resilience |
| [PollyBulkhead](https://www.nuget.org/packages/PollyBulkhead) | Bulkhead isolation for concurrent workloads |

| [PollyKafka](https://www.nuget.org/packages/PollyKafka) | Polly v8 resilience (retry, CB, timeout) for Confluent.Kafka producers and consumers |
| [PollyAzureServiceBus](https://www.nuget.org/packages/PollyAzureServiceBus) | Polly v8 resilience (retry, CB, timeout) for Azure Service Bus senders and receivers |
## License

MIT
