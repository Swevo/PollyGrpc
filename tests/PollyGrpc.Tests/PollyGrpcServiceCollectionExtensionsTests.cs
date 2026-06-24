public class PollyGrpcServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPollyGrpc_RegistersResiliencePipeline()
    {
        var services = new ServiceCollection();
        services.AddSingleton(GrpcChannel.ForAddress("https://localhost:5001"));
        services.AddPollyGrpc(p => p.AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            ShouldHandle = GrpcTransientErrors.IsTransient,
        }));

        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<ResiliencePipeline>();

        Assert.NotNull(pipeline);
    }

    [Fact]
    public void AddPollyGrpc_RegistersResilientGrpcChannel()
    {
        var services = new ServiceCollection();
        services.AddSingleton(GrpcChannel.ForAddress("https://localhost:5001"));
        services.AddPollyGrpc(p => { });

        var provider = services.BuildServiceProvider();
        var resilient = provider.GetRequiredService<ResilientGrpcChannel>();

        Assert.NotNull(resilient);
    }

    [Fact]
    public void AddPollyGrpc_WithAddress_RegistersChannelAndContainer()
    {
        var services = new ServiceCollection();
        services.AddPollyGrpc("https://localhost:5001", p => p.AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            ShouldHandle = GrpcTransientErrors.IsTransient,
        }));

        var provider = services.BuildServiceProvider();
        var resilient = provider.GetRequiredService<ResilientGrpcChannel>();

        Assert.NotNull(resilient);
    }

    [Fact]
    public void AddPollyGrpc_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddSingleton(GrpcChannel.ForAddress("https://localhost:5001"));

        var result = services.AddPollyGrpc(p => { });

        Assert.Same(services, result);
    }

    [Fact]
    public void AddPollyGrpc_WithAddress_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddPollyGrpc("https://localhost:5001", p => { });

        Assert.Same(services, result);
    }
}
