namespace Samlgate.Tests;

public class IniFileTests
{
    [Fact]
    public void WriteSection_ReplacesOnlyTargetSection_AndKeepsOthers()
    {
        using var dir = new TempDir();
        var path = dir.File("credentials");
        File.WriteAllText(path, """
            # managed by hand
            [default]
            aws_access_key_id = AKIADEFAULT

            [saml]
            aws_access_key_id = OLD
            stale_key = x

            [other]
            region = us-west-2
            """);

        IniFile.WriteSection(path, "saml", [("aws_access_key_id", "NEW")]);

        Assert.Contains("# managed by hand", File.ReadAllText(path));
        Assert.Equal("AKIADEFAULT", IniFile.ReadSection(path, "default")!["aws_access_key_id"]);
        Assert.Equal("NEW", IniFile.ReadSection(path, "saml")!["aws_access_key_id"]);
        Assert.False(IniFile.ReadSection(path, "saml")!.ContainsKey("stale_key"));
        Assert.Equal("us-west-2", IniFile.ReadSection(path, "other")!["region"]);
    }

    [Fact]
    public void WriteSection_AppendsMissingSection_AndCreatesDirectory()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "nested", "credentials");

        IniFile.WriteSection(path, "saml", [("a", "1")]);
        IniFile.WriteSection(path, "second", [("b", "2")]);

        Assert.Equal("1", IniFile.ReadSection(path, "saml")!["a"]);
        Assert.Equal("2", IniFile.ReadSection(path, "second")!["b"]);
    }

    [Fact]
    public void ReadSection_MissingFileOrSection_ReturnsNull()
    {
        using var dir = new TempDir();
        Assert.Null(IniFile.ReadSection(dir.File("nope"), "saml"));

        File.WriteAllText(dir.File("f"), "[a]\nx = 1\n");
        Assert.Null(IniFile.ReadSection(dir.File("f"), "b"));
    }

    [Fact]
    public void ReadSection_IsCaseInsensitive_AndTrimsValues()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("f"), "[ Default ]\nURL   =  https://x/?a=b  \n");

        Assert.Equal("https://x/?a=b", IniFile.ReadSection(dir.File("f"), "default")!["url"]);
    }
}
