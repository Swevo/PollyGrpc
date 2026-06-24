/// <summary>Dependency-injection extensions for <c>PollyGrpc</c>.</summary>
public static class PollyGrpcServiceCollectionExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="ResiliencePipeline"/> built by <paramref name="configure"/>
    /// and a transient <see cref="ResilientGrpcChannel"/> factory that wraps the
    /// <see cref="GrpcChannel"/> registered in the DI container.
    /// </summary>
    public static IServiceCollection AddPollyGrpc(
        this IServiceCollection services,
        Action<ResiliencePipelineBuilder> configure)
    {
        var builder = new ResiliencePipelineBuilder();
        configure(builder);
        var pipeline = builder.Build();

        services.AddSingleton(pipeline);
        services.AddTransient<ResilientGrpcChannel>(sp =>
            sp.GetRequiredService<GrpcChannel>().WithPolly(pipeline));

        return services;
    }

    /// <summary>
    /// Creates a <see cref="GrpcChannel"/> for <paramref name="address"/>, then registers
    /// a singleton <see cref="ResiliencePipeline"/> and transient <see cref="ResilientGrpcChannel"/>.
    /// </summary>
    public static IServiceCollection AddPollyGrpc(
        this IServiceCollection services,
        string address,
        Action<ResiliencePipelineBuilder> configure)
    {
        services.AddSingleton(GrpcChannel.ForAddress(address));
        return services.AddPollyGrpc(configure);
    }
}
