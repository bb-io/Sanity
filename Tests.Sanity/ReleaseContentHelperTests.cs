using Apps.Sanity.Utils;
using FluentAssertions;

namespace Tests.Sanity;

[TestClass]
public class ReleaseContentHelperTests
{
    [TestMethod]
    public void GetPublishedId_WithDraftId_ReturnsPublishedId()
    {
        ReleaseContentHelper.GetPublishedId("drafts.article-123")
            .Should()
            .Be("article-123");
    }

    [TestMethod]
    public void GetPublishedId_WithReleaseVersionId_ReturnsPublishedId()
    {
        ReleaseContentHelper.GetPublishedId("versions.releaseA.article-123")
            .Should()
            .Be("article-123");
    }

    [TestMethod]
    public void BuildVersionId_WithDraftId_UsesPublishedId()
    {
        ReleaseContentHelper.BuildVersionId("releaseA", "drafts.article-123")
            .Should()
            .Be("versions.releaseA.article-123");
    }

    [TestMethod]
    public void BuildVersionId_WithPublishedId_UsesReleasePrefix()
    {
        ReleaseContentHelper.BuildVersionId("releaseA", "article-123")
            .Should()
            .Be("versions.releaseA.article-123");
    }
}
