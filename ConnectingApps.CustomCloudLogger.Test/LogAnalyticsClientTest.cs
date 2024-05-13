using FluentAssertions;

namespace ConnectingApps.CustomCloudLogger.Test;

public class LogAnalyticsClientTest : IDisposable
{
    public class TryData
    {
        public string X { get; set; }
        public int Y { get; set; }
    }
    
    private static readonly string WorkSpaceId;
    private static readonly string SharedKey;
    private readonly LogAnalyticsClient _client = new(WorkSpaceId, SharedKey);
    
    static LogAnalyticsClientTest()
    {
        WorkSpaceId = Environment.GetEnvironmentVariable("WORKSPACE_ID")!;
        SharedKey = Environment.GetEnvironmentVariable("SHARED_KEY")!;
        WorkSpaceId.Should().NotBeNullOrEmpty();
        SharedKey.Should().NotBeNullOrEmpty();
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

        await _client.LogEntryAsync(logEntry, "TestLog");
    }
    
    [Fact]
    public async Task SendMessageDateOnlyTest()
    {
        var logEntry = new 
        {
            Date = DateTime.UtcNow,
            Message = $"Test log message System Text from {Environment.MachineName}",
            Severity = "Info",
            Correct = DateOnly.MaxValue
        };

        await _client.LogEntryAsync(logEntry, "TestLogCorrect");
    }
    
        
    [Fact]
    public async Task SendMessagePluralTest()
    {
        var logEntries = new TryData[]
        {
            new()
            {
                X = $"Test1 {Environment.MachineName}",
                Y = 1,
            },
            new()
            {
                X = $"Test2 {Environment.MachineName}",
                Y = 2,
            }
        };
        await _client.LogEntryAsync(logEntries.First(), "TuplesLog");
        await _client.LogEntriesAsync(logEntries, "TuplesLog");
    }
    
    [Fact]
    public async Task SendMessageDateOnlyNullableTest()
    {
        DateOnly? nullableDate = DateOnly.MaxValue;
        var logEntry = new 
        {
            Date = DateTime.UtcNow,
            Message = $"Test log message System Text from {Environment.MachineName}",
            Severity = "Info",
            Correct = nullableDate
        };

        await _client.LogEntryAsync(logEntry, "TestLogCorrect");
    }
    
    [Fact]
    public async Task SendMessageWrongTest()
    {
        var logEntry = new 
        {
            Date = DateTime.UtcNow,
            Message = $"Test log message System Text from {Environment.MachineName}",
            Severity = "Info",
            Wrong = new Tuple<string>("Hoi")
        };

        var toFail = () => _client.LogEntryAsync(logEntry, "TryLog");
        await toFail.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
    
    public void Dispose()
    {
        _client.Dispose();
    }
}