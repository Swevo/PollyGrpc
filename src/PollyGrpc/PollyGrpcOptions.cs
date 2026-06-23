namespace PollyGrpc;

/// <summary>
/// Configuration options for the Polly resilience pipeline applied to gRPC calls.
/// </summary>
public sealed class PollyGrpcOptions
{
    /// <summary>Maximum number of retry attempts on transient failures. Default: 3.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Base delay between retries (exponential back-off). Default: 200 ms.</summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Maximum delay cap for exponential back-off. Default: 10 seconds.</summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Minimum number of requests in the sampling window before the circuit breaker can trip. Default: 5.
    /// </summary>
    public int CircuitBreakerMinimumThroughput { get; set; } = 5;

    /// <summary>Failure ratio (0–1) at which the circuit breaker opens. Default: 0.5 (50%).</summary>
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    /// <summary>Duration of the sliding window used to evaluate failure rate. Default: 30 seconds.</summary>
    public TimeSpan CircuitBreakerSamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>How long the circuit stays open before allowing a probe. Default: 30 seconds.</summary>
    public TimeSpan CircuitBreakerBreakDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Per-call timeout. Default: 30 seconds.</summary>
    public TimeSpan CallTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// gRPC status codes that are considered transient and eligible for retry.
    /// Defaults to Unavailable, DeadlineExceeded, ResourceExhausted, Aborted, Internal.
    /// </summary>
    public ISet<StatusCode> TransientStatusCodes { get; set; } = new HashSet<StatusCode>
    {
        StatusCode.Unavailable,
        StatusCode.DeadlineExceeded,
        StatusCode.ResourceExhausted,
        StatusCode.Aborted,
        StatusCode.Internal,
    };
}
