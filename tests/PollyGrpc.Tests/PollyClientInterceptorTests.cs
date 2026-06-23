namespace PollyGrpc.Tests;

public class PollyClientInterceptorTests
{
    // ── Success path ──────────────────────────────────────────────────────

    [Fact]
    public async Task AsyncUnary_SuccessOnFirstAttempt_ReturnsResponse()
    {
        var result = await TestFactory.CallAsync(() => Task.FromResult("hello"));
        result.Should().Be("hello");
    }

    // ── Retry on transient StatusCodes ────────────────────────────────────

    [Theory]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.DeadlineExceeded)]
    [InlineData(StatusCode.ResourceExhausted)]
    [InlineData(StatusCode.Aborted)]
    [InlineData(StatusCode.Internal)]
    public async Task AsyncUnary_RetriesOnTransientStatus_Succeeds(StatusCode code)
    {
        int calls = 0;
        var result = await TestFactory.CallAsync(
            () =>
            {
                calls++;
                if (calls < 2) throw new RpcException(new Status(code, "transient"));
                return Task.FromResult("ok");
            },
            o => { o.MaxRetries = 3; o.CircuitBreakerMinimumThroughput = 100; });

        result.Should().Be("ok");
        calls.Should().Be(2);
    }

    [Fact]
    public async Task AsyncUnary_NonTransientStatus_NotRetried()
    {
        int calls = 0;
        var act = () => TestFactory.CallAsync(
            () =>
            {
                calls++;
                throw new RpcException(new Status(StatusCode.NotFound, "not found"));
            },
            o => { o.MaxRetries = 3; o.CircuitBreakerMinimumThroughput = 100; });

        await act.Should().ThrowAsync<RpcException>()
            .Where(e => e.StatusCode == StatusCode.NotFound);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task AsyncUnary_ExhaustsRetries_ThrowsTransientRpcException()
    {
        var act = () => TestFactory.CallAsync(
            () => throw new RpcException(new Status(StatusCode.Unavailable, "down")),
            o => { o.MaxRetries = 2; o.CircuitBreakerMinimumThroughput = 100; });

        await act.Should().ThrowAsync<TransientRpcException>()
            .Where(e => e.StatusCode == StatusCode.Unavailable);
    }

    // ── Circuit breaker ───────────────────────────────────────────────────

    [Fact]
    public async Task CircuitBreaker_OpensAfterThreshold()
    {
        var exceptions = new List<Exception>();
        for (int i = 0; i < 10; i++)
        {
            try
            {
                await TestFactory.CallAsync(
                    () => throw new RpcException(new Status(StatusCode.Unavailable, "down")),
                    o =>
                    {
                        o.MaxRetries = 0;
                        o.CircuitBreakerMinimumThroughput = 3;
                        o.CircuitBreakerFailureRatio = 0.5;
                        o.CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(10);
                        o.CircuitBreakerBreakDuration = TimeSpan.FromSeconds(10);
                    });
            }
            catch (Exception ex) { exceptions.Add(ex); }
        }

        // Note: each call creates a fresh interceptor so CB won't open from separate calls.
        // Instead verify that a single interceptor opens its CB.
        var interceptor = new PollyClientInterceptor(new PollyGrpcOptions
        {
            MaxRetries = 0,
            CircuitBreakerMinimumThroughput = 3,
            CircuitBreakerFailureRatio = 0.5,
            CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(10),
            CircuitBreakerBreakDuration = TimeSpan.FromSeconds(10),
        });
        var fakeInvoker = new FakeCallInvoker(
            () => throw new RpcException(new Status(StatusCode.Unavailable, "down")));
        var invoker = fakeInvoker.Intercept(interceptor);
        var cbExceptions = new List<Exception>();

        for (int i = 0; i < 10; i++)
        {
            try
            {
                await invoker.AsyncUnaryCall(FakeCallInvoker.TestMethod, null, new CallOptions(), "r")
                    .ResponseAsync;
            }
            catch (Exception ex) { cbExceptions.Add(ex); }
        }

        cbExceptions.Should().Contain(e => e is BrokenCircuitException);
    }

    // ── Timeout ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AsyncUnary_Timeout_ThrowsTimeoutRejectedException()
    {
        var act = () => TestFactory.CallAsync(
            async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                return "never";
            },
            o =>
            {
                o.MaxRetries = 0;
                o.CallTimeout = TimeSpan.FromMilliseconds(50);
                o.CircuitBreakerMinimumThroughput = 100;
            });

        await act.Should().ThrowAsync<TimeoutRejectedException>();
    }

    // ── Custom transient codes ─────────────────────────────────────────────

    [Fact]
    public async Task CustomTransientCode_IsRetried()
    {
        int calls = 0;
        var result = await TestFactory.CallAsync(
            () =>
            {
                calls++;
                if (calls < 2) throw new RpcException(new Status(StatusCode.PermissionDenied, "denied"));
                return Task.FromResult("ok");
            },
            o =>
            {
                o.MaxRetries = 2;
                o.TransientStatusCodes = new HashSet<StatusCode> { StatusCode.PermissionDenied };
                o.CircuitBreakerMinimumThroughput = 100;
            });

        result.Should().Be("ok");
        calls.Should().Be(2);
    }

    // ── TransientRpcException properties ──────────────────────────────────

    [Fact]
    public async Task TransientRpcException_HasCorrectProperties()
    {
        var act = () => TestFactory.CallAsync(
            () => throw new RpcException(new Status(StatusCode.Unavailable, "server down")),
            o => { o.MaxRetries = 0; o.CircuitBreakerMinimumThroughput = 100; });

        var ex = await act.Should().ThrowAsync<TransientRpcException>();
        ex.Which.StatusCode.Should().Be(StatusCode.Unavailable);
        ex.Which.RpcException.Should().NotBeNull();
    }

    // ── MaxRetries = 0 skips retry ────────────────────────────────────────

    [Fact]
    public async Task MaxRetries_Zero_NoRetry()
    {
        int calls = 0;
        var act = () => TestFactory.CallAsync(
            () =>
            {
                calls++;
                throw new RpcException(new Status(StatusCode.Unavailable, "down"));
            },
            o => { o.MaxRetries = 0; o.CircuitBreakerMinimumThroughput = 100; });

        try { await act(); } catch { }
        calls.Should().Be(1);
    }

    // ── Null guards ───────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        Action act = () => new PollyClientInterceptor(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
