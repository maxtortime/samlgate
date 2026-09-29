using System.Text;

namespace Samlgate.Tests;

internal static class TestSaml
{
    public const string Provider = "arn:aws:iam::123456789012:saml-provider/Entra";
    public const string DeveloperRole = "arn:aws:iam::123456789012:role/Developer";
    public const string ReadOnlyRole = "arn:aws:iam::123456789012:role/team/ReadOnly";

    /// <summary>A minimal SAML 2.0 response with the AWS attributes (unsigned — parsing only).</summary>
    public static string Response(IEnumerable<string> roleValues, int? sessionDuration = null)
    {
        var roles = string.Concat(roleValues.Select(v => $"<saml:AttributeValue>{v}</saml:AttributeValue>"));
        var duration = sessionDuration is { } seconds
            ? $"<saml:Attribute Name=\"https://aws.amazon.com/SAML/Attributes/SessionDuration\"><saml:AttributeValue>{seconds}</saml:AttributeValue></saml:Attribute>"
            : string.Empty;
        var xml = $"""
            <samlp:Response xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion">
              <saml:Assertion>
                <saml:AttributeStatement>
                  <saml:Attribute Name="https://aws.amazon.com/SAML/Attributes/RoleSessionName"><saml:AttributeValue>alice@example.com</saml:AttributeValue></saml:Attribute>
                  <saml:Attribute Name="https://aws.amazon.com/SAML/Attributes/Role">{roles}</saml:Attribute>
                  {duration}
                </saml:AttributeStatement>
              </saml:Assertion>
            </samlp:Response>
            """;
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(xml));
    }

    public static string Response(params string[] roleValues) => Response(roleValues, null);
}

internal sealed class TempDir : IDisposable
{
    public string Path { get; } =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "samlgate-tests", Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Path);

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // A browser child process may still hold a file briefly
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above on Windows
        }
    }
}
