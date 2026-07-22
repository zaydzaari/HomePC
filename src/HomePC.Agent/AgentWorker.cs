using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using HomePC.Core;

namespace HomePC.Agent;

public sealed class AgentWorker(HomePcConfig config, CommandGuard guard, ActionRegistry actions, AuditWriter audit, ILogger<AgentWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var attempt = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ConnectAndRunAsync(stoppingToken); attempt = 0; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Cloud connection ended; reconnecting"); await audit.WriteAsync("connection_error", ex.GetType().Name, stoppingToken);
                var cap = Math.Min(60, 1 << Math.Min(attempt++, 6));
                await Task.Delay(TimeSpan.FromSeconds(cap) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)), stoppingToken);
            }
        }
    }

    private async Task ConnectAndRunAsync(CancellationToken token)
    {
        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Authorization", "Bearer " + config.DeviceToken);
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(config.HeartbeatSeconds);
        var ws = new Uri(new Uri(config.WorkerUrl), "/device/ws").ToString().Replace("https://", "wss://", StringComparison.OrdinalIgnoreCase);
        await socket.ConnectAsync(new Uri(ws), token);
        await SendAsync(socket, JsonSerializer.Serialize(new { type = "hello", agentVersion = "1.0.0", platform = "windows" }), token);
        await audit.WriteAsync("connected", "cloud websocket authenticated", token);
        var buffer = new byte[config.MaxMessageBytes];
        while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
        {
            using var message = new MemoryStream(); WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer, token);
                if (result.MessageType == WebSocketMessageType.Close) { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "closing", token); return; }
                if (message.Length + result.Count > config.MaxMessageBytes) { await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "message too large", token); return; }
                await message.WriteAsync(buffer.AsMemory(0, result.Count), token);
            } while (!result.EndOfMessage);
            if (result.MessageType != WebSocketMessageType.Text) continue;
            await HandleAsync(socket, Encoding.UTF8.GetString(message.ToArray()), token);
        }
    }

    private async Task HandleAsync(ClientWebSocket socket, string text, CancellationToken token)
    {
        CommandEnvelope? command;
        try { command = JsonSerializer.Deserialize<CommandEnvelope>(text, Json); }
        catch (JsonException) { await audit.WriteAsync("rejected", "malformed_json", token); return; }
        if (command is null || command.Type != "command") return;
        if (!guard.TryAccept(command, DateTimeOffset.UtcNow, out var action, out var error))
        {
            await audit.WriteAsync("rejected", error ?? "invalid", token);
            await SendAsync(socket, JsonSerializer.Serialize(new CommandResult("result", command.CommandId, false, error, new AgentState(0, false, null), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())), token); return;
        }
        logger.LogInformation("Executing allow-listed action {Action} ({CommandId})", action, command.CommandId);
        var result = await actions.ExecuteAsync(command, action, token);
        await audit.WriteAsync(result.Success ? "action_success" : "action_failure", $"{action}:{result.Error ?? "ok"}", token);
        await SendAsync(socket, JsonSerializer.Serialize(result), token);
    }

    private static Task SendAsync(ClientWebSocket socket, string value, CancellationToken token) =>
        socket.SendAsync(Encoding.UTF8.GetBytes(value), WebSocketMessageType.Text, true, token);
}

