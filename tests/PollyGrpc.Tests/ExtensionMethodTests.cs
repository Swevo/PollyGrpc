using Grpc.Net.Client;

namespace PollyGrpc.Tests;

public class ExtensionMethodTests
{
    [Fact]
    public void AddPollyGrpcResilience_NullBuilder_Throws()
    {
        IHttpClientBuilder? builder = null;
        Action act = () => builder!.AddPollyGrpcResilience();
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddPollyGrpcResilience_WithDefaultOptions_Succeeds()
    {
        var services = new ServiceCollection();
        var builder = services.AddGrpcClient<Greeter.GreeterClient>("test");
        var result = builder.AddPollyGrpcResilience();
        result.Should().NotBeNull();
    }

    [Fact]
    public void AddPollyGrpcResilience_WithCustomOptions_Succeeds()
    {
        var services = new ServiceCollection();
        var builder = services.AddGrpcClient<Greeter.GreeterClient>("test");
        var result = builder.AddPollyGrpcResilience(o =>
        {
            o.MaxRetries = 5;
            o.CallTimeout = TimeSpan.FromSeconds(30);
        });
        result.Should().NotBeNull();
    }

    [Fact]
    public void WithPollyResilience_NullInvoker_Throws()
    {
        CallInvoker? invoker = null;
        Action act = () => invoker!.WithPollyResilience();
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithPollyResilience_ValidInvoker_ReturnsWrappedInvoker()
    {
        var channel = GrpcChannel.ForAddress("http://localhost:9999");
        var invoker = channel.CreateCallInvoker();
        var wrapped = invoker.WithPollyResilience();
        wrapped.Should().NotBeNull();
    }

    [Fact]
    public void WithPollyResilience_CustomOptions_ReturnsWrappedInvoker()
    {
        var channel = GrpcChannel.ForAddress("http://localhost:9999");
        var invoker = channel.CreateCallInvoker();
        var wrapped = invoker.WithPollyResilience(o => o.MaxRetries = 5);
        wrapped.Should().NotBeNull();
    }
}

// Minimal proto stub for DI extension tests
public static class Greeter
{
    public class GreeterClient : ClientBase<GreeterClient>
    {
        public GreeterClient() : base() { }
        public GreeterClient(ChannelBase channel) : base(channel) { }
        public GreeterClient(CallInvoker callInvoker) : base(callInvoker) { }
        protected override GreeterClient NewInstance(ClientBaseConfiguration configuration)
            => throw new NotImplementedException("test stub");
    }
}
