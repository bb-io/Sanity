using System.Reflection;
using Apps.Sanity.Services;
using FluentAssertions;
using Newtonsoft.Json.Linq;

namespace Tests.Sanity;

[TestClass]
public class ReleaseServiceTests
{
    [TestMethod]
    public void CreateReleaseVersionAction_ForNewVersion_IncludesPublishedId()
    {
        var document = new JObject
        {
            ["_id"] = "versions.release.mainPage"
        };

        var action = InvokeCreateReleaseVersionAction(document, []);

        action["actionType"]?.ToString().Should().Be("sanity.action.document.version.create");
        action["publishedId"]?.ToString().Should().Be("mainPage");
    }

    [TestMethod]
    public void CreateReleaseVersionAction_ForExistingVersion_DoesNotIncludePublishedId()
    {
        var versionId = "versions.release.translation.metadata.mainPage";
        var document = new JObject
        {
            ["_id"] = versionId
        };

        var action = InvokeCreateReleaseVersionAction(document, [versionId]);

        action["actionType"]?.ToString().Should().Be("sanity.action.document.version.replace");
        action["publishedId"].Should().BeNull();
    }

    private static JObject InvokeCreateReleaseVersionAction(JObject document, HashSet<string> existingDocumentIds)
    {
        var method = typeof(ReleaseService).GetMethod(
            "CreateReleaseVersionAction",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull();

        return ((JObject?)method!.Invoke(null, [document, existingDocumentIds]))!;
    }
}
