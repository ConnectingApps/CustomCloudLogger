using FluentAssertions;

namespace ConnectingApps.CustomCloudLogger.Test;

public class LogAnalyticsClientTest
{
    private static readonly string WorkSpaceId;
    private static readonly string SharedKey;
    private readonly LogAnalyticsClient _client;


    static LogAnalyticsClientTest()
    {
        WorkSpaceId = Environment.GetEnvironmentVariable("WORKSPACE_ID")!;
        SharedKey = Environment.GetEnvironmentVariable("SHARED_KEY")!;
        WorkSpaceId.Should().NotBeNullOrEmpty();
        SharedKey.Should().NotBeNullOrEmpty();
    }

    public LogAnalyticsClientTest()
    {
        _client = new LogAnalyticsClient(WorkSpaceId, SharedKey);
    }

    [Fact]
    public async Task SendMessageTest()
    {
        var logEntry = new 
        {
            Date = DateTime.UtcNow,
            Message = $"Test log message System Text from {Environment.MachineName}",
            Severity = "Info"
        };

        await _client.SendLogEntry(logEntry, "TestLog");
    }
    
}