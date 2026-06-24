/// <summary>
/// Wraps a <see cref="GrpcChannel"/> with a Polly v8 <see cref="ResiliencePipeline"/>,
/// applying retry, timeout, and circuit-breaker policies to every gRPC call.
/// </summary>
public sealed class ResilientGrpcChannel(GrpcChannel channel, ResiliencePipeline pipeline)
{
    /// <summary>The underlying <see cref="GrpcChannel"/>.</summary>
    public GrpcChannel Inner => channel;

    /// <summary>
    /// Executes a gRPC unary call, disposing the <see cref="AsyncUnaryCall{TResponse}"/>
    /// after the response is received, protected by the resilience pipeline.
    /// </summary>
    public Task<TResponse> ExecuteAsync<TResponse>(
        Func<CancellationToken, AsyncUnaryCall<TResponse>> call,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteAsync(async ct =>
        {
            using var grpcCall = call(ct);
            return await grpcCall.ResponseAsync;
        }, cancellationToken).AsTask();

    /// <summary>
    /// Executes any async gRPC operation returning <typeparamref name="TResponse"/>,
    /// protected by the resilience pipeline.
    /// </summary>
    public Task<TResponse> ExecuteAsync<TResponse>(
        Func<CancellationToken, Task<TResponse>> call,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteAsync(
            ct => new ValueTask<TResponse>(call(ct)),
            cancellationToken).AsTask();

    /// <summary>
    /// Executes any async gRPC operation with no return value,
    /// protected by the resilience pipeline.
    /// </summary>
    public Task ExecuteAsync(
        Func<CancellationToken, Task> call,
        CancellationToken cancellationToken = default)
        => pipeline.ExecuteAsync(
            ct => new ValueTask(call(ct)),
            cancellationToken).AsTask();
}
