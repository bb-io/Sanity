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
        "_createdAt", "_id", "_rev", "_type", "_updatedAt", "language"
    };

    public string ToHtml(JObject jObject, string contentId, string sourceLanguage, AssetService assetService,
        string datasetId, Dictionary<string, JObject>? referencedEntries = null,
        IEnumerable<string>? orderOfFields = null, List<FieldSizeRestriction>? fieldRestrictions = null,
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

            var propValue = property.Value;
            var convertedNode = ConvertTokenToHtml(doc, propValue, propName, assetService, datasetId, fieldRestrictions);
            if (convertedNode != null)
            {
                mainContentDiv.AppendChild(convertedNode);
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

    private static HtmlNode? ConvertTokenToHtml(HtmlDocument doc, JToken token, string currentPath,
        AssetService assetService, string datasetId, List<FieldSizeRestriction>? fieldRestrictions = null)
    {
        if (token is JObject obj)
        {
            if (obj["_type"]?.ToString() == "reference" && obj["_ref"] != null)
            {
                var refId = obj["_ref"]!.ToString();
                var referenceDiv = doc.CreateElement("div");
                referenceDiv.SetAttributeValue("data-json-path", currentPath);
                referenceDiv.SetAttributeValue("data-ref-id", refId);
                referenceDiv.SetAttributeValue("class", "reference");
                return referenceDiv;
            }

            return ConvertObjectToHtml(doc, obj, currentPath, assetService, datasetId, fieldRestrictions);
        }
        else if (token is JArray arr)
        {
            return ConvertArrayToHtml(doc, arr, currentPath, assetService, datasetId, fieldRestrictions);
        }
        else if (token is JValue jValue)
        {
            if (jValue.Type == JTokenType.String)
            {
                var div = doc.CreateElement("div");
                div.SetAttributeValue("data-json-path", currentPath);
                ApplySizeRestriction(div, currentPath, fieldRestrictions);
                div.AppendChild(doc.CreateTextNode(jValue.ToString()));
                return div;
            }
            return null;
        }

        return null;
    }

    private static HtmlNode? ConvertObjectToHtml(HtmlDocument doc, JObject obj, string currentPath,
        AssetService assetService, string datasetId, List<FieldSizeRestriction>? fieldRestrictions = null)
    {
        var typeValue = obj["_type"]?.ToString();
        
        if (typeValue == "block" || (obj.ContainsKey("children") && obj.ContainsKey("markDefs")))
        {
            var parentArray = new JArray { obj };
            var richTextNode = RichTextToHtmlConvertor.ConvertToHtml(parentArray, doc, currentPath, assetService, datasetId);
            return richTextNode;
        }

        var container = doc.CreateElement("div");
        bool hasChildren = false;

        foreach (var property in obj.Properties())
        {
            if (property.Name.StartsWith("_"))
                continue;

            string childPath = $"{currentPath}.{property.Name}";
            var childNode = ConvertTokenToHtml(doc, property.Value, childPath, assetService, datasetId, fieldRestrictions);
            if (childNode != null)
            {
                hasChildren = true;
                container.AppendChild(childNode);
            }
        }

        return hasChildren ? container : null;
    }

    private static HtmlNode? ConvertArrayToHtml(HtmlDocument doc, JArray arr, string currentPath,
        AssetService assetService, string datasetId, List<FieldSizeRestriction>? fieldRestrictions = null)
    {
        if (arr.Count > 0 && arr[0] is JObject firstItem)
        {
            var firstType = firstItem["_type"]?.ToString();
            if (firstType == "block" || (firstItem.ContainsKey("children") && firstItem.ContainsKey("markDefs")))
            {
                var richTextNode = RichTextToHtmlConvertor.ConvertToHtml(arr, doc, currentPath, assetService, datasetId);
                return richTextNode;
            }
        }

        var wrapperArr = doc.CreateElement("div");
        var hasChildren = false;
        for (int i = 0; i < arr.Count; i++)
        {
            var item = arr[i];
            string childPath = $"{currentPath}[{i}]";
            var childNode = ConvertTokenToHtml(doc, item, childPath, assetService, datasetId, fieldRestrictions);
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
}
