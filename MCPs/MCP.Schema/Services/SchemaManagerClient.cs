using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace MCP.Schema.Services;

/// <summary>
/// HTTP client for communicating with the Schema Manager
/// </summary>
public class SchemaManagerClient : ISchemaManagerClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SchemaManagerClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public SchemaManagerClient(IHttpClientFactory httpClientFactory, ILogger<SchemaManagerClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Schema");
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Gets all schemas from the Schema Manager
    /// </summary>
    public async Task<List<SchemaEntityDto>> GetAllSchemasAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching all schemas from Schema Manager");
            
            var response = await _httpClient.GetAsync("/api/Schema", cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var schemas = JsonSerializer.Deserialize<List<SchemaEntityDto>>(json, _jsonOptions) ?? new List<SchemaEntityDto>();
            
            _logger.LogDebug("Retrieved {Count} schemas from Schema Manager", schemas.Count);
            return schemas;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching all schemas from Schema Manager");
            throw;
        }
    }

    /// <summary>
    /// Gets a specific schema by ID
    /// </summary>
    public async Task<SchemaEntityDto?> GetSchemaByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching schema {Id} from Schema Manager", id);
            
            var response = await _httpClient.GetAsync($"/api/Schema/{id}", cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogDebug("Schema {Id} not found", id);
                return null;
            }
            
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var schema = JsonSerializer.Deserialize<SchemaEntityDto>(json, _jsonOptions);
            
            _logger.LogDebug("Retrieved schema {Id} from Schema Manager", id);
            return schema;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching schema {Id} from Schema Manager", id);
            throw;
        }
    }

    /// <summary>
    /// Gets a schema by composite key (version + name)
    /// </summary>
    public async Task<SchemaEntityDto?> GetSchemaByCompositeKeyAsync(string compositeKey, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching schema by composite key {CompositeKey} from Schema Manager", compositeKey);

            // Split composite key into version and name
            // Format: "version_name"
            var parts = compositeKey.Split('_', 2);
            if (parts.Length != 2)
            {
                _logger.LogWarning("Invalid composite key format: {CompositeKey}. Expected format: 'version_name'", compositeKey);
                return null;
            }

            var version = parts[0];
            var name = parts[1];

            var url = $"/api/Schema/composite/{Uri.EscapeDataString(version)}/{Uri.EscapeDataString(name)}";

            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogDebug("Schema with composite key {CompositeKey} not found", compositeKey);
                return null;
            }

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            var schema = JsonSerializer.Deserialize<SchemaEntityDto>(json, _jsonOptions);

            _logger.LogDebug("Retrieved schema by composite key {CompositeKey} from Schema Manager", compositeKey);
            return schema;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching schema by composite key {CompositeKey} from Schema Manager", compositeKey);
            throw;
        }
    }

    /// <summary>
    /// Gets schemas by version
    /// </summary>
    public async Task<List<SchemaEntityDto>> GetSchemasByVersionAsync(string version, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching schemas by version {Version} from Schema Manager", version);
            
            var response = await _httpClient.GetAsync($"/api/Schema/version/{Uri.EscapeDataString(version)}", cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var schemas = JsonSerializer.Deserialize<List<SchemaEntityDto>>(json, _jsonOptions) ?? new List<SchemaEntityDto>();
            
            _logger.LogDebug("Retrieved {Count} schemas by version {Version} from Schema Manager", schemas.Count, version);
            return schemas;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching schemas by version {Version} from Schema Manager", version);
            throw;
        }
    }

    /// <summary>
    /// Gets schemas by name
    /// </summary>
    public async Task<List<SchemaEntityDto>> GetSchemasByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching schemas by name {Name} from Schema Manager", name);
            
            var response = await _httpClient.GetAsync($"/api/Schema/name/{Uri.EscapeDataString(name)}", cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var schemas = JsonSerializer.Deserialize<List<SchemaEntityDto>>(json, _jsonOptions) ?? new List<SchemaEntityDto>();
            
            _logger.LogDebug("Retrieved {Count} schemas by name {Name} from Schema Manager", schemas.Count, name);
            return schemas;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching schemas by name {Name} from Schema Manager", name);
            throw;
        }
    }

    /// <summary>
    /// Validates a schema definition
    /// </summary>
    public async Task<SchemaValidationResult> ValidateSchemaAsync(string definition, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Validating schema definition with Schema Manager");
            
            var request = new { definition };
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/api/Schema/validate", content, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<SchemaValidationResult>(responseJson, _jsonOptions) ?? new SchemaValidationResult();
            
            _logger.LogDebug("Schema validation completed. Valid: {IsValid}", result.IsValid);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating schema definition with Schema Manager");
            throw;
        }
    }

    /// <summary>
    /// Analyzes breaking changes between two schema versions
    /// </summary>
    public async Task<BreakingChangeAnalysisResult> AnalyzeBreakingChangesAsync(string oldDefinition, string newDefinition, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Analyzing breaking changes with Schema Manager");

            var request = new { oldDefinition, newDefinition };
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/Schema/analyze-breaking-changes", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<BreakingChangeAnalysisResult>(responseJson, _jsonOptions) ?? new BreakingChangeAnalysisResult();

            _logger.LogDebug("Breaking change analysis completed. Has breaking changes: {HasBreakingChanges}", result.HasBreakingChanges);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing breaking changes with Schema Manager");
            throw;
        }
    }

    /// <summary>
    /// Gets paged schemas from the Schema Manager
    /// </summary>
    public async Task<PagedSchemaResult> GetPagedSchemasAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching paged schemas from Schema Manager. Page: {Page}, PageSize: {PageSize}", page, pageSize);

            var response = await _httpClient.GetAsync($"/api/Schema/paged?page={page}&pageSize={pageSize}", cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<PagedSchemaResult>(json, _jsonOptions) ?? new PagedSchemaResult();

            _logger.LogDebug("Retrieved {Count} schemas (Page {Page}/{TotalPages})", result.Data.Count, result.Page, result.TotalPages);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching paged schemas from Schema Manager. Page: {Page}, PageSize: {PageSize}", page, pageSize);
            throw;
        }
    }

    /// <summary>
    /// Gets schemas by definition
    /// </summary>
    public async Task<List<SchemaEntityDto>> GetSchemasByDefinitionAsync(string definition, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching schemas by definition from Schema Manager");

            var response = await _httpClient.GetAsync($"/api/Schema/definition/{Uri.EscapeDataString(definition)}", cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var schemas = JsonSerializer.Deserialize<List<SchemaEntityDto>>(json, _jsonOptions) ?? new List<SchemaEntityDto>();

            _logger.LogDebug("Retrieved {Count} schemas by definition from Schema Manager", schemas.Count);
            return schemas;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching schemas by definition from Schema Manager");
            throw;
        }
    }

    /// <summary>
    /// Checks if a schema exists by ID
    /// </summary>
    public async Task<bool> SchemaExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Checking if schema {Id} exists in Schema Manager", id);

            var response = await _httpClient.GetAsync($"/api/Schema/{id}/exists", cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var exists = JsonSerializer.Deserialize<bool>(json, _jsonOptions);

            _logger.LogDebug("Schema {Id} exists: {Exists}", id, exists);
            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if schema {Id} exists in Schema Manager", id);
            throw;
        }
    }

    /// <summary>
    /// Creates a new schema
    /// </summary>
    public async Task<SchemaEntityDto> CreateSchemaAsync(SchemaEntityDto schema, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Creating schema in Schema Manager. Version: {Version}, Name: {Name}", schema.Version, schema.Name);

            var json = JsonSerializer.Serialize(schema, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/Schema", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var created = JsonSerializer.Deserialize<SchemaEntityDto>(responseJson, _jsonOptions) ?? throw new InvalidOperationException("Failed to deserialize created schema");

            _logger.LogDebug("Successfully created schema {Id} in Schema Manager", created.Id);
            return created;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating schema in Schema Manager. Version: {Version}, Name: {Name}", schema.Version, schema.Name);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing schema
    /// </summary>
    public async Task<SchemaEntityDto> UpdateSchemaAsync(Guid id, SchemaEntityDto schema, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Updating schema {Id} in Schema Manager. Version: {Version}, Name: {Name}", id, schema.Version, schema.Name);

            var json = JsonSerializer.Serialize(schema, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync($"/api/Schema/{id}", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var updated = JsonSerializer.Deserialize<SchemaEntityDto>(responseJson, _jsonOptions) ?? throw new InvalidOperationException("Failed to deserialize updated schema");

            _logger.LogDebug("Successfully updated schema {Id} in Schema Manager", updated.Id);
            return updated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating schema {Id} in Schema Manager", id);
            throw;
        }
    }

    /// <summary>
    /// Deletes a schema
    /// </summary>
    public async Task<bool> DeleteSchemaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Deleting schema {Id} from Schema Manager", id);

            var response = await _httpClient.DeleteAsync($"/api/Schema/{id}", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogDebug("Schema {Id} not found for deletion", id);
                return false;
            }

            response.EnsureSuccessStatusCode();

            _logger.LogDebug("Successfully deleted schema {Id} from Schema Manager", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting schema {Id} from Schema Manager", id);
            throw;
        }
    }
}
