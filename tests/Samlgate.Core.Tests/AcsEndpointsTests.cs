using Samlgate.Aws;

namespace Samlgate.Tests;

public class AcsEndpointsTests
{
    [Theory]
    [InlineData("https://signin.aws.amazon.com/saml", true)]
    [InlineData("https://signin.aws.amazon.com/saml/acs/SAMLSPD7MKW", true)]
    [InlineData("https://ap-northeast-2.signin.aws.amazon.com/saml", true)]
    [InlineData("https://signin.amazonaws.cn/saml", true)]
    [InlineData("https://signin.amazonaws-us-gov.com/saml", true)]
    [InlineData("http://signin.aws.amazon.com/saml", false)]
    [InlineData("https://signin.aws.amazon.com/console", false)]
    [InlineData("https://signin.aws.amazon.com.example.com/saml", false)]
    [InlineData("https://login.microsoftonline.com/saml", false)]
    public void IsAcs_MatchesOnlyAwsAcsEndpoints(string url, bool expected)
    {
        Assert.Equal(expected, AcsEndpoints.IsAcs(new Uri(url)));
    }

    [Fact]
    public void IsAcs_HonoursTargetUrl()
    {
        const string target = "https://sso.internal.example/aws/saml";
        Assert.True(AcsEndpoints.IsAcs(new Uri("https://sso.internal.example/aws/saml?x=1"), target));
        Assert.False(AcsEndpoints.IsAcs(new Uri("https://sso.internal.example/other"), target));
    }

    [Fact]
    public void FetchUrlPatterns_CoverPartitionsAndTarget()
    {
        var patterns = AcsEndpoints.FetchUrlPatterns("https://sso.internal.example/aws/saml");
        Assert.Contains("https://signin.aws.amazon.com/saml*", patterns);
        Assert.Contains("https://*.signin.aws.amazon.com/saml*", patterns);
        Assert.Contains("https://signin.amazonaws.cn/saml*", patterns);
        Assert.Contains("https://sso.internal.example/aws/saml*", patterns);
    }
}
