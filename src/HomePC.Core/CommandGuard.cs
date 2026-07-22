using System.Text.Json;

namespace HomePC.Core;

public sealed class CommandGuard(int capacity = 512)
{
    private static readonly IReadOnlyDictionary<string, ActionId> Actions = new Dictionary<string, ActionId>(StringComparer.Ordinal)
    {
        ["open_notepad"] = ActionId.OpenNotepad, ["open_steam"] = ActionId.OpenSteam,
        ["open_discord"] = ActionId.OpenDiscord, ["open_chrome"] = ActionId.OpenChrome,
        ["gaming_mode"] = ActionId.GamingMode, ["study_mode"] = ActionId.StudyMode,
        ["movie_mode"] = ActionId.MovieMode, ["lock_pc"] = ActionId.LockPc,
        ["sleep_pc"] = ActionId.SleepPc, ["restart_pc"] = ActionId.RestartPc,
        ["shutdown_pc"] = ActionId.ShutdownPc, ["set_volume"] = ActionId.SetVolume,
        ["mute"] = ActionId.Mute, ["play_pause"] = ActionId.PlayPause,
        ["next_track"] = ActionId.NextTrack, ["previous_track"] = ActionId.PreviousTrack,
        ["monitor_off"] = ActionId.MonitorOff,
    };
    private readonly int _capacity = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private readonly Queue<string> _order = new();
    private readonly object _gate = new();

    public bool TryAccept(CommandEnvelope command, DateTimeOffset now, out ActionId action, out string? error)
    {
        action = default;
        if (command.Type != "command" || string.IsNullOrWhiteSpace(command.CommandId)) { error = "invalid_envelope"; return false; }
        if (command.ExpiresAt < now.ToUnixTimeMilliseconds()) { error = "expired_command"; return false; }
        if (command.IssuedAt > now.AddMinutes(1).ToUnixTimeMilliseconds()) { error = "future_command"; return false; }
        if (!Actions.TryGetValue(command.Action, out action)) { error = "unknown_action"; return false; }
        if (!ValidateParameters(action, command.Parameters, out error)) return false;
        lock (_gate)
        {
            if (!_seen.Add(command.CommandId)) { error = "duplicate_command"; return false; }
            _order.Enqueue(command.CommandId);
            while (_order.Count > _capacity) _seen.Remove(_order.Dequeue());
        }
        error = null; return true;
    }

    private static bool ValidateParameters(ActionId action, IReadOnlyDictionary<string, object> parameters, out string? error)
    {
        if (action == ActionId.SetVolume)
        {
            if (!parameters.TryGetValue("volume", out var raw) || raw is not JsonElement { ValueKind: JsonValueKind.Number } element || !element.TryGetInt32(out var value) || value is < 0 or > 100)
            { error = "invalid_volume"; return false; }
        }
        else if (action == ActionId.Mute)
        {
            if (!parameters.TryGetValue("muted", out var raw) || raw is not JsonElement { ValueKind: JsonValueKind.True or JsonValueKind.False })
            { error = "invalid_mute"; return false; }
        }
        else if (parameters.Count != 0) { error = "unexpected_parameters"; return false; }
        error = null; return true;
    }
}

