using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace Apps.Sanity.Utils;

public static class RichTextToHtmlConvertor
{
    public static HtmlNode ConvertToHtml(JArray jToken, HtmlDocument doc, string currentPath)
    {
        var wrapper = doc.CreateElement("div");
        wrapper.SetAttributeValue("data-json-path", currentPath);
        wrapper.SetAttributeValue("data-rich-text", "true");
        wrapper.SetAttributeValue("data-original-json", jToken.ToString(Newtonsoft.Json.Formatting.None));

        foreach (var block in jToken)
        {
            if (block is JObject blockObj)
            {
                var blockNode = ProcessBlock(blockObj, doc, $"{currentPath}");
                wrapper.AppendChild(blockNode);
            }
        }

        return wrapper;
    }

    private static HtmlNode ProcessBlock(JObject block, HtmlDocument doc, string basePath)
    {
        var blockType = block["_type"]?.ToString();
        var blockKey = block["_key"]?.ToString();
        var blockPath = $"{basePath}[?(@._key=='{blockKey}')]";

        switch (blockType)
        {
            case "block":
                return ProcessTextBlock(block, doc, blockPath);
            case "image":
                return ProcessImageBlock(block, doc, blockPath);
            case "reference":
                return ProcessReferenceBlock(block, doc, blockPath);
            default:
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
                        var spanNode = ProcessSpan(childObj, doc, markDefs);
                        blockNode.AppendChild(spanNode);
                    }
                }
            }
        }

        return blockNode;
    }

    private static HtmlNode ProcessSpan(JObject span, HtmlDocument doc, JArray? markDefs)
    {
        var text = span["text"]?.ToString() ?? "";
        var marks = span["marks"] as JArray;
        
        var contentNode = doc.CreateTextNode(text);
        var wrapperNode = doc.CreateElement("span");
        wrapperNode.SetAttributeValue("data-span-key", span["_key"]?.ToString()!);
        
        if (marks == null || !marks.Any())
        {
            wrapperNode.AppendChild(contentNode);
            return wrapperNode;
        }
        
        HtmlNode currentNode = contentNode;
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
                    linkNode.AppendChild(currentNode);
                    currentNode = linkNode;
                }
            }
            else
            {
                switch (markId)
                {
                    case "strong":
                        var strongNode = doc.CreateElement("b");
                        strongNode.AppendChild(currentNode);
                        currentNode = strongNode;
                        break;
                    case "em":
                        var emNode = doc.CreateElement("i");
                        emNode.AppendChild(currentNode);
                        currentNode = emNode;
                        break;
                    case "code":
                        var codeNode = doc.CreateElement("code");
                        codeNode.AppendChild(currentNode);
                        currentNode = codeNode;
                        break;
                    case "underline":
                        var underlineNode = doc.CreateElement("u");
                        underlineNode.AppendChild(currentNode);
                        currentNode = underlineNode;
                        break;
                    case "strike-through":
                        var strikeNode = doc.CreateElement("s");
                        strikeNode.AppendChild(currentNode);
                        currentNode = strikeNode;
                        break;
                    default:
                        var genericMarkNode = doc.CreateElement("span");
                        genericMarkNode.SetAttributeValue("data-mark", markId);
                        genericMarkNode.AppendChild(currentNode);
                        currentNode = genericMarkNode;
                        break;
                }
            }
        }
        
        wrapperNode.AppendChild(currentNode);
        return wrapperNode;
    }

    private static HtmlNode ProcessImageBlock(JObject block, HtmlDocument doc, string blockPath)
    {
        var imgNode = doc.CreateElement("img");
        imgNode.SetAttributeValue("data-block-path", blockPath);
        imgNode.SetAttributeValue("data-block-key", block["_key"]?.ToString()!);
        
        var assetRef = block["asset"]?["_ref"]?.ToString();
        if (!string.IsNullOrEmpty(assetRef))
        {
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
        
        return imgNode;
    }

    private static HtmlNode ProcessReferenceBlock(JObject block, HtmlDocument doc, string blockPath)
    {
        var refNode = doc.CreateElement("div");
        refNode.SetAttributeValue("data-block-path", blockPath);
        refNode.SetAttributeValue("data-block-key", block["_key"]?.ToString()!);
        refNode.SetAttributeValue("data-type", "reference");
        
        var refId = block["_ref"]?.ToString();
        if (!string.IsNullOrEmpty(refId))
        {
            refNode.SetAttributeValue("data-ref-id", refId);
        }
        
        return refNode;
    }
}