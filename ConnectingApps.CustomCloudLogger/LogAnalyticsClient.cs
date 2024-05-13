using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ConnectingApps.CustomCloudLogger;

/// <summary>
/// Client to send logs to Azure Log Analytics Workspace
/// </summary>
public class LogAnalyticsClient : IDisposable
{
    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    
    private static readonly ASCIIEncoding AsciiEncodingEncoding = new();

    private static readonly HashSet<Type> AllowedTypes = new()
    {
        typeof(string), typeof(bool), typeof(bool?), typeof(double), typeof(double?),
        typeof(int), typeof(int?), typeof(long), typeof(long?),
        typeof(DateTime), typeof(DateTime?), typeof(Guid), typeof(Guid?)
    };

    private readonly HttpClient _httpClient;
    private readonly string _workspaceId;
    private readonly string _sharedKey;
    private readonly string _requestBaseUrl;

    private LogAnalyticsClient(HttpClient client, string workspaceId, string sharedKey, string? endPointOverride = null)
    {
        if (string.IsNullOrEmpty(workspaceId))
        {
            throw new ArgumentNullException(nameof(workspaceId), "workspaceId cannot be null or empty");
        }

        if (string.IsNullOrEmpty(sharedKey))
        {
            throw new ArgumentNullException(nameof(sharedKey), "sharedKey cannot be null or empty");
        }

        if (!StringAnalyzer.IsBase64String(sharedKey))
        {
            throw new ArgumentException($"{nameof(sharedKey)} must be a valid Base64 encoded string", nameof(sharedKey));
        }

        var azureEndpoint = string.IsNullOrEmpty(endPointOverride) ? Consts.AzureCommercialEndpoint : endPointOverride;
        _workspaceId = workspaceId;
        _sharedKey = sharedKey;
        _requestBaseUrl = $"https://{_workspaceId}.{azureEndpoint}/api/logs?api-version={Consts.ApiVersion}";
        _httpClient = client;
    }

    public LogAnalyticsClient(string workspaceId, string sharedKey, string? endPointOverride = null)
        : this(new HttpClient(), workspaceId, sharedKey, endPointOverride)
    {
    }
    
    public Task LogEntryAsync<T>(T entity, string logType, string? resourceId = null,
        string? timeGeneratedCustomFieldName = null)
    {
        if (entity == null)
        {
            throw new ArgumentNullException(nameof(entity), $"parameter '{nameof(entity)}' cannot be null");
        }

        if (string.IsNullOrEmpty(logType))
        {
            throw new ArgumentNullException(nameof(logType), $"parameter '{nameof(logType)}' " +
                                                             $"cannot be null, and must be a string.");
        }

        if (logType.Length > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(logType), logType.Length, 
                "The size limit is 100 characters.");
        }

        if (!StringAnalyzer.IsAlphaNumUnderscore(logType))
        {
            throw new ArgumentOutOfRangeException(nameof(logType), logType, 
                "LogType can only contain letters, numbers, and underscore." +
                "It does no allow special characters.");
        }

        ValidatePropertyTypes(entity);
        return SendLogEntriesPrivateAsync(new List<T> {entity}, logType, resourceId, timeGeneratedCustomFieldName);
    }
    
    public async Task LogEntriesAsync<T>(IReadOnlyList<T> entities, string logType, string? resourceId = null,
        string? timeGeneratedCustomFieldName = null)
    {
        if (entities == null)
        {
            throw new ArgumentNullException(nameof(entities), $"parameter '{nameof(entities)}' cannot be null");
        }

        if (string.IsNullOrEmpty(logType))
        {
            throw new ArgumentNullException(nameof(logType), $"parameter '{nameof(logType)}' cannot be null, and must contain be a string.");
        }

        if (logType.Length > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(logType), logType.Length, "The size limit is 100 characters.");
        }

        if (!StringAnalyzer.IsAlphaNumUnderscore(logType))
        {
            throw new ArgumentOutOfRangeException(nameof(logType), logType, "LogType can only contain letters, numbers, and underscore");
        }

        if (entities.Count == 0)
        {
            return;
        }
        
        await SendLogEntriesPrivateAsync(entities, logType, resourceId, timeGeneratedCustomFieldName);
    }
    
    private async Task SendLogEntriesPrivateAsync<T>(IReadOnlyList<T> entities, string logType, string? resourceId = null, string? timeGeneratedCustomFieldName = null)
    {
        foreach (var entity in entities)
        {
            ValidatePropertyTypes(entity);
        }

        var dateTimeNow = DateTime.UtcNow.ToString("r", System.Globalization.CultureInfo.InvariantCulture);
        var entityAsJson = JsonSerializer.Serialize(entities, SerializeOptions);
        var authSignature = GetAuthSignature(entityAsJson, dateTimeNow);

        var headers = new Dictionary<string, string?>
        {
            {"Authorization", authSignature},
            {"Log-Type", logType},
            {"x-ms-date", dateTimeNow},
            {"time-generated-field", timeGeneratedCustomFieldName},
            {"x-ms-AzureResourceId", resourceId}
        };
        
        using var request = new HttpRequestMessage(HttpMethod.Post, this._requestBaseUrl);
        foreach (var header in headers.Where(h => !string.IsNullOrEmpty(h.Value)))
        {
            request.Headers.Add(header.Key, header.Value);
        }

        HttpContent httpContent = new StringContent(entityAsJson, Encoding.UTF8);
        httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        request.Content = httpContent;
        var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private string GetAuthSignature(string serializedJsonObject, string dateString)
    {
        var stringToSign = $"""
                            POST
                            {Encoding.UTF8.GetBytes(serializedJsonObject).Length}
                            application/json
                            x-ms-date:{dateString}
                            /api/logs
                            """.Replace(Environment.NewLine, "\n");
        
        var sharedKeyBytes = Convert.FromBase64String(_sharedKey);
        var stringToSignBytes = AsciiEncodingEncoding.GetBytes(stringToSign);
        using (var hmacsha256Encryption = new HMACSHA256(sharedKeyBytes))
        {
            var hashBytes = hmacsha256Encryption.ComputeHash(stringToSignBytes);
            var signedString = Convert.ToBase64String(hashBytes);
            return $"SharedKey {_workspaceId}:{signedString}";
        }
    }

    private void ValidatePropertyTypes<T>(T entity)
    {
        // Retrieve all properties of the entity type using reflection
        var properties = entity!.GetType().GetProperties();

        // Validate each property's type
        foreach (var propertyInfo in properties)
        {
            if (!AllowedTypes.Contains(propertyInfo.PropertyType))
            {
                var typedString = propertyInfo.PropertyType.ToString();
                // Check for .NET 6+ types
                if (typedString.EndsWith("System.DateOnly") || typedString.EndsWith("System.DateOnly]"))
                {
                    continue;
                }
                throw new ArgumentOutOfRangeException(
                    $"Property '{propertyInfo.Name}' of entity with type '{entity.GetType()}' " +
                    $"is not one of the valid properties:" +
                    $" String, Boolean, Double, Integer, DateTime, DateOnly and Guid.");
            }
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}