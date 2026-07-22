using System.Text.Json;

namespace HomePC.Core;

public sealed class CommandGuard(int capacity = 512)
{
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
        if (!ActionWire.TryParse(command.Action, out action)) { error = "unknown_action"; return false; }
        if (!ValidateActionParameters(action, command.Parameters, out error)) return false;
        lock (_gate)
        {
            if (!_seen.Add(command.CommandId)) { error = "duplicate_command"; return false; }
            _order.Enqueue(command.CommandId);
            while (_order.Count > _capacity) _seen.Remove(_order.Dequeue());
        }
        error = null; return true;
    }

    public static bool ValidateActionParameters(ActionId action, IReadOnlyDictionary<string, object> parameters, out string? error)
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
        else if (action == ActionId.RunRoutine)
        {
            if (parameters.Count != 1 || !parameters.TryGetValue("routineId", out var raw) || raw is not JsonElement { ValueKind: JsonValueKind.String } element ||
                string.IsNullOrWhiteSpace(element.GetString()) || element.GetString()!.Length > 48)
            { error = "invalid_routine_id"; return false; }
        }
        else if (parameters.Count != 0) { error = "unexpected_parameters"; return false; }
        error = null; return true;
    }
}
