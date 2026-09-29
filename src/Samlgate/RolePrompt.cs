using System.Globalization;
using Samlgate.Aws;

namespace Samlgate;

/// <summary>Numbered role list on stderr; Enter keeps the last used role.</summary>
internal static class RolePrompt
{
    public static AwsSamlRole Pick(IReadOnlyList<AwsSamlRole> roles, AwsSamlRole? last)
    {
        var defaultIndex = last is null ? -1 : roles.ToList().IndexOf(last);

        Console.Error.WriteLine();
        Console.Error.WriteLine("Select a role:");
        for (var i = 0; i < roles.Count; i++)
        {
            var marker = i == defaultIndex ? "*" : " ";
            Console.Error.WriteLine($" {marker} [{i + 1}] {roles[i].DisplayName}  {roles[i].RoleArn}");
        }

        while (true)
        {
            Console.Error.Write(defaultIndex >= 0
                ? $"Role number (Enter = {defaultIndex + 1}): "
                : "Role number: ");

            var input = Console.ReadLine();
            if (input is null)
            {
                throw new SamlgateException("No role selected.");
            }

            if (input.Trim().Length == 0 && defaultIndex >= 0)
            {
                return roles[defaultIndex];
            }

            if (int.TryParse(input.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number) &&
                number >= 1 && number <= roles.Count)
            {
                return roles[number - 1];
            }
        }
    }
}
