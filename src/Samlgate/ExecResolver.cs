using System.Diagnostics;

namespace Samlgate;

/// <summary>
/// Builds the ProcessStartInfo for 'samlgate exec'. On Windows, many CLIs are .cmd/.bat shims
/// (npm-installed tools, some AWS CLI setups) that Process.Start cannot run directly, so resolve
/// the command through PATH/PATHEXT and route batch files through cmd.exe.
/// </summary>
internal static class ExecResolver
{
    public static ProcessStartInfo CreateStartInfo(IReadOnlyList<string> command)
    {
        var executable = OperatingSystem.IsWindows() ? ResolveWindows(command[0]) : command[0];
        var isBatch = OperatingSystem.IsWindows() &&
            (executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
             executable.EndsWith(".bat", StringComparison.OrdinalIgnoreCase));

        var startInfo = new ProcessStartInfo(isBatch ? "cmd.exe" : executable) { UseShellExecute = false };
        if (isBatch)
        {
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(executable);
        }

        foreach (var argument in command.Skip(1))
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static string ResolveWindows(string name)
    {
        if (Path.IsPathRooted(name) || name.Contains('\\') || name.Contains('/'))
        {
            return name;
        }

        var extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
            .Split(';', StringSplitOptions.RemoveEmptyEntries);
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var candidates = Path.HasExtension(name) ? [name] : extensions.Select(ext => name + ext.ToLowerInvariant());

        foreach (var directory in directories)
        {
            foreach (var candidate in candidates)
            {
                var path = Path.Combine(directory, candidate);
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        return name;
    }
}
