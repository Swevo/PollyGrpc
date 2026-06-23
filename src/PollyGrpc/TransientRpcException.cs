namespace PollyGrpc;

/// <summary>
/// Wraps a transient <see cref="RpcException"/> so Polly can identify and retry it
/// without interfering with non-transient gRPC errors.
/// </summary>
public sealed class TransientRpcException : Exception
{
    /// <summary>The original <see cref="RpcException"/> that caused this exception.</summary>
    public RpcException RpcException { get; }

    /// <summary>The gRPC status code of the underlying error.</summary>
    public StatusCode StatusCode => RpcException.StatusCode;

    internal TransientRpcException(RpcException inner)
        : base($"Transient gRPC error: {inner.StatusCode} — {inner.Status.Detail}", inner)
    {
        RpcException = inner;
    }
}
