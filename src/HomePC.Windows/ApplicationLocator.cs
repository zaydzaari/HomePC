using Microsoft.Win32;

namespace HomePC.Windows;

public sealed class ApplicationLocator(IReadOnlyDictionary<string, string> overrides)
{
    public IReadOnlyDictionary<string, string?> DetectAll() => new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
    {
        ["notepad"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "notepad.exe"),
        ["steam"] = Find("steam", FindSteam), ["discord"] = Find("discord", FindDiscord), ["chrome"] = Find("chrome", FindChrome),
    };

    private string? Find(string name, Func<IEnumerable<string?>> candidates)
    {
        if (overrides.TryGetValue(name, out var configured) && !string.IsNullOrWhiteSpace(configured))
            return File.Exists(Environment.ExpandEnvironmentVariables(configured)) ? Environment.ExpandEnvironmentVariables(configured) : null;
        return candidates().Where(path => !string.IsNullOrWhiteSpace(path)).FirstOrDefault(File.Exists);
    }

    private static IEnumerable<string?> FindSteam()
    {
        if (!OperatingSystem.IsWindows()) yield break;
        yield return ReadRegistry(Registry.CurrentUser, @"SOFTWARE\Valve\Steam", "SteamExe");
        yield return ReadRegistry(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath") is { } p ? Path.Combine(p, "steam.exe") : null;
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steam.exe");
    }

    private static IEnumerable<string?> FindDiscord()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(local, "Discord", "Update.exe");
        yield return Directory.Exists(Path.Combine(local, "Discord"))
            ? Directory.EnumerateFiles(Path.Combine(local, "Discord"), "Discord.exe", SearchOption.AllDirectories).OrderDescending().FirstOrDefault() : null;
    }

    private static IEnumerable<string?> FindChrome()
    {
        if (!OperatingSystem.IsWindows()) yield break;
        yield return ReadRegistry(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe", null);
        yield return ReadRegistry(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe", null);
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe");
    }

    private static string? ReadRegistry(RegistryKey root, string key, string? value)
    {
        if (!OperatingSystem.IsWindows()) return null;
        using var subkey = root.OpenSubKey(key);
        return subkey?.GetValue(value) as string;
    }
}
