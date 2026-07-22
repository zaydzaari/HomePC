namespace HomePC.Core;

public static class ActionWire
{
    private static readonly IReadOnlyDictionary<string, ActionId> ByName = new Dictionary<string, ActionId>(StringComparer.Ordinal)
    {
        ["open_notepad"] = ActionId.OpenNotepad, ["open_steam"] = ActionId.OpenSteam,
        ["open_discord"] = ActionId.OpenDiscord, ["open_chrome"] = ActionId.OpenChrome,
        ["gaming_mode"] = ActionId.GamingMode, ["study_mode"] = ActionId.StudyMode,
        ["movie_mode"] = ActionId.MovieMode, ["lock_pc"] = ActionId.LockPc,
        ["sleep_pc"] = ActionId.SleepPc, ["restart_pc"] = ActionId.RestartPc,
        ["shutdown_pc"] = ActionId.ShutdownPc, ["set_volume"] = ActionId.SetVolume,
        ["mute"] = ActionId.Mute, ["play_pause"] = ActionId.PlayPause,
        ["next_track"] = ActionId.NextTrack, ["previous_track"] = ActionId.PreviousTrack,
        ["monitor_off"] = ActionId.MonitorOff, ["run_routine"] = ActionId.RunRoutine,
    };

    public static bool TryParse(string value, out ActionId action) => ByName.TryGetValue(value, out action);
    public static string Name(ActionId action) => ByName.First(x => x.Value == action).Key;
}
