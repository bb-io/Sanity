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
        if (node.NodeType == HtmlNodeType.Text)
        {
            currentText.Append(node.InnerText);
            return;
        }
        
        if (node.NodeType != HtmlNodeType.Element)
            return;
        
        if (node.Name == "br" && node.HasAttributes && node.Attributes.Contains("data-span-key"))
        {
            if (currentText.Length > 0)
            {
                spans.Add(CreateSpan(currentText.ToString(), currentMarks));
                currentText.Clear();
            }
            
            var spanKey = node.GetAttributeValue("data-span-key", null!);
            var emptySpan = new JObject
            {
                ["_key"] = spanKey,
                ["_type"] = "span",
                ["text"] = "",
                ["marks"] = new JArray()
            };
            
            spans.Add(emptySpan);
            return;
        }
        
        if (node.Name == "span" && node.HasAttributes && node.Attributes.Contains("data-span-key"))
        {
            var spanKey = node.GetAttributeValue("data-span-key", null!);
            var spanMarks = new List<string>(currentMarks);
            if (currentText.Length > 0)
            {
                spans.Add(CreateSpan(currentText.ToString(), currentMarks));
                currentText.Clear();
            }
            
            var spanText = new StringBuilder();
            foreach (var child in node.ChildNodes)
            {
                if (child.NodeType == HtmlNodeType.Text)
                {
                    spanText.Append(child.InnerText);
                }
                else if (child.NodeType == HtmlNodeType.Element)
                {
                    if (child.Name == "a" && child.Attributes["href"] != null!)
                    {
                        var href = child.GetAttributeValue("href", "");
                        var originalMarkKey = child.GetAttributeValue("data-mark-key", null!) ?? 
                                             GenerateSanityCompatibleKey();
                        
                        var markExists = false;
                        foreach (var existingMark in markDefs)
                        {
                            if (existingMark["_type"]?.ToString() == "link" && 
                                existingMark["href"]?.ToString() == href)
                            {
                                originalMarkKey = existingMark["_key"]!.ToString();
                                markExists = true;
                                break;
                            }
                        }
                        
                        if (!markExists)
                        {
                            var markDef = new JObject
                            {
                                ["_key"] = originalMarkKey,
                                ["_type"] = "link",
                                ["href"] = href
                            };
                            markDefs.Add(markDef);
                        }
                        
                        spanMarks.Add(originalMarkKey);
                        spanText.Append(child.InnerText);
                    }
                    else
                    {
                        var mark = GetMarkFromElement(child.Name);
                        if (mark != null)
                        {
                            if (!spanMarks.Contains(mark))
                                spanMarks.Add(mark);
                            foreach (var nestedChild in child.ChildNodes)
                            {
                                if (nestedChild.NodeType == HtmlNodeType.Text)
                                    spanText.Append(nestedChild.InnerText);
                            }
                        }
                    }
                }
            }
            
            JObject spanObj;
            if (!string.IsNullOrEmpty(spanKey))
            {
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
        
        if (node.Name == "a" && node.Attributes["href"] != null!)
        {
            if (currentText.Length > 0)
            {
                spans.Add(CreateSpan(currentText.ToString(), currentMarks));
                currentText.Clear();
            }
            
            var href = node.GetAttributeValue("href", "");
            var markKey = node.GetAttributeValue("data-mark-key", null!) ?? 
                         GenerateSanityCompatibleKey();
            
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
            
            var linksMarks = new List<string>(currentMarks) { markKey };
            spans.Add(CreateSpan(node.InnerText, linksMarks));
            return;
        }
        
        var elementMark = GetMarkFromElement(node.Name);
        if (elementMark != null)
        {
            currentMarks.Add(elementMark);
            foreach (var child in node.ChildNodes)
            {
                ProcessNodeForSpans(child, spans, currentText, currentMarks, markDefs);
            }
            
            currentMarks.Remove(elementMark);
        }
        else
        {
            if (currentText.Length > 0)
            {
                spans.Add(CreateSpan(currentText.ToString(), currentMarks));
                currentText.Clear();
            }
            
            foreach (var child in node.ChildNodes)
            {
                ProcessNodeForSpans(child, spans, currentText, currentMarks, markDefs);
            }
        }
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