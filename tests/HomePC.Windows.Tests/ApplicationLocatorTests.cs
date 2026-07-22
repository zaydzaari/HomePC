using HomePC.Windows;
using Xunit;

namespace HomePC.Windows.Tests;

public sealed class ApplicationLocatorTests
{
    [Fact] public void InvalidOverrideIsNotReportedAsDetected()
    {
        var detected = new ApplicationLocator(new Dictionary<string, string> { ["steam"] = @"Z:\definitely\missing\steam.exe" }).DetectAll();
        Assert.Null(detected["steam"]); Assert.True(detected.ContainsKey("notepad"));
    }
}
