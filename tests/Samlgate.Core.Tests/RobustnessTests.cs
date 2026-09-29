using System.Diagnostics;

namespace Samlgate.Tests;

public class RobustnessTests
{
    [Fact]
    public async Task LoginLock_SecondAcquireWaitsForFirst()
    {
        using var dir = new TempDir();
        var lockFile = dir.File("login.lock");
        var waited = false;

        var first = await LoginLock.AcquireAsync(lockFile, TimeSpan.FromSeconds(5));
        var stopwatch = Stopwatch.StartNew();
        var second = LoginLock.AcquireAsync(lockFile, TimeSpan.FromSeconds(5), _ => waited = true);

        await Task.Delay(600);
        Assert.False(second.IsCompleted);
        first.Dispose();

        (await second).Dispose();
        Assert.True(waited);
        Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public async Task LoginLock_TimesOutWithFriendlyError()
    {
        using var dir = new TempDir();
        using var held = await LoginLock.AcquireAsync(dir.File("login.lock"), TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<SamlgateException>(() =>
            LoginLock.AcquireAsync(dir.File("login.lock"), TimeSpan.FromMilliseconds(300)));
    }

    [Fact]
    public void WriteSection_WritesThroughSymlink()
    {
        using var dir = new TempDir();
        var target = dir.File("real-credentials");
        var link = dir.File("credentials");
        File.WriteAllText(target, "[other]\nx = 1\n");
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return; // Windows without Developer Mode cannot create symlinks
        }

        IniFile.WriteSection(link, "saml", [("a", "1")]);

        Assert.NotNull(new FileInfo(link).LinkTarget);
        Assert.Equal("1", IniFile.ReadSection(target, "saml")!["a"]);
        Assert.Equal("1", IniFile.ReadSection(target, "other")!["x"]);
    }
}
