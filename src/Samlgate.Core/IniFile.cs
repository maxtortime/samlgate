namespace Samlgate;

/// <summary>
/// Just enough INI handling for ~/.aws/credentials and saml2aws-style config files.
/// Writes replace a single section in place and leave every other line (profiles, comments) untouched.
/// </summary>
public static class IniFile
{
    /// <summary>Reads one section's key/value pairs. Returns null if the file or the section does not exist.</summary>
    public static Dictionary<string, string>? ReadSection(string path, string section)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        Dictionary<string, string>? current = null;
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';'))
            {
                continue;
            }

            if (IsHeader(line))
            {
                if (current is not null)
                {
                    return current;
                }

                if (HeaderName(line).Equals(section, StringComparison.OrdinalIgnoreCase))
                {
                    current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                continue;
            }

            var separator = line.IndexOf('=');
            if (current is not null && separator > 0)
            {
                current[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }
        }

        return current;
    }

    /// <summary>
    /// Replaces a whole section (appending it if missing). Other sections and comments are preserved.
    /// The file is written to a temporary file first and then moved into place.
    /// </summary>
    public static void WriteSection(
        string path, string section, IReadOnlyList<(string Key, string Value)> values, bool ownerOnly = false)
    {
        var lines = File.Exists(path) ? new List<string>(File.ReadAllLines(path)) : [];

        // Find the target header and the next header (= end of the target section)
        var start = -1;
        var end = lines.Count;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (!IsHeader(line))
            {
                continue;
            }

            if (start >= 0)
            {
                end = i;
                break;
            }

            if (HeaderName(line).Equals(section, StringComparison.OrdinalIgnoreCase))
            {
                start = i;
            }
        }

        var body = values.Select(v => $"{v.Key} = {v.Value}").ToList();

        if (start < 0)
        {
            if (lines.Count > 0 && lines[^1].Trim().Length > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add($"[{section}]");
            lines.AddRange(body);
        }
        else
        {
            if (end < lines.Count)
            {
                body.Add(string.Empty); // blank line before the following section
            }

            lines.RemoveRange(start + 1, end - start - 1);
            lines.InsertRange(start + 1, body);
        }

        WriteAtomically(path, lines, ownerOnly);
    }

    private static void WriteAtomically(string path, List<string> lines, bool ownerOnly)
    {
        // Write through symlinks (dotfile setups link ~/.aws/credentials) instead of replacing the link with a file
        if (File.Exists(path) && new FileInfo(path).ResolveLinkTarget(returnFinalTarget: true) is { } target)
        {
            path = target.FullName;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);

        var temp = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        File.WriteAllLines(temp, lines);
        if (ownerOnly && !OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        File.Move(temp, path, overwrite: true);
    }

    private static bool IsHeader(string line) => line.StartsWith('[') && line.EndsWith(']');

    private static string HeaderName(string line) => line[1..^1].Trim();
}
