using System.Text.Json;

namespace HomePC.Core;

public interface IWindowsController
{
    Task LaunchAsync(string app, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
    Task LockAsync(CancellationToken cancellationToken);
    Task SleepAsync(CancellationToken cancellationToken);
    Task RestartAsync(CancellationToken cancellationToken);
    Task ShutdownAsync(CancellationToken cancellationToken);
    Task SetVolumeAsync(int volume, CancellationToken cancellationToken);
    Task SetMutedAsync(bool muted, CancellationToken cancellationToken);
    Task MediaKeyAsync(ActionId action, CancellationToken cancellationToken);
    Task MonitorOffAsync(CancellationToken cancellationToken);
    Task SetPowerPlanAsync(string plan, CancellationToken cancellationToken);
    Task CloseAllowListedAsync(string app, CancellationToken cancellationToken);
    AgentState State { get; }
    IReadOnlyDictionary<string, string?> DetectedApplications { get; }
}

public sealed class ActionRegistry(IWindowsController windows, HomePcConfig config)
{
    public async Task<CommandResult> ExecuteAsync(CommandEnvelope command, ActionId action, CancellationToken cancellationToken)
    {
        try
        {
            switch (action)
            {
                case ActionId.OpenNotepad: await windows.LaunchAsync("notepad", [], cancellationToken); break;
                case ActionId.OpenSteam: await windows.LaunchAsync("steam", [], cancellationToken); break;
                case ActionId.OpenDiscord: await windows.LaunchAsync("discord", [], cancellationToken); break;
                case ActionId.OpenChrome: await windows.LaunchAsync("chrome", [], cancellationToken); break;
                case ActionId.GamingMode: await RunModeAsync("gaming", cancellationToken); break;
                case ActionId.StudyMode: await RunModeAsync("study", cancellationToken); break;
                case ActionId.MovieMode: await RunModeAsync("movie", cancellationToken); break;
                case ActionId.LockPc: await windows.LockAsync(cancellationToken); break;
                case ActionId.SleepPc: Require(config.Permissions.AllowSleep, "sleep_disabled"); await windows.SleepAsync(cancellationToken); break;
                case ActionId.RestartPc: Require(config.Permissions.AllowRestart, "restart_disabled"); await windows.RestartAsync(cancellationToken); break;
                case ActionId.ShutdownPc: Require(config.Permissions.AllowShutdown, "shutdown_disabled"); await windows.ShutdownAsync(cancellationToken); break;
                case ActionId.SetVolume: await windows.SetVolumeAsync(GetInt(command, "volume"), cancellationToken); break;
                case ActionId.Mute: await windows.SetMutedAsync(GetBool(command, "muted"), cancellationToken); break;
                case ActionId.PlayPause or ActionId.NextTrack or ActionId.PreviousTrack: await windows.MediaKeyAsync(action, cancellationToken); break;
                case ActionId.MonitorOff: await windows.MonitorOffAsync(cancellationToken); break;
                case ActionId.RunRoutine: await RunRoutineAsync(GetString(command, "routineId"), cancellationToken); break;
                default: throw new InvalidOperationException("unknown_action");
            }
            return Result(command.CommandId, true, null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or FileNotFoundException or UnauthorizedAccessException)
        { return Result(command.CommandId, false, ex.Message); }
    }

    private async Task RunRoutineAsync(string id, CancellationToken cancellationToken)
    {
        if (!config.Routines.TryGetValue(id, out var routine)) throw new InvalidOperationException($"routine_not_found:{id}");
        foreach (var step in routine.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ActionWire.TryParse(step.Action, out var action) || action == ActionId.RunRoutine) throw new InvalidOperationException("invalid_routine_step");
            var now = DateTimeOffset.UtcNow;
            var nested = new CommandEnvelope("command", Guid.NewGuid().ToString("N"), step.Action,
                step.Parameters.ToDictionary(x => x.Key, x => (object)x.Value), now.ToUnixTimeMilliseconds(), now.AddSeconds(15).ToUnixTimeMilliseconds());
            var result = await ExecuteAsync(nested, action, cancellationToken);
            if (!result.Success) throw new InvalidOperationException($"routine_step_failed:{step.Action}:{result.Error}");
        }
    }

    private async Task RunModeAsync(string name, CancellationToken cancellationToken)
    {
        if (!config.Modes.TryGetValue(name, out var mode)) throw new InvalidOperationException($"mode_not_configured:{name}");
        foreach (var app in mode.Launch) await windows.LaunchAsync(app, app.Equals("chrome", StringComparison.OrdinalIgnoreCase) ? mode.Urls : [], cancellationToken);
        if (mode.Volume is not null) await windows.SetVolumeAsync(mode.Volume.Value, cancellationToken);
        if (mode.PowerPlan is not null) { Require(config.Permissions.AllowPowerPlanChange, "power_plan_change_disabled"); await windows.SetPowerPlanAsync(mode.PowerPlan, cancellationToken); }
        if (mode.Close.Count > 0) Require(config.Permissions.AllowProcessClose, "process_close_disabled");
        foreach (var app in mode.Close) await windows.CloseAllowListedAsync(app, cancellationToken);
    }

    private static void Require(bool allowed, string error) { if (!allowed) throw new InvalidOperationException(error); }
    private static int GetInt(CommandEnvelope c, string key) => ((JsonElement)c.Parameters[key]).GetInt32();
    private static bool GetBool(CommandEnvelope c, string key) => ((JsonElement)c.Parameters[key]).GetBoolean();
    private static string GetString(CommandEnvelope c, string key) => ((JsonElement)c.Parameters[key]).GetString() ?? throw new InvalidOperationException($"invalid_{key}");
    private CommandResult Result(string id, bool ok, string? error) => new("result", id, ok, error, windows.State, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
}
