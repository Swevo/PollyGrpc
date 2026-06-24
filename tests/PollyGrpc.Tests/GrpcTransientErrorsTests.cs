public class GrpcTransientErrorsTests
{
    [Theory]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.DeadlineExceeded)]
    [InlineData(StatusCode.ResourceExhausted)]
    [InlineData(StatusCode.Aborted)]
    public void StatusCodes_ContainsTransientStatusCode(StatusCode statusCode)
    {
        Assert.Contains(statusCode, GrpcTransientErrors.StatusCodes);
    }

    [Theory]
    [InlineData(StatusCode.OK)]
    [InlineData(StatusCode.NotFound)]
    [InlineData(StatusCode.InvalidArgument)]
    [InlineData(StatusCode.PermissionDenied)]
    [InlineData(StatusCode.Unauthenticated)]
    [InlineData(StatusCode.AlreadyExists)]
    [InlineData(StatusCode.Internal)]
    public void StatusCodes_DoesNotContainNonTransientStatusCode(StatusCode statusCode)
    {
        Assert.DoesNotContain(statusCode, GrpcTransientErrors.StatusCodes);
    }

    [Fact]
    public void StatusCodes_HasFourEntries()
    {
        Assert.Equal(4, GrpcTransientErrors.StatusCodes.Count);
    }

    [Fact]
    public void IsTransient_IsNotNull()
    {
        Assert.NotNull(GrpcTransientErrors.IsTransient);
    }
}
