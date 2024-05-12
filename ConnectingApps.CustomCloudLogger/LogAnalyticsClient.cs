using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ConnectingApps.CustomCloudLogger;

/// <summary>
/// Client to send logs to Azure Log Analytics.
/// </summary>
public class LogAnalyticsClient : IDisposable
{
    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly HashSet<Type> AllowedTypes = new HashSet<Type>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="LogAnalyticsClient"/> class.
    /// </summary>
    /// <param name="workspaceId">Azure Log Analytics Workspace ID</param>
    /// <param name="sharedKey">Azure Log Analytics Workspace Shared Key</param>
    /// <param name="endPointOverride">The Azure Cloud to use.</param>
    public LogAnalyticsClient(string workspaceId, string sharedKey, string? endPointOverride = null)
        : this(new HttpClient(), workspaceId, sharedKey, endPointOverride)
    {
    }

    /// <summary>
    /// Send an entity as a single log entry to Azure Log Analytics.
    /// </summary>
    /// <typeparam name="T">Entity Type</typeparam>
    /// <param name="entity">The object</param>
    /// <param name="logType">The log type</param>
    /// <param name="resourceId">The resource id</param>
    /// <param name="timeGeneratedCustomFieldName">The name of the field that contains the Time Generated data</param>
    /// <returns>Does not return anything.</returns>
    public Task SendLogEntry<T>(T entity, string logType, string? resourceId = null, string? timeGeneratedCustomFieldName = null)
    {
        if (entity == null)
        {
            throw new ArgumentNullException(nameof(entity), $"parameter '{nameof(entity)}' cannot be null");
        }

        if (string.IsNullOrEmpty(logType))
        {
            throw new ArgumentNullException(nameof(logType), $"parameter '{nameof(logType)}' " +
                                                             $"cannot be null, and must contain a string.");
        }

        if (logType.Length > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(logType), logType.Length, 
                "The size limit for this parameter is 100 characters.");
        }

        if (!StringAnalyzer.IsAlphaNumUnderscore(logType))
        {
            throw new ArgumentOutOfRangeException(nameof(logType), logType, "Log-Type can only contain letters, numbers, and underscore (_). It does notor special characters.");
        }

        ValidatePropertyTypes(entity);
        List<T> list = new List<T> { entity };
        return SendLogEntries(list, logType, resourceId, timeGeneratedCustomFieldName);
    }

    /// <summary>
    /// Send a collection of entities in a batch to Azure Log Analytics.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="entities">The collection of objects</param>
    /// <param name="logType">The log type</param>
    /// <param name="resourceId">The resource id</param>
    /// <param name="timeGeneratedCustomFieldName">The name of the field that contains the Time Generated data</param>
    /// <returns>Does not return anything.</returns>
    public async Task SendLogEntries<T>(List<T> entities, string logType, string? resourceId = null, string? timeGeneratedCustomFieldName = null)
    {
        if (entities == null)
        {
            throw new ArgumentNullException(nameof(entities), $"parameter '{nameof(entities)}' cannot be null");
        }

        if (string.IsNullOrEmpty(logType))
        {
            throw new ArgumentNullException(nameof(logType), $"parameter '{nameof(logType)}' cannot be null, and must contain a string.");
        }

        if (logType.Length > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(logType), logType.Length, "The size limit for this parameter is 100 characters.");
        }

        if (!StringAnalyzer.IsAlphaNumUnderscore(logType))
        {
            throw new ArgumentOutOfRangeException(nameof(logType), logType, "Log-Type can only contain letters, numbers, and underscore (_). It does not support numerics or special characters.");
        }

        foreach (var entity in entities)
        {
            ValidatePropertyTypes(entity);
        }

        // Room for improvement: Identify if there is a timeGeneratedCustomFieldName specified, and if so, ensure the value of the field conforms with the ISO 8601 datetime format.

        var dateTimeNow = DateTime.UtcNow.ToString("r", System.Globalization.CultureInfo.InvariantCulture);


        var entityAsJson = JsonSerializer.Serialize(entities, SerializeOptions);
        var authSignature = this.GetAuthSignature(entityAsJson, dateTimeNow);

        using var request = new HttpRequestMessage(HttpMethod.Post, this._requestBaseUrl);
        request.Headers.Clear();
        request.Headers.Add("Authorization", authSignature);
        request.Headers.Add("Log-Type", logType);
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("x-ms-date", dateTimeNow);
        if (!string.IsNullOrWhiteSpace(timeGeneratedCustomFieldName))
        {
            // The name of the field that contains custom timestamp data.
            request.Headers.Add("time-generated-field", timeGeneratedCustomFieldName);
        }

        if (!string.IsNullOrWhiteSpace(resourceId))
        {
            // The Resource ID in Azure for a given resource to connect the logs with.
            request.Headers.Add("x-ms-AzureResourceId", resourceId);
        }

        HttpContent httpContent = new StringContent(entityAsJson, Encoding.UTF8);
        httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        request.Content = httpContent;
        var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private string GetAuthSignature(string serializedJsonObject, string dateString)
    {
        string stringToSign = $"POST\n{Encoding.UTF8.GetBytes(serializedJsonObject).Length}\napplication/json\nx-ms-date:{dateString}\n/api/logs";
        string signedString;

        var encoding = new ASCIIEncoding();
        var sharedKeyBytes = Convert.FromBase64String(_sharedKey);
        var stringToSignBytes = encoding.GetBytes(stringToSign);
        using (var hmacsha256Encryption = new HMACSHA256(sharedKeyBytes))
        {
            var hashBytes = hmacsha256Encryption.ComputeHash(stringToSignBytes);
            signedString = Convert.ToBase64String(hashBytes);
        }

        return $"SharedKey {_workspaceId}:{signedString}";
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
                throw new ArgumentOutOfRangeException(
                    $"Property '{propertyInfo.Name}' of entity with type '{entity.GetType()}' " +
                    $"is not one of the valid properties:" +
                    $" String, Boolean, Double, Integer, DateTime, and Guid.");
            }
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}