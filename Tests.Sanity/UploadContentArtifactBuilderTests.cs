using Apps.Sanity.Models;
using Apps.Sanity.Utils;
using FluentAssertions;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Tests.Sanity;

[TestClass]
public class UploadContentArtifactBuilderTests
{
    [TestMethod]
    public void BuildHtml_DocumentLevel_ShouldUpdateMainAndReferencedMetadata()
    {
        var oldMainJson = new JObject
        {
            ["_id"] = "old-main",
            ["language"] = "en"
        };
        var newMainJson = new JObject
        {
            ["_id"] = "versions.release.translated-main",
            ["language"] = "fr",
            ["title"] = "Titre"
        };
        var oldRefJson = new JObject
        {
            ["_id"] = "old-ref"
        };
        var newRefJson = new JObject
        {
            ["_id"] = "versions.release.translated-ref",
            ["language"] = "fr"
        };

        var html = $@"<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<meta name=""blackbird-content-id"" content=""old-main"">
<meta name=""blackbird-original-json"" content=""{ToBase64(oldMainJson)}"">
</head>
<body>
<div data-content-id=""old-main"">
  <div data-ref-id=""old-ref""></div>
</div>
<div id=""blackbird-referenced-documents"">
  <div class=""referenced-document"" data-content-id=""old-ref"" data-original-ref-id=""old-ref"" data-original-json=""{ToBase64(oldRefJson)}""></div>
</div>
</body>
</html>";

        var output = UploadContentArtifactBuilder.BuildHtml(
            html,
            "fr",
            "versions.release.translated-main",
            new BlackbirdExportMetadata
            {
                HtmlLanguage = "fr",
                Ucid = "versions.release.translated-main",
                ContentName = "Titre",
                SystemName = "Sanity",
                SystemRef = "https://www.sanity.io/"
            },
            newMainJson,
            new Dictionary<string, string>
            {
                ["old-ref"] = "versions.release.translated-ref"
            },
            new Dictionary<string, JObject>
            {
                ["versions.release.translated-ref"] = newRefJson
            });

        var doc = new HtmlDocument();
        doc.LoadHtml(output);

        doc.DocumentNode.SelectSingleNode("/html")!.GetAttributeValue("lang", string.Empty).Should().Be("fr");
        doc.DocumentNode.SelectSingleNode("//meta[@name='blackbird-content-id']")!
            .GetAttributeValue("content", string.Empty).Should().Be("versions.release.translated-main");
        doc.DocumentNode.SelectSingleNode("//meta[@name='blackbird-ucid']")!
            .GetAttributeValue("content", string.Empty).Should().Be("versions.release.translated-main");
        doc.DocumentNode.SelectSingleNode("//meta[@name='blackbird-content-name']")!
            .GetAttributeValue("content", string.Empty).Should().Be("Titre");
        FromBase64(doc.DocumentNode.SelectSingleNode("//meta[@name='blackbird-original-json']")!
                .GetAttributeValue("content", string.Empty))
            .Should().Be(newMainJson.ToString(Formatting.None));

        doc.DocumentNode.SelectSingleNode("//body/div[@data-content-id]")!
            .GetAttributeValue("data-content-id", string.Empty).Should().Be("versions.release.translated-main");
        doc.DocumentNode.SelectSingleNode("//*[@data-ref-id]")!
            .GetAttributeValue("data-ref-id", string.Empty).Should().Be("versions.release.translated-ref");

        var referencedDoc = doc.DocumentNode.SelectSingleNode("//div[@class='referenced-document']")!;
        referencedDoc.GetAttributeValue("data-content-id", string.Empty).Should().Be("versions.release.translated-ref");
        referencedDoc.GetAttributeValue("data-original-ref-id", string.Empty).Should().Be("old-ref");
        FromBase64(referencedDoc.GetAttributeValue("data-original-json", string.Empty))
            .Should().Be(newRefJson.ToString(Formatting.None));
    }

    [TestMethod]
    public void BuildHtml_FieldLevel_ShouldUpdateReferenceIdsAndRemoveEmptyAdminUrl()
    {
        var html = @"<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<meta name=""blackbird-content-id"" content=""old-main"">
<meta name=""blackbird-admin-url"" content=""http://localhost:3333/structure/post;old-main"">
</head>
<body>
<div data-content-id=""old-main""></div>
<div id=""referenced-entries"">
  <div id=""ref-old-ref"" data-content-id=""old-ref""></div>
</div>
</body>
</html>";

        var output = UploadContentArtifactBuilder.BuildHtml(
            html,
            "fr",
            "drafts.old-main",
            new BlackbirdExportMetadata
            {
                HtmlLanguage = "fr",
                Ucid = "drafts.old-main",
                ContentName = null,
                SystemName = "Sanity",
                SystemRef = "https://www.sanity.io/"
            },
            null,
            new Dictionary<string, string>
            {
                ["old-ref"] = "drafts.old-ref"
            });

        var doc = new HtmlDocument();
        doc.LoadHtml(output);
        
        doc.DocumentNode.SelectSingleNode("//body/div[@data-content-id]")!
            .GetAttributeValue("data-content-id", string.Empty).Should().Be("drafts.old-main");

        var refEntry = doc.DocumentNode.SelectSingleNode("//div[@id='referenced-entries']//div[@data-content-id]")!;
        refEntry.GetAttributeValue("data-content-id", string.Empty).Should().Be("drafts.old-ref");
        refEntry.Id.Should().Be("ref-drafts.old-ref");
    }

    private static string ToBase64(JObject json)
    {
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json.ToString(Formatting.None)));
    }

    private static string FromBase64(string base64)
    {
        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }
}
