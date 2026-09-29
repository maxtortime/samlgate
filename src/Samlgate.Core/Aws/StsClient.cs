using Amazon;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;

namespace Samlgate.Aws;

/// <summary>Temporary credentials returned by STS (and stored in / read from the credentials file).</summary>
public sealed record AwsSessionCredentials(
    string AccessKeyId,
    string SecretAccessKey,
    string SessionToken,
    DateTimeOffset Expiration,
    string PrincipalArn);

/// <summary>STS AssumeRoleWithSAML. The call is unsigned — the SAML assertion is the credential.</summary>
public sealed class StsClient(Func<RegionEndpoint, IAmazonSecurityTokenService>? clientFactory = null)
{
    public const int DefaultDurationSeconds = 3600;

    private readonly Func<RegionEndpoint, IAmazonSecurityTokenService> clientFactory = clientFactory ?? CreateClient;

    public async Task<AwsSessionCredentials> AssumeRoleWithSamlAsync(
        SamlAssertion assertion, AwsSamlRole role, string? region, int? requestedDuration,
        CancellationToken cancellationToken = default)
    {
        using var client = clientFactory(RegionEndpoint.GetBySystemName(RegionFor(role.Partition, region)));
        var duration = requestedDuration ?? assertion.SessionDurationSeconds ?? DefaultDurationSeconds;

        try
        {
            return await CallAsync(client, assertion, role, duration, cancellationToken);
        }
        catch (AmazonSecurityTokenServiceException e) when (e.ErrorCode == "ValidationError" && duration > DefaultDurationSeconds)
        {
            // Asking for more than the role's MaxSessionDuration is rejected; retry with the 1 hour default
            return await CallAsync(client, assertion, role, DefaultDurationSeconds, cancellationToken);
        }
    }

    internal static string RegionFor(string partition, string? region) =>
        !string.IsNullOrWhiteSpace(region)
            ? region
            : partition switch
            {
                "aws-cn" => "cn-north-1",
                "aws-us-gov" => "us-gov-west-1",
                _ => "us-east-1",
            };

    private static AmazonSecurityTokenServiceClient CreateClient(RegionEndpoint region) =>
        new(new AnonymousAWSCredentials(), new AmazonSecurityTokenServiceConfig
        {
            RegionEndpoint = region,
            Timeout = TimeSpan.FromSeconds(30),
        });

    private static async Task<AwsSessionCredentials> CallAsync(
        IAmazonSecurityTokenService client, SamlAssertion assertion, AwsSamlRole role, int durationSeconds,
        CancellationToken cancellationToken)
    {
        var response = await client.AssumeRoleWithSAMLAsync(new AssumeRoleWithSAMLRequest
        {
            RoleArn = role.RoleArn,
            PrincipalArn = role.PrincipalArn,
            SAMLAssertion = assertion.Base64Response,
            DurationSeconds = durationSeconds,
        }, cancellationToken);

        var credentials = response.Credentials ?? throw new SamlgateException("STS returned no credentials.");
        var expiration = credentials.Expiration ?? throw new SamlgateException("STS response is missing Expiration.");

        return new AwsSessionCredentials(
            credentials.AccessKeyId,
            credentials.SecretAccessKey,
            credentials.SessionToken,
            new DateTimeOffset(expiration.ToUniversalTime()),
            response.AssumedRoleUser?.Arn is { Length: > 0 } arn ? arn : role.RoleArn);
    }
}
