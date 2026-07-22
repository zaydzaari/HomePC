using System.Text.Json;
using HomePC.Core;

namespace HomePC.Agent;

public sealed class AuditWriter(HomePcConfig config)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    public async Task WriteAsync(string eventName, string detail, CancellationToken cancellationToken)
    {
        var path = Path.GetFullPath(config.AuditLogPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(path) && new FileInfo(path).Length > 2_000_000)
            {
                var archive = path + ".1"; if (File.Exists(archive)) File.Delete(archive); File.Move(path, archive);
            }
            var line = JsonSerializer.Serialize(new { at = DateTimeOffset.UtcNow, @event = eventName, detail = detail.Length > 256 ? detail[..256] : detail });
            await File.AppendAllTextAsync(path, line + Environment.NewLine, cancellationToken);
        }
        finally { _gate.Release(); }
    }
}

