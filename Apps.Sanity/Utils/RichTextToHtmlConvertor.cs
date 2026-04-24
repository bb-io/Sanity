using Apps.Sanity.Services;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace Apps.Sanity.Utils;

public static class RichTextToHtmlConvertor
{
    public static HtmlNode ConvertToHtml(JArray jToken, HtmlDocument doc, string currentPath, string entityId,
        AssetService assetService, string datasetId,
        HashSet<string>? excludedFields = null,
        HashSet<string>? strictExcludedFields = null)
    {
        var wrapper = doc.CreateElement("div");
        wrapper.SetAttributeValue("data-json-path", currentPath);
        JsonToHtmlConverter.AddBlackbirdKey(wrapper, entityId, currentPath);
        wrapper.SetAttributeValue("data-rich-text", "true");
        wrapper.SetAttributeValue("data-original-json", jToken.ToString(Newtonsoft.Json.Formatting.None));

        foreach (var block in jToken)
        {
            if (block is JObject blockObj)
            {
                var blockIndex = jToken.IndexOf(block);
                var blockNode = ProcessBlock(blockObj, doc, $"{currentPath}", blockIndex, assetService, datasetId,
                    excludedFields, strictExcludedFields);
                wrapper.AppendChild(blockNode);
            }
        }

        return wrapper;
    }

    private static HtmlNode ProcessBlock(JObject block, HtmlDocument doc, string basePath, int blockIndex,
        AssetService assetService, string datasetId,
        HashSet<string>? excludedFields,
        HashSet<string>? strictExcludedFields)
    {
        var blockType = block["_type"]?.ToString();
        var blockKey = block["_key"]?.ToString();
        var blockPath = $"{basePath}[?(@._key=='{blockKey}')]";
        var blockJsonPath = $"{basePath}[{blockIndex}]";

        switch (blockType)
        {
            case "block":
                return ProcessTextBlock(block, doc, blockPath);
            case "image":
                return ProcessImageBlock(block, doc, blockPath, blockJsonPath, assetService, datasetId,
                    excludedFields, strictExcludedFields);
            case "reference":
            case "snippet-ref":
                return ProcessReferenceBlock(block, doc, blockPath);
            default:
                if (block["content"] is JArray contentArray)
                {
                    return ProcessCustomBlock(block, doc, blockPath, blockJsonPath, contentArray, assetService,
                        datasetId, excludedFields, strictExcludedFields);
                }
                
                var unknownNode = doc.CreateElement("div");
                unknownNode.SetAttributeValue("data-block-path", blockPath);
                unknownNode.SetAttributeValue("data-type", blockType ?? "unknown");
                return unknownNode;
        }
    }

    private static HtmlNode ProcessTextBlock(JObject block, HtmlDocument doc, string blockPath)
    {
        var style = block["style"]?.ToString() ?? "normal";
        var listItem = block["listItem"]?.ToString();
        var level = block["level"]?.Value<int>() ?? 0;

        HtmlNode blockNode;
        if (!string.IsNullOrEmpty(listItem))
        {
            blockNode = doc.CreateElement("li");
            blockNode.SetAttributeValue("data-list-type", listItem);
            blockNode.SetAttributeValue("data-list-level", level.ToString());
        }
        else
        {
            blockNode = style switch
            {
                "h1" => doc.CreateElement("h1"),
                "h2" => doc.CreateElement("h2"),
                "h3" => doc.CreateElement("h3"),
                "h4" => doc.CreateElement("h4"),
                "h5" => doc.CreateElement("h5"),
                "h6" => doc.CreateElement("h6"),
                "blockquote" => doc.CreateElement("blockquote"),
                _ => doc.CreateElement("p")
            };
        }
        blockNode.SetAttributeValue("data-block-path", blockPath);
        blockNode.SetAttributeValue("data-block-key", block["_key"]?.ToString()!);

        var markDefs = block["markDefs"] as JArray;
        var children = block["children"] as JArray;
        if (children != null)
        {
            foreach (var child in children)
            {
                if (child is JObject childObj)
                {
                    var childType = childObj["_type"]?.ToString();
                    if (childType == "span")
                    {
                        AppendSpanContent(childObj, blockNode, doc, markDefs);
                    }
                }
            }
        }

        return blockNode;
    }

    private static void AppendSpanContent(JObject span, HtmlNode parentNode, HtmlDocument doc, JArray? markDefs)
    {
        var text = span["text"]?.ToString() ?? "";
        var marks = span["marks"] as JArray;

        if (string.IsNullOrEmpty(text))
        {
            var brNode = doc.CreateElement("br");
            parentNode.AppendChild(brNode);
            return;
        }

        if (marks == null || !marks.Any())
        {
            AppendTextWithBrElements(text, parentNode, doc);
            return;
        }

        // For marked spans, split on \n, wrap each part in marks, and insert <br> between them
        var parts = text.Split('\n');
        for (int partIdx = 0; partIdx < parts.Length; partIdx++)
        {
            if (partIdx > 0)
            {
                parentNode.AppendChild(doc.CreateElement("br"));
            }
            
            var partText = parts[partIdx];
            if (string.IsNullOrEmpty(partText))
                continue;

            HtmlNode formattedNode = doc.CreateTextNode(partText);
            foreach (var mark in marks)
            {
                var markId = mark.ToString();
                var markDef = markDefs?.FirstOrDefault(m => m["_key"]?.ToString() == markId);
            
                if (markDef != null)
                {
                    var markType = markDef["_type"]?.ToString();
                    if (markType == "link")
                    {
                        var linkNode = doc.CreateElement("a");
                        linkNode.SetAttributeValue("href", markDef["href"]?.ToString()!);
                        linkNode.AppendChild(formattedNode);
                        formattedNode = linkNode;
                    }
                }
                else
                {
                    switch (markId)
                    {
                        case "strong":
                            var strongNode = doc.CreateElement("b");
                            strongNode.AppendChild(formattedNode);
                            formattedNode = strongNode;
                            break;
                        case "em":
                            var emNode = doc.CreateElement("i");
                            emNode.AppendChild(formattedNode);
                            formattedNode = emNode;
                            break;
                        case "code":
                            var codeNode = doc.CreateElement("code");
                            codeNode.AppendChild(formattedNode);
                            formattedNode = codeNode;
                            break;
                        case "underline":
                            var underlineNode = doc.CreateElement("u");
                            underlineNode.AppendChild(formattedNode);
                            formattedNode = underlineNode;
                            break;
                        case "strike-through":
                            var strikeNode = doc.CreateElement("s");
                            strikeNode.AppendChild(formattedNode);
                            formattedNode = strikeNode;
                            break;
                        default:
                            var genericMarkNode = doc.CreateElement("span");
                            genericMarkNode.SetAttributeValue("data-mark", markId);
                            genericMarkNode.AppendChild(formattedNode);
                            formattedNode = genericMarkNode;
                            break;
                    }
                }
            }

            parentNode.AppendChild(formattedNode);
        }
    }

    /// <summary>
    /// Appends text content to a parent node, converting \n to actual &lt;br&gt; elements.
    /// </summary>
    private static void AppendTextWithBrElements(string text, HtmlNode parentNode, HtmlDocument doc)
    {
        var parts = text.Split('\n');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 0)
            {
                parentNode.AppendChild(doc.CreateTextNode(parts[i]));
            }
            if (i < parts.Length - 1)
            {
                parentNode.AppendChild(doc.CreateElement("br"));
            }
        }
    }

    private static HtmlNode ProcessImageBlock(JObject block, HtmlDocument doc, string blockPath, string blockJsonPath,
        AssetService assetService, string datasetId,
        HashSet<string>? excludedFields,
        HashSet<string>? strictExcludedFields)
    {
        var container = doc.CreateElement("div");
        container.SetAttributeValue("data-block-path", blockPath);
        container.SetAttributeValue("data-block-key", block["_key"]?.ToString()!);
        container.SetAttributeValue("data-type", block["_type"]?.ToString() ?? "image");

        var imgNode = doc.CreateElement("img");
        imgNode.SetAttributeValue("translate", "no");
        imgNode.SetAttributeValue("style", "height: auto; max-width: 50%;");
        
        var assetRef = block["asset"]?["_ref"]?.ToString();
        if (!string.IsNullOrEmpty(assetRef))
        {
            var assetUrl = assetService.GetAssetUrlAsync(datasetId, assetRef).Result;
            
            imgNode.SetAttributeValue("src", assetUrl);
            imgNode.SetAttributeValue("data-asset-ref", assetRef);
            if (assetRef.Contains("-"))
            {
                var parts = assetRef.Split('-');
                if (parts.Length >= 3)
                {
                    imgNode.SetAttributeValue("data-format", parts[^1]);
                    var dimensions = parts[^2];
                    if (dimensions.Contains("x"))
                    {
                        var dimensionParts = dimensions.Split('x');
                        imgNode.SetAttributeValue("width", dimensionParts[0]);
                        imgNode.SetAttributeValue("height", dimensionParts[1]);
                    }
                }
            }
        }
        
        container.AppendChild(imgNode);

        if (!ShouldSkipPropertyByExclusion("alt", block["alt"], excludedFields, strictExcludedFields)
            && block["alt"] is JValue altValue && altValue.Type == JTokenType.String)
        {
            var altText = altValue.ToString();
            if (!string.IsNullOrEmpty(altText))
            {
                var altNode = doc.CreateElement("div");
                altNode.SetAttributeValue("data-json-path", $"{blockJsonPath}.alt");
                altNode.AppendChild(doc.CreateTextNode(altText));
                container.AppendChild(altNode);
            }
        }

        return container;
    }

    private static HtmlNode ProcessReferenceBlock(JObject block, HtmlDocument doc, string blockPath)
    {
        var refNode = doc.CreateElement("div");
        refNode.SetAttributeValue("translate", "no");
        refNode.SetAttributeValue("data-block-path", blockPath);
        refNode.SetAttributeValue("data-block-key", block["_key"]?.ToString()!);
        
        var blockType = block["_type"]?.ToString() ?? "reference";
        refNode.SetAttributeValue("data-type", blockType);

        var refId = block["_ref"]?.ToString();
        if (!string.IsNullOrEmpty(refId))
        {
            refNode.SetAttributeValue("data-ref-id", refId);
        }

        return refNode;
    }

    private static HtmlNode ProcessCustomBlock(JObject block, HtmlDocument doc, string blockPath, string blockJsonPath,
        JArray contentArray, AssetService assetService, string datasetId,
        HashSet<string>? excludedFields,
        HashSet<string>? strictExcludedFields)
    {
        var customNode = doc.CreateElement("div");
        customNode.SetAttributeValue("data-block-path", blockPath);
        customNode.SetAttributeValue("data-type", block["_type"]?.ToString() ?? "custom");
        customNode.SetAttributeValue("data-block-key", block["_key"]?.ToString()!);
        
        var blockCopy = (JObject)block.DeepClone();
        blockCopy.Remove("content");
        customNode.SetAttributeValue("data-original-block", blockCopy.ToString(Newtonsoft.Json.Formatting.None));

        AppendStringLeaves(block, customNode, doc, blockJsonPath, ["content"], excludedFields,
            strictExcludedFields);
        
        for (int i = 0; i < contentArray.Count; i++)
        {
            var contentBlock = contentArray[i];
            if (contentBlock is JObject contentBlockObj)
            {
                var childNode = ProcessBlock(contentBlockObj, doc, blockJsonPath + ".content", i, assetService,
                    datasetId, excludedFields, strictExcludedFields);
                if (childNode != null)
                {
                    customNode.AppendChild(childNode);
                }
            }
        }
        
        return customNode;
    }

    private static void AppendStringLeaves(JToken token, HtmlNode parentNode, HtmlDocument doc, string currentPath,
        HashSet<string>? excludedPropertyNames = null,
        HashSet<string>? excludedFields = null,
        HashSet<string>? strictExcludedFields = null)
    {
        if (token is JValue value)
        {
            if (value.Type != JTokenType.String)
            {
                return;
            }

            var text = value.ToString();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var textNode = doc.CreateElement("div");
            textNode.SetAttributeValue("data-json-path", currentPath);
            textNode.AppendChild(doc.CreateTextNode(text));
            parentNode.AppendChild(textNode);
            return;
        }

        if (token is JObject obj)
        {
            foreach (var property in obj.Properties())
            {
                if (property.Name.StartsWith("_"))
                {
                    continue;
                }

                if (excludedPropertyNames != null && excludedPropertyNames.Contains(property.Name))
                {
                    continue;
                }

                if (ShouldSkipPropertyByExclusion(property.Name, property.Value, excludedFields, strictExcludedFields))
                {
                    continue;
                }

                AppendStringLeaves(property.Value, parentNode, doc, $"{currentPath}.{property.Name}",
                    excludedPropertyNames, excludedFields, strictExcludedFields);
            }

            return;
        }

        if (token is JArray array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                AppendStringLeaves(array[i], parentNode, doc, $"{currentPath}[{i}]", excludedPropertyNames,
                    excludedFields, strictExcludedFields);
            }
        }
    }

    private static bool ShouldSkipPropertyByExclusion(string propertyName, JToken? propertyValue,
        HashSet<string>? excludedFields, HashSet<string>? strictExcludedFields)
    {
        if (strictExcludedFields != null && strictExcludedFields.Contains(propertyName))
        {
            return true;
        }

        if (excludedFields == null || !excludedFields.Contains(propertyName))
        {
            return false;
        }

        if (propertyValue is JObject)
        {
            return false;
        }

        if (propertyValue is JArray arr)
        {
            return !arr.Any(item => item is JObject || item is JArray);
        }

        return true;
    }
}
