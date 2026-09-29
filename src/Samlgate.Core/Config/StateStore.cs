namespace Samlgate.Config;

/// <summary>Small persisted state that is not user configuration — currently the last role picked per account.</summary>
public sealed class StateStore(string stateFile)
{
    private const string RolesSection = "last_role";

    public static StateStore Default => new(AppPaths.StateFile);

    public string? GetLastRole(string account) =>
        IniFile.ReadSection(stateFile, RolesSection) is { } roles && roles.TryGetValue(account, out var arn) ? arn : null;

    public void SetLastRole(string account, string roleArn)
    {
        var roles = IniFile.ReadSection(stateFile, RolesSection) ?? new Dictionary<string, string>();
        if (roles.TryGetValue(account, out var existing) && existing == roleArn)
        {
            return;
        }

        roles[account] = roleArn;
        IniFile.WriteSection(stateFile, RolesSection, roles.Select(r => (r.Key, r.Value)).ToList());
    }
}
