using Apps.Sanity.Utils;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace Apps.Sanity.Converters;

public class DocumentLevelHtmlToJsonConverter : IHtmlToJsonConverter
{
    public List<JObject> ToJsonPatches(string html, JObject mainContent, string targetLanguage, bool publish,
        Dictionary<string, JObject>? referencedContents = null)
    {
        var mainContentId = HtmlHelper.ExtractContentId(html);
        var patches = new List<JObject>();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var contentRoot = doc.DocumentNode.SelectSingleNode($"//div[@data-content-id='{mainContentId}']");
        if (contentRoot == null)
        {
            return patches;
        }

        var translatedContent = new JObject();

        var richTextNodes = contentRoot.SelectNodes(".//*[@data-rich-text='true']");
        if (richTextNodes != null)
        {
            foreach (var richTextNode in richTextNodes)
            {
                var path = richTextNode.GetAttributeValue("data-json-path", null!);
                if (string.IsNullOrEmpty(path)) continue;

                var richTextArray = ConvertRichTextFromHtml(richTextNode);
                if (richTextArray != null)
                {
                    SetNestedProperty(translatedContent, path, richTextArray);
                }
            }
        }

        var nodesWithPath = contentRoot.SelectNodes(".//*[@data-json-path]");
        if (nodesWithPath != null)
        {
            foreach (var node in nodesWithPath)
            {
                if (node.GetAttributeValue("data-rich-text", "false") == "true")
                    continue;

                var dataJsonPath = node.GetAttributeValue("data-json-path", null!);
                if (dataJsonPath == null) continue;

                var newText = node.InnerText.Trim();
                if (string.IsNullOrEmpty(newText)) continue;

                SetNestedProperty(translatedContent, dataJsonPath, newText);
            }
        }

        if (translatedContent.HasValues)
        {
            translatedContent["language"] = targetLanguage;

            patches.Add(translatedContent);
        }

        return patches;
    }

    private static JArray ConvertRichTextFromHtml(HtmlNode richTextNode)
    {
        var originalJson = richTextNode.GetAttributeValue("data-original-json", null!);
        JArray result;
        if (!string.IsNullOrEmpty(originalJson))
        {
            try
            {
                result = JArray.Parse(originalJson);
            }
            catch
            {
                result = new JArray();
            }
        }
        else
        {
            result = new JArray();
        }

        result.Clear();
        foreach (var child in richTextNode.ChildNodes)
        {
            if (child.NodeType == HtmlNodeType.Element)
            {
                var block = ParseBlockFromHtmlElement(child);
                if (block != null)
                {
                    result.Add(block);
                }
            }
        }

        return result;
    }

    private static JObject? ParseBlockFromHtmlElement(HtmlNode element)
    {
        var blockKey = element.GetAttributeValue("data-block-key", null);
        if (string.IsNullOrEmpty(blockKey))
        {
            blockKey = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 12);
        }

        var block = new JObject
        {
            ["_type"] = "block",
            ["_key"] = blockKey,
            ["markDefs"] = new JArray(),
            ["children"] = new JArray()
        };

        // Determine style based on HTML tag
        var style = element.Name.ToLower() switch
        {
            "h1" => "h1",
            "h2" => "h2",
            "h3" => "h3",
            "h4" => "h4",
            "h5" => "h5",
            "h6" => "h6",
            _ => "normal"
        };
        block["style"] = style;

        // Handle list items
        if (element.Name == "li")
        {
            var listType = element.GetAttributeValue("data-list-type", "bullet");
            var listLevel = element.GetAttributeValue("data-list-level", "1");
            
            block["listItem"] = listType;
            if (int.TryParse(listLevel, out var level))
            {
                block["level"] = level;
            }
        }

        var children = (JArray)block["children"]!;
        var markDefs = (JArray)block["markDefs"]!;
        ParseInlineContent(element, children, markDefs);

        return block;
    }

    private static void ParseInlineContent(HtmlNode node, JArray children, JArray markDefs, List<string>? currentMarks = null)
    {
        currentMarks ??= new List<string>();

        foreach (var child in node.ChildNodes)
        {
            if (child.NodeType == HtmlNodeType.Text)
            {
                var text = child.InnerText;
                if (!string.IsNullOrEmpty(text))
                {
                    children.Add(new JObject
                    {
                        ["_type"] = "span",
                        ["_key"] = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 12),
                        ["text"] = text,
                        ["marks"] = new JArray(currentMarks)
                    });
                }
            }
            else if (child.NodeType == HtmlNodeType.Element)
            {
                var elementName = child.Name.ToLower();
                var newMarks = new List<string>(currentMarks);

                // Handle links
                if (elementName == "a" && child.Attributes["href"] != null)
                {
                    var href = child.GetAttributeValue("href", "");
                    var markKey = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 12);
                    
                    markDefs.Add(new JObject
                    {
                        ["_key"] = markKey,
                        ["_type"] = "link",
                        ["href"] = href
                    });
                    
                    newMarks.Add(markKey);
                    ParseInlineContent(child, children, markDefs, newMarks);
                }
                // Handle formatting marks
                else if (elementName == "b" || elementName == "strong")
                {
                    newMarks.Add("strong");
                    ParseInlineContent(child, children, markDefs, newMarks);
                }
                else if (elementName == "i" || elementName == "em")
                {
                    newMarks.Add("em");
                    ParseInlineContent(child, children, markDefs, newMarks);
                }
                else
                {
                    ParseInlineContent(child, children, markDefs, currentMarks);
                }
            }
        }
    }

    private static void SetNestedProperty(JObject obj, string propertyPath, object value)
    {
        var parts = propertyPath.Split('.');
        var current = obj;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];
            var bracketIndex = part.IndexOf('[');

            if (bracketIndex > 0)
            {
                var arrayName = part.Substring(0, bracketIndex);
                var indexStr = part.Substring(bracketIndex + 1, part.Length - bracketIndex - 2);

                if (!int.TryParse(indexStr, out var index))
                {
                    continue;
                }

                if (current[arrayName] == null)
                {
                    current[arrayName] = new JArray();
                }

                var arr = (JArray)current[arrayName]!;
                while (arr.Count <= index)
                {
                    arr.Add(new JObject());
                }

                current = (JObject)arr[index]!;
            }
            else
            {
                if (current[part] == null)
                {
                    current[part] = new JObject();
                }

                current = (JObject)current[part]!;
            }
        }

        var lastPart = parts[^1];
        var lastBracketIndex = lastPart.IndexOf('[');

        if (lastBracketIndex > 0)
        {
            var arrayName = lastPart.Substring(0, lastBracketIndex);
            var indexStr = lastPart.Substring(lastBracketIndex + 1, lastPart.Length - lastBracketIndex - 2);

            if (int.TryParse(indexStr, out var index))
            {
                if (current[arrayName] == null)
                {
                    current[arrayName] = new JArray();
                }

                var arr = (JArray)current[arrayName]!;
                while (arr.Count <= index)
                {
                    arr.Add(new JObject());
                }

                if (value is JArray jArray)
                {
                    arr[index] = jArray;
                }
                else if (value is JToken jToken)
                {
                    arr[index] = jToken;
                }
                else
                {
                    arr[index] = JToken.FromObject(value);
                }
            }
        }
        else
        {
            if (value is JArray jArray)
            {
                current[lastPart] = jArray;
            }
            else if (value is JToken jToken)
            {
                current[lastPart] = jToken;
            }
            else
            {
                current[lastPart] = JToken.FromObject(value);
            }
        }
    }
}
