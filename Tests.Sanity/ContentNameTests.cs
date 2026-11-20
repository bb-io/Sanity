using Apps.Sanity.Utils;
using FluentAssertions;
using Newtonsoft.Json.Linq;

namespace Tests.Sanity;

[TestClass]
public class ContentNameTests
{
    private static JObject Article() => JObject.Parse("""
        {
          "_id": "article",
          "name": "Internal name",
          "title": [
            { "_key": "EN", "_type": "internationalizedArrayArticleTitleValue", "value": "English title" },
            { "_key": "unique-key", "language": "fr", "_type": "internationalizedArrayArticleTitleValue", "value": "French title" }
          ],
          "content": [
            { "_key": "EN", "_type": "internationalizedArrayArticleContentValue", "value": [{ "_ref": "snippet" }] }
          ],
          "relatedArticles": [{ "_type": "reference", "_ref": "another-article" }],
          "labels": ["label"],
          "slug": { "current": "article" },
          "legacy": { "value": "Legacy name" },
          "snippet": { "title": "Snippet title" }
        }
        """);

    [TestMethod]
    public void TitleFieldsIncludeOnlyRootStringsAndLocalizedStrings()
    {
        var article = Article();

        BlackbirdExportMetadataFactory.GetTitleFields(article).Should().BeEquivalentTo(["name", "title"]);
        BlackbirdExportMetadataFactory.TryGetContentName(article, "en", "content").Should().BeNull();
        BlackbirdExportMetadataFactory.TryGetContentName(article, "en", "legacy").Should().BeNull();
        BlackbirdExportMetadataFactory.TryGetContentName(article, "en", "snippet.title").Should().BeNull();
        BlackbirdExportMetadataFactory.TryGetContentName(article, "en", "_id").Should().BeNull();
    }

    [TestMethod]
    public void SelectedTitleUsesRequestedLocaleInBothArrayFormats()
    {
        var article = Article();

        BlackbirdExportMetadataFactory.TryGetContentName(article, "en", "title").Should().Be("English title");
        BlackbirdExportMetadataFactory.TryGetContentName(article, "FR", "TITLE").Should().Be("French title");
        BlackbirdExportMetadataFactory.TryGetContentName(article, "unique-key", "title").Should().BeNull();
        BlackbirdExportMetadataFactory.TryGetContentName(article, null, "name").Should().Be("Internal name");
    }

    [TestMethod]
    public void SelectedTitleRemainsEmptyWithoutASelectionOrMatchingTranslation()
    {
        var article = Article();

        BlackbirdExportMetadataFactory.TryGetContentName(article, "en", null).Should().BeNull();
        BlackbirdExportMetadataFactory.TryGetContentName(article, "en", " ").Should().BeNull();
        BlackbirdExportMetadataFactory.TryGetContentName(article, "en", "missing").Should().BeNull();
        BlackbirdExportMetadataFactory.TryGetContentName(article, null, "title").Should().BeNull();
        BlackbirdExportMetadataFactory.TryGetContentName(article, "de", "title").Should().BeNull();
    }

    [TestMethod]
    public void DefaultNamePrefersEligibleNameAndSkipsUnsupportedFields()
    {
        var article = Article();

        BlackbirdExportMetadataFactory.TryGetContentName(article, "fr", useDefaultFields: true).Should().Be("Internal name");

        article["name"] = article["legacy"]!.DeepClone();
        BlackbirdExportMetadataFactory.TryGetContentName(article, "fr", useDefaultFields: true).Should().Be("French title");
    }
}
