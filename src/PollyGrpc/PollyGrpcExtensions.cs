/// <summary>Extension methods for adding Polly resilience to gRPC channels.</summary>
public static class PollyGrpcExtensions
{
    /// <summary>Wraps a <see cref="GrpcChannel"/> with the given <see cref="ResiliencePipeline"/>.</summary>
    public static ResilientGrpcChannel WithPolly(
        this GrpcChannel channel,
        ResiliencePipeline pipeline)
        => new(channel, pipeline);

    /// <summary>Wraps a <see cref="GrpcChannel"/> with a pipeline built by <paramref name="configure"/>.</summary>
    public static ResilientGrpcChannel WithPolly(
        this GrpcChannel channel,
        Action<ResiliencePipelineBuilder> configure)
    {
        var builder = new ResiliencePipelineBuilder();
        configure(builder);
        return new(channel, builder.Build());
    }
}
