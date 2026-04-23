using Apps.Sanity.Utils;
using FluentAssertions;
using Newtonsoft.Json.Linq;

namespace Tests.Sanity;

[TestClass]
public class BlackbirdExportMetadataFactoryTests
{
    [TestMethod]
    public void Create_ShouldBuildSanityMetadataFromContent()
    {
        var content = new JObject
        {
            ["_id"] = "versions.rmeNW7CRm.0b36c357-45d9-413e-be0c-cbc549d3bae8",
            ["_type"] = "faq",
            ["language"] = "en",
            ["title"] = "FAQ title"
        };

        var metadata = BlackbirdExportMetadataFactory.Create(content, "fallback-id", "fr", "http://localhost:3333/");

        metadata.HtmlLanguage.Should().Be("en");
        metadata.Ucid.Should().Be("versions.rmeNW7CRm.0b36c357-45d9-413e-be0c-cbc549d3bae8");
        metadata.ContentName.Should().Be("FAQ title");
        metadata.SystemName.Should().Be("Sanity");
        metadata.SystemRef.Should().Be("https://www.sanity.io/");
    }

    [TestMethod]
    public void Create_ShouldReadLocalizedNameFromInternationalizedArray()
    {
        var content = new JObject
        {
            ["_id"] = "doc-123",
            ["_type"] = "post",
            ["language"] = "en",
            ["name"] = new JArray
            {
                new JObject
                {
                    ["_key"] = "en",
                    ["_type"] = "internationalizedArrayStringValue",
                    ["value"] = "English name"
                },
                new JObject
                {
                    ["_key"] = "fr",
                    ["_type"] = "internationalizedArrayStringValue",
                    ["value"] = "Nom francais"
                }
            }
        };

        var metadata = BlackbirdExportMetadataFactory.Create(content, "fallback-id", "fr", null);

        metadata.ContentName.Should().Be("English name");
    }

    [TestMethod]
    public void Create_ShouldUseUcidOverrideForTargetVariantOutput()
    {
        var content = new JObject
        {
            ["_id"] = "translated-ua-id",
            ["_type"] = "post",
            ["language"] = "ua",
            ["title"] = "Ukrainian title"
        };

        var metadata = BlackbirdExportMetadataFactory.Create(
            content,
            "fallback-id",
            "ua",
            "source-default-id");

        metadata.HtmlLanguage.Should().Be("ua");
        metadata.Ucid.Should().Be("source-default-id");
    }
}
