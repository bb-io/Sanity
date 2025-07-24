using Apps.Sanity.Services;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace Apps.Sanity.Utils;

public static class JsonToHtmlConverter
{
    public static string ToHtml(this JObject jObject, string contentId, string sourceLanguage, 
        AssetService assetService, string datasetId, Dictionary<string, JObject>? referencedEntries = null)
    {
        var doc = new HtmlDocument();

        var htmlNode = doc.CreateElement("html");
        htmlNode.SetAttributeValue("lang", "en");
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

        var bodyNode = doc.CreateElement("body");
        htmlNode.AppendChild(bodyNode);

        var mainContentDiv = doc.CreateElement("div");
        mainContentDiv.SetAttributeValue("data-content-id", contentId);
        bodyNode.AppendChild(mainContentDiv);

        foreach (var property in jObject.Properties())
        {
            var propName = property.Name;
            var propValue = property.Value;
            if (propName.StartsWith("_"))
            {
                continue;
            }

            var convertedNode = ConvertTokenToHtml(doc, propValue, propName, sourceLanguage, assetService, datasetId, referencedEntries);
            if (convertedNode != null)
            {
                mainContentDiv.AppendChild(convertedNode);
            }
        }

        if (referencedEntries != null && referencedEntries.Any())
        {
            var referencesSection = doc.CreateElement("div");
            referencesSection.SetAttributeValue("id", "referenced-entries");
            referencesSection.SetAttributeValue("class", "references-container");
            bodyNode.AppendChild(referencesSection);

            foreach (var entry in referencedEntries)
            {
                string refId = entry.Key;
                JObject refContent = entry.Value;
                
                var refDiv = doc.CreateElement("div");
                refDiv.SetAttributeValue("data-content-id", refId);
                refDiv.SetAttributeValue("class", "referenced-entry");
                refDiv.SetAttributeValue("id", $"ref-{refId}");
                referencesSection.AppendChild(refDiv);
                
                foreach (var property in refContent.Properties())
                {
                    string propName = property.Name;
                    JToken propValue = property.Value;

                    if (propName.StartsWith("_"))
                    {
                        continue;
                    }

                    var convertedNode = ConvertTokenToHtml(doc, propValue, propName, sourceLanguage, assetService, datasetId);
                    if (convertedNode != null)
                    {
                        refDiv.AppendChild(convertedNode);
                    }
                }
            }
        }

        return doc.DocumentNode.OuterHtml;
    }

    private static HtmlNode? ConvertTokenToHtml(HtmlDocument doc, JToken token, string currentPath, string sourceLanguage,
        AssetService assetService, string datasetId,  Dictionary<string, JObject>? referencedEntries = null)
    {
        if (token is JObject obj)
        {
            if (obj["_type"]?.ToString() == "reference" && obj["_ref"] != null && referencedEntries != null)
            {
                var refId = obj["_ref"]!.ToString();
                var referenceDiv = doc.CreateElement("div");
                referenceDiv.SetAttributeValue("data-json-path", currentPath);
                referenceDiv.SetAttributeValue("data-ref-id", refId);
                referenceDiv.SetAttributeValue("class", "reference");
                return referenceDiv;
            }
            
            return ConvertObjectToHtml(doc, obj, currentPath, sourceLanguage, assetService, datasetId, referencedEntries);
        }
        else if (token is JArray arr)
        {
            return ConvertArrayToHtml(doc, arr, currentPath, sourceLanguage, assetService, datasetId, referencedEntries);
        }
        else if (token is JValue)
        {
            return null;
        }
        else
        {
            return null;
        }
    }

    private static HtmlNode? ConvertObjectToHtml(HtmlDocument doc, JObject obj, string currentPath, string sourceLanguage,
        AssetService assetService, string datasetId, Dictionary<string, JObject>? referencedEntries = null)
    {
        if (IsInternationalizedValue(obj))
        {
            var lang = obj["_key"]?.ToString();
            if (lang == null || lang != sourceLanguage)
            {
                return null;
            }

            if (obj.TryGetValue("value", out var valueToken))
            {
                if (valueToken.Type == JTokenType.String)
                {
                    var span = doc.CreateElement("span");
                    span.SetAttributeValue("data-json-path", $"{currentPath}[{lang}].value");
                    span.AppendChild(doc.CreateTextNode(valueToken.ToString()));
                    return span;
                }
                else if (valueToken is JObject complexObj)
                {
                    var div = doc.CreateElement("div");
                    div.SetAttributeValue("data-json-path", $"{currentPath}[{lang}]");

                    foreach (var property in complexObj.Properties().Where(x => !x.Name.StartsWith("_")))
                    {
                        var childPath = $"{currentPath}[{lang}].value.{property.Name}";
                        var childValue = property.Value;

                        if (childValue.Type == JTokenType.String)
                        {
                            var span = doc.CreateElement("span");
                            span.SetAttributeValue("data-json-path", childPath);
                            span.AppendChild(doc.CreateTextNode(childValue.ToString()));
                            div.AppendChild(span);
                        }
                        else
                        {
                            var childNode = ConvertTokenToHtml(doc, childValue, childPath, sourceLanguage, assetService, datasetId, referencedEntries);
                            if (childNode != null)
                            {
                                div.AppendChild(childNode);
                            }
                        }
                    }
                    return div;
                }
                else if (valueToken is JArray jArrayValue)
                {
                    var richTextNode = RichTextToHtmlConvertor.ConvertToHtml(jArrayValue, doc, $"{currentPath}[{lang}].value", assetService, datasetId);
                    return richTextNode;
                }
            }

            return null;
        }

        var container = doc.CreateElement("div");
        bool hasChildren = false;

        foreach (var property in obj.Properties())
        {
            if (property.Name.StartsWith("_")) continue; 

            string childPath = currentPath == null ? property.Name : $"{currentPath}.{property.Name}";
            var childNode = ConvertTokenToHtml(doc, property.Value, childPath, sourceLanguage, assetService, datasetId, referencedEntries);
            if (childNode != null)
            {
                hasChildren = true;
                container.AppendChild(childNode);
            }
        }

        return hasChildren ? container : null;
    }

    private static HtmlNode? ConvertArrayToHtml(HtmlDocument doc, JArray arr, string currentPath, string sourceLanguage, 
        AssetService assetService, string datasetId, Dictionary<string, JObject>? referencedEntries = null)
    {
        if (IsInternationalizedArray(arr))
        {
            var itemForSource = arr
                .OfType<JObject>()
                .FirstOrDefault(o => o["_key"]?.ToString() == sourceLanguage);

            if (itemForSource != null)
            {
                var itemNode = ConvertObjectToHtml(doc, itemForSource, currentPath, sourceLanguage, assetService, datasetId, referencedEntries);
                if (itemNode != null)
                {
                    return itemNode;
                }
            }

            return null; 
        }

        var wrapperArr = doc.CreateElement("div");
        var hasChildren = false;
        for (int i = 0; i < arr.Count; i++)
        {
            var item = arr[i];
            string childPath = $"{currentPath}[{i}]";
            var childNode = ConvertTokenToHtml(doc, item, childPath, sourceLanguage, assetService, datasetId, referencedEntries);
            if (childNode != null)
            {
                hasChildren = true;
                wrapperArr.AppendChild(childNode);
            }
        }

        return hasChildren ? wrapperArr : null;
    }

    private static bool IsInternationalizedValue(JObject obj)
    {
        if (obj.TryGetValue("_type", out var typeToken))
        {
            string typeStr = typeToken.ToString();
            return typeStr.Contains("internationalizedArray", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool IsInternationalizedArray(JArray arr)
    {
        return arr.Count > 0 && arr.All(i => i is JObject jo && IsInternationalizedValue(jo));
    }

    private static string EscapeHtml(string text)
    {
        return System.Net.WebUtility.HtmlEncode(text);
    }
    
    private static string GetContentTitle(JObject content, string sourceLanguage)
    {
        if (content.TryGetValue("title", out var titleToken))
        {
            if (titleToken is JArray titleArray && IsInternationalizedArray(titleArray))
            {
                var localizedTitle = titleArray
                    .OfType<JObject>()
                    .FirstOrDefault(o => o["_key"]?.ToString() == sourceLanguage);
                
                if (localizedTitle != null && localizedTitle["value"] != null)
                {
                    return localizedTitle["value"]!.ToString();
                }
            }
            else if (titleToken.Type == JTokenType.String)
            {
                return titleToken.ToString();
            }
        }
        
        if (content.TryGetValue("name", out var nameToken) && nameToken.Type == JTokenType.String)
        {
            return nameToken.ToString();
        }
        
        if (content.TryGetValue("slug", out var slugToken) && 
            slugToken is JObject slugObj &&
            slugObj["current"] != null)
        {
            return slugObj["current"]!.ToString();
        }
        
        return string.Empty;
    }
}