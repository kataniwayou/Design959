using Microsoft.Extensions.Logging;
using Shared.MCP.Interfaces;
using System.Text;
using System.Text.Json;

namespace Shared.MCP.Transport;

/// <summary>
/// STDIO transport for MCP communication using stdin/stdout
/// </summary>
public class StdioTransport : IMcpTransport
{
    private readonly ILogger<StdioTransport> _logger;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _readTask;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public StdioTransport(ILogger<StdioTransport> logger)
    {
        _logger = logger;
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
        _logger.LogInformation("Starting STDIO transport");
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        // Start reading from stdin
        _readTask = Task.Run(() => ReadFromStdinAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the transport layer
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping STDIO transport");
        
        _cancellationTokenSource?.Cancel();
        
        if (_readTask != null)
        {
            try
            {
                await _readTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
        }
        
        _cancellationTokenSource?.Dispose();
    }

    /// <summary>
    /// Sends a message through the transport
    /// </summary>
    public async Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            // Write message to stdout as a single line (JSON-RPC over STDIO)
            await Console.Out.WriteLineAsync(message);
            await Console.Out.FlushAsync();
            
            _logger.LogDebug("Sent message to stdout: {MessageLength} characters", message.Length);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Reads messages from stdin
    /// </summary>
    private async Task ReadFromStdinAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Started reading from stdin");
        
        try
        {
            using var reader = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
            
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                
                if (line == null)
                {
                    // EOF reached
                    _logger.LogInformation("EOF reached on stdin, stopping transport");
                    break;
                }
                
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                
                _logger.LogDebug("Received message from stdin: {MessageLength} characters", line.Length);
                
                // Raise the MessageReceived event
                if (MessageReceived != null)
                {
                    await MessageReceived(line);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Stdin reading cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading from stdin");
        }
    }
}

