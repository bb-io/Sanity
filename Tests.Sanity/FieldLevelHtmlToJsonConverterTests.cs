using Apps.Sanity.Utils;
using FluentAssertions;
using Newtonsoft.Json.Linq;

namespace Tests.Sanity;

[TestClass]
public class FieldLevelHtmlToJsonConverterTests
{
    /// <summary>
    /// Regression test: when an internationalized array (e.g. tabs[].name) is nested inside
    /// a regular array (e.g. tabs[]), the insert patch path should target the nested array
    /// (e.g. tabs[0].name[-1]) not the parent array (e.g. tabs[-1]).
    /// </summary>
    [TestMethod]
    public void ToJsonPatches_NestedInternationalizedArray_ShouldInsertIntoCorrectNestedPath()
    {
        // Arrange: original JSON with tabs[] containing name[] internationalized arrays
        var originalJson = new JObject
        {
            ["_id"] = "productCatalogue",
            ["_type"] = "productCatalogue",
            ["description"] = new JArray
            {
                new JObject
                {
                    ["_key"] = "en",
                    ["_type"] = "internationalizedArrayStringValue",
                    ["value"] = "Paper tablets and accessories designed for better thinking."
                }
            },
            ["header"] = new JArray
            {
                new JObject
                {
                    ["_key"] = "en",
                    ["_type"] = "internationalizedArrayStringValue",
                    ["value"] = "All products"
                }
            },
            ["tabs"] = new JArray
            {
                new JObject
                {
                    ["_key"] = "148994055362",
                    ["name"] = new JArray
                    {
                        new JObject
                        {
                            ["_key"] = "en",
                            ["_type"] = "internationalizedArrayStringValue",
                            ["value"] = "reMarkable Paper Pro Move"
                        }
                    },
                    ["products"] = new JArray()
                },
                new JObject
                {
                    ["_key"] = "248994055362",
                    ["name"] = new JArray
                    {
                        new JObject
                        {
                            ["_key"] = "en",
                            ["_type"] = "internationalizedArrayStringValue",
                            ["value"] = "reMarkable Paper Pro"
                        }
                    },
                    ["products"] = new JArray()
                }
            }
        };

        var html = @"<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<meta name=""blackbird-content-id"" content=""productCatalogue"">
<meta name=""blackbird-localization-strategy"" content=""FieldLevel"">
</head>
<body>
<div data-content-id=""productCatalogue"">
<div data-json-path=""description[en].value"">Tablettes papier et accessoires conçus pour mieux réfléchir.</div>
<div data-json-path=""header[en].value"">Tous les produits</div>
<div>
<div>
<div data-json-path=""tabs[0].name[en].value"">reMarkable Paper Pro Move FR</div>
</div>
<div>
<div data-json-path=""tabs[1].name[en].value"">reMarkable Paper Pro FR</div>
</div>
</div>
</div>
</body>
</html>";

        // Act
        var patches = HtmlToJsonConvertor.ToJsonPatches(html, originalJson, "fr", false);

        // Assert: should have patches for description, header, tabs[0].name, tabs[1].name
        patches.Should().NotBeEmpty();

        // Check that no patch inserts into "tabs[-1]" — that was the old bug
        foreach (var patch in patches)
        {
            var insert = patch["patch"]?["insert"];
            if (insert != null)
            {
                var afterPath = insert["after"]?.ToString();
                afterPath.Should().NotBe("tabs[-1]",
                    "patches should NOT insert into top-level tabs array; " +
                    "they should insert into the nested name array within each tab");
            }
        }

        // Check that tabs[0].name patch has the correct insert path
        var tab0NamePatch = patches.FirstOrDefault(p =>
        {
            var insert = p["patch"]?["insert"];
            return insert?["after"]?.ToString() == "tabs[0].name[-1]";
        });
        tab0NamePatch.Should().NotBeNull("should have insert patch for tabs[0].name[-1]");
        
        var tab0Items = tab0NamePatch!["patch"]!["insert"]!["items"] as JArray;
        tab0Items.Should().NotBeNull();
        tab0Items![0]!["_key"]!.ToString().Should().Be("fr");
        tab0Items[0]!["_type"]!.ToString().Should().Be("internationalizedArrayStringValue");
        tab0Items[0]!["value"]!.ToString().Should().Be("reMarkable Paper Pro Move FR");

        // Check that tabs[1].name patch has the correct insert path
        var tab1NamePatch = patches.FirstOrDefault(p =>
        {
            var insert = p["patch"]?["insert"];
            return insert?["after"]?.ToString() == "tabs[1].name[-1]";
        });
        tab1NamePatch.Should().NotBeNull("should have insert patch for tabs[1].name[-1]");
        
        var tab1Items = tab1NamePatch!["patch"]!["insert"]!["items"] as JArray;
        tab1Items.Should().NotBeNull();
        tab1Items![0]!["_key"]!.ToString().Should().Be("fr");
        tab1Items[0]!["value"]!.ToString().Should().Be("reMarkable Paper Pro FR");
    }

    /// <summary>
    /// Regression test: when a nested internationalized array already has the target language,
    /// the insert path should still correctly target the nested array (not the parent).
    /// Verifies top-level internationalized arrays (description, header) still work correctly.
    /// </summary>
    [TestMethod]
    public void ToJsonPatches_TopLevelInternationalizedArrays_ShouldStillWorkCorrectly()
    {
        var originalJson = new JObject
        {
            ["_id"] = "testDoc",
            ["_type"] = "testType",
            ["description"] = new JArray
            {
                new JObject
                {
                    ["_key"] = "en",
                    ["_type"] = "internationalizedArrayStringValue",
                    ["value"] = "English description"
                }
            },
            ["header"] = new JArray
            {
                new JObject
                {
                    ["_key"] = "en",
                    ["_type"] = "internationalizedArrayStringValue",
                    ["value"] = "English header"
                }
            }
        };

        var html = @"<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<meta name=""blackbird-content-id"" content=""testDoc"">
<meta name=""blackbird-localization-strategy"" content=""FieldLevel"">
</head>
<body>
<div data-content-id=""testDoc"">
<div data-json-path=""description[en].value"">Description française</div>
<div data-json-path=""header[en].value"">En-tête français</div>
</div>
</body>
</html>";

        var patches = HtmlToJsonConvertor.ToJsonPatches(html, originalJson, "fr", false);

        patches.Should().HaveCount(2);

        var descPatch = patches.FirstOrDefault(p =>
            p["patch"]?["insert"]?["after"]?.ToString() == "description[-1]");
        descPatch.Should().NotBeNull("should have insert after description[-1]");
        var descItems = descPatch!["patch"]!["insert"]!["items"] as JArray;
        descItems![0]!["value"]!.ToString().Should().Be("Description française");

        var headerPatch = patches.FirstOrDefault(p =>
            p["patch"]?["insert"]?["after"]?.ToString() == "header[-1]");
        headerPatch.Should().NotBeNull("should have insert after header[-1]");
        var headerItems = headerPatch!["patch"]!["insert"]!["items"] as JArray;
        headerItems![0]!["value"]!.ToString().Should().Be("En-tête français");
    }

    /// <summary>
    /// Test RichText patches for nested internationalized arrays (e.g. tabs[].sections[]).
    /// Verifies that the RichTextToJsonConvertor generates correct insert paths for
    /// internationalized rich text arrays nested inside regular arrays.
    /// </summary>
    [TestMethod]
    public void ToJsonPatches_NestedRichTextInternationalizedArray_ShouldInsertIntoCorrectPath()
    {
        var originalJson = new JObject
        {
            ["_id"] = "productCatalogue",
            ["_type"] = "productCatalogue",
            ["tabs"] = new JArray
            {
                new JObject
                {
                    ["_key"] = "148994055362",
                    ["sections"] = new JArray
                    {
                        new JObject
                        {
                            ["_key"] = "en",
                            ["_type"] = "internationalizedArrayBlockContent",
                            ["value"] = new JArray
                            {
                                new JObject
                                {
                                    ["_key"] = "block1",
                                    ["_type"] = "section",
                                    ["name"] = "Test section"
                                }
                            }
                        }
                    }
                }
            }
        };

        var originalJsonEncoded = System.Net.WebUtility.HtmlEncode(
            "[{\"_key\":\"block1\",\"_type\":\"section\",\"name\":\"Test section\"}]");

        var html = $@"<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<meta name=""blackbird-content-id"" content=""productCatalogue"">
<meta name=""blackbird-localization-strategy"" content=""FieldLevel"">
</head>
<body>
<div data-content-id=""productCatalogue"">
<div>
<div>
<div data-json-path=""tabs[0].sections[en].value"" data-rich-text=""true"" data-original-json=""{originalJsonEncoded}"">
<div data-block-path=""tabs[0].sections[en].value[?(@._key=='block1')]"" data-type=""section""></div>
</div>
</div>
</div>
</div>
</body>
</html>";

        var patches = HtmlToJsonConvertor.ToJsonPatches(html, originalJson, "fr", false);

        // Rich text patches should target tabs[0].sections[-1], NOT tabs[-1]
        foreach (var patch in patches)
        {
            var insert = patch["patch"]?["insert"];
            if (insert != null)
            {
                var afterPath = insert["after"]?.ToString();
                var replacePath = insert["replace"]?.ToString();
                
                if (afterPath != null)
                {
                    afterPath.Should().NotBe("tabs[-1]",
                        "rich text patches should NOT insert into top-level tabs array");
                    afterPath.Should().Be("tabs[0].sections[-1]",
                        "rich text patches should insert into the nested sections array");
                }
            }
        }
    }
}
