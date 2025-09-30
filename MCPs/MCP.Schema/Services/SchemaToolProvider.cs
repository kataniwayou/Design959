using Microsoft.Extensions.Logging;
using Shared.MCP.Interfaces;
using Shared.MCP.Models;
using System.Text.Json;

namespace MCP.Schema.Services;

/// <summary>
/// Provides MCP tools for schema management operations
/// </summary>
public class SchemaToolProvider : IMcpToolProvider
{
    private readonly ISchemaManagerClient _schemaClient;
    private readonly ILogger<SchemaToolProvider> _logger;

    public SchemaToolProvider(ISchemaManagerClient schemaClient, ILogger<SchemaToolProvider> logger)
    {
        _schemaClient = schemaClient;
        _logger = logger;
    }

    /// <summary>
    /// Lists all available schema tools
    /// </summary>
    public Task<ListToolsResponse> ListToolsAsync(ListToolsRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Listing schema tools");

        var tools = new List<McpTool>
        {
            new McpTool
            {
                Name = "get-schema",
                Description = "Retrieve a specific schema by ID or composite key",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        id = new { type = "string", description = "Schema ID (GUID)" },
                        composite_key = new { type = "string", description = "Composite key (version_name)" }
                    },
                    oneOf = new[]
                    {
                        new { required = new[] { "id" } },
                        new { required = new[] { "composite_key" } }
                    }
                }
            },
            new McpTool
            {
                Name = "list-schemas",
                Description = "List schemas with optional filtering",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        version = new { type = "string", description = "Filter by version" },
                        name = new { type = "string", description = "Filter by name" },
                        limit = new { type = "integer", description = "Maximum number of results", minimum = 1, maximum = 100 }
                    }
                }
            },
            new McpTool
            {
                Name = "validate-schema",
                Description = "Validate a schema definition",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        definition = new { type = "string", description = "Schema definition to validate" }
                    },
                    required = new[] { "definition" }
                }
            },
            new McpTool
            {
                Name = "analyze-breaking-changes",
                Description = "Analyze breaking changes between two schema versions",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        old_definition = new { type = "string", description = "Old schema definition" },
                        new_definition = new { type = "string", description = "New schema definition" }
                    },
                    required = new[] { "old_definition", "new_definition" }
                }
            },
            new McpTool
            {
                Name = "search-schemas",
                Description = "Search schemas by various criteria",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "Search query" },
                        version = new { type = "string", description = "Filter by version" },
                        schema_type = new { type = "string", description = "Filter by schema type" }
                    },
                    required = new[] { "query" }
                }
            },
            new McpTool
            {
                Name = "get-paged-schemas",
                Description = "Get paginated list of schemas with metadata",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        page = new { type = "integer", description = "Page number (1-based)", minimum = 1 },
                        page_size = new { type = "integer", description = "Page size (1-100)", minimum = 1, maximum = 100 }
                    }
                }
            },
            new McpTool
            {
                Name = "get-schemas-by-definition",
                Description = "Find all schemas that match a specific definition",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        definition = new { type = "string", description = "Schema definition to search for" }
                    },
                    required = new[] { "definition" }
                }
            },
            new McpTool
            {
                Name = "check-schema-exists",
                Description = "Check if a schema exists by ID",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        id = new { type = "string", description = "Schema ID (GUID)" }
                    },
                    required = new[] { "id" }
                }
            },
            new McpTool
            {
                Name = "create-schema",
                Description = "Create a new schema entity",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        version = new { type = "string", description = "Schema version" },
                        name = new { type = "string", description = "Schema name" },
                        description = new { type = "string", description = "Schema description (optional)" },
                        definition = new { type = "string", description = "JSON schema definition" },
                        schema_type = new { type = "string", description = "Schema type (optional)" }
                    },
                    required = new[] { "version", "name", "definition" }
                }
            },
            new McpTool
            {
                Name = "update-schema",
                Description = "Update an existing schema (validates for breaking changes and references)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        id = new { type = "string", description = "Schema ID (GUID)" },
                        version = new { type = "string", description = "Schema version" },
                        name = new { type = "string", description = "Schema name" },
                        description = new { type = "string", description = "Schema description (optional)" },
                        definition = new { type = "string", description = "JSON schema definition" },
                        schema_type = new { type = "string", description = "Schema type (optional)" }
                    },
                    required = new[] { "id", "version", "name", "definition" }
                }
            },
            new McpTool
            {
                Name = "delete-schema",
                Description = "Delete a schema (validates for references before deletion)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        id = new { type = "string", description = "Schema ID (GUID)" }
                    },
                    required = new[] { "id" }
                }
            }
        };

        _logger.LogDebug("Listed {Count} schema tools", tools.Count);

        return Task.FromResult(new ListToolsResponse
        {
            Tools = tools
        });
    }

    /// <summary>
    /// Calls a specific schema tool
    /// </summary>
    public async Task<CallToolResponse> CallToolAsync(CallToolRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Calling schema tool: {Name}", request.Name);

            var content = new List<McpToolContent>();

            switch (request.Name)
            {
                case "get-schema":
                    content = await ExecuteGetSchemaTool(request.Arguments, cancellationToken);
                    break;
                case "list-schemas":
                    content = await ExecuteListSchemasTool(request.Arguments, cancellationToken);
                    break;
                case "validate-schema":
                    content = await ExecuteValidateSchemaTool(request.Arguments, cancellationToken);
                    break;
                case "analyze-breaking-changes":
                    content = await ExecuteAnalyzeBreakingChangesTool(request.Arguments, cancellationToken);
                    break;
                case "search-schemas":
                    content = await ExecuteSearchSchemasTool(request.Arguments, cancellationToken);
                    break;
                case "get-paged-schemas":
                    content = await ExecuteGetPagedSchemasTool(request.Arguments, cancellationToken);
                    break;
                case "get-schemas-by-definition":
                    content = await ExecuteGetSchemasByDefinitionTool(request.Arguments, cancellationToken);
                    break;
                case "check-schema-exists":
                    content = await ExecuteCheckSchemaExistsTool(request.Arguments, cancellationToken);
                    break;
                case "create-schema":
                    content = await ExecuteCreateSchemaTool(request.Arguments, cancellationToken);
                    break;
                case "update-schema":
                    content = await ExecuteUpdateSchemaTool(request.Arguments, cancellationToken);
                    break;
                case "delete-schema":
                    content = await ExecuteDeleteSchemaTool(request.Arguments, cancellationToken);
                    break;
                default:
                    throw new ArgumentException($"Unknown tool: {request.Name}");
            }

            return new CallToolResponse
            {
                Content = content,
                IsError = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling schema tool: {Name}", request.Name);
            
            return new CallToolResponse
            {
                Content = new List<McpToolContent>
                {
                    new McpToolContent
                    {
                        Type = "text",
                        Text = $"Error: {ex.Message}"
                    }
                },
                IsError = true
            };
        }
    }

    /// <summary>
    /// Checks if a tool exists
    /// </summary>
    public Task<bool> ToolExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        var validTools = new[] {
            "get-schema", "list-schemas", "validate-schema", "analyze-breaking-changes", "search-schemas",
            "get-paged-schemas", "get-schemas-by-definition", "check-schema-exists",
            "create-schema", "update-schema", "delete-schema"
        };
        return Task.FromResult(validTools.Contains(name));
    }

    private async Task<List<McpToolContent>> ExecuteGetSchemaTool(Dictionary<string, object>? arguments, CancellationToken cancellationToken)
    {
        var id = arguments?.GetValueOrDefault("id")?.ToString();
        var compositeKey = arguments?.GetValueOrDefault("composite_key")?.ToString();

        SchemaEntityDto? schema = null;

        if (!string.IsNullOrEmpty(id) && Guid.TryParse(id, out var schemaId))
        {
            schema = await _schemaClient.GetSchemaByIdAsync(schemaId, cancellationToken);
        }
        else if (!string.IsNullOrEmpty(compositeKey))
        {
            schema = await _schemaClient.GetSchemaByCompositeKeyAsync(compositeKey, cancellationToken);
        }

        if (schema == null)
        {
            return new List<McpToolContent>
            {
                new McpToolContent
                {
                    Type = "text",
                    Text = "Schema not found"
                }
            };
        }

        return new List<McpToolContent>
        {
            new McpToolContent
            {
                Type = "text",
                Text = JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true })
            }
        };
    }

    private async Task<List<McpToolContent>> ExecuteListSchemasTool(Dictionary<string, object>? arguments, CancellationToken cancellationToken)
    {
        var version = arguments?.GetValueOrDefault("version")?.ToString();
        var name = arguments?.GetValueOrDefault("name")?.ToString();
        var limitStr = arguments?.GetValueOrDefault("limit")?.ToString();
        var limit = int.TryParse(limitStr, out var l) ? l : 50;

        List<SchemaEntityDto> schemas;

        if (!string.IsNullOrEmpty(version))
        {
            schemas = await _schemaClient.GetSchemasByVersionAsync(version, cancellationToken);
        }
        else if (!string.IsNullOrEmpty(name))
        {
            schemas = await _schemaClient.GetSchemasByNameAsync(name, cancellationToken);
        }
        else
        {
            schemas = await _schemaClient.GetAllSchemasAsync(cancellationToken);
        }

        // Apply limit
        schemas = schemas.Take(limit).ToList();

        return new List<McpToolContent>
        {
            new McpToolContent
            {
                Type = "text",
                Text = JsonSerializer.Serialize(schemas, new JsonSerializerOptions { WriteIndented = true })
            }
        };
    }

    private async Task<List<McpToolContent>> ExecuteValidateSchemaTool(Dictionary<string, object>? arguments, CancellationToken cancellationToken)
    {
        var definition = arguments?.GetValueOrDefault("definition")?.ToString();

        if (string.IsNullOrEmpty(definition))
        {
            throw new ArgumentException("Schema definition is required");
        }

        var result = await _schemaClient.ValidateSchemaAsync(definition, cancellationToken);

        return new List<McpToolContent>
        {
            new McpToolContent
            {
                Type = "text",
                Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
            }
        };
    }

    private async Task<List<McpToolContent>> ExecuteAnalyzeBreakingChangesTool(Dictionary<string, object>? arguments, CancellationToken cancellationToken)
    {
        var oldDefinition = arguments?.GetValueOrDefault("old_definition")?.ToString();
        var newDefinition = arguments?.GetValueOrDefault("new_definition")?.ToString();

        if (string.IsNullOrEmpty(oldDefinition) || string.IsNullOrEmpty(newDefinition))
        {
            throw new ArgumentException("Both old_definition and new_definition are required");
        }

        var result = await _schemaClient.AnalyzeBreakingChangesAsync(oldDefinition, newDefinition, cancellationToken);

        return new List<McpToolContent>
        {
            new McpToolContent
            {
                Type = "text",
                Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
            }
        };
    }

    private async Task<List<McpToolContent>> ExecuteSearchSchemasTool(Dictionary<string, object>? arguments, CancellationToken cancellationToken)
    {
        var query = arguments?.GetValueOrDefault("query")?.ToString();
        var version = arguments?.GetValueOrDefault("version")?.ToString();
        var schemaType = arguments?.GetValueOrDefault("schema_type")?.ToString();

        if (string.IsNullOrEmpty(query))
        {
            throw new ArgumentException("Search query is required");
        }

        // Get all schemas and filter based on criteria
        var allSchemas = await _schemaClient.GetAllSchemasAsync(cancellationToken);

        var filteredSchemas = allSchemas.Where(s =>
            (string.IsNullOrEmpty(version) || s.Version.Contains(query, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(schemaType) || (s.SchemaType?.Contains(schemaType, StringComparison.OrdinalIgnoreCase) ?? false)) &&
            (s.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             (s.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
             (s.Definition?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
        ).ToList();

        return new List<McpToolContent>
        {
            new McpToolContent
            {
                Type = "text",
                Text = JsonSerializer.Serialize(filteredSchemas, new JsonSerializerOptions { WriteIndented = true })
            }
        };
    }

    private async Task<List<McpToolContent>> ExecuteGetPagedSchemasTool(Dictionary<string, object>? arguments, CancellationToken cancellationToken)
    {
        var pageStr = arguments?.GetValueOrDefault("page")?.ToString();
        var pageSizeStr = arguments?.GetValueOrDefault("page_size")?.ToString();

        var page = int.TryParse(pageStr, out var p) ? p : 1;
        var pageSize = int.TryParse(pageSizeStr, out var ps) ? ps : 10;

        var result = await _schemaClient.GetPagedSchemasAsync(page, pageSize, cancellationToken);

        return new List<McpToolContent>
        {
            new McpToolContent
            {
                Type = "text",
                Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
            }
        };
    }

    private async Task<List<McpToolContent>> ExecuteGetSchemasByDefinitionTool(Dictionary<string, object>? arguments, CancellationToken cancellationToken)
    {
        var definition = arguments?.GetValueOrDefault("definition")?.ToString();

        if (string.IsNullOrEmpty(definition))
        {
            throw new ArgumentException("Definition is required");
        }

        var schemas = await _schemaClient.GetSchemasByDefinitionAsync(definition, cancellationToken);

        return new List<McpToolContent>
        {
            new McpToolContent
            {
                Type = "text",
                Text = JsonSerializer.Serialize(schemas, new JsonSerializerOptions { WriteIndented = true })
            }
        };
    }

    private async Task<List<McpToolContent>> ExecuteCheckSchemaExistsTool(Dictionary<string, object>? arguments, CancellationToken cancellationToken)
    {
        var id = arguments?.GetValueOrDefault("id")?.ToString();

        if (string.IsNullOrEmpty(id) || !Guid.TryParse(id, out var schemaId))
        {
            throw new ArgumentException("Valid schema ID (GUID) is required");
        }

        var exists = await _schemaClient.SchemaExistsAsync(schemaId, cancellationToken);

        return new List<McpToolContent>
        {
            new McpToolContent
            {
                Type = "text",
                Text = JsonSerializer.Serialize(new { exists }, new JsonSerializerOptions { WriteIndented = true })
            }
        };
    }

    private async Task<List<McpToolContent>> ExecuteCreateSchemaTool(Dictionary<string, object>? arguments, CancellationToken cancellationToken)
    {
        var version = arguments?.GetValueOrDefault("version")?.ToString();
        var name = arguments?.GetValueOrDefault("name")?.ToString();
        var description = arguments?.GetValueOrDefault("description")?.ToString();
        var definition = arguments?.GetValueOrDefault("definition")?.ToString();
        var schemaType = arguments?.GetValueOrDefault("schema_type")?.ToString();

        if (string.IsNullOrEmpty(version) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(definition))
        {
            throw new ArgumentException("Version, name, and definition are required");
        }

        var schema = new SchemaEntityDto
        {
            Version = version,
            Name = name,
            Description = description,
            Definition = definition,
            SchemaType = schemaType
        };

        var created = await _schemaClient.CreateSchemaAsync(schema, cancellationToken);

        return new List<McpToolContent>
        {
            new McpToolContent
            {
                Type = "text",
                Text = JsonSerializer.Serialize(created, new JsonSerializerOptions { WriteIndented = true })
            }
        };
    }

    private async Task<List<McpToolContent>> ExecuteUpdateSchemaTool(Dictionary<string, object>? arguments, CancellationToken cancellationToken)
    {
        var id = arguments?.GetValueOrDefault("id")?.ToString();
        var version = arguments?.GetValueOrDefault("version")?.ToString();
        var name = arguments?.GetValueOrDefault("name")?.ToString();
        var description = arguments?.GetValueOrDefault("description")?.ToString();
        var definition = arguments?.GetValueOrDefault("definition")?.ToString();
        var schemaType = arguments?.GetValueOrDefault("schema_type")?.ToString();

        if (string.IsNullOrEmpty(id) || !Guid.TryParse(id, out var schemaId))
        {
            throw new ArgumentException("Valid schema ID (GUID) is required");
        }

        if (string.IsNullOrEmpty(version) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(definition))
        {
            throw new ArgumentException("Version, name, and definition are required");
        }

        var schema = new SchemaEntityDto
        {
            Id = schemaId,
            Version = version,
            Name = name,
            Description = description,
            Definition = definition,
            SchemaType = schemaType
        };

        var updated = await _schemaClient.UpdateSchemaAsync(schemaId, schema, cancellationToken);

        return new List<McpToolContent>
        {
            new McpToolContent
            {
                Type = "text",
                Text = JsonSerializer.Serialize(updated, new JsonSerializerOptions { WriteIndented = true })
            }
        };
    }

    private async Task<List<McpToolContent>> ExecuteDeleteSchemaTool(Dictionary<string, object>? arguments, CancellationToken cancellationToken)
    {
        var id = arguments?.GetValueOrDefault("id")?.ToString();

        if (string.IsNullOrEmpty(id) || !Guid.TryParse(id, out var schemaId))
        {
            throw new ArgumentException("Valid schema ID (GUID) is required");
        }

        var success = await _schemaClient.DeleteSchemaAsync(schemaId, cancellationToken);

        return new List<McpToolContent>
        {
            new McpToolContent
            {
                Type = "text",
                Text = JsonSerializer.Serialize(new { success, message = success ? "Schema deleted successfully" : "Schema not found or could not be deleted" }, new JsonSerializerOptions { WriteIndented = true })
            }
        };
    }
}
