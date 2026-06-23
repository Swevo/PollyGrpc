namespace PollyGrpc;

/// <summary>
/// A gRPC <see cref="Interceptor"/> that wraps every unary and server-streaming call
/// in a Polly v8 resilience pipeline: retry, circuit breaker, and per-call timeout.
/// </summary>
public sealed class PollyClientInterceptor : Interceptor
{
    private readonly ResiliencePipeline _pipeline;
    private readonly PollyGrpcOptions _options;

    /// <summary>
    /// Initialises the interceptor with the given options, building the Polly pipeline.
    /// </summary>
    public PollyClientInterceptor(PollyGrpcOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _pipeline = BuildPipeline(options);
    }

    /// <inheritdoc />
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var responseTask = _pipeline.ExecuteAsync(async ct =>
        {
            var call = continuation(request, context);
            try
            {
                return await call.ResponseAsync.WaitAsync(ct).ConfigureAwait(false);
            }
            catch (RpcException ex) when (_options.TransientStatusCodes.Contains(ex.StatusCode))
            {
                throw new TransientRpcException(ex);
            }
        }).AsTask();

        return new AsyncUnaryCall<TResponse>(
            responseTask,
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
    }

    /// <inheritdoc />
    public override TResponse BlockingUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        try
        {
            return continuation(request, context);
        }
        catch (RpcException ex) when (_options.TransientStatusCodes.Contains(ex.StatusCode))
        {
            // Blocking calls cannot use async Polly pipelines; throw as-is.
            throw new TransientRpcException(ex);
        }
    }

    private static ResiliencePipeline BuildPipeline(PollyGrpcOptions options)
    {
        var predicateBuilder = new PredicateBuilder().Handle<TransientRpcException>();
        var builder = new ResiliencePipelineBuilder();

        if (options.MaxRetries >= 1)
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = predicateBuilder,
                MaxRetryAttempts = options.MaxRetries,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = options.BaseDelay,
                MaxDelay = options.MaxDelay,
            });
        }

        builder
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = predicateBuilder,
                FailureRatio = options.CircuitBreakerFailureRatio,
                MinimumThroughput = options.CircuitBreakerMinimumThroughput,
                SamplingDuration = options.CircuitBreakerSamplingDuration,
                BreakDuration = options.CircuitBreakerBreakDuration,
            })
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = options.CallTimeout,
            });

        return builder.Build();
    }
}
