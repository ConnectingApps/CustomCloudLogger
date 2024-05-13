# CustomCloudLogger
Send messages in a custom format to log analytics workspace in Azure


# A LogAnalytics Client for .NET Standard 2.0

The easiest and most generic way to send logs to Azure Log Analytics Workspace from your apps.
Just create a custom object and send it to Log Analytics. It will be shown as a log entry in the logs. This helps make logging easy in your applications, regardless of wether you're using .NET Core, .NET Framework, Xamarin, Tizen etc. Almost all .NET variants and versions are supported. The product is inspired by a package developed for .NET Core and .NET 6 named [LogAnalytics.Client](https://www.nuget.org/packages/loganalytics.client).


## NuGet

The [CustomCloudLogger](https://www.nuget.org/packages/ConnectingApps.CustomCloudLogger/1.0.0-initial) is available on NuGet.

## Support for .NET Versions and Variants
As explained, nearly all .NET variants and versions are supported. The `DateOnly` data type is also supported [when using .NET 6 or higher](https://devblogs.microsoft.com/dotnet/date-time-and-time-zone-enhancements-in-net-6/).


## How to use the LogAnalytics Client

### Installing the package

```
dotnet add package ConnectingApps.CustomCloudLogger
```

### Use the LogAnalyticsClient

Here is how you can use it:

```csharp
LogAnalyticsClient _client = new(WorkSpaceId, SharedKey);
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
```

In the code shown above, these things are done:
1. An instance of LogAnalyticsClient is created.
1. A single log entry is created because of the call to `LogEntryAsync`.
1. Multiple log entries are created because of the call to `LogEntriesAsync`

> Do not forget to dispose the client when using this in production code

## What do to with Azure?
In case you ran the code above, you'll find your your data in this table:

`TuplesLog_CL`

A typical KQL query in your log analytics workspace in Azure would look like this:

```KQL
TuplesLog_CL
| where X_s contains "Test"
```

If you are do not have a shared key to use here, you can generate one:

```bash
az monitor log-analytics workspace get-shared-keys --resource-group "YourResourceGroupName" --workspace-name "YourWorkspaceName"
```

## Development 

Contribute? Give it a go, but double check if the tests do succeed.
