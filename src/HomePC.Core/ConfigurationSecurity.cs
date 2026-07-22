using System.Security.Cryptography;
using System.Text;

namespace HomePC.Core;

public static class ConfigurationSecurity
{
    private const string Prefix = "dpapi:v1:";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("HomePC.Configuration.v1");

    public static bool IsProtected(string value) => value.StartsWith(Prefix, StringComparison.Ordinal);

    public static string Protect(string plaintext)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("DPAPI protection requires Windows.");
        if (string.IsNullOrWhiteSpace(plaintext)) throw new ArgumentException("Secret cannot be empty.", nameof(plaintext));
        var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plaintext), Entropy, DataProtectionScope.CurrentUser);
        return Prefix + Convert.ToBase64String(protectedBytes);
    }

    public static string Unprotect(string value)
    {
        if (!IsProtected(value)) return value;
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("DPAPI protection requires Windows.");
        var bytes = Convert.FromBase64String(value[Prefix.Length..]);
        return Encoding.UTF8.GetString(ProtectedData.Unprotect(bytes, Entropy, DataProtectionScope.CurrentUser));
    }

    public static HomePcConfig Unprotect(HomePcConfig config)
    {
        config.DeviceToken = Unprotect(config.DeviceToken);
        config.AdminToken = Unprotect(config.AdminToken);
        return config;
    }
}
