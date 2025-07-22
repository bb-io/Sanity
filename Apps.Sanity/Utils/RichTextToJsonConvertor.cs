using System.Text;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace Apps.Sanity.Utils;

public static class RichTextToJsonConvertor
{
    public static JArray ConvertFromHtml(HtmlNode richTextNode)
    {
        // If the original JSON is preserved, we can use it as a base
        var originalJson = richTextNode.GetAttributeValue("data-original-json", null);
        JArray result;
        
        if (!string.IsNullOrEmpty(originalJson))
        {
            try
            {
                result = JArray.Parse(originalJson);
            }
            catch
            {
                // If parsing fails, start with an empty array
                result = new JArray();
            }
        }
        else
        {
            result = new JArray();
        }
        
        // Clear the array to rebuild it from the HTML
        result.Clear();
        
        // Process all children of the rich text container
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
    
    public static JObject CreatePatchObject(HtmlNode richTextNode, JObject currentJObject, string contentId, 
        string sourceLanguage, string targetLanguage)
    {
        // Get the JSON path from the HTML node
        var jsonPath = richTextNode.GetAttributeValue("data-json-path", null);
        if (string.IsNullOrEmpty(jsonPath))
            return null;
        
        // Convert the rich text HTML to Sanity-compatible JSON
        var convertedJson = ConvertFromHtml(richTextNode);
        
        // Extract the base path (without language specifier) and check if target language exists
        var basePath = ExtractBasePath(jsonPath, sourceLanguage);
        bool targetLanguageExists = DoesTargetLanguageExist(currentJObject, basePath, targetLanguage);
        
        // Create appropriate patch based on whether target language exists
        if (targetLanguageExists)
        {
            // If target language exists, use 'replace'
            return CreateReplacePatch(contentId, basePath, targetLanguage, convertedJson);
        }
        else
        {
            // If target language doesn't exist, use 'insert.after'
            return CreateInsertAfterPatch(contentId, basePath, targetLanguage, convertedJson);
        }
    }
    
    private static string ExtractBasePath(string jsonPath, string sourceLanguage)
    {
        // Extract the base path without the language specifier
        // Example: "contentMultilingual[en].value" -> "contentMultilingual"
        int bracketIndex = jsonPath.IndexOf('[');
        if (bracketIndex > 0)
        {
            return jsonPath.Substring(0, bracketIndex);
        }
        return jsonPath;
    }
    
    private static bool DoesTargetLanguageExist(JObject jObject, string basePath, string targetLanguage)
    {
        // Check if the target language already exists in the array
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
    
    private static JObject CreateReplacePatch(string contentId, string basePath, string targetLanguage, JArray content)
    {
        // Create patch object for replace operation
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
                            ["_type"] = InferInternationalizedType(basePath, targetLanguage),
                            ["value"] = content
                        }
                    }
                }
            }
        };
        
        return patch;
    }
    
    private static JObject CreateInsertAfterPatch(string contentId, string basePath, string targetLanguage, JArray content)
    {
        // Create patch object for insert.after operation
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
                            ["_type"] = InferInternationalizedType(basePath, targetLanguage),
                            ["value"] = content
                        }
                    }
                }
            }
        };
        
        return patch;
    }
    
    private static string InferInternationalizedType(string basePath, string targetLanguage)
    {
        // We need to determine the correct type for this specific field
        // Based on the pattern observed in the HTML and basePath
        
        // Check if the basePath indicates a specific content type
        if (basePath.Contains("contentMultilingual"))
        {
            return "internationalizedArrayBlockAndSnippetArrayValue";
        }
        else if (basePath.Contains("description"))
        {
            return "internationalizedArrayBlockContent";
        }
        else if (basePath.Contains("title") || basePath.Contains("name"))
        {
            return "internationalizedArrayStringValue";
        }
        
        // Default fallback
        return "internationalizedArrayBlockContent";
    }
    
    private static JObject ConvertHtmlElementToBlock(HtmlNode node)
    {
        // Try to get the block key from the data attribute
        string blockKey = node.GetAttributeValue("data-block-key", null);
        
        if (node.Name == "li" && node.ParentNode != null)
        {
            return ProcessListItemToBlock(node);
        }
        else if (node.Name == "ul" || node.Name == "ol")
        {
            // We don't directly convert lists - we process their list items individually
            var listItems = new JArray();
            foreach (var child in node.ChildNodes)
            {
                if (child.Name == "li")
                {
                    var listItemBlock = ProcessListItemToBlock(child);
                    if (listItemBlock != null)
                    {
                        listItems.Add(listItemBlock);
                    }
                }
            }
            
            // Return null as we don't want to add the list itself as a block
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
        else
        {
            // Process text blocks (paragraphs, headings, etc.)
            return ProcessTextToBlock(node);
        }
    }
    
    private static JObject ProcessListItemToBlock(HtmlNode listItemNode)
    {
        var block = new JObject();
        
        // Try to get the block key from the data attribute
        string blockKey = listItemNode.GetAttributeValue("data-block-key", Guid.NewGuid().ToString("N"));
        
        block["_key"] = blockKey;
        block["_type"] = "block";
        
        // Determine list type (bullet/numbered)
        string listType = "bullet"; // Default to bullet
        if (listItemNode.ParentNode != null)
        {
            if (listItemNode.ParentNode.Name == "ol")
            {
                listType = "number";
            }
        }
        
        block["listItem"] = listType;
        
        // Level is usually 1 for normal lists
        block["level"] = 1;
        
        // Style is always normal for list items
        block["style"] = "normal";
        
        // Process children (text content)
        block["children"] = ProcessChildrenToSpans(listItemNode);
        
        // Empty mark definitions
        block["markDefs"] = new JArray();
        
        return block;
    }
    
    private static JObject ProcessImageToBlock(HtmlNode imgNode)
    {
        var block = new JObject();
        
        string blockKey = imgNode.GetAttributeValue("data-block-key", Guid.NewGuid().ToString("N"));
        string assetRef = imgNode.GetAttributeValue("data-asset-ref", "");
        
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
        
        string blockKey = refNode.GetAttributeValue("data-block-key", Guid.NewGuid().ToString("N"));
        string refId = refNode.GetAttributeValue("data-ref-id", "");
        
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
        
        // Try to get the block key from the data attribute
        string blockKey = textNode.GetAttributeValue("data-block-key", Guid.NewGuid().ToString("N"));
        
        block["_key"] = blockKey;
        block["_type"] = "block";
        
        // Determine style based on node type
        string style = "normal";
        switch (textNode.Name)
        {
            case "h1":
                style = "h1";
                break;
            case "h2":
                style = "h2";
                break;
            case "h3":
                style = "h3";
                break;
            case "h4":
                style = "h4";
                break;
            case "h5":
                style = "h5";
                break;
            case "h6":
                style = "h6";
                break;
            case "blockquote":
                style = "blockquote";
                break;
            default:
                style = "normal";
                break;
        }
        
        block["style"] = style;
        
        // Process children (text content)
        block["children"] = ProcessChildrenToSpans(textNode);
        
        // Empty mark definitions
        block["markDefs"] = new JArray();
        
        return block;
    }
    
    private static JArray ProcessChildrenToSpans(HtmlNode parentNode)
    {
        var spans = new JArray();
        var currentText = new StringBuilder();
        var currentMarks = new List<string>();
        
        ProcessNodeForSpans(parentNode, spans, currentText, currentMarks);
        
        // Add any remaining text as a final span
        if (currentText.Length > 0)
        {
            spans.Add(CreateSpan(currentText.ToString(), currentMarks));
        }
        
        return spans;
    }
    
    private static void ProcessNodeForSpans(HtmlNode node, JArray spans, StringBuilder currentText, List<string> currentMarks)
    {
        if (node.NodeType == HtmlNodeType.Text)
        {
            currentText.Append(node.InnerText);
            return;
        }
        
        // Skip non-element nodes
        if (node.NodeType != HtmlNodeType.Element)
            return;
        
        // Special handling for span with data-span-key which might contain marks
        if (node.Name == "span" && node.HasAttributes && node.Attributes.Contains("data-span-key"))
        {
            string spanKey = node.GetAttributeValue("data-span-key", null);
            
            // Clear current marks as we're starting a new span with potentially different marks
            var spanMarks = new List<string>(currentMarks);
            
            // If there's pending text, flush it as a span
            if (currentText.Length > 0)
            {
                spans.Add(CreateSpan(currentText.ToString(), currentMarks));
                currentText.Clear();
            }
            
            // Create a new StringBuilder for this span's content
            var spanText = new StringBuilder();
            
            // Process all children to collect text and marks
            foreach (var child in node.ChildNodes)
            {
                if (child.NodeType == HtmlNodeType.Text)
                {
                    spanText.Append(child.InnerText);
                }
                else if (child.NodeType == HtmlNodeType.Element)
                {
                    // Handle formatting elements within the span
                    var mark = GetMarkFromElement(child.Name);
                    if (mark != null)
                    {
                        // Add the mark
                        if (!spanMarks.Contains(mark))
                            spanMarks.Add(mark);
                        
                        // Process nested content
                        foreach (var nestedChild in child.ChildNodes)
                        {
                            if (nestedChild.NodeType == HtmlNodeType.Text)
                                spanText.Append(nestedChild.InnerText);
                        }
                    }
                }
            }
            
            // Add the completed span with all collected marks
            JObject spanObj;
            if (!string.IsNullOrEmpty(spanKey))
            {
                // Use original span key if available
                spanObj = new JObject
                {
                    ["_key"] = spanKey,
                    ["_type"] = "span",
                    ["text"] = spanText.ToString(),
                    ["marks"] = new JArray(spanMarks)
                };
            }
            else
            {
                spanObj = CreateSpan(spanText.ToString(), spanMarks);
            }
            
            spans.Add(spanObj);
            return;
        }
        
        // Handle direct formatting elements (outside spans)
        var elementMark = GetMarkFromElement(node.Name);
        if (elementMark != null)
        {
            // Add the mark for this formatting
            currentMarks.Add(elementMark);
            
            // Process all children with the current marks
            foreach (var child in node.ChildNodes)
            {
                ProcessNodeForSpans(child, spans, currentText, currentMarks);
            }
            
            // Remove the mark after processing children
            currentMarks.Remove(elementMark);
        }
        else
        {
            // For non-formatting elements, we need to close the current span and start a new one
            if (currentText.Length > 0)
            {
                spans.Add(CreateSpan(currentText.ToString(), currentMarks));
                currentText.Clear();
            }
            
            // Process all children
            foreach (var child in node.ChildNodes)
            {
                ProcessNodeForSpans(child, spans, currentText, currentMarks);
            }
        }
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