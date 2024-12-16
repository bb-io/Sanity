using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace Apps.Sanity.Utils;

public static class JsonToHtmlConverter
{
    public static string ToHtml(this JObject jObject, string contentId, string sourceLanguage)
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
        
        var titleNode = doc.CreateElement("title");
        titleNode.AppendChild(doc.CreateTextNode("Content"));
        headNode.AppendChild(titleNode);

        var bodyNode = doc.CreateElement("body");
        htmlNode.AppendChild(bodyNode);

        foreach (var property in jObject.Properties())
        {
            string propName = property.Name;
            JToken propValue = property.Value;

            if (propName.StartsWith("_"))
            {
                continue;
            }

            var convertedNode = ConvertTokenToHtml(doc, propValue, propName, sourceLanguage);
            if (convertedNode != null!)
            {
                bodyNode.AppendChild(convertedNode);
            }
        }

        return doc.DocumentNode.OuterHtml;
    }

    private static HtmlNode? ConvertTokenToHtml(HtmlDocument doc, JToken token, string currentPath, string sourceLanguage)
    {
        if (token is JObject obj)
        {
            return ConvertObjectToHtml(doc, obj, currentPath, sourceLanguage);
        }
        else if (token is JArray arr)
        {
            return ConvertArrayToHtml(doc, arr, currentPath, sourceLanguage);
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

    private static HtmlNode? ConvertObjectToHtml(HtmlDocument doc, JObject obj, string currentPath, string sourceLanguage)
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
                            var childNode = ConvertTokenToHtml(doc, childValue, childPath, sourceLanguage);
                            if (childNode != null)
                            {
                                div.AppendChild(childNode);
                            }
                        }
                    }
                    return div;
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
            var childNode = ConvertTokenToHtml(doc, property.Value, childPath, sourceLanguage);
            if (childNode != null)
            {
                hasChildren = true;
                container.AppendChild(childNode);
            }
        }

        return hasChildren ? container : null;
    }

    private static HtmlNode? ConvertArrayToHtml(HtmlDocument doc, JArray arr, string currentPath, string sourceLanguage)
    {
        if (IsInternationalizedArray(arr))
        {
            var itemForSource = arr
                .OfType<JObject>()
                .FirstOrDefault(o => o["_key"]?.ToString() == sourceLanguage);

            if (itemForSource != null)
            {
                var wrapper = doc.CreateElement("div");
                wrapper.SetAttributeValue("data-json-path", currentPath);

                var itemNode = ConvertObjectToHtml(doc, itemForSource, currentPath, sourceLanguage);
                if (itemNode != null)
                {
                    wrapper.AppendChild(itemNode);
                    return wrapper;
                }
            }

            return null; 
        }

        var wrapperArr = doc.CreateElement("div");
        bool hasChildren = false;

        for (int i = 0; i < arr.Count; i++)
        {
            var item = arr[i];
            string childPath = $"{currentPath}[{i}]";
            var childNode = ConvertTokenToHtml(doc, item, childPath, sourceLanguage);
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
}