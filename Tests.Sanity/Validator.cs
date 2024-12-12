using Apps.Sanity.Connections;
using Blackbird.Applications.Sdk.Common.Authentication;
using FluentAssertions;
using Tests.Sanity.Base;

namespace Tests.Sanity;

[TestClass]
public class Validator : TestBase
{
    [TestMethod]
    public async Task ValidatesCorrectConnection()
    {
        var validator = new ConnectionValidator();

        var result = await validator.ValidateConnection(Creds, CancellationToken.None);
        result.IsValid.Should().Be(true);
    }

    [TestMethod]
    public async Task DoesNotValidateIncorrectConnection()
    {
        var validator = new ConnectionValidator();

        var newCreds = Creds.Select(x => new AuthenticationCredentialsProvider(x.KeyName, x.Value + "_incorrect"));
        var result = await validator.ValidateConnection(newCreds, CancellationToken.None);
        result.IsValid.Should().Be(false);
    }
}