using System.Globalization;

namespace Samlgate.Aws;

/// <summary>
/// Reads and writes a profile in the AWS shared credentials file using the same keys saml2aws writes
/// (including x_principal_arn and x_security_token_expires), so tools that read those keep working.
/// </summary>
public static class CredentialsFile
{
    private const string ExpiresKey = "x_security_token_expires";
    private const string PrincipalKey = "x_principal_arn";

    public static void Write(string path, string profile, AwsSessionCredentials credentials)
    {
        IniFile.WriteSection(path, profile,
        [
            ("aws_access_key_id", credentials.AccessKeyId),
            ("aws_secret_access_key", credentials.SecretAccessKey),
            ("aws_session_token", credentials.SessionToken),
            ("aws_security_token", credentials.SessionToken),
            (PrincipalKey, credentials.PrincipalArn),
            (ExpiresKey, credentials.Expiration.ToLocalTime().ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture)),
        ], ownerOnly: true);
    }

    /// <summary>Stored credentials, or null when the profile is missing or was not written by samlgate/saml2aws.</summary>
    public static AwsSessionCredentials? Read(string path, string profile)
    {
        if (IniFile.ReadSection(path, profile) is not { } section ||
            !section.TryGetValue("aws_access_key_id", out var accessKeyId) ||
            !section.TryGetValue("aws_secret_access_key", out var secretAccessKey) ||
            !section.TryGetValue(ExpiresKey, out var rawExpires) ||
            !DateTimeOffset.TryParse(rawExpires, CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiration))
        {
            return null;
        }

        var sessionToken = section.GetValueOrDefault("aws_session_token") ?? section.GetValueOrDefault("aws_security_token");
        if (string.IsNullOrEmpty(sessionToken))
        {
            return null;
        }

        return new AwsSessionCredentials(accessKeyId, secretAccessKey, sessionToken, expiration,
            section.GetValueOrDefault(PrincipalKey) ?? string.Empty);
    }

    /// <summary>Usable for at least <paramref name="margin"/> more.</summary>
    public static bool IsValid(this AwsSessionCredentials credentials, TimeSpan margin) =>
        credentials.Expiration - DateTimeOffset.Now > margin;

    /// <summary>arn:aws:sts::123456789012:assumed-role/Developer/alice → "Developer (123456789012)"</summary>
    public static string DescribePrincipal(string principalArn)
    {
        var parts = principalArn.Split(':');
        if (parts.Length < 6)
        {
            return principalArn;
        }

        var resource = parts[5].Split('/');
        var name = resource.Length >= 2 ? resource[1] : parts[5];
        return parts[4].Length > 0 ? $"{name} ({parts[4]})" : name;
    }
}
