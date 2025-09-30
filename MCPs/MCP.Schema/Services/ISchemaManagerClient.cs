using System.Text.Json.Serialization;

namespace MCP.Schema.Services;

/// <summary>
/// Interface for communicating with the Schema Manager via HTTP
/// </summary>
public interface ISchemaManagerClient
{
    /// <summary>
    /// Gets all schemas from the Schema Manager
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of schema entities</returns>
    Task<List<SchemaEntityDto>> GetAllSchemasAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific schema by ID
    /// </summary>
    /// <param name="id">Schema ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Schema entity or null if not found</returns>
    Task<SchemaEntityDto?> GetSchemaByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a schema by composite key (version + name)
    /// </summary>
    /// <param name="compositeKey">Composite key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Schema entity or null if not found</returns>
    Task<SchemaEntityDto?> GetSchemaByCompositeKeyAsync(string compositeKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets schemas by version
    /// </summary>
    /// <param name="version">Version</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of schema entities</returns>
    Task<List<SchemaEntityDto>> GetSchemasByVersionAsync(string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets schemas by name
    /// </summary>
    /// <param name="name">Schema name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of schema entities</returns>
    Task<List<SchemaEntityDto>> GetSchemasByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a schema definition
    /// </summary>
    /// <param name="definition">Schema definition to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result</returns>
    Task<SchemaValidationResult> ValidateSchemaAsync(string definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes breaking changes between two schema versions
    /// </summary>
    /// <param name="oldDefinition">Old schema definition</param>
    /// <param name="newDefinition">New schema definition</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Breaking change analysis result</returns>
    Task<BreakingChangeAnalysisResult> AnalyzeBreakingChangesAsync(string oldDefinition, string newDefinition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets paged schemas from the Schema Manager
    /// </summary>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Page size (1-100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paged schema result</returns>
    Task<PagedSchemaResult> GetPagedSchemasAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets schemas by definition
    /// </summary>
    /// <param name="definition">Schema definition</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of schema entities</returns>
    Task<List<SchemaEntityDto>> GetSchemasByDefinitionAsync(string definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a schema exists by ID
    /// </summary>
    /// <param name="id">Schema ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if schema exists</returns>
    Task<bool> SchemaExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new schema
    /// </summary>
    /// <param name="schema">Schema entity to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created schema entity</returns>
    Task<SchemaEntityDto> CreateSchemaAsync(SchemaEntityDto schema, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing schema
    /// </summary>
    /// <param name="id">Schema ID</param>
    /// <param name="schema">Schema entity with updated data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated schema entity</returns>
    Task<SchemaEntityDto> UpdateSchemaAsync(Guid id, SchemaEntityDto schema, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a schema
    /// </summary>
    /// <param name="id">Schema ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> DeleteSchemaAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// DTO for schema entity data
/// </summary>
public class SchemaEntityDto
{
    public Guid Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Definition { get; set; }
    public string? SchemaType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CompositeKey => $"{Version}_{Name}";
}

/// <summary>
/// Schema validation result
/// </summary>
public class SchemaValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Breaking change analysis result
/// </summary>
public class BreakingChangeAnalysisResult
{
    public bool HasBreakingChanges { get; set; }
    public List<string> BreakingChanges { get; set; } = new();
    public List<string> NonBreakingChanges { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Paged schema result
/// </summary>
public class PagedSchemaResult
{
    public List<SchemaEntityDto> Data { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
