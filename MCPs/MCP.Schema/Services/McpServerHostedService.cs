using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.MCP.Configuration;
using Shared.MCP.Interfaces;
using Shared.MCP.Models;
using Shared.MCP.Transport;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MCP.Schema.Services;

/// <summary>
/// Hosted service that runs the MCP server with STDIO transport
/// </summary>
public class McpServerHostedService : IHostedService
{
    private readonly ILogger<McpServerHostedService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly McpServerOptions _options;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public McpServerHostedService(
        ILogger<McpServerHostedService> logger,
        IServiceProvider serviceProvider,
        IOptions<McpServerOptions> options)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _cancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// Starts the MCP server
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting MCP Server with STDIO transport");

        try
        {
            // Wire up transport to MCP server
            var transport = _serviceProvider.GetRequiredService<IMcpTransport>();
            var mcpServer = _serviceProvider.GetRequiredService<IMcpServer>();

            if (transport is StdioTransport stdioTransport)
            {
                // Connect transport MessageReceived event to MCP server
                stdioTransport.MessageReceived += async (message) =>
                {
                    try
                    {
                        _logger.LogDebug("Processing incoming MCP message: {MessageLength} characters", message.Length);

                        // Check if this is a notification (no id field) or a request (has id field)
                        using var doc = System.Text.Json.JsonDocument.Parse(message);
                        var hasId = doc.RootElement.TryGetProperty("id", out _);

                        if (hasId)
                        {
                            // This is a request - deserialize and process
                            var request = System.Text.Json.JsonSerializer.Deserialize<McpRequest>(message);
                            if (request != null)
                            {
                                _logger.LogInformation("Processing MCP request: {Method}", request.Method);
                                var response = await mcpServer.ProcessRequestAsync(request, _cancellationTokenSource.Token);

                                // Serialize with options to ignore null values (JSON-RPC spec compliance)
                                var jsonOptions = new System.Text.Json.JsonSerializerOptions
                                {
                                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                                };
                                var responseJson = System.Text.Json.JsonSerializer.Serialize(response, jsonOptions);

                                _logger.LogInformation("Sending MCP response: {ResponseLength} characters", responseJson.Length);

                                // Send response back through STDIO
                                await stdioTransport.SendMessageAsync(responseJson, _cancellationTokenSource.Token);
                            }
                        }
                        else
                        {
                            // This is a notification - deserialize and handle (no response)
                            var notification = System.Text.Json.JsonSerializer.Deserialize<McpNotification>(message);
                            if (notification != null)
                            {
                                _logger.LogInformation("Handling MCP notification: {Method}", notification.Method);
                                await mcpServer.HandleNotificationAsync(notification, _cancellationTokenSource.Token);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing MCP message: {Message}", message);

                        // Try to extract the id from the malformed request
                        object? requestId = null;
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(message);
                            if (doc.RootElement.TryGetProperty("id", out var idElement))
                            {
                                requestId = idElement.ValueKind switch
                                {
                                    System.Text.Json.JsonValueKind.Number => idElement.GetInt32(),
                                    System.Text.Json.JsonValueKind.String => idElement.GetString(),
                                    _ => null
                                };
                            }
                        }
                        catch
                        {
                            // If we can't parse the message at all, id remains null
                        }

                        // Send an error response - don't use WhenWritingNull because id must always be present
                        var errorResponse = new
                        {
                            jsonrpc = "2.0",
                            id = requestId,
                            error = new
                            {
                                code = -32603,
                                message = ex.Message
                            }
                        };

                        var jsonOptions = new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                        };

                        var errorJson = System.Text.Json.JsonSerializer.Serialize(errorResponse, jsonOptions);

                        await stdioTransport.SendMessageAsync(errorJson, _cancellationTokenSource.Token);
                    }
                };
            }

            // Start the transport
            await transport.StartAsync(cancellationToken);

            _logger.LogInformation("MCP Server started successfully with STDIO transport");
            _logger.LogInformation("Ready to receive JSON-RPC messages on stdin");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start MCP Server");
            throw;
        }
    }

    /// <summary>
    /// Stops the MCP server
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping MCP Server");

        try
        {
            _cancellationTokenSource.Cancel();

            var transport = _serviceProvider.GetRequiredService<IMcpTransport>();
            await transport.StopAsync(cancellationToken);

            _logger.LogInformation("MCP Server stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping MCP Server");
        }
        finally
        {
            _cancellationTokenSource.Dispose();
        }
    }
}
