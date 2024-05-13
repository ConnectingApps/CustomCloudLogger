using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConnectingApps.CustomCloudLogger;

public interface ILogAnalyticsClient
{
    Task LogEntryAsync<T>(T entity, string logType, string? resourceId = null,
        string? timeGeneratedCustomFieldName = null);

    Task LogEntriesAsync<T>(IReadOnlyList<T> entities, string logType, string? resourceId = null,
        string? timeGeneratedCustomFieldName = null);
}