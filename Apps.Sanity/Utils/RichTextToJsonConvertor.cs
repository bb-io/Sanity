using System.Text;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace Apps.Sanity.Utils;

public static class RichTextToJsonConvertor
{
    public static JObject? CreatePatchObject(HtmlNode richTextNode, JObject currentJObject, string contentId, 
        string sourceLanguage, string targetLanguage)
    {
        var jsonPath = richTextNode.GetAttributeValue("data-json-path", null!);
        if (string.IsNullOrEmpty(jsonPath))
            return null;
        
        var convertedJson = ConvertFromHtml(richTextNode);
        var basePath = ExtractBasePath(jsonPath);
        var targetLanguageExists = DoesTargetLanguageExist(currentJObject, basePath, targetLanguage);
        
        var contentType = ExtractContentType(currentJObject, basePath, sourceLanguage);
        return targetLanguageExists 
            ? CreateReplacePatch(contentId, basePath, targetLanguage, convertedJson, contentType) 
            : CreateInsertAfterPatch(contentId, basePath, targetLanguage, convertedJson, contentType);
    }
    
    private static JArray ConvertFromHtml(HtmlNode richTextNode)
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
                var blockObj = ConvertHtmlElementToBlock(child);
                if (blockObj != null)
                {
                    result.Add(blockObj);
                }
            }
        }
        
        return result;
    }
    
    private static string ExtractContentType(JObject jObject, string basePath, string sourceLanguage)
    {
        if (jObject[basePath] is JArray array)
        {
            foreach (var item in array)
            {
                if (item is JObject obj && obj["_key"]?.ToString() == sourceLanguage)
                {
                    return obj["_type"]?.ToString() ?? "internationalizedArrayBlockContent";
                }
            }
        }
        
        return "internationalizedArrayBlockContent";
    }
    
    private static string ExtractBasePath(string jsonPath)
    {
        var bracketIndex = jsonPath.IndexOf('[');
        if (bracketIndex > 0)
        {
            return jsonPath.Substring(0, bracketIndex);
        }
        
        return jsonPath;
    }
    
    private static bool DoesTargetLanguageExist(JObject jObject, string basePath, string targetLanguage)
    {
        if (jObject[basePath] is JArray array)
        {
            foreach (var item in array)
            {
                if (item is JObject obj && obj["_key"]?.ToString() == targetLanguage)
                {
                    return true;
                }
            }
        }
        return false;
    }
    
    private static JObject CreateReplacePatch(string contentId, string basePath, string targetLanguage, JArray content, string contentType)
    {
        var patch = new JObject
        {
            ["patch"] = new JObject
            {
                ["id"] = contentId,
                ["insert"] = new JObject
                {
                    ["replace"] = $"{basePath}[_key==\"{targetLanguage}\"]",
                    ["items"] = new JArray
                    {
                        new JObject
                        {
                            ["_key"] = targetLanguage,
                            ["_type"] = contentType,
                            ["value"] = content
                        }
                    }
                }
            }
        };
        
        return patch;
    }
    
    private static JObject CreateInsertAfterPatch(string contentId, string basePath, string targetLanguage, JArray content, string contentType)
    {
        var patch = new JObject
        {
            ["patch"] = new JObject
            {
                ["id"] = contentId,
                ["insert"] = new JObject
                {
                    ["after"] = $"{basePath}[-1]",
                    ["items"] = new JArray
                    {
                        new JObject
                        {
                            ["_key"] = targetLanguage,
                            ["_type"] = contentType,
                            ["value"] = content
                        }
                    }
                }
            }
        };
        
        return patch;
    }
    
    private static JObject? ConvertHtmlElementToBlock(HtmlNode node)
    {
        if (node.Name == "li" && node.ParentNode != null!)
        {
            return ProcessListItemToBlock(node);
        }
        else if (node.Name == "ul" || node.Name == "ol")
        {
            return null;
        }
        else if (node.Name == "img")
        {
            return ProcessImageToBlock(node);
        }
        else if (node.Name == "div" && node.GetAttributeValue("data-type", "") == "reference")
        {
            return ProcessReferenceToBlock(node);
        }
        
        return ProcessTextToBlock(node);
    }
    
    private static JObject ProcessListItemToBlock(HtmlNode listItemNode)
    {
        var block = new JObject();
        var blockKey = listItemNode.GetAttributeValue("data-block-key", Guid.NewGuid().ToString("N"));
        
        block["_key"] = blockKey;
        block["_type"] = "block";
        
        var listType = listItemNode.GetAttributeValue("data-list-type", null!);
        if (string.IsNullOrEmpty(listType) && listItemNode.ParentNode != null!)
        {
            listType = listItemNode.ParentNode.Name == "ol" ? "number" : "bullet";
        }
        else if (string.IsNullOrEmpty(listType))
        {
            listType = "bullet";
        }
        
        block["listItem"] = listType;
        var level = 1;
        var levelAttr = listItemNode.GetAttributeValue("data-list-level", null!);
        if (!string.IsNullOrEmpty(levelAttr) && int.TryParse(levelAttr, out int parsedLevel))
        {
            level = parsedLevel;
        }
        
        block["level"] = level;
        block["style"] = "normal";
        
        var markDefs = new JArray();
        block["children"] = ProcessChildrenToSpans(listItemNode, markDefs);
        block["markDefs"] = markDefs;
        return block;
    }
    
    private static JObject ProcessImageToBlock(HtmlNode imgNode)
    {
        var block = new JObject();
        var blockKey = imgNode.GetAttributeValue("data-block-key", Guid.NewGuid().ToString("N"));
        var assetRef = imgNode.GetAttributeValue("data-asset-ref", "");
        
        block["_key"] = blockKey;
        block["_type"] = "image";
        
        if (!string.IsNullOrEmpty(assetRef))
        {
            block["asset"] = new JObject
            {
                ["_ref"] = assetRef,
                ["_type"] = "reference"
            };
        }
        
        return block;
    }
    
    private static JObject ProcessReferenceToBlock(HtmlNode refNode)
    {
        var block = new JObject();
        var blockKey = refNode.GetAttributeValue("data-block-key", Guid.NewGuid().ToString("N"));
        var refId = refNode.GetAttributeValue("data-ref-id", "");
        
        block["_key"] = blockKey;
        block["_type"] = "reference";
        if (!string.IsNullOrEmpty(refId))
        {
            block["_ref"] = refId;
        }
        
        return block;
    }
    
    private static JObject ProcessTextToBlock(HtmlNode textNode)
    {
        var block = new JObject();
        
        var blockKey = textNode.GetAttributeValue("data-block-key", Guid.NewGuid().ToString("N"));
        block["_key"] = blockKey;
        block["_type"] = "block";

        string style = textNode.Name switch
        {
            "h1" => "h1",
            "h2" => "h2",
            "h3" => "h3",
            "h4" => "h4",
            "h5" => "h5",
            "h6" => "h6",
            "blockquote" => "blockquote",
            _ => "normal"
        };

        block["style"] = style;
        var markDefs = new JArray();
        block["children"] = ProcessChildrenToSpans(textNode, markDefs);
        block["markDefs"] = markDefs;
        
        return block;
    }
    
    private static JArray ProcessChildrenToSpans(HtmlNode parentNode, JArray? markDefs = null)
    {
        var spans = new JArray();
        var currentText = new StringBuilder();
        var currentMarks = new List<string>();
        
        markDefs ??= new JArray();
        ProcessNodeForSpans(parentNode, spans, currentText, currentMarks, markDefs);
        if (currentText.Length > 0)
        {
            spans.Add(CreateSpan(currentText.ToString(), currentMarks));
        }
        
        return spans;
    }
    
    private static void ProcessNodeForSpans(HtmlNode node, JArray spans, StringBuilder currentText, 
        List<string> currentMarks, JArray markDefs)
    {
        // Direct text nodes - append to current text buffer
        if (node.NodeType == HtmlNodeType.Text)
        {
            currentText.Append(node.InnerText);
            return;
        }
        
        if (node.NodeType != HtmlNodeType.Element)
            return;
        
        // Handle BR tags as empty spans
        if (node.Name == "br")
        {
            // Create a span for the accumulated text before the br
            if (currentText.Length > 0)
            {
                spans.Add(CreateSpan(currentText.ToString(), currentMarks));
                currentText.Clear();
            }
            
            // Create an empty span for the br itself
            var emptySpan = new JObject
            {
                ["_key"] = GenerateSanityCompatibleKey(),
                ["_type"] = "span",
                ["text"] = "",
                ["marks"] = new JArray()
            };
            
            spans.Add(emptySpan);
            return;
        }
        
        // Handle links separately as they need markDefs
        if (node.Name == "a" && node.Attributes["href"] != null!)
        {
            if (currentText.Length > 0)
            {
                spans.Add(CreateSpan(currentText.ToString(), currentMarks));
                currentText.Clear();
            }
            
            var href = node.GetAttributeValue("href", "");
            var markKey = GenerateSanityCompatibleKey();
            
            bool markExists = false;
            foreach (var existingMark in markDefs)
            {
                if (existingMark["_type"]?.ToString() == "link" && 
                    existingMark["href"]?.ToString() == href)
                {
                    markKey = existingMark["_key"]!.ToString();
                    markExists = true;
                    break;
                }
            }
            
            if (!markExists)
            {
                var markDef = new JObject
                {
                    ["_key"] = markKey,
                    ["_type"] = "link",
                    ["href"] = href
                };
                markDefs.Add(markDef);
            }
            
            var linkMarks = new List<string>(currentMarks) { markKey };
            
            // Process children of link to maintain any nested formatting
            var linkText = new StringBuilder();
            var linkChildMarks = new List<string>(linkMarks);
            
            foreach (var child in node.ChildNodes)
            {
                if (child.NodeType == HtmlNodeType.Text)
                {
                    linkText.Append(child.InnerText);
                }
                else if (child.NodeType == HtmlNodeType.Element)
                {
                    var childElementMark = GetMarkFromElement(child.Name);
                    if (childElementMark != null)
                    {
                        if (!linkChildMarks.Contains(childElementMark))
                            linkChildMarks.Add(childElementMark);
                        
                        foreach (var nestedChild in child.ChildNodes)
                        {
                            if (nestedChild.NodeType == HtmlNodeType.Text)
                                linkText.Append(nestedChild.InnerText);
                        }
                    }
                }
            }
            
            spans.Add(CreateSpan(linkText.ToString(), linkChildMarks));
            return;
        }
        
        // Handle formatting elements
        var elementMark = GetMarkFromElement(node.Name);
        if (elementMark != null)
        {
            // Add the mark for this formatting element
            if (!currentMarks.Contains(elementMark))
                currentMarks.Add(elementMark);
            
            // Process children with the current mark applied
            foreach (var child in node.ChildNodes)
            {
                ProcessNodeForSpans(child, spans, currentText, currentMarks, markDefs);
            }
            
            // Remove this mark after processing children
            currentMarks.Remove(elementMark);
        }
        else
        {
            // For non-formatting elements (like divs), create a span for accumulated text
            // before processing children separately
            if (currentText.Length > 0 && !IsBlockElement(node.Name))
            {
                spans.Add(CreateSpan(currentText.ToString(), currentMarks));
                currentText.Clear();
            }
            
            // Process all children
            foreach (var child in node.ChildNodes)
            {
                ProcessNodeForSpans(child, spans, currentText, currentMarks, markDefs);
            }
        }
    }
    
    private static bool IsBlockElement(string nodeName)
    {
        return nodeName == "p" || nodeName == "div" || nodeName == "li" ||
               nodeName.StartsWith("h") && nodeName.Length == 2 && char.IsDigit(nodeName[1]);
    }
    
    private static string GenerateSanityCompatibleKey()
    {
        var random = new Random();
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        return new string(Enumerable.Repeat(chars, 12)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
    
    private static string? GetMarkFromElement(string elementName)
    {
        return elementName switch
        {
            "b" or "strong" => "strong",
            "i" or "em" => "em",
            "code" => "code",
            "u" => "underline",
            "s" => "strike-through",
            "a" => "link",
            _ => null
        };
    }
    
    private static JObject CreateSpan(string text, List<string> marks)
    {
        var span = new JObject
        {
            ["_key"] = Guid.NewGuid().ToString("N"),
            ["_type"] = "span",
            ["text"] = text,
            ["marks"] = new JArray(marks)
        };
        
        return span;
    }
}