namespace PollyGrpc;

/// <summary>
/// Extension methods for adding Polly resilience to gRPC via <see cref="IHttpClientBuilder"/>.
/// </summary>
public static class HttpClientBuilderExtensions
{
    /// <summary>
    /// Adds a <see cref="PollyClientInterceptor"/> to the gRPC channel created by this
    /// <see cref="IHttpClientBuilder"/>.
    /// </summary>
    /// <param name="builder">The HTTP client builder used for the gRPC channel.</param>
    /// <param name="configure">Optional delegate to customise resilience options.</param>
    /// <returns>The original builder to allow chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services
    ///     .AddGrpcClient&lt;Greeter.GreeterClient&gt;(o => o.Address = new Uri("https://localhost:5001"))
    ///     .AddPollyGrpcResilience();
    /// </code>
    /// </example>
    public static IHttpClientBuilder AddPollyGrpcResilience(
        this IHttpClientBuilder builder,
        Action<PollyGrpcOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new PollyGrpcOptions();
        configure?.Invoke(options);
        var interceptor = new PollyClientInterceptor(options);

        return builder.AddInterceptor(_ => interceptor);
    }
}
