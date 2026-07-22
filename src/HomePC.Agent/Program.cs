using System.Text.Json;
using HomePC.Agent;
using HomePC.Core;
using HomePC.Windows;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "HomePC Agent");
var configPath = ConfigLoader.ResolvePath(args);
if (args.Contains("--protect-config", StringComparer.Ordinal))
{
    ConfigLoader.Protect(configPath);
    Console.WriteLine($"Protected device and admin tokens for the current Windows user: {configPath}");
    return;
}
var config = ConfigLoader.Load(configPath);
var issues = ConfigurationValidator.Validate(config);
if (issues.Count > 0) throw new InvalidOperationException("Invalid configuration: " + string.Join("; ", issues.Select(i => $"{i.Path}: {i.Message}")));
if (args.Contains("--validate-config", StringComparer.Ordinal))
{
    Console.WriteLine($"Configuration valid: {configPath}");
    return;
}
builder.Services.AddSingleton(config);
builder.Services.AddSingleton<IWindowsController, NativeWindowsController>();
builder.Services.AddSingleton<ActionRegistry>();
builder.Services.AddSingleton<CommandGuard>();
builder.Services.AddSingleton<AuditWriter>();
builder.Services.AddHostedService<AgentWorker>();
await builder.Build().RunAsync();

namespace HomePC.Agent
{
    internal static class ConfigLoader
    {
        private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip };
        public static string ResolvePath(string[] args)
        {
            var index = Array.IndexOf(args, "--config");
            var supplied = index >= 0 && index + 1 < args.Length ? args[index + 1] : Environment.GetEnvironmentVariable("HOMEPC_CONFIG") ?? "generated/homepc.json";
            if (Path.IsPathFullyQualified(supplied)) return supplied;
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory is not null)
            {
                var candidate = Path.GetFullPath(Path.Combine(directory.FullName, supplied));
                if (File.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
            return Path.GetFullPath(supplied);
        }
        public static HomePcConfig Load(string path) => ConfigurationSecurity.Unprotect(
            JsonSerializer.Deserialize<HomePcConfig>(File.ReadAllText(path), Json) ?? throw new InvalidDataException("Configuration is empty."));

        public static void Protect(string path)
        {
            var config = JsonSerializer.Deserialize<HomePcConfig>(File.ReadAllText(path), Json) ?? throw new InvalidDataException("Configuration is empty.");
            if (!ConfigurationSecurity.IsProtected(config.DeviceToken)) config.DeviceToken = ConfigurationSecurity.Protect(config.DeviceToken);
            if (!ConfigurationSecurity.IsProtected(config.AdminToken)) config.AdminToken = ConfigurationSecurity.Protect(config.AdminToken);
            var output = new JsonSerializerOptions(Json) { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            File.WriteAllText(path, JsonSerializer.Serialize(config, output));
        }
    }
}
