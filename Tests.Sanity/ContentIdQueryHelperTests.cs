using Apps.Sanity.Utils;
using FluentAssertions;

namespace Tests.Sanity;

[TestClass]
public class ContentIdQueryHelperTests
{
    [TestMethod]
    public void ExtractExactContentIds_WithMultipleIds_ReturnsAllIds()
    {
        const string query = "_id == \"versions.releaseA.doc-1\" || _id == \"doc-2\"";

        var ids = ContentIdQueryHelper.ExtractExactContentIds(query);

        ids.Should().BeEquivalentTo(["versions.releaseA.doc-1", "doc-2"]);
    }

    [TestMethod]
    public void RequiresRawPerspective_WithVersionIdQuery_ReturnsTrue()
    {
        ContentIdQueryHelper.RequiresRawPerspective("_id == \"versions.releaseA.doc-1\"", false)
            .Should()
            .BeTrue();
    }

    [TestMethod]
    public void RequiresRawPerspective_WithDraftIdQuery_ReturnsTrue()
    {
        ContentIdQueryHelper.RequiresRawPerspective("_id == \"drafts.doc-1\"", false)
            .Should()
            .BeTrue();
    }

    [TestMethod]
    public void RequiresRawPerspective_WithPublishedIdQuery_ReturnsFalse()
    {
        ContentIdQueryHelper.RequiresRawPerspective("_id == \"doc-1\"", false)
            .Should()
            .BeFalse();
    }
}
