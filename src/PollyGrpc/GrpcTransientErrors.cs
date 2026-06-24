/// <summary>
/// Pre-built Polly <see cref="PredicateBuilder"/> for common gRPC transient errors.
/// Covers Unavailable (14), DeadlineExceeded (4), ResourceExhausted (8), and Aborted (10).
/// </summary>
public static class GrpcTransientErrors
{
    /// <summary>
    /// gRPC status codes that indicate a transient failure safe to retry on idempotent calls.
    /// </summary>
    public static readonly IReadOnlySet<StatusCode> StatusCodes = new HashSet<StatusCode>
    {
        StatusCode.Unavailable,       // 14 — server temporarily unavailable, most common transient gRPC error
        StatusCode.DeadlineExceeded,  // 4  — request timed out before server could respond
        StatusCode.ResourceExhausted, // 8  — quota or rate limit exceeded (similar to HTTP 429)
        StatusCode.Aborted,           // 10 — operation aborted (e.g. transaction conflict); safe to retry
    };

    /// <summary>
    /// A <see cref="PredicateBuilder"/> that handles <see cref="RpcException"/> for all
    /// status codes in <see cref="StatusCodes"/>. Assign to <c>ShouldHandle</c> on any Polly strategy.
    /// </summary>
    public static readonly PredicateBuilder IsTransient =
        (PredicateBuilder)new PredicateBuilder()
            .Handle<RpcException>(ex => StatusCodes.Contains(ex.StatusCode));
}
