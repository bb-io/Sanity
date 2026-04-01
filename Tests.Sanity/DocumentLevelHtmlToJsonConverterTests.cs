using Apps.Sanity.Converters;
using FluentAssertions;
using Newtonsoft.Json.Linq;

namespace Tests.Sanity;

[TestClass]
public class DocumentLevelHtmlToJsonConverterTests
{
    /// <summary>
    /// Regression test: when a rich text block has a span with text "\n" (which becomes a &lt;br/&gt; in HTML)
    /// followed by another span with actual text, the "\n" span should preserve its original text
    /// and not steal the translation meant for the next span.
    /// See: block f69bf60639f2 with children c22dc9bd9d75 ("\n") and e04d224a21c4 (translatable text).
    /// </summary>
    [TestMethod]
    public void ToJsonPatches_RichTextWithNewlineSpan_ShouldPreserveNewlineAndAssignTranslationCorrectly()
    {
        // Arrange: original JSON with a rich text block containing a \n span followed by a text span
        var originalJson = new JObject
        {
            ["_id"] = "test-doc-123",
            ["_type"] = "page",
            ["language"] = "en",
            ["successText"] = new JArray
            {
                new JObject
                {
                    ["_key"] = "edbf48160298",
                    ["_type"] = "block",
                    ["children"] = new JArray
                    {
                        new JObject
                        {
                            ["_key"] = "c22dc9bd9d75",
                            ["_type"] = "span",
                            ["marks"] = new JArray(),
                            ["text"] = "Thank you for signing up."
                        }
                    },
                    ["markDefs"] = new JArray(),
                    ["style"] = "h2"
                },
                new JObject
                {
                    ["_key"] = "f69bf60639f2",
                    ["_type"] = "block",
                    ["children"] = new JArray
                    {
                        new JObject
                        {
                            ["_key"] = "c22dc9bd9d75",
                            ["_type"] = "span",
                            ["marks"] = new JArray(),
                            ["text"] = "\n"
                        },
                        new JObject
                        {
                            ["_key"] = "e04d224a21c4",
                            ["_type"] = "span",
                            ["marks"] = new JArray { "587a573f00ad" },
                            ["text"] = "In a few minutes, you'll receive a confirmation email."
                        }
                    },
                    ["markDefs"] = new JArray
                    {
                        new JObject
                        {
                            ["_key"] = "587a573f00ad",
                            ["_type"] = "variant",
                            ["value"] = "fluid-body-xs"
                        }
                    },
                    ["style"] = "normal"
                }
            }
        };

        var originalJsonBase64 = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(originalJson.ToString(Newtonsoft.Json.Formatting.None)));

        // Build HTML as it would look after translation to French
        var html = $@"<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<meta name=""blackbird-content-id"" content=""test-doc-123"">
<meta name=""blackbird-localization-strategy"" content=""DocumentLevel"">
<meta name=""blackbird-original-json"" content=""{originalJsonBase64}"">
</head>
<body>
<div data-content-id=""test-doc-123"">
<div data-json-path=""successText"" data-rich-text=""true"" data-original-json=""ignored"">
<h2 data-block-path=""successText[?(@._key=='edbf48160298')]"" data-block-key=""edbf48160298"">Merci de vous être inscrit.</h2>
<p data-block-path=""successText[?(@._key=='f69bf60639f2')]"" data-block-key=""f69bf60639f2""><br/><span data-mark=""587a573f00ad"">Dans quelques minutes, vous recevrez un e-mail de confirmation.</span></p>
</div>
</div>
</body>
</html>";

        var converter = new DocumentLevelHtmlToJsonConverter();

        // Act
        var result = converter.ToJsonPatches(html, originalJson, "fr", false);

        // Assert
        result.Mutations.Should().NotBeEmpty();
        var mainMutation = result.Mutations.First(m => m.IsMainDocument);
        var content = mainMutation.Content;

        var successText = content["successText"] as JArray;
        successText.Should().NotBeNull();

        // Find block f69bf60639f2
        var block = successText!.FirstOrDefault(b => b["_key"]?.ToString() == "f69bf60639f2") as JObject;
        block.Should().NotBeNull("Block f69bf60639f2 should exist");

        var children = block!["children"] as JArray;
        children.Should().NotBeNull();
        children!.Count.Should().Be(2);

        // Span c22dc9bd9d75 should preserve "\n", NOT get the French translation
        var newlineSpan = children[0] as JObject;
        newlineSpan!["_key"]!.ToString().Should().Be("c22dc9bd9d75");
        newlineSpan["text"]!.ToString().Should().Be("\n",
            "The newline-only span should preserve its original text and NOT receive the translation of the next span");

        // Span e04d224a21c4 should get the French translation
        var textSpan = children[1] as JObject;
        textSpan!["_key"]!.ToString().Should().Be("e04d224a21c4");
        textSpan["text"]!.ToString().Should().Be("Dans quelques minutes, vous recevrez un e-mail de confirmation.",
            "The actual text span should receive the French translation");
    }

    /// <summary>
    /// Verify that normal multi-span blocks without newline-only spans still work correctly.
    /// </summary>
    [TestMethod]
    public void ToJsonPatches_RichTextWithNormalMultipleSpans_ShouldMapSegmentsCorrectly()
    {
        var originalJson = new JObject
        {
            ["_id"] = "test-doc-456",
            ["_type"] = "page",
            ["language"] = "en",
            ["body"] = new JArray
            {
                new JObject
                {
                    ["_key"] = "block1",
                    ["_type"] = "block",
                    ["children"] = new JArray
                    {
                        new JObject
                        {
                            ["_key"] = "span1",
                            ["_type"] = "span",
                            ["marks"] = new JArray(),
                            ["text"] = "Hello "
                        },
                        new JObject
                        {
                            ["_key"] = "span2",
                            ["_type"] = "span",
                            ["marks"] = new JArray { "strong" },
                            ["text"] = "world"
                        }
                    },
                    ["markDefs"] = new JArray(),
                    ["style"] = "normal"
                }
            }
        };

        var originalJsonBase64 = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(originalJson.ToString(Newtonsoft.Json.Formatting.None)));

        var html = $@"<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<meta name=""blackbird-content-id"" content=""test-doc-456"">
<meta name=""blackbird-localization-strategy"" content=""DocumentLevel"">
<meta name=""blackbird-original-json"" content=""{originalJsonBase64}"">
</head>
<body>
<div data-content-id=""test-doc-456"">
<div data-json-path=""body"" data-rich-text=""true"" data-original-json=""ignored"">
<p data-block-path=""body[?(@._key=='block1')]"" data-block-key=""block1"">Bonjour <b>le monde</b></p>
</div>
</div>
</body>
</html>";

        var converter = new DocumentLevelHtmlToJsonConverter();

        var result = converter.ToJsonPatches(html, originalJson, "fr", false);

        var mainMutation = result.Mutations.First(m => m.IsMainDocument);
        var content = mainMutation.Content;
        var body = content["body"] as JArray;
        var block = body![0] as JObject;
        var children = block!["children"] as JArray;

        var span1 = children![0] as JObject;
        span1!["text"]!.ToString().Should().Be("Bonjour ");

        var span2 = children[1] as JObject;
        span2!["text"]!.ToString().Should().Be("le monde");
    }

    /// <summary>
    /// Regression test: Plain text values containing \n should survive the HTML roundtrip.
    /// JSON→HTML converts \n to &lt;br&gt;, and HTML→JSON should convert &lt;br&gt; back to \n.
    /// E.g. productDescription = "Our most advanced\n    paper tablet."
    /// </summary>
    [TestMethod]
    public void ToJsonPatches_PlainTextWithNewline_ShouldPreserveNewlineInRoundtrip()
    {
        var originalJson = new JObject
        {
            ["_id"] = "test-doc-789",
            ["_type"] = "product",
            ["language"] = "en",
            ["productDescription"] = "Our most advanced\n    paper tablet."
        };

        var originalJsonBase64 = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(originalJson.ToString(Newtonsoft.Json.Formatting.None)));

        // Simulate what the HTML would look like after JSON→HTML conversion:
        // \n becomes <br>, then TMS translates the text parts but preserves <br>
        var html = $@"<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<meta name=""blackbird-content-id"" content=""test-doc-789"">
<meta name=""blackbird-localization-strategy"" content=""DocumentLevel"">
<meta name=""blackbird-original-json"" content=""{originalJsonBase64}"">
</head>
<body>
<div data-content-id=""test-doc-789"">
<div data-json-path=""productDescription"">Notre plus avancée<br>    tablette papier.</div>
</div>
</body>
</html>";

        var converter = new DocumentLevelHtmlToJsonConverter();
        var result = converter.ToJsonPatches(html, originalJson, "fr", false);

        var mainMutation = result.Mutations.First(m => m.IsMainDocument);
        var content = mainMutation.Content;

        var description = content["productDescription"]?.ToString();
        description.Should().Be("Notre plus avancée\n    tablette papier.",
            "Newlines in plain text values should be preserved via <br> roundtripping");
    }

    /// <summary>
    /// Regression test: Rich text span with \n in the middle of text (not a \n-only span)
    /// should preserve \n through the HTML roundtrip.
    /// </summary>
    [TestMethod]
    public void ToJsonPatches_RichTextSpanWithEmbeddedNewline_ShouldPreserveNewline()
    {
        var originalJson = new JObject
        {
            ["_id"] = "test-doc-newline-embed",
            ["_type"] = "page",
            ["language"] = "en",
            ["heading"] = new JArray
            {
                new JObject
                {
                    ["_key"] = "block1",
                    ["_type"] = "block",
                    ["children"] = new JArray
                    {
                        new JObject
                        {
                            ["_key"] = "span1",
                            ["_type"] = "span",
                            ["marks"] = new JArray(),
                            ["text"] = "Try it for \n50 days"
                        }
                    },
                    ["markDefs"] = new JArray(),
                    ["style"] = "h2"
                }
            }
        };

        var originalJsonBase64 = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(originalJson.ToString(Newtonsoft.Json.Formatting.None)));

        // After JSON→HTML, the \n becomes <br>, then translation preserves <br>
        var html = $@"<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<meta name=""blackbird-content-id"" content=""test-doc-newline-embed"">
<meta name=""blackbird-localization-strategy"" content=""DocumentLevel"">
<meta name=""blackbird-original-json"" content=""{originalJsonBase64}"">
</head>
<body>
<div data-content-id=""test-doc-newline-embed"">
<div data-json-path=""heading"" data-rich-text=""true"" data-original-json=""ignored"">
<h2 data-block-path=""heading[?(@._key=='block1')]"" data-block-key=""block1"">Essayez-le pendant <br>50 jours</h2>
</div>
</div>
</body>
</html>";

        var converter = new DocumentLevelHtmlToJsonConverter();
        var result = converter.ToJsonPatches(html, originalJson, "fr", false);

        var mainMutation = result.Mutations.First(m => m.IsMainDocument);
        var content = mainMutation.Content;
        var heading = content["heading"] as JArray;
        var block = heading![0] as JObject;
        var children = block!["children"] as JArray;

        var span = children![0] as JObject;
        span!["text"]!.ToString().Should().Be("Essayez-le pendant \n50 jours",
            "Embedded \\n in rich text spans should be preserved through <br> roundtripping");
    }

    /// <summary>
    /// Regression test: When a rich text array contains mixed types (e.g., a "block" and a "linkButtonGroup"),
    /// ConvertArrayToHtml processes items individually. For block items, ConvertObjectToHtml wraps them
    /// in a JArray for rich text processing. On upload, the JArray must be unwrapped back to a JObject
    /// to avoid introducing an extra array layer.
    /// </summary>
    [TestMethod]
    public void ToJsonPatches_MixedArrayWithSingleBlock_ShouldNotWrapBlockInExtraArray()
    {
        // Arrange: title is an array where first item is a non-block type, second is a block
        // This means ConvertArrayToHtml won't detect it as rich text and will iterate items individually
        var originalJson = new JObject
        {
            ["_id"] = "test-doc-mixed",
            ["_type"] = "page",
            ["language"] = "en",
            ["title"] = new JArray
            {
                new JObject
                {
                    ["_key"] = "btn1",
                    ["_type"] = "linkButtonGroup",
                    ["alignment"] = "left"
                },
                new JObject
                {
                    ["_key"] = "block1",
                    ["_type"] = "block",
                    ["children"] = new JArray
                    {
                        new JObject
                        {
                            ["_key"] = "span1",
                            ["_type"] = "span",
                            ["marks"] = new JArray(),
                            ["text"] = "Hello world"
                        }
                    },
                    ["markDefs"] = new JArray(),
                    ["style"] = "h2"
                }
            }
        };

        var originalJsonBase64 = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(originalJson.ToString(Newtonsoft.Json.Formatting.None)));

        // The block at title[1] gets wrapped in data-rich-text="true" div by ConvertObjectToHtml
        var blockOriginalJson = new JArray
        {
            new JObject
            {
                ["_key"] = "block1",
                ["_type"] = "block",
                ["children"] = new JArray
                {
                    new JObject
                    {
                        ["_key"] = "span1",
                        ["_type"] = "span",
                        ["marks"] = new JArray(),
                        ["text"] = "Hello world"
                    }
                },
                ["markDefs"] = new JArray(),
                ["style"] = "h2"
            }
        };
        var blockOriginalJsonEncoded = System.Net.WebUtility.HtmlEncode(
            blockOriginalJson.ToString(Newtonsoft.Json.Formatting.None));

        var html = $@"<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<meta name=""blackbird-content-id"" content=""test-doc-mixed"">
<meta name=""blackbird-localization-strategy"" content=""DocumentLevel"">
<meta name=""blackbird-original-json"" content=""{originalJsonBase64}"">
</head>
<body>
<div data-content-id=""test-doc-mixed"">
<div>
<div data-json-path=""title[0].alignment"">left</div>
<div data-json-path=""title[1]"" data-rich-text=""true"" data-original-json=""{blockOriginalJsonEncoded}"">
<h2 data-block-path=""title[1][?(@._key=='block1')]"" data-block-key=""block1"">Bonjour le monde</h2>
</div>
</div>
</div>
</body>
</html>";

        var converter = new DocumentLevelHtmlToJsonConverter();
        var result = converter.ToJsonPatches(html, originalJson, "fr", false);

        var mainMutation = result.Mutations.First(m => m.IsMainDocument);
        var content = mainMutation.Content;
        var title = content["title"] as JArray;
        title.Should().NotBeNull();
        title!.Count.Should().Be(2, "The title array should still have exactly 2 items");

        // title[1] should remain a JObject (block), NOT become a JArray wrapping the block
        var secondItem = title[1];
        secondItem.Type.Should().Be(Newtonsoft.Json.Linq.JTokenType.Object,
            "The block at title[1] should remain a JObject, not be wrapped in an extra JArray");

        var blockObj = secondItem as JObject;
        blockObj!["_type"]!.ToString().Should().Be("block");

        var children = blockObj["children"] as JArray;
        var span = children![0] as JObject;
        span!["text"]!.ToString().Should().Be("Bonjour le monde",
            "The translated text should be applied to the block's span");
    }

    /// <summary>
    /// Regression test: ExcludedFields should exclude fields at ANY level of nesting,
    /// not only top-level properties. When "colorTheme" is excluded, it should be stripped
    /// from nested objects (e.g., sections[0].colorTheme) during download, and on upload
    /// the original value should be preserved from the base64-encoded original JSON.
    /// </summary>
    [TestMethod]
    public void ToJsonPatches_ExcludedFieldsAtNestedLevel_ShouldPreserveOriginalValues()
    {
        // Arrange: document has nested objects with "colorTheme" and "name" fields
        var originalJson = new JObject
        {
            ["_id"] = "test-doc-excluded",
            ["_type"] = "page",
            ["language"] = "en",
            ["sections"] = new JArray
            {
                new JObject
                {
                    ["_key"] = "sec1",
                    ["_type"] = "section",
                    ["colorTheme"] = "light-neutral",
                    ["name"] = "Portfolio section",
                    ["paddingTop"] = "large"
                },
                new JObject
                {
                    ["_key"] = "sec2",
                    ["_type"] = "section",
                    ["colorTheme"] = "dark-warm",
                    ["name"] = "Hero section"
                }
            },
            ["colorTheme"] = "global-light"
        };

        var originalJsonBase64 = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(originalJson.ToString(Newtonsoft.Json.Formatting.None)));

        // HTML that a TMS would produce: colorTheme fields should NOT be present
        // (because they were excluded during download). But even if they are present
        // in old HTML files, the upload side should skip them thanks to the meta tag.
        // Simulate an HTML where colorTheme IS present (worst case / backward compat):
        var html = $@"<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<meta name=""blackbird-content-id"" content=""test-doc-excluded"">
<meta name=""blackbird-localization-strategy"" content=""DocumentLevel"">
<meta name=""blackbird-original-json"" content=""{originalJsonBase64}"">
<meta name=""blackbird-excluded-fields"" content=""colorTheme"">
</head>
<body>
<div data-content-id=""test-doc-excluded"">
<div data-json-path=""sections[0].colorTheme"">CORRUPTED-VALUE</div>
<div data-json-path=""sections[0].name"">Section Portfolio</div>
<div data-json-path=""sections[0].paddingTop"">large</div>
<div data-json-path=""sections[1].colorTheme"">CORRUPTED-VALUE-2</div>
<div data-json-path=""sections[1].name"">Section Héros</div>
<div data-json-path=""colorTheme"">CORRUPTED-GLOBAL</div>
</div>
</body>
</html>";

        var converter = new DocumentLevelHtmlToJsonConverter();
        var result = converter.ToJsonPatches(html, originalJson, "fr", false);

        var mainMutation = result.Mutations.First(m => m.IsMainDocument);
        var content = mainMutation.Content;

        // Excluded fields should retain ORIGINAL values, not the corrupted HTML values
        content["colorTheme"]!.ToString().Should().Be("global-light",
            "Top-level excluded field should retain original value");

        var sections = content["sections"] as JArray;
        sections.Should().NotBeNull();

        var sec1 = sections![0] as JObject;
        sec1!["colorTheme"]!.ToString().Should().Be("light-neutral",
            "Nested excluded field sections[0].colorTheme should retain original value");
        sec1["name"]!.ToString().Should().Be("Section Portfolio",
            "Non-excluded nested field should be updated with translation");

        var sec2 = sections[1] as JObject;
        sec2!["colorTheme"]!.ToString().Should().Be("dark-warm",
            "Nested excluded field sections[1].colorTheme should retain original value");
        sec2["name"]!.ToString().Should().Be("Section Héros",
            "Non-excluded nested field should be updated with translation");
    }
}
