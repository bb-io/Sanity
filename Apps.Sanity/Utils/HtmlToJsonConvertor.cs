using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace Apps.Sanity.Utils;

public static class HtmlToJsonConvertor
{
    public static List<JObject> ToJsonPatches(string html, JObject currentJObject, string targetLanguage)
    {
        var contentId = HtmlHelper.ExtractContentId(html);
        var patches = new List<JObject>();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var htmlNode = doc.DocumentNode.SelectSingleNode("//html");
        var sourceLanguage = htmlNode?.GetAttributeValue("lang", "unknown")!;

        var nodesWithPath = doc.DocumentNode.SelectNodes("//*[@data-json-path]");
        if (nodesWithPath == null)
        {
            return patches;
        }

        var groupedPatches = new Dictionary<string, JObject>();

        foreach (var node in nodesWithPath)
        {
            var dataJsonPath = node.GetAttributeValue("data-json-path", null);
            if (dataJsonPath == null) continue;

            var newText = node.InnerText.Trim();
            if (string.IsNullOrEmpty(newText)) continue;

            dataJsonPath = dataJsonPath.Replace($"[{sourceLanguage}]", $"[{targetLanguage}]");
            var parsedPathSegments = dataJsonPath.Split('.');

            var parentDiv = node.Ancestors("div").FirstOrDefault(d => d.Attributes["data-json-path"] != null);
            var parentPathKey = parentDiv?.GetAttributeValue("data-json-path", "") ?? dataJsonPath;

            if (!ContainsLanguage(parsedPathSegments) || node.Name == "div")
            {
                continue;
            }

            var (jsonPropertyPath, foundKeyIndex) = BuildJsonPropertyPath(currentJObject, parsedPathSegments,
                targetLanguage, out var shouldInsert);

            if (!groupedPatches.TryGetValue(parentPathKey, out var existingPatch))
            {
                existingPatch = new JObject
                {
                    ["patch"] = new JObject
                    {
                        ["id"] = contentId
                    }
                };
                groupedPatches[parentPathKey] = existingPatch;
            }

            var patchContent = (JObject)existingPatch["patch"];

            if (shouldInsert)
            {
                var arrayName = GetArrayName(parsedPathSegments);
                var itemType = InferInternationalizedType(currentJObject, arrayName);

                if (patchContent["insert"] == null)
                {
                    patchContent["insert"] = new JObject
                    {
                        ["after"] = $"{arrayName}[-1]",
                        ["items"] = new JArray()
                    };
                }

                var insertContent = (JObject)patchContent["insert"];
                var itemsArray = (JArray)insertContent["items"];

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
                    SetNestedProperty((JObject)valueObj, parts[1], newText);
                }
                else if (lastSegment == "value")
                {
                    existingItem["value"] = newText;
                }
                else
                {
                    SetNestedProperty((JObject)valueObj, lastSegment, newText);
                }
            }
            else
            {
                // Handle sets
                if (patchContent["set"] == null)
                {
                    patchContent["set"] = new JObject();
                }

                var setContent = (JObject)patchContent["set"];
                setContent[jsonPropertyPath] = newText;
            }
        }

        foreach (var kvp in groupedPatches)
        {
            patches.Add(kvp.Value);
        }

        return patches;
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

    private static (string jsonPropertyPath, int foundIndex) BuildJsonPropertyPath(
        JObject current, string[] segments, string targetLanguage, out bool shouldInsert)
    {
        shouldInsert = false;
        var pathParts = new List<string>();
        JObject currentObj = current;

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
                            if (itemObj?["_key"]?.ToString() == lang)
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

                        JObject foundItem = null;
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

        return (string.Join(".", pathParts), 0);
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

    private static void SetNestedProperty(JObject obj, string propertyPath, string value)
    {
        var parts = propertyPath.Split('.');
        JObject current = obj;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var p = parts[i];
            if (current[p] == null)
            {
                current[p] = new JObject();
            }

            current = (JObject)current[p];
        }

        current[parts.Last()] = value;
    }
}