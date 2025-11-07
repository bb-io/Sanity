using Apps.Sanity.Models.Requests;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace Apps.Sanity.Utils;

public static class HtmlToJsonConvertor
{
    public static List<JObject> ToJsonPatches(string html, JObject mainContent, string targetLanguage, bool publish,
        Dictionary<string, JObject>? referencedContents = null)
    {
        var mainContentId = HtmlHelper.ExtractContentId(html);
        var patches = new List<JObject>();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var htmlNode = doc.DocumentNode.SelectSingleNode("//html");
        var sourceLanguage = htmlNode?.GetAttributeValue("lang", "unknown")!;
        
        ProcessContentDiv(doc, mainContentId, mainContent, sourceLanguage, targetLanguage, patches, publish: publish);
        if (referencedContents != null && referencedContents.Any())
        {
            var refsContainer = doc.DocumentNode.SelectSingleNode("//div[@id='referenced-entries']");
            if (refsContainer != null)
            {
                var refDivs = refsContainer.SelectNodes(".//div[@data-content-id]");
                if (refDivs != null)
                {
                    foreach (var refDiv in refDivs)
                    {
                        var refId = refDiv.GetAttributeValue("data-content-id", null!);
                        if (string.IsNullOrEmpty(refId) || !referencedContents.TryGetValue(refId, out var refContent))
                            continue;
                            
                        ProcessContentDiv(doc, refId, refContent, sourceLanguage, targetLanguage, patches, publish: publish, refDiv);
                    }
                }
            }
        }

        return patches;
    }
    
    private static void ProcessContentDiv(HtmlDocument doc, string contentId, JObject contentObj, 
        string sourceLanguage, string targetLanguage, List<JObject> patches, bool publish, HtmlNode? contentRoot = null)
    {
        contentRoot ??= doc.DocumentNode.SelectSingleNode($"//div[@data-content-id='{contentId}']");
        if (contentRoot == null) return;
        
        var richTextNodes = contentRoot.SelectNodes(".//*[@data-rich-text='true']");
        if (richTextNodes != null)
        {
            foreach (var richTextNode in richTextNodes)
            {
                var richTextPatch = RichTextToJsonConvertor.CreatePatchObject(
                    richTextNode, 
                    contentObj, 
                    contentId, 
                    sourceLanguage, 
                    targetLanguage
                );
                
                if (richTextPatch != null)
                {
                    patches.Add(richTextPatch);
                }
            }
        }

        var nodesWithPath = contentRoot.SelectNodes(".//*[@data-json-path]");
        if (nodesWithPath == null) return;

        var groupedPatches = new Dictionary<string, JObject>();
        foreach (var node in nodesWithPath)
        {
            if (node.GetAttributeValue("data-rich-text", "false") == "true")
                continue;
                
            var dataJsonPath = node.GetAttributeValue("data-json-path", null!);
            if (dataJsonPath == null) continue;

            var newText = node.InnerText.Trim();
            if (string.IsNullOrEmpty(newText)) continue;

            dataJsonPath = dataJsonPath.Replace($"[{sourceLanguage}]", $"[{targetLanguage}]", StringComparison.OrdinalIgnoreCase);
            var parsedPathSegments = dataJsonPath.Split('.');

            var parentDiv = node.Ancestors("div").FirstOrDefault(d => d.Attributes["data-json-path"] != null);
            var parentPathKey = parentDiv?.GetAttributeValue("data-json-path", "") ?? dataJsonPath;

            if (!ContainsLanguage(parsedPathSegments))
            {
                continue;
            }

            var jsonPropertyPath = BuildJsonPropertyPath(contentObj, parsedPathSegments, out var shouldInsertAfter);
            if (!groupedPatches.TryGetValue(parentPathKey, out var existingPatch))
            {
                var id = publish
                    ? contentId
                    : $"drafts.{contentId}";
                
                existingPatch = new JObject
                {
                    ["patch"] = new JObject
                    {
                        ["id"] = id
                    }
                };
                groupedPatches[parentPathKey] = existingPatch;
            }

            var patchContent = (JObject)existingPatch["patch"]!;
            if (shouldInsertAfter)
            {
                var arrayName = GetArrayName(parsedPathSegments);
                var itemType = InferInternationalizedType(contentObj, arrayName);

                if (patchContent["insert"] == null)
                {
                    patchContent["insert"] = new JObject
                    {
                        ["after"] = $"{arrayName}[-1]",
                        ["items"] = new JArray()
                    };
                }

                var insertContent = (JObject)patchContent["insert"]!;
                var itemsArray = (JArray)insertContent["items"]!;
                var existingItem = itemsArray
                    .OfType<JObject>()
                    .FirstOrDefault(i => i["_key"]?.ToString() == targetLanguage);

                if (existingItem == null)
                {
                    existingItem = new JObject
                    {
                        ["_key"] = targetLanguage,
                        ["_type"] = itemType,
                        ["value"] = new JObject()
                    };
                    
                    itemsArray.Add(existingItem);
                }

                var valueObj = existingItem["value"];

                if (valueObj is JValue)
                {
                    valueObj = new JObject();
                    existingItem["value"] = valueObj;
                }

                var lastSegment = jsonPropertyPath.Split('.').Last();
                if (jsonPropertyPath.Contains(".value."))
                {
                    var parts = jsonPropertyPath.Split(new[] { ".value." }, StringSplitOptions.None);
                    SetNestedProperty((JObject)valueObj!, parts[1], newText);
                }
                else if (lastSegment == "value")
                {
                    existingItem["value"] = newText;
                }
                else
                {
                    SetNestedProperty((JObject)valueObj!, lastSegment, newText);
                }
            }
            else
            {
                if (patchContent["set"] == null)
                {
                    patchContent["set"] = new JObject();
                }

                var setContent = (JObject)patchContent["set"]!;
                setContent[jsonPropertyPath] = newText;
            }
        }

        foreach (var kvp in groupedPatches)
        {
            patches.Add(kvp.Value);
        }
    }

    private static bool ContainsLanguage(string[] segments)
    {
        foreach (var segment in segments)
        {
            var lang = ExtractLangKey(segment);
            if (lang != null) return true;
        }

        return false;
    }

    private static string BuildJsonPropertyPath(JObject current, string[] segments, out bool shouldInsert)
    {
        shouldInsert = false;
        var pathParts = new List<string>();
        var currentObj = current;

        for (int i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];

            if (currentObj == null)
            {
                pathParts.Add(segment);
                continue;
            }

            int bracketIndex = segment.IndexOf('[');
            if (bracketIndex > 0 && segment.EndsWith("]"))
            {
                var propertyName = segment.Substring(0, bracketIndex);
                var indexText = segment.Substring(bracketIndex + 1, segment.Length - bracketIndex - 2);

                if (int.TryParse(indexText, out int numericIndex))
                {
                    pathParts.Add($"{propertyName}[{numericIndex}]");

                    if (currentObj[propertyName] is JArray arr && numericIndex < arr.Count)
                    {
                        currentObj = arr[numericIndex] as JObject;
                    }
                    else
                    {
                        currentObj = null;
                    }
                }
                else
                {
                    var lang = indexText;
                    var arrayToken = currentObj[propertyName];

                    if (arrayToken is JArray arr)
                    {
                        int idx = -1;
                        for (int k = 0; k < arr.Count; k++)
                        {
                            var itemObj = arr[k] as JObject;
                            if (itemObj?["_key"]?.ToString().Equals(lang, StringComparison.OrdinalIgnoreCase) == true)
                            {
                                idx = k;
                                break;
                            }
                        }

                        if (idx == -1)
                        {
                            shouldInsert = true;
                            idx = arr.Count;
                        }

                        pathParts.Add($"{propertyName}[{idx}]");

                        JObject? foundItem = null;
                        if (idx < arr.Count)
                        {
                            foundItem = arr[idx] as JObject;
                        }

                        currentObj = foundItem;
                    }
                    else
                    {
                        shouldInsert = true;
                        pathParts.Add($"{propertyName}[0]");
                        currentObj = null;
                    }
                }
            }
            else
            {
                pathParts.Add(segment);
                if (currentObj[segment] is JObject childObj)
                {
                    currentObj = childObj;
                }
                else
                {
                    currentObj = null;
                }
            }
        }

        return string.Join(".", pathParts);
    }
    
    private static string? ExtractLangKey(string segment)
    {
        int start = segment.IndexOf('[');
        int end = segment.IndexOf(']');
        if (start > 0 && end > start)
        {
            return segment.Substring(start + 1, end - start - 1);
        }

        return null;
    }

    private static string GetArrayName(string[] segments)
    {
        foreach (var s in segments)
        {
            var lang = ExtractLangKey(s);
            if (lang != null)
            {
                return s.Substring(0, s.IndexOf('['));
            }
        }

        return segments[0];
    }

    private static string InferInternationalizedType(JObject current, string arrayName)
    {
        var arr = current[arrayName] as JArray;
        if (arr != null && arr.Count > 0 && arr[0] is JObject firstItem)
        {
            var typeVal = firstItem["_type"]?.ToString();
            if (!string.IsNullOrEmpty(typeVal))
            {
                return typeVal;
            }
        }

        return "internationalizedArrayStringValue";
    }

    private static void SetNestedProperty(JObject? obj, string propertyPath, string value)
    {
        var parts = propertyPath.Split('.');
        var current = obj;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var p = parts[i];
            current![p] ??= new JObject();
            current = (JObject)current[p]!;
        }

        if (current != null)
        {
            current[parts.Last()] = value;
        }
    }
}