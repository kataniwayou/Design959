using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Shared.MCP.Interfaces;
using System.Text;
using System.Threading.Channels;

namespace Shared.MCP.Transport;

/// <summary>
/// HTTP Server-Sent Events transport for MCP communication
/// </summary>
public class HttpSseTransport : IMcpTransport
{
    private readonly ILogger<HttpSseTransport> _logger;
    private readonly Channel<string> _messageChannel;
    private readonly ChannelWriter<string> _messageWriter;
    private readonly ChannelReader<string> _messageReader;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _processingTask;

    public HttpSseTransport(ILogger<HttpSseTransport> logger)
    {
        _logger = logger;
        var channel = Channel.CreateUnbounded<string>();
        _messageChannel = channel;
        _messageWriter = channel.Writer;
        _messageReader = channel.Reader;
    }

    /// <summary>
    /// Event raised when a message is received
    /// </summary>
    public event Func<string, Task>? MessageReceived;

    /// <summary>
    /// Starts the transport layer
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting HTTP SSE transport");
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _processingTask = ProcessMessagesAsync(_cancellationTokenSource.Token);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the transport layer
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping HTTP SSE transport");
        
        _cancellationTokenSource?.Cancel();
        _messageWriter.Complete();

        if (_processingTask != null)
        {
            try
            {
                await _processingTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
        }

        _cancellationTokenSource?.Dispose();
    }

    /// <summary>
    /// Sends a message through the transport
    /// </summary>
    public async Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        try
        {
            await _messageWriter.WriteAsync(message, cancellationToken);
            _logger.LogDebug("Message queued for sending: {MessageLength} characters", message.Length);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to queue message - channel may be closed");
            throw;
        }
    }

    /// <summary>
    /// Handles incoming HTTP request for SSE connection
    /// </summary>
    public async Task HandleSseConnectionAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        // Disable response buffering for SSE
        var bufferingFeature = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>();
        bufferingFeature?.DisableBuffering();

        context.Response.Headers["Content-Type"] = "text/event-stream";
        context.Response.Headers["Cache-Control"] = "no-cache";
        context.Response.Headers["Connection"] = "keep-alive";
        context.Response.Headers["Access-Control-Allow-Origin"] = "*";
        context.Response.Headers["Access-Control-Allow-Headers"] = "Cache-Control";

        _logger.LogInformation("SSE connection established from {RemoteIpAddress}", context.Connection.RemoteIpAddress);

        try
        {
            // For backwards compatibility with old HTTP+SSE transport (protocol version 2024-11-05)
            // Send an 'endpoint' event telling the client where to POST messages
            var scheme = context.Request.Scheme;
            var host = context.Request.Host.ToString();
            var basePath = context.Request.Path.Value?.Replace("/sse", "") ?? "/mcp";
            var messageEndpoint = $"{scheme}://{host}{basePath}/message";

            await SendSseEventAsync(context.Response, "endpoint", messageEndpoint, cancellationToken);
            _logger.LogInformation("Sent endpoint event: {Endpoint}", messageEndpoint);

            _logger.LogDebug("SSE connection ready, waiting for messages");

            // Keep the connection alive and process outgoing messages
            // Use a task that completes when cancellation is requested
            var keepAliveTask = Task.Delay(Timeout.Infinite, cancellationToken);
            var messageTask = Task.Run(async () =>
            {
                await foreach (var message in _messageReader.ReadAllAsync(cancellationToken))
                {
                    await SendSseEventAsync(context.Response, "message", message, cancellationToken);
                    _logger.LogDebug("Sent SSE message: {MessageLength} characters", message.Length);
                }
            }, cancellationToken);

            // Wait for either cancellation or message processing to complete
            await Task.WhenAny(keepAliveTask, messageTask);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SSE connection cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SSE connection");
        }
        finally
        {
            _logger.LogInformation("SSE connection closed");
        }
    }

    /// <summary>
    /// Event raised when a message is received and needs a response
    /// </summary>
    public event Func<string, Task<string>>? MessageReceivedWithResponse;

    /// <summary>
    /// Handles incoming HTTP POST request with MCP message
    /// </summary>
    public async Task HandleHttpPostAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Received POST request. ContentLength: {ContentLength}, ContentType: {ContentType}",
                context.Request.ContentLength, context.Request.ContentType);

            // Read the request body
            string message;
            using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: false))
            {
                message = await reader.ReadToEndAsync(cancellationToken);
            }

            _logger.LogInformation("Received HTTP message: {MessageLength} characters, Content: {Message}",
                message.Length, message);

            // Try the new response-based event first
            if (MessageReceivedWithResponse != null)
            {
                var response = await MessageReceivedWithResponse(message);

                _logger.LogInformation("Sending response directly via POST: {ResponseLength} characters", response.Length);

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync(response, cancellationToken);
            }
            // Fall back to old event-based approach
            else if (MessageReceived != null)
            {
                await MessageReceived(message);

                context.Response.StatusCode = 200;
                await context.Response.WriteAsync("OK", cancellationToken);
            }
            else
            {
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync("OK", cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling HTTP POST request");

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("Internal Server Error", cancellationToken);
            }
        }
    }

    private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Started message processing task");
        
        try
        {
            await foreach (var message in _messageReader.ReadAllAsync(cancellationToken))
            {
                _logger.LogDebug("Processing outgoing message: {MessageLength} characters", message.Length);
                // Messages are handled by the SSE connection handler
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Message processing cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in message processing");
        }
    }

    private static async Task SendSseEventAsync(HttpResponse response, string eventType, string data, CancellationToken cancellationToken)
    {
        var sseData = $"event: {eventType}\ndata: {data}\n\n";
        var bytes = Encoding.UTF8.GetBytes(sseData);
        await response.Body.WriteAsync(bytes, cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}
