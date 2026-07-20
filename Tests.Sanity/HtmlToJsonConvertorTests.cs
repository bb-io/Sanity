using Apps.Sanity.Utils;
using FluentAssertions;
using Newtonsoft.Json.Linq;

namespace Tests.Sanity;

[TestClass]
public class HtmlToJsonConvertorTests
{
    private const string ContentId = "b3844f48-000d-4033-9053-266f205cbea8";

    private static string BuildHeadingHtml() =>
        $"<html lang=\"EN\"><head>" +
        $"<meta name=\"blackbird-content-id\" content=\"{ContentId}\">" +
        $"<meta name=\"blackbird-localization-strategy\" content=\"FieldLevel\">" +
        $"</head><body><div data-content-id=\"{ContentId}\">" +
        $"<div data-json-path=\"heading[EN].value\" data-blackbird-key=\"{ContentId}.heading[EN].value\">What’s included?</div>" +
        $"</div></body></html>";

    private static JObject BuildDocument(params (string Key, string Value)[] headings)
    {
        var headingArray = new JArray();
        foreach (var (key, value) in headings)
        {
            headingArray.Add(new JObject
            {
                ["_key"] = key,
                ["_type"] = "internationalizedArraySnippetHeadingValue",
                ["value"] = value
            });
        }

        return new JObject
        {
            ["_id"] = $"drafts.{ContentId}",
            ["_type"] = "snippet",
            ["heading"] = headingArray
        };
    }

    private static JObject GetHeadingPatch(List<JObject> patches) =>
        patches.Single(p => p["patch"] is JObject);

    [TestMethod]
    public void ToJsonPatches_TargetLanguageAlreadyExists_UpdatesExistingItemWithoutInsertingDuplicate()
    {
        // Draft already contains an ES heading (as happens when re-syncing a locale).
        var document = BuildDocument(("EN", "What’s included?"), ("ES", "¿Qué incluye?"), ("FR", "Qu’est-ce qui est compris ?"));

        var patches = HtmlToJsonConvertor.ToJsonPatches(BuildHeadingHtml(), document, "ES", publish: false);

        var patch = (JObject)GetHeadingPatch(patches)["patch"]!;

        // Must NOT append a duplicate _key == "ES"; must update the existing item in place.
        patch["insert"].Should().BeNull("re-syncing an existing locale must not append a duplicate array item");
        patch["set"].Should().NotBeNull();
        ((JObject)patch["set"]!).Properties()
            .Should().ContainSingle()
            .Which.Name.Should().Be("heading[1].value");
    }

    [TestMethod]
    public void ToJsonPatches_TargetLanguageAbsent_InsertsNewLanguageItem()
    {
        var document = BuildDocument(("EN", "What’s included?"), ("FR", "Qu’est-ce qui est compris ?"));

        var patches = HtmlToJsonConvertor.ToJsonPatches(BuildHeadingHtml(), document, "ES", publish: false);

        var patch = (JObject)GetHeadingPatch(patches)["patch"]!;

        patch["insert"].Should().NotBeNull("a brand new locale must be inserted");
        var insertedItems = (JArray)patch["insert"]!["items"]!;
        insertedItems.Should().ContainSingle();
        insertedItems[0]!["_key"]!.ToString().Should().Be("ES");
    }

    [TestMethod]
    public void ToJsonPatches_StaleDedupedKeyPresent_EmitsUnsetToSelfHeal()
    {
        // Simulates prior corruption: a duplicate insert was auto-renamed to "ES_deduped_3".
        var document = BuildDocument(
            ("EN", "What’s included?"),
            ("ES", "¿Qué incluye?"),
            ("FR", "Qu’est-ce qui est compris ?"),
            ("ES_deduped_3", "¿Qué incluye?"));

        var patches = HtmlToJsonConvertor.ToJsonPatches(BuildHeadingHtml(), document, "ES", publish: false);

        var patch = (JObject)GetHeadingPatch(patches)["patch"]!;

        patch["unset"].Should().NotBeNull("stale deduped keys must be cleaned up so the document can publish");
        var unset = ((JArray)patch["unset"]!).Select(x => x.ToString()).ToList();
        unset.Should().Contain("heading[_key==\"ES_deduped_3\"]");
    }
}
