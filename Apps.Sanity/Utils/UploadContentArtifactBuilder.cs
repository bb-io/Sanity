using Apps.Sanity.Models;
using Blackbird.Filters.Shared;
using Blackbird.Filters.Transformations;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Apps.Sanity.Utils;

public static class UploadContentArtifactBuilder
{
    public static string BuildHtml(string html, string targetLanguage, string targetContentId,
        BlackbirdExportMetadata metadata, JObject? mainContent = null,
        IReadOnlyDictionary<string, string>? referenceIdMapping = null,
        IReadOnlyDictionary<string, JObject>? referencedContents = null)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var htmlNode = doc.DocumentNode.SelectSingleNode("//html");
        htmlNode?.SetAttributeValue("lang", targetLanguage);

        HtmlHelper.UpsertMetaTag(doc, "blackbird-content-id", targetContentId);
        HtmlHelper.UpsertMetaTag(doc, "blackbird-ucid", metadata.Ucid);
        HtmlHelper.UpsertMetaTag(doc, "blackbird-content-name", metadata.ContentName);
        HtmlHelper.UpsertMetaTag(doc, "blackbird-system-name", metadata.SystemName);
        HtmlHelper.UpsertMetaTag(doc, "blackbird-system-ref", metadata.SystemRef);

        var mainContentDiv = doc.DocumentNode.SelectSingleNode("//body/div[@data-content-id]")
            ?? doc.DocumentNode.SelectSingleNode("//div[@data-content-id]");
        mainContentDiv?.SetAttributeValue("data-content-id", targetContentId);

        if (mainContent != null)
        {
            HtmlHelper.UpsertMetaTag(
                doc,
                "blackbird-original-json",
                Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(mainContent.ToString(Formatting.None))));
        }

        if (referenceIdMapping != null && referenceIdMapping.Any())
        {
            foreach (var referenceNode in doc.DocumentNode.SelectNodes("//*[@data-ref-id]") ?? Enumerable.Empty<HtmlNode>())
            {
                var refId = referenceNode.GetAttributeValue("data-ref-id", string.Empty);
                if (referenceIdMapping.TryGetValue(refId, out var targetRefId))
                {
                    referenceNode.SetAttributeValue("data-ref-id", targetRefId);
                }
            }

            foreach (var refEntryNode in doc.DocumentNode.SelectNodes("//div[@id='referenced-entries']//div[@data-content-id]") ?? Enumerable.Empty<HtmlNode>())
            {
                var refId = refEntryNode.GetAttributeValue("data-content-id", string.Empty);
                if (referenceIdMapping.TryGetValue(refId, out var targetRefId))
                {
                    refEntryNode.SetAttributeValue("data-content-id", targetRefId);
                    if (!string.IsNullOrWhiteSpace(refEntryNode.Id))
                    {
                        refEntryNode.Id = $"ref-{targetRefId}";
                    }
                }
            }

            foreach (var refDocumentNode in doc.DocumentNode.SelectNodes("//div[@id='blackbird-referenced-documents']//div[@class='referenced-document']") ?? Enumerable.Empty<HtmlNode>())
            {
                var originalRefId = refDocumentNode.GetAttributeValue("data-original-ref-id", string.Empty);
                if (!referenceIdMapping.TryGetValue(originalRefId, out var targetRefId))
                {
                    continue;
                }

                refDocumentNode.SetAttributeValue("data-content-id", targetRefId);
                if (referencedContents != null && referencedContents.TryGetValue(targetRefId, out var refContent))
                {
                    refDocumentNode.SetAttributeValue(
                        "data-original-json",
                        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(refContent.ToString(Formatting.None))));
                }
            }
        }

        return doc.DocumentNode.OuterHtml;
    }

    public static string BuildTransformation(Transformation transformation, string targetLanguage,
        BlackbirdExportMetadata metadata)
    {
        transformation.TargetLanguage = targetLanguage;
        transformation.TargetSystemReference ??= new SystemReference();
        transformation.TargetSystemReference.ContentId = metadata.Ucid;
        transformation.TargetSystemReference.ContentName = metadata.ContentName;
        transformation.TargetSystemReference.PublicUrl = null;
        transformation.TargetSystemReference.SystemName = metadata.SystemName;
        transformation.TargetSystemReference.SystemRef = metadata.SystemRef;

        return transformation.Serialize();
    }
}
