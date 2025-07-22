using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Text;

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
                if (blockNode != null)
                {
                    wrapper.AppendChild(blockNode);
                }
            }
        }

        return wrapper;
    }

    private static HtmlNode ProcessBlock(JObject block, HtmlDocument doc, string basePath)
    {
        string blockType = block["_type"]?.ToString();
        string blockKey = block["_key"]?.ToString();
        string blockPath = $"{basePath}[?(@._key=='{blockKey}')]";

        switch (blockType)
        {
            case "block":
                return ProcessTextBlock(block, doc, blockPath);
            case "image":
                return ProcessImageBlock(block, doc, blockPath);
            case "reference":
                return ProcessReferenceBlock(block, doc, blockPath);
            default:
                // For unknown block types, create a placeholder div
                var unknownNode = doc.CreateElement("div");
                unknownNode.SetAttributeValue("data-block-path", blockPath);
                unknownNode.SetAttributeValue("data-type", blockType ?? "unknown");
                return unknownNode;
        }
    }

    private static HtmlNode ProcessTextBlock(JObject block, HtmlDocument doc, string blockPath)
    {
        string style = block["style"]?.ToString() ?? "normal";
        string listItem = block["listItem"]?.ToString();
        int level = block["level"]?.Value<int>() ?? 0;

        // Create the appropriate HTML element based on style and listItem
        HtmlNode blockNode;
        
        if (!string.IsNullOrEmpty(listItem))
        {
            // This is a list item, but we'll return a li element
            // The parent list (ul/ol) will be created when processing consecutive list items
            blockNode = doc.CreateElement("li");
            blockNode.SetAttributeValue("data-list-type", listItem);
            blockNode.SetAttributeValue("data-list-level", level.ToString());
        }
        else
        {
            // Create element based on style
            blockNode = style switch
            {
                "h1" => doc.CreateElement("h1"),
                "h2" => doc.CreateElement("h2"),
                "h3" => doc.CreateElement("h3"),
                "h4" => doc.CreateElement("h4"),
                "h5" => doc.CreateElement("h5"),
                "h6" => doc.CreateElement("h6"),
                "blockquote" => doc.CreateElement("blockquote"),
                _ => doc.CreateElement("p")  // default to paragraph
            };
        }

        blockNode.SetAttributeValue("data-block-path", blockPath);
        blockNode.SetAttributeValue("data-block-key", block["_key"]?.ToString());

        // Process mark definitions for later use
        var markDefs = block["markDefs"] as JArray;
        
        // Process children (spans)
        var children = block["children"] as JArray;
        if (children != null)
        {
            foreach (var child in children)
            {
                if (child is JObject childObj)
                {
                    string childType = childObj["_type"]?.ToString();
                    if (childType == "span")
                    {
                        string text = childObj["text"]?.ToString() ?? "";
                        var spanNode = ProcessSpan(childObj, doc, markDefs);
                        blockNode.AppendChild(spanNode);
                    }
                }
            }
        }

        return blockNode;
    }

    private static HtmlNode ProcessSpan(JObject span, HtmlDocument doc, JArray markDefs)
    {
        string text = span["text"]?.ToString() ?? "";
        var marks = span["marks"] as JArray;
        
        // Start with a text node
        var contentNode = doc.CreateTextNode(text);
        var wrapperNode = doc.CreateElement("span");
        wrapperNode.SetAttributeValue("data-span-key", span["_key"]?.ToString());
        
        if (marks == null || !marks.Any())
        {
            wrapperNode.AppendChild(contentNode);
            return wrapperNode;
        }

        // Build nested elements for each mark
        HtmlNode currentNode = contentNode;
        foreach (var mark in marks)
        {
            string markId = mark.ToString();
            
            // Check if this is a mark reference
            var markDef = markDefs?.FirstOrDefault(m => m["_key"]?.ToString() == markId);
            
            if (markDef != null)
            {
                // Handle complex marks like links
                string markType = markDef["_type"]?.ToString();
                if (markType == "link")
                {
                    var linkNode = doc.CreateElement("a");
                    linkNode.SetAttributeValue("href", markDef["href"]?.ToString());
                    linkNode.AppendChild(currentNode);
                    currentNode = linkNode;
                }
            }
            else
            {
                // Handle basic marks
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
        imgNode.SetAttributeValue("data-block-key", block["_key"]?.ToString());
        
        // Get asset reference
        string assetRef = block["asset"]?["_ref"]?.ToString();
        if (!string.IsNullOrEmpty(assetRef))
        {
            imgNode.SetAttributeValue("data-asset-ref", assetRef);
            
            // Extract dimensions and format from reference if available
            // Format: image-{id}-{dimensions}-{format}
            if (assetRef.Contains("-"))
            {
                var parts = assetRef.Split('-');
                if (parts.Length >= 3)
                {
                    imgNode.SetAttributeValue("data-format", parts[parts.Length - 1]);
                    
                    string dimensions = parts[parts.Length - 2];
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
        refNode.SetAttributeValue("data-block-key", block["_key"]?.ToString());
        refNode.SetAttributeValue("data-type", "reference");
        
        string refId = block["_ref"]?.ToString();
        if (!string.IsNullOrEmpty(refId))
        {
            refNode.SetAttributeValue("data-ref-id", refId);
        }
        
        return refNode;
    }
    
    // Helper method to extract lists from consecutive list items
    public static void ProcessLists(HtmlNode container)
    {
        var listItems = container.SelectNodes(".//li[@data-list-type]")?.ToList();
        
        if (listItems == null || !listItems.Any())
            return;
            
        while (listItems.Any())
        {
            var firstItem = listItems.First();
            string listType = firstItem.GetAttributeValue("data-list-type", "");
            int level = int.Parse(firstItem.GetAttributeValue("data-list-level", "1"));
            
            // Store the parent node and next sibling before modification
            HtmlNode parentNode = firstItem.ParentNode;
            HtmlNode nextSibling = firstItem.NextSibling;
            
            // Create the appropriate list container
            HtmlNode listNode = listType == "bullet" 
                ? container.OwnerDocument.CreateElement("ul") 
                : container.OwnerDocument.CreateElement("ol");
            
            // Add all consecutive items of the same type and level
            var itemsInList = new List<HtmlNode>();
            foreach (var item in listItems.TakeWhile(li => 
                li.GetAttributeValue("data-list-type", "") == listType &&
                int.Parse(li.GetAttributeValue("data-list-level", "1")) == level))
            {
                itemsInList.Add(item);
            }
            
            // Move these items under the list
            foreach (var item in itemsInList)
            {
                // Remove the attributes we used for identification
                item.Attributes.Remove("data-list-type");
                item.Attributes.Remove("data-list-level");
                
                // Remove from original location and add to list
                item.Remove();
                listNode.AppendChild(item);
                
                // Remove from our tracking list
                listItems.Remove(item);
            }

            // Insert the list at the position where the first item was
            if (nextSibling != null)
                parentNode.InsertBefore(listNode, nextSibling);
            else
                parentNode.AppendChild(listNode);
        }
    }
}