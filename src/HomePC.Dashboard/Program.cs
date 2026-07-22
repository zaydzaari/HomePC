using System.Net;
using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using HomePC.Core;
using HomePC.Windows;

var configPath = ResolveConfig(args);
var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
var config = ConfigurationSecurity.Unprotect(JsonSerializer.Deserialize<HomePcConfig>(File.ReadAllText(configPath), jsonOptions) ?? throw new InvalidDataException("Configuration is empty."));
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(config.DashboardUrl);
builder.Services.AddSingleton(config);
builder.Services.AddSingleton<IWindowsController, NativeWindowsController>();
builder.Services.AddSingleton<ActionRegistry>();
var app = builder.Build();

app.MapGet("/", (HomePcConfig cfg, IWindowsController windows) => Results.Content(Page(cfg, windows, configPath), "text/html"));
app.MapGet("/api/status", (HomePcConfig cfg, IWindowsController windows) => Results.Json(new
{
    uptimeSeconds = Environment.TickCount64 / 1000, worker = Redact(cfg.WorkerUrl), state = windows.State,
    applications = windows.DetectedApplications.ToDictionary(x => x.Key, x => x.Value is not null),
    validation = ConfigurationValidator.Validate(cfg).Select(x => new { x.Path, x.Message }),
}));
app.MapGet("/api/cloud", async (HomePcConfig cfg, CancellationToken token) =>
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cfg.AdminToken);
    try
    {
        using var response = await client.GetAsync(new Uri(new Uri(cfg.WorkerUrl), "/admin/status"), token);
        return Results.Content(await response.Content.ReadAsStringAsync(token), "application/json", statusCode: (int)response.StatusCode);
    }
    catch (HttpRequestException) { return Results.Json(new { linked = false, agent = new { online = false }, error = "cloud_unreachable" }, statusCode: 503); }
});
app.MapPost("/test/{action}", async (string action, ActionRegistry registry, CancellationToken token) =>
{
    if (!Enum.TryParse<ActionId>(action, true, out var id)) return Results.BadRequest("Unknown action.");
    if (id is ActionId.SleepPc or ActionId.RestartPc or ActionId.ShutdownPc) return Results.BadRequest("Dangerous actions cannot be tested from the dashboard.");
    IReadOnlyDictionary<string, object> parameters = id switch
    {
        ActionId.Mute => JsonSerializer.Deserialize<Dictionary<string, object>>("{\"muted\":true}")!,
        ActionId.SetVolume => JsonSerializer.Deserialize<Dictionary<string, object>>("{\"volume\":50}")!,
        _ => new Dictionary<string, object>(),
    };
    var command = new CommandEnvelope("command", Guid.NewGuid().ToString(), ToWire(id), parameters, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), DateTimeOffset.UtcNow.AddSeconds(10).ToUnixTimeMilliseconds());
    return Results.Json(await registry.ExecuteAsync(command, id, token));
});
app.MapPost("/volume/{level:int}", async (int level, IWindowsController windows, CancellationToken token) =>
{
    if (level is < 0 or > 100) return Results.BadRequest("Volume must be 0-100.");
    await windows.SetVolumeAsync(level, token); return Results.Json(windows.State);
});
app.MapPost("/permissions/{name}/{enabled:bool}", async (string name, bool enabled) =>
{
    var document = JsonDocument.Parse(await File.ReadAllTextAsync(configPath));
    var root = JsonSerializer.Deserialize<Dictionary<string, object>>(document.RootElement.GetRawText()) ?? [];
    var cfg = JsonSerializer.Deserialize<HomePcConfig>(document.RootElement.GetRawText(), jsonOptions) ?? throw new InvalidDataException();
    var permissions = new PermissionConfig
    {
        AllowSleep = name == "sleep" ? enabled : cfg.Permissions.AllowSleep,
        AllowRestart = name == "restart" ? enabled : cfg.Permissions.AllowRestart,
        AllowShutdown = name == "shutdown" ? enabled : cfg.Permissions.AllowShutdown,
        AllowPowerPlanChange = cfg.Permissions.AllowPowerPlanChange,
        AllowProcessClose = cfg.Permissions.AllowProcessClose,
    };
    root["permissions"] = permissions;
    await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(root, jsonOptions));
    return Results.Text("Saved. Restart the agent and dashboard for the permission change to take effect.");
});
app.MapPost("/routines", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    var id = form["id"].ToString().Trim();
    var name = form["name"].ToString().Trim();
    JsonNode? steps;
    try { steps = JsonNode.Parse(form["steps"].ToString()); }
    catch (JsonException) { return Results.BadRequest("Steps must be a valid JSON array."); }
    if (steps is not JsonArray) return Results.BadRequest("Steps must be a JSON array.");
    var root = JsonNode.Parse(await File.ReadAllTextAsync(configPath))?.AsObject() ?? throw new InvalidDataException();
    var routines = root["routines"] as JsonObject ?? new JsonObject();
    root["routines"] = routines;
    routines[id] = new JsonObject { ["name"] = name, ["exposeToGoogleHome"] = form.ContainsKey("expose"), ["steps"] = steps };
    var candidate = ConfigurationSecurity.Unprotect(root.Deserialize<HomePcConfig>(jsonOptions) ?? throw new InvalidDataException());
    var issue = ConfigurationValidator.Validate(candidate).FirstOrDefault(x => x.Path.StartsWith($"routines.{id}", StringComparison.Ordinal));
    if (issue is not null) return Results.BadRequest($"{issue.Path}: {issue.Message}");
    await File.WriteAllTextAsync(configPath, root.ToJsonString(jsonOptions));
    return Results.Redirect("/");
});
app.MapPost("/routines/{id}/delete", async (string id) =>
{
    var root = JsonNode.Parse(await File.ReadAllTextAsync(configPath))?.AsObject() ?? throw new InvalidDataException();
    if (root["routines"] is JsonObject routines) routines.Remove(id);
    await File.WriteAllTextAsync(configPath, root.ToJsonString(jsonOptions));
    return Results.Redirect("/");
});
app.MapPost("/routines/{id}/run", async (string id, IWindowsController windows, CancellationToken token) =>
{
    var cfg = ConfigurationSecurity.Unprotect(JsonSerializer.Deserialize<HomePcConfig>(await File.ReadAllTextAsync(configPath, token), jsonOptions) ?? throw new InvalidDataException());
    var registry = new ActionRegistry(windows, cfg);
    var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(new { routineId = id }))!;
    var now = DateTimeOffset.UtcNow;
    var command = new CommandEnvelope("command", Guid.NewGuid().ToString("N"), "run_routine", parameters, now.ToUnixTimeMilliseconds(), now.AddMinutes(1).ToUnixTimeMilliseconds());
    return Results.Json(await registry.ExecuteAsync(command, ActionId.RunRoutine, token));
});
await app.RunAsync();

static string ResolveConfig(string[] values)
{
    var index = Array.IndexOf(values, "--config");
    return Path.GetFullPath(index >= 0 && index + 1 < values.Length ? values[index + 1] : Environment.GetEnvironmentVariable("HOMEPC_CONFIG") ?? "generated/homepc.json");
}
static string Redact(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Authority) : "invalid";
static string ToWire(ActionId id) => string.Concat(id.ToString().Select((c, i) => char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
static string Page(HomePcConfig cfg, IWindowsController windows, string path)
{
    var e = HtmlEncoder.Default;
    var apps = string.Join("", windows.DetectedApplications.Select(x =>
        $"<div class='app'><span>{e.Encode(Title(x.Key))}</span><strong class='{(x.Value is null ? "warn" : "ok")}'>{(x.Value is null ? "Not found" : "Ready")}</strong></div>"));
    var buttons = new (string Id, string Label)[]
    {
        ("OpenNotepad", "Open Notepad"), ("OpenSteam", "Open Steam"), ("OpenDiscord", "Open Discord"),
        ("OpenChrome", "Open Chrome"), ("GamingMode", "Gaming mode"), ("StudyMode", "Study mode"),
        ("MovieMode", "Movie mode"), ("LockPc", "Lock PC"), ("SetVolume", "Volume 50%"),
        ("Mute", "Mute"), ("PlayPause", "Play / pause"), ("NextTrack", "Next track"),
        ("PreviousTrack", "Previous track"), ("MonitorOff", "Monitor off"),
    };
    var controls = string.Join("", buttons.Select(x => $"<form method='post' action='/test/{x.Id}'><button>{e.Encode(x.Label)}</button></form>"));
    var issues = ConfigurationValidator.Validate(cfg);
    var validation = issues.Count == 0 ? "Configuration valid" : string.Join("<br>", issues.Select(x => e.Encode(x.Path + ": " + x.Message)));
    var routines = cfg.Routines.Count == 0 ? "<p>No custom routines yet.</p>" : string.Join("", cfg.Routines.Select(x =>
        $"<div class='routine'><div><strong>{e.Encode(x.Value.Name)}</strong><small>{e.Encode(x.Key)} · {x.Value.Steps.Count} steps · {(x.Value.ExposeToGoogleHome ? "Google Home" : "Local only")}</small></div><div><form method='post' action='/routines/{Uri.EscapeDataString(x.Key)}/run'><button>Run</button></form><form method='post' action='/routines/{Uri.EscapeDataString(x.Key)}/delete'><button class='danger'>Delete</button></form></div></div>"));
    var configName = Path.GetFileName(path);
    return $$$"""
    <!doctype html>
    <html lang="en">
    <head>
      <meta charset="utf-8">
      <meta name="viewport" content="width=device-width,initial-scale=1">
      <title>HomePC Dashboard</title>
      <style>
        :root{color-scheme:dark;--bg:#070b16;--panel:rgba(18,27,48,.82);--line:#263556;--text:#f5f7ff;--muted:#9ba9c5;--accent:#75a7ff;--good:#61d69b;--warn:#ffc970}
        *{box-sizing:border-box}body{margin:0;min-height:100vh;background:radial-gradient(circle at 15% 0,#172a50 0,transparent 34rem),var(--bg);color:var(--text);font:15px/1.5 Inter,ui-sans-serif,system-ui,sans-serif}
        main{width:min(1160px,calc(100% - 32px));margin:auto;padding:48px 0 72px}header{display:flex;align-items:flex-end;justify-content:space-between;gap:24px;margin-bottom:28px}
        .eyebrow{color:var(--accent);font-size:12px;font-weight:800;letter-spacing:.14em;text-transform:uppercase}h1{font-size:clamp(34px,5vw,58px);line-height:1;margin:8px 0 10px;letter-spacing:-.045em}h2{font-size:18px;margin:0 0 16px}p{color:var(--muted);margin:0}.live{display:flex;align-items:center;gap:9px;padding:9px 13px;border:1px solid #315a51;border-radius:999px;background:#102921;color:var(--good);font-weight:750}.dot{width:8px;height:8px;border-radius:50%;background:currentColor;box-shadow:0 0 16px currentColor}
        .grid{display:grid;grid-template-columns:repeat(12,1fr);gap:16px}.card{grid-column:span 12;background:var(--panel);border:1px solid var(--line);border-radius:20px;padding:22px;box-shadow:0 18px 60px rgba(0,0,0,.2);backdrop-filter:blur(14px)}.status{grid-column:span 12}.apps{grid-column:span 4}.actions{grid-column:span 8}.permissions{grid-column:span 8}.privacy{grid-column:span 4}.routines{grid-column:span 12}
        .stats{display:grid;grid-template-columns:repeat(4,1fr);gap:12px}.stat{padding:15px;border-radius:14px;background:#0c1426;border:1px solid #202f4d}.stat small{display:block;color:var(--muted);margin-bottom:5px}.stat strong{font-size:16px}.ok{color:var(--good)}.warn{color:var(--warn)}
        .app{display:flex;justify-content:space-between;align-items:center;padding:12px 0;border-bottom:1px solid #22304b}.app:last-child{border:0}.app strong{font-size:13px}
        .buttons{display:flex;flex-wrap:wrap;gap:9px}form{display:inline}button{appearance:none;border:1px solid #345184;background:#172b50;color:#eaf1ff;padding:10px 13px;border-radius:11px;font:inherit;font-weight:700;cursor:pointer}button:hover{background:#224174;border-color:#5684cf}.volume{display:flex;align-items:center;gap:12px;margin-top:18px;padding-top:18px;border-top:1px solid var(--line)}input[type=range]{width:min(320px,45vw);accent-color:var(--accent)}
        .permission-list{display:grid;grid-template-columns:repeat(3,1fr);gap:10px}.permission{padding:14px;border-radius:14px;background:#0c1426;border:1px solid #202f4d}.permission b{display:block;text-transform:capitalize;margin-bottom:9px}.permission span{color:var(--muted);font-size:13px}.permission form{display:block}.permission button{width:100%;margin-top:12px;background:#33251b;border-color:#6f4d2d;color:#ffdba7}.privacy p+p{margin-top:12px}.mono{font-family:ui-monospace,SFMono-Regular,Consolas,monospace;font-size:13px;word-break:break-all;color:#c7d5f2}.routine{display:flex;justify-content:space-between;gap:16px;align-items:center;padding:12px 0;border-bottom:1px solid var(--line)}.routine small{display:block;color:var(--muted);margin-top:3px}.danger{background:#40212a;border-color:#814052}.editor{display:grid;grid-template-columns:1fr 1fr;gap:10px;margin-top:20px;padding-top:20px;border-top:1px solid var(--line)}.editor label{color:var(--muted)}.editor input,.editor textarea{display:block;width:100%;margin-top:6px;background:#0c1426;color:var(--text);border:1px solid #2d4268;border-radius:10px;padding:10px;font:inherit}.editor textarea{min-height:120px;font-family:ui-monospace,Consolas,monospace}.editor .wide{grid-column:span 2}.editor .check input{display:inline;width:auto;margin-right:8px}
        @media(max-width:820px){main{padding-top:28px}header{align-items:flex-start;flex-direction:column}.apps,.actions,.permissions,.privacy{grid-column:span 12}.stats{grid-template-columns:repeat(2,1fr)}.permission-list{grid-template-columns:1fr}}
      </style>
    </head>
    <body><main>
      <header><div><div class="eyebrow">Google Home for Windows</div><h1>HomePC control center</h1><p>Local status, safe action tests, and permission controls.</p></div><div id="cloud" class="live"><span class="dot"></span>Checking connection</div></header>
      <div class="grid">
        <section class="card status"><h2>System status</h2><div class="stats">
          <div class="stat"><small>Cloud</small><strong id="linked">Checking</strong></div>
          <div class="stat"><small>Windows agent</small><strong id="agent">Checking</strong></div>
          <div class="stat"><small>Configuration</small><strong class="{{{(issues.Count == 0 ? "ok" : "warn")}}}">{{{validation}}}</strong></div>
          <div class="stat"><small>Audio</small><strong>{{{windows.State.Volume}}}% · {{{(windows.State.Muted ? "Muted" : "Active")}}}</strong></div>
        </div></section>
        <section class="card apps"><h2>Detected applications</h2>{{{apps}}}</section>
        <section class="card actions"><h2>Safe manual tests</h2><div class="buttons">{{{controls}}}</div><div class="volume"><label for="volume">PC volume</label><input id="volume" type="range" min="0" max="100" value="{{{windows.State.Volume}}}"><button onclick="fetch('/volume/'+volume.value,{method:'POST'})">Apply</button></div></section>
        <section class="card permissions"><h2>Protected power actions</h2><p style="margin-bottom:16px">Disabled by default. Changes apply after the Windows agent restarts.</p><div class="permission-list">{{{Toggle("sleep", cfg.Permissions.AllowSleep)}}}{{{Toggle("restart", cfg.Permissions.AllowRestart)}}}{{{Toggle("shutdown", cfg.Permissions.AllowShutdown)}}}</div></section>
        <section class="card privacy"><h2>Privacy boundary</h2><p>Dashboard: <span class="mono">127.0.0.1 only</span></p><p>Worker: <span class="mono">Cloudflare endpoint configured</span></p><p>Config: <span class="mono">{{{e.Encode(configName)}}}</span></p><p>Device and admin tokens are never rendered.</p></section>
        <section class="card routines"><h2>Custom routines</h2><p>Compose approved actions. Arbitrary commands and scripts are never accepted.</p>{{{routines}}}<form class="editor" method="post" action="/routines"><label>Routine ID<input name="id" required pattern="[a-z][a-z0-9-]{1,47}" placeholder="work-setup"></label><label>Display name<input name="name" required maxlength="60" placeholder="Open my work setup"></label><label class="wide">Steps (JSON array)<textarea name="steps" required>[{"action":"open_notepad","parameters":{}},{"action":"set_volume","parameters":{"volume":20}}]</textarea></label><label class="check wide"><input type="checkbox" name="expose" checked>Expose as a Google Home switch after the agent reconnects</label><div class="wide"><button>Save routine</button></div></form></section>
      </div>
    </main><script>
      fetch('/api/cloud').then(r=>r.json()).then(x=>{const connected=x.linked&&x.agent?.online;linked.textContent=x.linked?'Linked':'Not linked';agent.textContent=x.agent?.online?'Online':'Offline';linked.className=x.linked?'ok':'warn';agent.className=x.agent?.online?'ok':'warn';cloud.innerHTML=`<span class="dot"></span>${connected?'Connected':'Attention needed'}`;if(!connected)cloud.style.color='var(--warn)'}).catch(()=>{cloud.innerHTML='<span class="dot"></span>Cloud unavailable';cloud.style.color='var(--warn)'})
    </script></body></html>
    """;
}
static string Toggle(string name, bool current) => $"<div class='permission'><b>{HtmlEncoder.Default.Encode(name)}</b><span>{(current ? "Enabled" : "Disabled")}</span><form method='post' action='/permissions/{name}/{(!current).ToString().ToLowerInvariant()}'><button>{(current ? "Disable" : "Enable")}</button></form></div>";
static string Title(string value) => string.Join(" ", value.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries).Select(x => char.ToUpperInvariant(x[0]) + x[1..]));
