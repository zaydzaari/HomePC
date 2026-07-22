using System.Text.Json;
using HomePC.Core;
using Xunit;

namespace HomePC.Core.Tests;

public sealed class CoreTests
{
    [Fact] public void ConfigurationRejectsInsecureWorkerAndBadMode()
    {
        var config = new HomePcConfig { WorkerUrl = "http://example.test", DeviceToken = "short", Modes = { ["bad"] = new() { Volume = 101, Launch = ["cmd"] } } };
        var issues = ConfigurationValidator.Validate(config);
        Assert.Contains(issues, x => x.Path == "workerUrl"); Assert.Contains(issues, x => x.Path == "deviceToken"); Assert.Contains(issues, x => x.Path == "modes.bad.volume");
    }

    [Fact] public void GuardRejectsExpiredUnknownReplayAndInvalidVolume()
    {
        var guard = new CommandGuard(2); var now = DateTimeOffset.UtcNow;
        Assert.False(guard.TryAccept(Command("x", "open_notepad", now.AddSeconds(-20), now.AddSeconds(-10)), now, out _, out var expired)); Assert.Equal("expired_command", expired);
        Assert.False(guard.TryAccept(Command("x", "run_shell", now, now.AddSeconds(5)), now, out _, out var unknown)); Assert.Equal("unknown_action", unknown);
        var valid = Command("same", "open_notepad", now, now.AddSeconds(5)); Assert.True(guard.TryAccept(valid, now, out _, out _));
        Assert.False(guard.TryAccept(valid, now, out _, out var duplicate)); Assert.Equal("duplicate_command", duplicate);
        Assert.False(guard.TryAccept(Command("v", "set_volume", now, now.AddSeconds(5), "{\"volume\":101}"), now, out _, out var volume)); Assert.Equal("invalid_volume", volume);
    }

    [Theory] [InlineData(-1, 0)] [InlineData(50, 50)] [InlineData(101, 100)]
    public void VolumeClampingContract(int supplied, int expected) => Assert.Equal(expected, Math.Clamp(supplied, 0, 100));

    [Fact] public async Task DisabledRestartAndShutdownNeverCallWindows()
    {
        var fake = new FakeWindows(); var registry = new ActionRegistry(fake, new HomePcConfig()); var now = DateTimeOffset.UtcNow;
        var restart = await registry.ExecuteAsync(Command("r", "restart_pc", now, now.AddSeconds(5)), ActionId.RestartPc, default);
        var shutdown = await registry.ExecuteAsync(Command("s", "shutdown_pc", now, now.AddSeconds(5)), ActionId.ShutdownPc, default);
        Assert.False(restart.Success); Assert.Equal("restart_disabled", restart.Error); Assert.False(shutdown.Success); Assert.Empty(fake.Calls);
    }

    [Fact] public async Task ModeParsingExecutesOnlyConfiguredAllowList()
    {
        var fake = new FakeWindows(); var config = new HomePcConfig { Modes = { ["gaming"] = new() { Launch = ["steam", "discord"], Volume = 55 } } };
        var result = await new ActionRegistry(fake, config).ExecuteAsync(Command("m", "gaming_mode", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(5)), ActionId.GamingMode, default);
        Assert.True(result.Success); Assert.Equal(["launch:steam", "launch:discord", "volume:55"], fake.Calls);
    }

    [Fact] public void MalformedJsonThrowsJsonException() => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CommandEnvelope>("{broken"));
    [Fact] public void ResultCreationHasExpectedEnvelope() => Assert.Equal("result", new CommandResult("result", "id", true, null, new(10, false, null), 1).Type);

    [Fact] public void RoutineValidationRejectsNestedAndUnknownActions()
    {
        var config = BaseConfig();
        config.Routines["bad-routine"] = new() { Name = "Bad", Steps = [new() { Action = "run_routine" }] };
        Assert.Contains(ConfigurationValidator.Validate(config), x => x.Path == "routines.bad-routine.steps[0].action");
    }

    [Fact] public async Task RoutineExecutesApprovedStepsInOrder()
    {
        var fake = new FakeWindows(); var config = BaseConfig();
        config.Routines["focus-mode"] = new()
        {
            Name = "Focus mode",
            Steps = [new() { Action = "open_notepad" }, new() { Action = "set_volume", Parameters = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("{\"volume\":20}")! }]
        };
        var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>("{\"routineId\":\"focus-mode\"}")!;
        var now = DateTimeOffset.UtcNow;
        var command = new CommandEnvelope("command", "routine", "run_routine", parameters, now.ToUnixTimeMilliseconds(), now.AddMinutes(1).ToUnixTimeMilliseconds());
        var result = await new ActionRegistry(fake, config).ExecuteAsync(command, ActionId.RunRoutine, default);
        Assert.True(result.Success); Assert.Equal(["launch:notepad", "volume:20"], fake.Calls);
    }

    [Fact] public void DpapiRoundTripProtectsSecretsForCurrentUser()
    {
        if (!OperatingSystem.IsWindows()) return;
        var protectedValue = ConfigurationSecurity.Protect("a-secret-value");
        Assert.StartsWith("dpapi:v1:", protectedValue); Assert.Equal("a-secret-value", ConfigurationSecurity.Unprotect(protectedValue));
    }

    private static HomePcConfig BaseConfig() => new() { WorkerUrl = "https://example.workers.dev", DeviceToken = new('d', 32), AdminToken = new('a', 32) };

    private static CommandEnvelope Command(string id, string action, DateTimeOffset issued, DateTimeOffset expires, string parameters = "{}") =>
        new("command", id, action, JsonSerializer.Deserialize<Dictionary<string, object>>(parameters)!, issued.ToUnixTimeMilliseconds(), expires.ToUnixTimeMilliseconds());

    private sealed class FakeWindows : IWindowsController
    {
        public List<string> Calls { get; } = []; public AgentState State { get; private set; } = new(50, false, null);
        public IReadOnlyDictionary<string, string?> DetectedApplications => new Dictionary<string, string?>();
        public Task LaunchAsync(string app, IReadOnlyList<string> arguments, CancellationToken c) { Calls.Add("launch:" + app); return Task.CompletedTask; }
        public Task LockAsync(CancellationToken c) { Calls.Add("lock"); return Task.CompletedTask; }
        public Task SleepAsync(CancellationToken c) { Calls.Add("sleep"); return Task.CompletedTask; }
        public Task RestartAsync(CancellationToken c) { Calls.Add("restart"); return Task.CompletedTask; }
        public Task ShutdownAsync(CancellationToken c) { Calls.Add("shutdown"); return Task.CompletedTask; }
        public Task SetVolumeAsync(int volume, CancellationToken c) { Calls.Add("volume:" + volume); State = State with { Volume = volume }; return Task.CompletedTask; }
        public Task SetMutedAsync(bool muted, CancellationToken c) { Calls.Add("mute:" + muted); return Task.CompletedTask; }
        public Task MediaKeyAsync(ActionId action, CancellationToken c) { Calls.Add("media:" + action); return Task.CompletedTask; }
        public Task MonitorOffAsync(CancellationToken c) { Calls.Add("monitor"); return Task.CompletedTask; }
        public Task SetPowerPlanAsync(string plan, CancellationToken c) { Calls.Add("plan:" + plan); return Task.CompletedTask; }
        public Task CloseAllowListedAsync(string app, CancellationToken c) { Calls.Add("close:" + app); return Task.CompletedTask; }
    }
}
