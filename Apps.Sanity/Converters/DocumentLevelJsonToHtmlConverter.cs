using Apps.Sanity.Models;
using Apps.Sanity.Services;
using Apps.Sanity.Utils;
using Blackbird.Filters.Shared;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace Apps.Sanity.Converters;

public class DocumentLevelJsonToHtmlConverter : IJsonToHtmlConverter
{
    private static readonly HashSet<string> DefaultExcludedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "_createdAt", "_id", "_rev", "_type", "_updatedAt", "language", "_system"
    };

    private class ConversionContext
    {
        public Dictionary<string, JObject> LocalizableReferences { get; set; } = new();
        public AssetService AssetService { get; set; } = default!;
        public string DatasetId { get; set; } = default!;
        public List<FieldSizeRestriction>? FieldRestrictions { get; set; }
        public Dictionary<string, JObject>? ReferencedEntries { get; set; }
    }

    public async Task<string> ToHtmlAsync(JObject jObject, 
        string contentId, 
        string sourceLanguage, 
        AssetService assetService,
        string datasetId, 
        Dictionary<string, JObject>? referencedEntries = null,
        IEnumerable<string>? orderOfFields = null, 
        List<FieldSizeRestriction>? fieldRestrictions = null,
        IEnumerable<string>? excludedFields = null)
    {
        var allExcludedFields = new HashSet<string>(DefaultExcludedFields, StringComparer.OrdinalIgnoreCase);
        if (excludedFields != null)
        {
            foreach (var field in excludedFields)
            {
                allExcludedFields.Add(field);
            }
        }

        // Track localizable referenced documents
        var context = new ConversionContext
        {
            LocalizableReferences = new Dictionary<string, JObject>(),
            AssetService = assetService,
            DatasetId = datasetId,
            FieldRestrictions = fieldRestrictions,
            ReferencedEntries = referencedEntries
        };

        var doc = new HtmlDocument();

        var htmlNode = doc.CreateElement("html");
        htmlNode.SetAttributeValue("lang", sourceLanguage);
        doc.DocumentNode.AppendChild(htmlNode);

        var headNode = doc.CreateElement("head");
        htmlNode.AppendChild(headNode);

        var metaCharset = doc.CreateElement("meta");
        metaCharset.SetAttributeValue("charset", "UTF-8");
        headNode.AppendChild(metaCharset);

        var metaBlackbird = doc.CreateElement("meta");
        metaBlackbird.SetAttributeValue("name", "blackbird-content-id");
        metaBlackbird.SetAttributeValue("content", EscapeHtml(contentId));
        headNode.AppendChild(metaBlackbird);

        var metaStrategy = doc.CreateElement("meta");
        metaStrategy.SetAttributeValue("name", "blackbird-localization-strategy");
        metaStrategy.SetAttributeValue("content", "DocumentLevel");
        headNode.AppendChild(metaStrategy);

        // Store original JSON to preserve non-translatable fields
        var metaOriginalJson = doc.CreateElement("meta");
        metaOriginalJson.SetAttributeValue("name", "blackbird-original-json");
        var originalJsonString = jObject.ToString(Newtonsoft.Json.Formatting.None);
        var originalJsonBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(originalJsonString));
        metaOriginalJson.SetAttributeValue("content", originalJsonBase64);
        headNode.AppendChild(metaOriginalJson);

        var bodyNode = doc.CreateElement("body");
        htmlNode.AppendChild(bodyNode);

        var mainContentDiv = doc.CreateElement("div");
        mainContentDiv.SetAttributeValue("data-content-id", contentId);
        bodyNode.AppendChild(mainContentDiv);

        var properties = jObject.Properties().ToList();
        var orderOfFieldsList = orderOfFields?.ToList() ?? new List<string>();
        if (orderOfFields != null && orderOfFieldsList.Any())
        {
            properties = ReorderProperties(properties, orderOfFieldsList);
        }

        foreach (var property in properties)
        {
            var propName = property.Name;
            
            if (allExcludedFields.Contains(propName))
            {
                continue;
            }

            // Skip GUID properties (expanded referenced documents)
            // They are already handled through referencedEntries
            if (IsGuidProperty(propName))
            {
                continue;
            }

            var propValue = property.Value;
            var convertedNode = await ConvertTokenToHtml(doc, propValue, propName, context);
            if (convertedNode != null)
            {
                mainContentDiv.AppendChild(convertedNode);
            }
        }

        // Add referenced documents section at the end of body
        if (context.LocalizableReferences.Any())
        {
            var referencesSection = doc.CreateElement("div");
            referencesSection.SetAttributeValue("id", "blackbird-referenced-documents");
            bodyNode.AppendChild(referencesSection);

            foreach (var (refId, refContent) in context.LocalizableReferences)
            {
                var refDocDiv = doc.CreateElement("div");
                refDocDiv.SetAttributeValue("data-content-id", refId);
                refDocDiv.SetAttributeValue("data-original-ref-id", refId);
                refDocDiv.SetAttributeValue("class", "referenced-document");
                referencesSection.AppendChild(refDocDiv);

                // Store original JSON of referenced document
                var refJsonString = refContent.ToString(Newtonsoft.Json.Formatting.None);
                var refJsonBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(refJsonString));
                refDocDiv.SetAttributeValue("data-original-json", refJsonBase64);

                foreach (var refProperty in refContent.Properties())
                {
                    if (allExcludedFields.Contains(refProperty.Name))
                        continue;

                    var refFieldPath = $"{refProperty.Name}";
                    var refFieldNode = await ConvertTokenToHtml(doc, refProperty.Value, refFieldPath, context);
                    if (refFieldNode != null)
                    {
                        refDocDiv.AppendChild(refFieldNode);
                    }
                }
            }
        }

        return doc.DocumentNode.OuterHtml;
    }

    private static List<JProperty> ReorderProperties(List<JProperty> properties, List<string> orderOfFields)
    {
        var propertyDict = properties.ToDictionary(p => p.Name, p => p);
        var orderedProperties = orderOfFields
            .Where(fieldName => propertyDict.ContainsKey(fieldName))
            .Select(fieldName => propertyDict[fieldName])
            .ToList();

        orderedProperties.AddRange(properties.Where(p => !orderOfFields.Contains(p.Name)));
        return orderedProperties;
    }

    private static async Task<HtmlNode?> ConvertTokenToHtml(HtmlDocument doc, 
        JToken token, 
        string currentPath,
        ConversionContext context)
    {
        if (token is JObject obj)
        {
            if (obj["_type"]?.ToString() == "reference" && obj["_ref"] != null)
            {
                var refId = obj["_ref"]!.ToString();
                
                if (refId.StartsWith("image-"))
                {
                    try
                    {
                        var assetUrl = await context.AssetService.GetAssetUrlAsync(context.DatasetId, refId);
                        var imgElement = doc.CreateElement("img");
                        imgElement.SetAttributeValue("src", assetUrl);
                        imgElement.SetAttributeValue("data-json-path", currentPath);
                        imgElement.SetAttributeValue("data-ref-id", refId);
                        return imgElement;
                    }
                    catch
                    {
                        // Fallback to div if asset URL cannot be retrieved
                        var referenceDiv = doc.CreateElement("div");
                        referenceDiv.SetAttributeValue("data-json-path", currentPath);
                        referenceDiv.SetAttributeValue("data-ref-id", refId);
                        referenceDiv.SetAttributeValue("class", "reference asset");
                        return referenceDiv;
                    }
                }
                else
                {
                    var referenceDiv = doc.CreateElement("div");
                    referenceDiv.SetAttributeValue("data-json-path", currentPath);
                    referenceDiv.SetAttributeValue("data-ref-id", refId);
                    referenceDiv.SetAttributeValue("class", "reference");
                    
                    var refContent = context.ReferencedEntries != null && context.ReferencedEntries.ContainsKey(refId)
                        ? context.ReferencedEntries[refId]
                        : null;
                    
                    // If reference has language, it's localizable - add to separate section
                    if(refContent != null && refContent["language"] != null)
                    {
                        // Mark as localizable reference
                        referenceDiv.SetAttributeValue("data-localizable-ref", "true");
                        
                        // Add to localizable references collection
                        if (!context.LocalizableReferences.ContainsKey(refId))
                        {
                            context.LocalizableReferences[refId] = refContent;
                        }
                    }
                    else if (refContent != null)
                    {
                        // Non-localizable reference - include content inline (old behavior)
                        foreach (var refProperty in refContent.Properties())
                        {
                            if (DefaultExcludedFields.Contains(refProperty.Name))
                                continue;
                            
                            var refFieldPath = $"{refId}.{refProperty.Name}";
                            var refFieldNode = await ConvertTokenToHtml(doc, refProperty.Value, refFieldPath, context);
                            
                            if (refFieldNode != null)
                            {
                                referenceDiv.AppendChild(refFieldNode);
                            }
                        }
                    }
                    
                    return referenceDiv;
                }
            }

            return await ConvertObjectToHtml(doc, obj, currentPath, context);
        }
        else if (token is JArray arr)
        {
            return await ConvertArrayToHtml(doc, arr, currentPath, context);
        }
        else if (token is JValue jValue)
        {
            if (jValue.Type == JTokenType.String)
            {
                var div = doc.CreateElement("div");
                div.SetAttributeValue("data-json-path", currentPath);
                ApplySizeRestriction(div, currentPath, context.FieldRestrictions);
                div.AppendChild(doc.CreateTextNode(jValue.ToString()));
                return div;
            }
            return null;
        }

        return null;
    }

    private static async Task<HtmlNode?> ConvertObjectToHtml(HtmlDocument doc, JObject obj, string currentPath,
        ConversionContext context)
    {
        var typeValue = obj["_type"]?.ToString();
        
        if (typeValue == "block" || (obj.ContainsKey("children") && obj.ContainsKey("markDefs")))
        {
            var parentArray = new JArray { obj };
            var richTextNode = RichTextToHtmlConvertor.ConvertToHtml(parentArray, doc, currentPath, context.AssetService, context.DatasetId);
            return richTextNode;
        }

        var container = doc.CreateElement("div");
        bool hasChildren = false;

        foreach (var property in obj.Properties())
        {
            if (property.Name.StartsWith("_"))
                continue;

            string childPath = $"{currentPath}.{property.Name}";
            var childNode = await ConvertTokenToHtml(doc, property.Value, childPath, context);
            if (childNode != null)
            {
                hasChildren = true;
                container.AppendChild(childNode);
            }
        }

        return hasChildren ? container : null;
    }

    private static async Task<HtmlNode?> ConvertArrayToHtml(HtmlDocument doc, JArray arr, string currentPath,
        ConversionContext context)
    {
        if (arr.Count > 0 && arr[0] is JObject firstItem)
        {
            var firstType = firstItem["_type"]?.ToString();
            if (firstType == "block" || (firstItem.ContainsKey("children") && firstItem.ContainsKey("markDefs")))
            {
                var richTextNode = RichTextToHtmlConvertor.ConvertToHtml(arr, doc, currentPath, context.AssetService, context.DatasetId);
                return richTextNode;
            }
        }

        var wrapperArr = doc.CreateElement("div");
        var hasChildren = false;
        for (int i = 0; i < arr.Count; i++)
        {
            var item = arr[i];
            string childPath = $"{currentPath}[{i}]";
            var childNode = await ConvertTokenToHtml(doc, item, childPath, context);
            if (childNode != null)
            {
                hasChildren = true;
                wrapperArr.AppendChild(childNode);
            }
        }

        return hasChildren ? wrapperArr : null;
    }

    private static string EscapeHtml(string text)
    {
        return System.Net.WebUtility.HtmlEncode(text);
    }

    private static void ApplySizeRestriction(HtmlNode node, string currentPath, List<FieldSizeRestriction>? fieldRestrictions)
    {
        if (fieldRestrictions == null || !fieldRestrictions.Any())
        {
            return;
        }

        var fieldName = ExtractFieldName(currentPath);
        if (fieldName != null)
        {
            var restriction = fieldRestrictions.FirstOrDefault(r => r.FieldName == fieldName);
            if (restriction != null && restriction.Restrictions != null!)
            {
                var serialized = SizeRestrictionHelper.Serialize(restriction.Restrictions);
                if (serialized != null)
                {
                    node.SetAttributeValue("data-blackbird-size", serialized);
                }
            }
        }
    }

    private static string? ExtractFieldName(string currentPath)
    {
        if (string.IsNullOrEmpty(currentPath))
        {
            return null;
        }

        var parts = currentPath.Split('.');
        if (parts.Length > 0)
        {
            var firstPart = parts[0];
            var bracketIndex = firstPart.IndexOf('[');
            return bracketIndex >= 0 ? firstPart.Substring(0, bracketIndex) : firstPart;
        }

        return null;
    }

    /// <summary>
    /// Check if property name is a GUID (expanded referenced document)
    /// GUIDs are in format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
    /// </summary>
    private static bool IsGuidProperty(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return false;

        // GUID format: 8-4-4-4-12 characters separated by dashes
        return Guid.TryParse(propertyName, out _);
    }

    /// <summary>
    /// Remove all GUID properties from the document
    /// These are expanded referenced documents that should not be saved
    /// </summary>
    private static void RemoveGuidProperties(JObject obj)
    {
        var guidProperties = obj.Properties()
            .Where(p => IsGuidProperty(p.Name))
            .ToList();

        foreach (var prop in guidProperties)
        {
            obj.Remove(prop.Name);
        }
    }
}
