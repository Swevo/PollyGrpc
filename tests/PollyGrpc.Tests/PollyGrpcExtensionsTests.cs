public class PollyGrpcExtensionsTests
{
    private static readonly GrpcChannel _channel = GrpcChannel.ForAddress("https://localhost:5001");
    private static readonly ResiliencePipeline _pipeline = new ResiliencePipelineBuilder().Build();

    [Fact]
    public void WithPolly_Pipeline_ReturnsResilientGrpcChannel()
    {
        var resilient = _channel.WithPolly(_pipeline);

        Assert.NotNull(resilient);
        Assert.Same(_channel, resilient.Inner);
    }

    [Fact]
    public void WithPolly_Configure_ReturnsResilientGrpcChannel()
    {
        var resilient = _channel.WithPolly(p => p.AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 2,
            ShouldHandle = GrpcTransientErrors.IsTransient,
        }));

        Assert.NotNull(resilient);
        Assert.Same(_channel, resilient.Inner);
    }

    [Fact]
    public void WithPolly_InnerIsOriginalChannel()
    {
        var resilient = _channel.WithPolly(_pipeline);

        Assert.Same(_channel, resilient.Inner);
    }
}
