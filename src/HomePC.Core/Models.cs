using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomePC.Core;

[JsonConverter(typeof(JsonStringEnumConverter<ActionId>))]
public enum ActionId
{
    OpenNotepad, OpenSteam, OpenDiscord, OpenChrome, GamingMode, StudyMode, MovieMode,
    LockPc, SleepPc, RestartPc, ShutdownPc, SetVolume, Mute, PlayPause, NextTrack,
    PreviousTrack, MonitorOff, RunRoutine
}

public sealed record CommandEnvelope(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("commandId")] string CommandId,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("parameters")] IReadOnlyDictionary<string, object> Parameters,
    [property: JsonPropertyName("issuedAt")] long IssuedAt,
    [property: JsonPropertyName("expiresAt")] long ExpiresAt);

public sealed record CommandResult(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("commandId")] string CommandId,
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("state")] AgentState State,
    [property: JsonPropertyName("completedAt")] long CompletedAt);

public sealed record AgentState(int Volume, bool Muted, string? Mode);

public sealed class HomePcConfig
{
    public int SchemaVersion { get; init; } = 2;
    public string WorkerUrl { get; init; } = "";
    public string DeviceToken { get; set; } = "";
    public string AdminToken { get; set; } = "";
    public string DashboardUrl { get; init; } = "http://127.0.0.1:5187";
    public PermissionConfig Permissions { get; init; } = new();
    public Dictionary<string, string> Applications { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, ModeConfig> Modes { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, RoutineConfig> Routines { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public int HeartbeatSeconds { get; init; } = 25;
    public int MaxMessageBytes { get; init; } = 32_768;
    public string AuditLogPath { get; init; } = "logs/homepc-audit.jsonl";
}

public sealed class RoutineConfig
{
    public string Name { get; init; } = "";
    public bool ExposeToGoogleHome { get; init; }
    public List<RoutineStepConfig> Steps { get; init; } = [];
}

public sealed class RoutineStepConfig
{
    public string Action { get; init; } = "";
    public Dictionary<string, JsonElement> Parameters { get; init; } = new(StringComparer.Ordinal);
}

public sealed class PermissionConfig
{
    public bool AllowSleep { get; init; }
    public bool AllowRestart { get; init; }
    public bool AllowShutdown { get; init; }
    public bool AllowPowerPlanChange { get; init; }
    public bool AllowProcessClose { get; init; }
}

public sealed class ModeConfig
{
    public List<string> Launch { get; init; } = [];
    public List<string> Urls { get; init; } = [];
    public int? Volume { get; init; }
    public string? PowerPlan { get; init; }
    public List<string> Close { get; init; } = [];
}

public sealed record ValidationIssue(string Path, string Message);
