namespace ConnectingApps.CustomCloudLogger.Console;

internal class TryData
{
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public string X { get; set; } = null!;
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public int Y { get; set; }
}

class Program
{
    static async Task Main()
    {
        System.Console.WriteLine("BEGIN");
        var workSpaceId = Environment.GetEnvironmentVariable("WORKSPACE_ID")!;
        var sharedKey = Environment.GetEnvironmentVariable("SHARED_KEY")!;
        using (var client = new LogAnalyticsClient(workSpaceId, sharedKey))
        {
            var logEntries = new TryData[]
            {
                new()
                {
                    X = $"Test1 Console {Environment.MachineName}",
                    Y = 1,
                },
                new()
                {
                    X = $"Test2 Console {Environment.MachineName}",
                    Y = 2,
                }
            };
            await client.LogEntryAsync(logEntries.First(), "TuplesLog");
            await client.LogEntriesAsync(logEntries, "TuplesLog");
            System.Console.WriteLine("END");
        }
    }
}

