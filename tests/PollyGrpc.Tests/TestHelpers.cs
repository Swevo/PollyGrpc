namespace PollyGrpc.Tests;

/// <summary>
/// A fake <see cref="CallInvoker"/> that delegates to a handler for unary calls.
/// </summary>
internal sealed class FakeCallInvoker : CallInvoker
{
    private readonly Func<Task<string>> _handler;

    public FakeCallInvoker(Func<Task<string>> handler) => _handler = handler;

    public static readonly Method<string, string> TestMethod = new(
        MethodType.Unary, "TestService", "TestMethod",
        Marshallers.Create(
            s => System.Text.Encoding.UTF8.GetBytes(s),
            b => System.Text.Encoding.UTF8.GetString(b)),
        Marshallers.Create(
            s => System.Text.Encoding.UTF8.GetBytes(s),
            b => System.Text.Encoding.UTF8.GetString(b)));

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
    {
        Task<TResponse> task;
        try
        {
            task = (Task<TResponse>)(object)_handler();
        }
        catch (Exception ex)
        {
            task = Task.FromException<TResponse>(ex);
        }
        return new AsyncUnaryCall<TResponse>(
            task,
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
    }

    public override TResponse BlockingUnaryCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        => throw new NotSupportedException();

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string? host, CallOptions options)
        => throw new NotSupportedException();

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        => throw new NotSupportedException();

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string? host, CallOptions options)
        => throw new NotSupportedException();
}

internal static class TestFactory
{
    public static PollyGrpcOptions FastOptions(Action<PollyGrpcOptions>? configure = null)
    {
        var opts = new PollyGrpcOptions
        {
            BaseDelay = TimeSpan.Zero,
            MaxDelay = TimeSpan.Zero,
            CallTimeout = TimeSpan.FromSeconds(10),
            CircuitBreakerMinimumThroughput = 100,
            CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(10),
            CircuitBreakerBreakDuration = TimeSpan.FromSeconds(1),
        };
        configure?.Invoke(opts);
        return opts;
    }

    /// <summary>Creates an intercepted <see cref="CallInvoker"/> backed by the supplied handler.</summary>
    public static CallInvoker MakeInvoker(
        Func<Task<string>> handler,
        Action<PollyGrpcOptions>? configure = null)
    {
        var options = FastOptions(configure);
        var interceptor = new PollyClientInterceptor(options);
        return new FakeCallInvoker(handler).Intercept(interceptor);
    }

    /// <summary>Issues a fake unary call via the intercepted invoker.</summary>
    public static Task<string> CallAsync(
        Func<Task<string>> handler,
        Action<PollyGrpcOptions>? configure = null)
    {
        var invoker = MakeInvoker(handler, configure);
        var call = invoker.AsyncUnaryCall(FakeCallInvoker.TestMethod, null, new CallOptions(), "request");
        return call.ResponseAsync;
    }
}
