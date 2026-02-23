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

        // Extract original JSON from meta tag to preserve non-translatable fields
        JObject translatedContent;
        var metaOriginalJson = doc.DocumentNode.SelectSingleNode("//meta[@name='blackbird-original-json']");
        if (metaOriginalJson != null)
        {
            var originalJsonBase64 = metaOriginalJson.GetAttributeValue("content", null!);
            if (!string.IsNullOrEmpty(originalJsonBase64))
            {
                try
                {
                    var originalJsonString = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(originalJsonBase64));
                    translatedContent = JObject.Parse(originalJsonString);
                }
                catch
                {
                    // Fallback to empty object if parsing fails
                    translatedContent = new JObject();
                }
            }
            else
            {
                translatedContent = new JObject();
            }
        }
        else
        {
            // Fallback to empty object if meta tag not found (for backward compatibility)
            translatedContent = new JObject();
        }

        var richTextNodes = contentRoot.SelectNodes(".//*[@data-rich-text='true']");
        if (richTextNodes != null)
        {
            foreach (var richTextNode in richTextNodes)
            {
                var path = richTextNode.GetAttributeValue("data-json-path", null!);
                if (string.IsNullOrEmpty(path)) continue;

                // Get original rich text array from translatedContent to preserve marks
                var originalRichTextArray = GetNestedProperty(translatedContent, path) as JArray;
                var richTextArray = ConvertRichTextFromHtml(richTextNode, originalRichTextArray);
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
            // Handle draft/published ID conversion
            if (translatedContent["_id"] != null)
            {
                var currentId = translatedContent["_id"]!.ToString();
                
                if (!publish && !currentId.StartsWith("drafts."))
                {
                    // Convert to draft ID if publish is false and not already a draft
                    translatedContent["_id"] = $"drafts.{currentId}";
                }
                else if (publish && currentId.StartsWith("drafts."))
                {
                    // Convert to published ID if publish is true and currently a draft
                    translatedContent["_id"] = currentId.Substring("drafts.".Length);
                }
            }
            
            translatedContent["language"] = targetLanguage;

            patches.Add(translatedContent);
        }

        return patches;
    }

    private static JArray ConvertRichTextFromHtml(HtmlNode richTextNode, JArray? originalRichTextArray = null)
    {
        JArray result;
        
        // First, try to use the provided original array from translatedContent
        if (originalRichTextArray != null && originalRichTextArray.Count > 0)
        {
            // Deep clone to avoid modifying the original
            result = (JArray)originalRichTextArray.DeepClone();
        }
        else
        {
            // Fallback: try to parse from HTML attribute
            var originalJson = richTextNode.GetAttributeValue("data-original-json", null!);
            if (!string.IsNullOrEmpty(originalJson))
            {
                try
                {
                    // Ensure HTML entities are decoded
                    originalJson = System.Net.WebUtility.HtmlDecode(originalJson);
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
        }

        // Use original JSON as base and only update text from HTML
        if (result.Count > 0)
        {
            // Build a map of block keys to HTML elements
            var blockElementMap = new Dictionary<string, HtmlNode>();
            foreach (var child in richTextNode.ChildNodes)
            {
                if (child.NodeType == HtmlNodeType.Element)
                {
                    var blockKey = child.GetAttributeValue("data-block-key", null!);
                    if (!string.IsNullOrEmpty(blockKey))
                    {
                        blockElementMap[blockKey] = child;
                    }
                }
            }

            // Update text in original blocks while preserving marks and markDefs
            foreach (var block in result)
            {
                if (block is JObject blockObj)
                {
                    var blockKey = blockObj["_key"]?.ToString();
                    if (!string.IsNullOrEmpty(blockKey) && blockElementMap.TryGetValue(blockKey, out var htmlElement))
                    {
                        UpdateBlockTextFromHtml(blockObj, htmlElement);
                    }
                }
            }

            return result;
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

    private static void UpdateBlockTextFromHtml(JObject block, HtmlNode htmlElement)
    {
        var children = block["children"] as JArray;
        if (children == null || children.Count == 0)
        {
            return;
        }

        var htmlText = ExtractPlainText(htmlElement);
        if (string.IsNullOrEmpty(htmlText))
        {
            return;
        }

        if (children.Count == 1 && children[0] is JObject singleSpan && singleSpan["_type"]?.ToString() == "span")
        {
            singleSpan["text"] = htmlText;
            return;
        }

        var textSegments = ExtractTextSegments(htmlElement);
        var segmentIndex = 0;
        foreach (var child in children)
        {
            if (child is JObject childObj && childObj["_type"]?.ToString() == "span")
            {
                if (segmentIndex < textSegments.Count)
                {
                    childObj["text"] = textSegments[segmentIndex];
                    segmentIndex++;
                }
            }
        }
    }

    private static string ExtractPlainText(HtmlNode element)
    {
        var text = element.InnerText.Replace("\n", " ").Trim();
        return System.Net.WebUtility.HtmlDecode(text);
    }

    private static List<string> ExtractTextSegments(HtmlNode element)
    {
        var segments = new List<string>();
        ExtractTextSegmentsRecursive(element, segments);
        return segments;
    }

    private static void ExtractTextSegmentsRecursive(HtmlNode node, List<string> segments)
    {
        foreach (var child in node.ChildNodes)
        {
            if (child.NodeType == HtmlNodeType.Text)
            {
                var text = child.InnerText;
                if (!string.IsNullOrEmpty(text))
                {
                    segments.Add(text);
                }
            }
            else if (child.NodeType == HtmlNodeType.Element)
            {
                // Skip elements with data-mark attribute and recursively process their children
                if (child.Name.ToLower() == "br")
                {
                    // Handle line breaks
                    if (segments.Count > 0)
                    {
                        segments[segments.Count - 1] += "\n";
                    }
                }
                else if (child.Name.ToLower() == "span" && child.Attributes["data-mark"] != null)
                {
                    // This is a mark wrapper, process children
                    ExtractTextSegmentsRecursive(child, segments);
                }
                else if (child.Name.ToLower() == "a" || child.Name.ToLower() == "b" || 
                         child.Name.ToLower() == "i" || child.Name.ToLower() == "strong" || 
                         child.Name.ToLower() == "em")
                {
                    // These are mark wrappers, process children
                    ExtractTextSegmentsRecursive(child, segments);
                }
                else
                {
                    ExtractTextSegmentsRecursive(child, segments);
                }
            }
        }
    }

    private static JObject? ParseBlockFromHtmlElement(HtmlNode element)
    {
        var blockKey = element.GetAttributeValue("data-block-key", string.Empty);
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

    private static JToken? GetNestedProperty(JObject obj, string propertyPath)
    {
        var parts = propertyPath.Split('.');
        JToken? current = obj;

        foreach (var part in parts)
        {
            if (current == null)
            {
                return null;
            }

            var bracketIndex = part.IndexOf('[');

            if (bracketIndex > 0)
            {
                var arrayName = part.Substring(0, bracketIndex);
                var indexStr = part.Substring(bracketIndex + 1, part.Length - bracketIndex - 2);

                if (current is JObject jObj && jObj[arrayName] is JArray arr)
                {
                    if (int.TryParse(indexStr, out var index) && index < arr.Count)
                    {
                        current = arr[index];
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
            else
            {
                if (current is JObject jObj)
                {
                    current = jObj[part];
                }
                else
                {
                    return null;
                }
            }
        }

        return current;
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
