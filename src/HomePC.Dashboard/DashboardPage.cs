using System.Text.Encodings.Web;
using HomePC.Core;
using HomePC.Windows;

internal static class DashboardPage
{
    private static readonly (string Id, string Label)[] Controls =
    [
        ("OpenNotepad", "Open Notepad"), ("OpenSteam", "Open Steam"), ("OpenDiscord", "Open Discord"),
        ("OpenChrome", "Open Chrome"), ("GamingMode", "Gaming mode"), ("StudyMode", "Study mode"),
        ("MovieMode", "Movie mode"), ("LockPc", "Lock PC"), ("SetVolume", "Volume 50%"),
        ("Mute", "Mute"), ("PlayPause", "Play / pause"), ("NextTrack", "Next track"),
        ("PreviousTrack", "Previous track"), ("MonitorOff", "Monitor off"),
    ];

    public static string Render(HomePcConfig config, IWindowsController windows, string configPath)
    {
        var encoder = HtmlEncoder.Default;
        var issues = ConfigurationValidator.Validate(config);
        var validation = issues.Count == 0
            ? "Configuration valid"
            : string.Join("<br>", issues.Select(issue => encoder.Encode($"{issue.Path}: {issue.Message}")));
        var applications = string.Join("", windows.DetectedApplications.Select(item =>
            $"<div class='app-row'><span class='app-name'>{encoder.Encode(ToTitle(item.Key))}</span><span class='status-label {(item.Value is null ? "status-warning" : "status-ok")}'><span class='status-dot'></span>{(item.Value is null ? "Not found" : "Ready")}</span></div>"));
        var controls = string.Join("", Controls.Select(control =>
            $"<form class='action-form' method='post' action='/test/{control.Id}'><button class='button button-secondary' type='submit'>{encoder.Encode(control.Label)}</button></form>"));
        var routines = config.Routines.Count == 0
            ? "<div class='empty-state'><strong>No routines configured</strong><span>Create an allow-listed sequence when you need one.</span></div>"
            : string.Join("", config.Routines.Select(routine =>
                $"<div class='routine-row'><div><strong>{encoder.Encode(routine.Value.Name)}</strong><span>{encoder.Encode(routine.Key)} &middot; {routine.Value.Steps.Count} steps &middot; {(routine.Value.ExposeToGoogleHome ? "Google Home" : "Local only")}</span></div><div class='row-actions'><form class='action-form' method='post' action='/routines/{Uri.EscapeDataString(routine.Key)}/run'><button class='button button-secondary' type='submit'>Run</button></form><form method='post' action='/routines/{Uri.EscapeDataString(routine.Key)}/delete' data-confirm='Delete this routine?'><button class='button button-danger' type='submit'>Delete</button></form></div></div>"));
        var configName = encoder.Encode(Path.GetFileName(configPath));

        return $$$"""
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width,initial-scale=1">
          <meta name="color-scheme" content="dark">
          <title>HomePC Control Center</title>
          <link rel="stylesheet" href="/dashboard.css">
          <script src="/dashboard.js" defer></script>
        </head>
        <body><div class="shell">
          <aside class="sidebar">
            <div class="brand"><div class="brand-mark" aria-hidden="true">H</div><div><strong>HomePC</strong><span>Windows control bridge</span></div></div>
            <nav class="nav" aria-label="Dashboard sections"><a href="#overview">Overview</a><a href="#controls">Controls</a><a href="#routines">Routines</a><a href="#security">Security</a></nav>
            <div class="sidebar-note"><strong>Local administration</strong>Available only on this PC</div>
          </aside>
          <main><div class="content">
            <header class="page-header"><div><div class="overline">Local dashboard</div><h1>Control center</h1><p>Monitor the bridge, test approved actions, and manage routines.</p></div><div id="cloud" class="connection" role="status" aria-live="polite"><span class="status-dot"></span>Checking connection</div></header>
            <div class="grid" id="overview">
              <section class="panel status-panel" aria-labelledby="status-heading"><div class="panel-header"><div><h2 id="status-heading">System status</h2><p>Live health of the local agent and cloud connection.</p></div></div><div class="stats"><div class="stat"><small>Cloud</small><strong id="linked">Checking</strong></div><div class="stat"><small>Windows agent</small><strong id="agent">Checking</strong></div><div class="stat"><small>Configuration</small><strong class="{{{(issues.Count == 0 ? "ok" : "warn")}}}">{{{validation}}}</strong></div><div class="stat"><small>Audio</small><strong>{{{windows.State.Volume}}}% &middot; {{{(windows.State.Muted ? "Muted" : "Active")}}}</strong></div></div></section>
              <section class="panel apps" id="controls"><div class="panel-header"><div><h2>Detected applications</h2><p>Allow-listed local targets.</p></div></div><div class="panel-body">{{{applications}}}</div></section>
              <section class="panel actions"><div class="panel-header"><div><h2>Manual controls</h2><p>Tests use the same validation path as Google Home.</p></div></div><div class="panel-body"><div class="buttons">{{{controls}}}</div><div class="volume"><label for="volume">PC volume</label><input id="volume" type="range" min="0" max="100" value="{{{windows.State.Volume}}}"><output id="volumeValue" for="volume">{{{windows.State.Volume}}}%</output><button id="applyVolume" class="button button-primary" type="button">Apply</button></div></div></section>
              <section class="panel permissions" id="security"><div class="panel-header"><div><h2>Protected power actions</h2><p>Disabled by default. Changes apply after restart.</p></div></div><div class="panel-body"><div class="permission-list">{{{Permission("sleep", config.Permissions.AllowSleep)}}}{{{Permission("restart", config.Permissions.AllowRestart)}}}{{{Permission("shutdown", config.Permissions.AllowShutdown)}}}</div></div></section>
              <section class="panel privacy"><div class="panel-header"><div><h2>Security boundary</h2><p>What this interface can expose.</p></div></div><div class="panel-body privacy-list"><div class="privacy-item"><span>Dashboard binding</span><strong class="mono">127.0.0.1 only</strong></div><div class="privacy-item"><span>Cloud transport</span><strong>Outbound authenticated WebSocket</strong></div><div class="privacy-item"><span>Configuration</span><strong class="mono">{{{configName}}}</strong></div><div class="privacy-item"><span>Secrets</span><strong>DPAPI protected and never rendered</strong></div></div></section>
              <section class="panel routines" id="routines"><div class="panel-header"><div><h2>Routines</h2><p>Sequences of approved actions; scripts and shell commands are rejected.</p></div></div><div class="panel-body">{{{routines}}}</div><details class="creator"><summary>Create routine</summary><form class="editor" method="post" action="/routines"><label class="field">Routine ID<input name="id" required pattern="[a-z][a-z0-9-]{1,47}" placeholder="work-setup" aria-describedby="routine-id-help"><span id="routine-id-help" class="helper">Lowercase letters, numbers, and hyphens.</span></label><label class="field">Display name<input name="name" required maxlength="60" placeholder="Open my work setup"></label><label class="field wide">Steps (JSON array)<textarea name="steps" required spellcheck="false">[{"action":"open_notepad","parameters":{}},{"action":"set_volume","parameters":{"volume":20}}]</textarea><span class="helper">Use 1–12 documented allow-listed actions. Nested routines are not accepted.</span></label><label class="check wide"><input type="checkbox" name="expose" checked><span>Expose as a Google Home switch after the agent reconnects.</span></label><div class="editor-actions wide"><button class="button button-primary" type="submit">Save routine</button></div></form></details></section>
            </div>
          </div></main>
        </div><div id="toast" class="toast" role="status" aria-live="polite"></div></body></html>
        """;
    }

    private static string Permission(string name, bool enabled) =>
        $"<div class='permission'><div class='permission-top'><b>{HtmlEncoder.Default.Encode(name)}</b><span class='{(enabled ? "ok" : "")}'>{(enabled ? "Enabled" : "Disabled")}</span></div><form class='permission-form' method='post' action='/permissions/{name}/{(!enabled).ToString().ToLowerInvariant()}'><button class='button' type='submit'>{(enabled ? "Disable" : "Enable")}</button></form></div>";

    private static string ToTitle(string value) => string.Join(" ", value.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}
