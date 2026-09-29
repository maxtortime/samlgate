using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Samlgate.Aws;

/// <summary>One value of the AWS Role attribute: a role ARN paired with the SAML provider ARN that trusts it.</summary>
public sealed record AwsSamlRole(string RoleArn, string PrincipalArn)
{
    /// <summary>arn:aws:iam::123456789012:role/path/Developer → "Developer"</summary>
    public string RoleName => RoleArn[(RoleArn.LastIndexOf('/') + 1)..];

    /// <summary>12-digit account ID, or empty if the ARN is malformed.</summary>
    public string AccountId => RoleArn.Split(':') is { Length: > 4 } parts ? parts[4] : string.Empty;

    /// <summary>aws, aws-cn or aws-us-gov.</summary>
    public string Partition => RoleArn.Split(':') is { Length: > 1 } parts && parts[1].Length > 0 ? parts[1] : "aws";

    public string DisplayName => AccountId.Length > 0 ? $"{RoleName} ({AccountId})" : RoleName;
}

/// <summary>
/// The attributes samlgate needs from a SAMLResponse. The base64 response itself is kept because
/// STS AssumeRoleWithSAML takes it verbatim.
/// </summary>
public sealed class SamlAssertion
{
    private const string AssertionNamespace = "urn:oasis:names:tc:SAML:2.0:assertion";
    private const string RoleAttribute = "https://aws.amazon.com/SAML/Attributes/Role";
    private const string SessionDurationAttribute = "https://aws.amazon.com/SAML/Attributes/SessionDuration";

    public required string Base64Response { get; init; }
    public required IReadOnlyList<AwsSamlRole> Roles { get; init; }
    public int? SessionDurationSeconds { get; init; }

    public static SamlAssertion Parse(string base64Response)
    {
        XDocument document;
        try
        {
            var xml = Encoding.UTF8.GetString(Convert.FromBase64String(base64Response.Trim()));
            document = XDocument.Parse(xml);
        }
        catch (Exception e) when (e is FormatException or XmlException)
        {
            throw new SamlgateException("The SAML response is not valid base64-encoded XML.", e);
        }

        XNamespace ns = AssertionNamespace;
        var roles = new List<AwsSamlRole>();
        int? sessionDuration = null;

        foreach (var attribute in document.Descendants(ns + "Attribute"))
        {
            var name = (string?)attribute.Attribute("Name");
            var values = attribute.Elements(ns + "AttributeValue").Select(v => v.Value.Trim());

            if (name == RoleAttribute)
            {
                roles.AddRange(values.Select(TryParseRole).OfType<AwsSamlRole>());
            }
            else if (name == SessionDurationAttribute &&
                     int.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) && seconds > 0)
            {
                sessionDuration = seconds;
            }
        }

        if (roles.Count == 0)
        {
            throw new SamlgateException("The SAML response has no AWS Role attribute. Check that your user is assigned a role in the IdP's AWS app.");
        }

        return new SamlAssertion
        {
            Base64Response = base64Response.Trim(),
            Roles = roles.Distinct().ToList(),
            SessionDurationSeconds = sessionDuration,
        };
    }

    /// <summary>"arn:...:role/X,arn:...:saml-provider/Y" — the order of the two ARNs differs between IdPs.</summary>
    private static AwsSamlRole? TryParseRole(string value)
    {
        var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return null;
        }

        var roleArn = parts.FirstOrDefault(p => p.Contains(":role/", StringComparison.Ordinal));
        var principalArn = parts.FirstOrDefault(p => p.Contains(":saml-provider/", StringComparison.Ordinal));
        return roleArn is null || principalArn is null ? null : new AwsSamlRole(roleArn, principalArn);
    }
}
