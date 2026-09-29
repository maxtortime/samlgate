using Samlgate.Aws;

namespace Samlgate.Tests;

public class SamlAssertionTests
{
    [Fact]
    public void Parse_ReadsRolesInEitherOrder_AndSessionDuration()
    {
        var response = TestSaml.Response(
            [$"{TestSaml.DeveloperRole},{TestSaml.Provider}", $"{TestSaml.Provider},{TestSaml.ReadOnlyRole}"],
            sessionDuration: 28800);

        var assertion = SamlAssertion.Parse(response);

        Assert.Equal(2, assertion.Roles.Count);
        Assert.All(assertion.Roles, r => Assert.Equal(TestSaml.Provider, r.PrincipalArn));
        Assert.Equal(TestSaml.ReadOnlyRole, assertion.Roles[1].RoleArn);
        Assert.Equal(28800, assertion.SessionDurationSeconds);
        Assert.Equal(response, assertion.Base64Response);
    }

    [Fact]
    public void Parse_WithoutRoles_Throws()
    {
        Assert.Throws<SamlgateException>(() => SamlAssertion.Parse(TestSaml.Response("not-a-role-pair")));
    }

    [Fact]
    public void Parse_Garbage_ThrowsFriendlyError()
    {
        Assert.Throws<SamlgateException>(() => SamlAssertion.Parse("%%%not base64"));
    }

    [Theory]
    [InlineData("arn:aws:iam::123456789012:role/team/ReadOnly", "ReadOnly", "123456789012", "aws")]
    [InlineData("arn:aws-cn:iam::111122223333:role/Admin", "Admin", "111122223333", "aws-cn")]
    [InlineData("arn:aws-us-gov:iam::444455556666:role/Ops", "Ops", "444455556666", "aws-us-gov")]
    public void Role_ExposesNameAccountAndPartition(string arn, string name, string account, string partition)
    {
        var role = new AwsSamlRole(arn, TestSaml.Provider);
        Assert.Equal(name, role.RoleName);
        Assert.Equal(account, role.AccountId);
        Assert.Equal(partition, role.Partition);
        Assert.Equal($"{name} ({account})", role.DisplayName);
    }
}
