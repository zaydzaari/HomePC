using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using HomePC.Core;
using HomePC.Windows;

var configPath = ResolveConfig(args);
var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
var config = ConfigurationSecurity.Unprotect(JsonSerializer.Deserialize<HomePcConfig>(File.ReadAllText(configPath), jsonOptions) ?? throw new InvalidDataException("Configuration is empty."));
var publishedWebRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = Directory.Exists(publishedWebRoot) ? publishedWebRoot : null,
});
builder.WebHost.UseUrls(config.DashboardUrl);
builder.Services.AddSingleton(config);
builder.Services.AddSingleton<IWindowsController, NativeWindowsController>();
builder.Services.AddSingleton<ActionRegistry>();
var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'; object-src 'none'";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});
app.UseStaticFiles();

app.MapGet("/", (HomePcConfig cfg, IWindowsController windows) => Results.Content(DashboardPage.Render(cfg, windows, configPath), "text/html"));
app.MapGet("/api/status", (HomePcConfig cfg, IWindowsController windows) => Results.Json(new
{
    uptimeSeconds = Environment.TickCount64 / 1000,
    worker = Redact(cfg.WorkerUrl),
    state = windows.State,
    applications = windows.DetectedApplications.ToDictionary(item => item.Key, item => item.Value is not null),
    validation = ConfigurationValidator.Validate(cfg).Select(issue => new { issue.Path, issue.Message }),
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
    catch (HttpRequestException)
    {
        return Results.Json(new { linked = false, agent = new { online = false }, error = "cloud_unreachable" }, statusCode: 503);
    }
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
    var now = DateTimeOffset.UtcNow;
    var command = new CommandEnvelope("command", Guid.NewGuid().ToString(), ToWire(id), parameters, now.ToUnixTimeMilliseconds(), now.AddSeconds(10).ToUnixTimeMilliseconds());
    return Results.Json(await registry.ExecuteAsync(command, id, token));
});
app.MapPost("/volume/{level:int}", async (int level, IWindowsController windows, CancellationToken token) =>
{
    if (level is < 0 or > 100) return Results.BadRequest("Volume must be 0-100.");
    await windows.SetVolumeAsync(level, token);
    return Results.Json(windows.State);
});
app.MapPost("/permissions/{name}/{enabled:bool}", async (string name, bool enabled) =>
{
    var document = JsonDocument.Parse(await File.ReadAllTextAsync(configPath));
    var root = JsonSerializer.Deserialize<Dictionary<string, object>>(document.RootElement.GetRawText()) ?? [];
    var current = JsonSerializer.Deserialize<HomePcConfig>(document.RootElement.GetRawText(), jsonOptions) ?? throw new InvalidDataException();
    root["permissions"] = new PermissionConfig
    {
        AllowSleep = name == "sleep" ? enabled : current.Permissions.AllowSleep,
        AllowRestart = name == "restart" ? enabled : current.Permissions.AllowRestart,
        AllowShutdown = name == "shutdown" ? enabled : current.Permissions.AllowShutdown,
        AllowPowerPlanChange = current.Permissions.AllowPowerPlanChange,
        AllowProcessClose = current.Permissions.AllowProcessClose,
    };
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
    var issue = ConfigurationValidator.Validate(candidate).FirstOrDefault(candidateIssue => candidateIssue.Path.StartsWith($"routines.{id}", StringComparison.Ordinal));
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
    var current = ConfigurationSecurity.Unprotect(JsonSerializer.Deserialize<HomePcConfig>(await File.ReadAllTextAsync(configPath, token), jsonOptions) ?? throw new InvalidDataException());
    var registry = new ActionRegistry(windows, current);
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
static string ToWire(ActionId id) => string.Concat(id.ToString().Select((character, index) => char.IsUpper(character) && index > 0 ? "_" + char.ToLowerInvariant(character) : char.ToLowerInvariant(character).ToString()));
