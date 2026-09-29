namespace Samlgate;

/// <summary>Well-known file locations. Every path can be redirected with an environment variable.</summary>
public static class AppPaths
{
    public static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>samlgate account config (saml2aws-compatible INI). SAMLGATE_CONFIG overrides.</summary>
    public static string ConfigFile =>
        Environment.GetEnvironmentVariable("SAMLGATE_CONFIG") is { Length: > 0 } path
            ? ExpandHome(path)
            : Path.Combine(Home, ".samlgate");

    /// <summary>saml2aws config, read as a fallback so existing users can switch without migrating.</summary>
    public static string Saml2AwsConfigFile =>
        Environment.GetEnvironmentVariable("SAML2AWS_CONFIGFILE") is { Length: > 0 } path
            ? ExpandHome(path)
            : Path.Combine(Home, ".saml2aws");

    /// <summary>AWS shared credentials file (AWS_SHARED_CREDENTIALS_FILE overrides, like the AWS CLI).</summary>
    public static string DefaultCredentialsFile =>
        Environment.GetEnvironmentVariable("AWS_SHARED_CREDENTIALS_FILE") is { Length: > 0 } path
            ? ExpandHome(path)
            : Path.Combine(Home, ".aws", "credentials");

    /// <summary>
    /// Per-user data (browser profiles, remembered role choices). SAMLGATE_DATA_DIR overrides.
    /// Windows: %LOCALAPPDATA%\samlgate, macOS: ~/Library/Application Support/samlgate, Linux: ~/.local/share/samlgate.
    /// </summary>
    public static string DataDir =>
        Environment.GetEnvironmentVariable("SAMLGATE_DATA_DIR") is { Length: > 0 } path
            ? ExpandHome(path)
            : Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create), "samlgate");

    public static string StateFile => Path.Combine(DataDir, "state");

    public static string ExpandHome(string path)
    {
        if (path == "~")
        {
            return Home;
        }

        if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith(@"~\", StringComparison.Ordinal))
        {
            return Path.Combine(Home, path[2..]);
        }

        return path;
    }
}
