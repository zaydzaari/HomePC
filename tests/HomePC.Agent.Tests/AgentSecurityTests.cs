using System.Text.Json;
using HomePC.Core;
using Xunit;

namespace HomePC.Agent.Tests;

public sealed class AgentSecurityTests
{
    [Fact] public void EveryCloudActionMapsToFixedEnum()
    {
        var actions = new[] { "open_notepad", "open_steam", "open_discord", "open_chrome", "gaming_mode", "study_mode", "movie_mode", "lock_pc", "sleep_pc", "restart_pc", "shutdown_pc", "set_volume", "mute", "play_pause", "next_track", "previous_track", "monitor_off" };
        var guard = new CommandGuard();
        foreach (var action in actions)
        {
            var parameters = action == "set_volume" ? "{\"volume\":50}" : action == "mute" ? "{\"muted\":true}" : "{}";
            var command = new CommandEnvelope("command", Guid.NewGuid().ToString(), action, JsonSerializer.Deserialize<Dictionary<string, object>>(parameters)!, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), DateTimeOffset.UtcNow.AddSeconds(5).ToUnixTimeMilliseconds());
            Assert.True(guard.TryAccept(command, DateTimeOffset.UtcNow, out _, out _));
        }
    }

    [Fact] public void ExtraParametersAreRejected()
    {
        var command = new CommandEnvelope("command", "id", "open_notepad", JsonSerializer.Deserialize<Dictionary<string, object>>("{\"path\":\"cmd.exe\"}")!, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), DateTimeOffset.UtcNow.AddSeconds(5).ToUnixTimeMilliseconds());
        Assert.False(new CommandGuard().TryAccept(command, DateTimeOffset.UtcNow, out _, out var error)); Assert.Equal("unexpected_parameters", error);
    }
}
