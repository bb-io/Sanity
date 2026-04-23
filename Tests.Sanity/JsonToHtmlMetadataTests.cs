using Apps.Sanity.Converters;
using Apps.Sanity.Models;
using FluentAssertions;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace Tests.Sanity;

[TestClass]
public class JsonToHtmlMetadataTests
{
    [TestMethod]
    public async Task FieldLevelConverter_ShouldEmitBlackbirdInteroperabilityMetadata()
    {
        var content = new JObject
        {
            ["_id"] = "doc-1",
            ["title"] = new JArray
            {
                new JObject
                {
                    ["_key"] = "en",
                    ["_type"] = "internationalizedArrayStringValue",
                    ["value"] = "Hello world"
                }
            }
        };

        var converter = new FieldLevelJsonToHtmlConverter();
        var html = await converter.ToHtmlAsync(
            content,
            "doc-1",
            "en",
            null!,
            "production",
            metadata: new BlackbirdExportMetadata
            {
                HtmlLanguage = "en",
                Ucid = "doc-1",
                ContentName = "Hello world",
                SystemName = "Sanity",
                SystemRef = "https://www.sanity.io/"
            });

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        doc.DocumentNode.SelectSingleNode("/html")!.GetAttributeValue("lang", string.Empty).Should().Be("en");
        doc.DocumentNode.SelectSingleNode("//meta[@name='blackbird-ucid']")!
            .GetAttributeValue("content", string.Empty).Should().Be("doc-1");
        doc.DocumentNode.SelectSingleNode("//meta[@name='blackbird-content-name']")!
            .GetAttributeValue("content", string.Empty).Should().Be("Hello world");
        doc.DocumentNode.SelectSingleNode("//meta[@name='blackbird-system-name']")!
            .GetAttributeValue("content", string.Empty).Should().Be("Sanity");
        doc.DocumentNode.SelectSingleNode("//meta[@name='blackbird-system-ref']")!
            .GetAttributeValue("content", string.Empty).Should().Be("https://www.sanity.io/");
    }

    [TestMethod]
    public async Task DocumentLevelConverter_ShouldEmitBlackbirdInteroperabilityMetadata()
    {
        var content = new JObject
        {
            ["_id"] = "doc-2",
            ["_type"] = "faq",
            ["language"] = "en",
            ["title"] = "Hello world"
        };

        var converter = new DocumentLevelJsonToHtmlConverter();
        var html = await converter.ToHtmlAsync(
            content,
            "doc-2",
            "en",
            null!,
            "production",
            metadata: new BlackbirdExportMetadata
            {
                HtmlLanguage = "en",
                Ucid = "doc-2",
                ContentName = "Hello world",
                SystemName = "Sanity",
                SystemRef = "https://www.sanity.io/"
            });

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        doc.DocumentNode.SelectSingleNode("/html")!.GetAttributeValue("lang", string.Empty).Should().Be("en");
        doc.DocumentNode.SelectSingleNode("//meta[@name='blackbird-ucid']")!
            .GetAttributeValue("content", string.Empty).Should().Be("doc-2");
        doc.DocumentNode.SelectSingleNode("//meta[@name='blackbird-content-name']")!
            .GetAttributeValue("content", string.Empty).Should().Be("Hello world");
        doc.DocumentNode.SelectSingleNode("//meta[@name='blackbird-system-name']")!
            .GetAttributeValue("content", string.Empty).Should().Be("Sanity");
        doc.DocumentNode.SelectSingleNode("//meta[@name='blackbird-system-ref']")!
            .GetAttributeValue("content", string.Empty).Should().Be("https://www.sanity.io/");
    }

    [TestMethod]
    public async Task FieldLevelConverter_ShouldEmitBlackbirdKeysForLocalizableFields()
    {
        var content = new JObject
        {
            ["_id"] = "doc-keys-1",
            ["title"] = new JArray
            {
                new JObject
                {
                    ["_key"] = "en",
                    ["_type"] = "internationalizedArrayStringValue",
                    ["value"] = "Hello world"
                }
            },
            ["body"] = new JArray
            {
                new JObject
                {
                    ["_key"] = "en",
                    ["_type"] = "internationalizedArrayBlockContent",
                    ["value"] = new JArray
                    {
                        new JObject
                        {
                            ["_key"] = "block-1",
                            ["_type"] = "block",
                            ["children"] = new JArray
                            {
                                new JObject
                                {
                                    ["_key"] = "span-1",
                                    ["_type"] = "span",
                                    ["marks"] = new JArray(),
                                    ["text"] = "Paragraph"
                                }
                            },
                            ["markDefs"] = new JArray(),
                            ["style"] = "normal"
                        }
                    }
                }
            }
        };

        var converter = new FieldLevelJsonToHtmlConverter();
        var html = await converter.ToHtmlAsync(content, "doc-keys-1", "en", null!, "production");

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        doc.DocumentNode.SelectSingleNode("//div[@data-json-path='title[en].value']")!
            .GetAttributeValue("data-blackbird-key", string.Empty).Should().Be("doc-keys-1.title[en].value");
        doc.DocumentNode.SelectSingleNode("//div[@data-json-path='body[en].value']")!
            .GetAttributeValue("data-blackbird-key", string.Empty).Should().Be("doc-keys-1.body[en].value");
    }

    [TestMethod]
    public async Task DocumentLevelConverter_ShouldEmitBlackbirdKeysForLocalizableFields()
    {
        var content = new JObject
        {
            ["_id"] = "doc-keys-2",
            ["_type"] = "faq",
            ["language"] = "en",
            ["title"] = "Hello world",
            ["body"] = new JArray
            {
                new JObject
                {
                    ["_key"] = "block-1",
                    ["_type"] = "block",
                    ["children"] = new JArray
                    {
                        new JObject
                        {
                            ["_key"] = "span-1",
                            ["_type"] = "span",
                            ["marks"] = new JArray(),
                            ["text"] = "Paragraph"
                        }
                    },
                    ["markDefs"] = new JArray(),
                    ["style"] = "normal"
                }
            }
        };

        var converter = new DocumentLevelJsonToHtmlConverter();
        var html = await converter.ToHtmlAsync(content, "doc-keys-2", "en", null!, "production");

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        doc.DocumentNode.SelectSingleNode("//div[@data-json-path='title']")!
            .GetAttributeValue("data-blackbird-key", string.Empty).Should().Be("doc-keys-2.title");
        doc.DocumentNode.SelectSingleNode("//div[@data-json-path='body']")!
            .GetAttributeValue("data-blackbird-key", string.Empty).Should().Be("doc-keys-2.body");
    }
}
