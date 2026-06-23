namespace PollyGrpc;

/// <summary>
/// Extension methods for applying Polly resilience directly to a <see cref="CallInvoker"/>.
/// </summary>
public static class CallInvokerExtensions
{
    /// <summary>
    /// Returns a new <see cref="CallInvoker"/> that intercepts all calls with a
    /// <see cref="PollyClientInterceptor"/>.
    /// </summary>
    /// <param name="invoker">The base call invoker (e.g. from <c>channel.CreateCallInvoker()</c>).</param>
    /// <param name="configure">Optional delegate to customise resilience options.</param>
    public static CallInvoker WithPollyResilience(
        this CallInvoker invoker,
        Action<PollyGrpcOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(invoker);

        var options = new PollyGrpcOptions();
        configure?.Invoke(options);

        return invoker.Intercept(new PollyClientInterceptor(options));
    }
}
