namespace Samlgate.Aws;

/// <summary>
/// AWS SAML assertion consumer service (ACS) endpoints. The IdP makes the browser POST the
/// SAMLResponse to one of these; samlgate intercepts that request instead of letting it reach AWS.
/// </summary>
public static class AcsEndpoints
{
    /// <summary>Hosts per partition. Regional endpoints are &lt;region&gt;.signin.aws.amazon.com.</summary>
    private static readonly string[] Hosts =
    [
        "signin.aws.amazon.com",
        "signin.amazonaws.cn",
        "signin.amazonaws-us-gov.com",
    ];

    public static bool IsAcs(Uri uri, string? targetUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(targetUrl) &&
            Uri.TryCreate(targetUrl, UriKind.Absolute, out var target) &&
            uri.Host.Equals(target.Host, StringComparison.OrdinalIgnoreCase) &&
            uri.AbsolutePath.StartsWith(target.AbsolutePath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (uri.Scheme != Uri.UriSchemeHttps || !uri.AbsolutePath.StartsWith("/saml", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Hosts.Any(host =>
            uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Chrome DevTools Fetch.enable URL patterns (glob: * and ?) covering every ACS endpoint.</summary>
    public static IReadOnlyList<string> FetchUrlPatterns(string? targetUrl = null)
    {
        var patterns = Hosts
            .SelectMany(host => new[] { $"https://{host}/saml*", $"https://*.{host}/saml*" })
            .ToList();

        if (!string.IsNullOrWhiteSpace(targetUrl))
        {
            patterns.Add(targetUrl.TrimEnd('*') + "*");
        }

        return patterns;
    }
}
