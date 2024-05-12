using FluentAssertions;

namespace ConnectingApps.CustomCloudLogger.Test;

public class UnitTest1
{
    private readonly string _workSpaceId;
    private readonly string _sharedKey;

    public UnitTest1()
    {
        _workSpaceId = Environment.GetEnvironmentVariable("WORKSPACE_ID")!;
        _sharedKey = Environment.GetEnvironmentVariable("SHARED_KEY")!;
    }
    
    [Fact]
    public void WorkSpaceIdTest()
    {
        _workSpaceId.Should().NotBeNullOrEmpty("WORKSPACE_ID environment variable must be set");
        _workSpaceId.Length.Should().Be(36);
    }
    
    
    [Fact]
    public void SharedKeyTest()
    {
        _sharedKey.Should().NotBeNullOrEmpty("SHARED_KEY environment variable must be set");
        _sharedKey.Length.Should().Be(88);
    }
}