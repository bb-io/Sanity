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

        // Find all elements with data-json-path attribute
        var nodesWithPath = doc.DocumentNode.SelectNodes("//*[@data-json-path]");
        if (nodesWithPath == null)
        {
            return patches;
        }

        foreach (var node in nodesWithPath)
        {
            var dataJsonPath = node.GetAttributeValue("data-json-path", null);
            if (dataJsonPath == null) continue;

            // Extract text content
            string newText = node.InnerText.Trim();
            if (string.IsNullOrEmpty(newText)) continue;

            // We only want to produce patches for the target language.
            // Check if dataJsonPath includes something like [en] or [fr].
            // data-json-path might look like: "localized[en]", "artist[en].value.name"
            // We'll parse it and see if we have a segment with [lang].
            var parsedPathSegments = dataJsonPath.Split('.');

            // We must identify the segment that contains the language:
            // e.g. "localized[en]" segment means the array name is "localized" and the lang is "en".
            // If no segment matches targetLanguage as a [lang], skip.
            if (!ContainsTargetLanguage(parsedPathSegments, targetLanguage))
            {
                continue;
            }

            // Now we must navigate currentJObject according to this path.
            // The path is in a form: segmentOne[en].value.description or localized[en]
            // Steps:
            // 1. For each segment, check if it contains [lang]. If it does, resolve array index from _key.
            // 2. Otherwise just navigate properties.

            bool shouldInsert = false;
            var (jsonPropertyPath, foundKeyIndex) = BuildJsonPropertyPath(currentJObject, parsedPathSegments,
                targetLanguage, out shouldInsert);

            // jsonPropertyPath might look like "localized[0].value" or "artist[0].value.description"
            // We need to set the last property in that path to newText.
            // The patch structure:
            // {
            //   "patch": {
            //     "id": contentId,
            //     "set": {
            //       "localized[0].value": "new text"
            //     }
            //   }
            // }

            var patchObj = new JObject();
            var patchContent = new JObject();
            patchObj["patch"] = patchContent;
            patchContent["id"] = contentId;

            if (shouldInsert)
            {
                // If we must insert:
                // We have something like "localized" array without the targetLanguage key.
                // Insert after the last item: "after": "localized[-1]"
                // Construct the item:
                // Determine _type. We'll guess from the array:
                var arrayName = GetArrayName(parsedPathSegments);
                var itemType = InferInternationalizedType(currentJObject, arrayName);

                var insertContent = new JObject();
                patchContent["insert"] = insertContent;
                insertContent["after"] = $"{arrayName}[-1]"; // insert at the end

                // Construct item to insert
                var newItem = new JObject
                {
                    ["_key"] = targetLanguage,
                    ["_type"] = itemType,
                    ["value"] = newText
                };

                // If the path points deeper, like "artist[en].value.description",
                // we need to insert a structured object instead of a simple string.
                // For simplicity, if we detect ".value." in the path, we assume complex object:
                if (jsonPropertyPath.Contains(".value."))
                {
                    // split at ".value."
                    var parts = jsonPropertyPath.Split(new[] { ".value." }, StringSplitOptions.None);
                    // parts[0] = "localized[0]" or something similar
                    // parts[1] = "description"
                    // means we must create { "value": { "description": "newText" } }
                    var valueObj = new JObject();
                    SetNestedProperty(valueObj, parts[1], newText);
                    newItem["value"] = valueObj;
                }

                var itemsArray = new JArray();
                itemsArray.Add(newItem);
                insertContent["items"] = itemsArray;
            }
            else
            {
                // Just a set patch:
                var setContent = new JObject();
                patchContent["set"] = setContent;

                // If the path includes ".value." and we have a complex structure:
                // For sets, we must set the full property. 
                // We'll just set the property corresponding to the final segment.
                // Example: "artist[0].value.description" => set {"artist[0].value.description": newText}
                // If it's a simple "localized[0].value" => set {"localized[0].value": newText}

                setContent[jsonPropertyPath] = newText;
            }

            patches.Add(patchObj);
        }

        return patches;
    }

    private static bool ContainsTargetLanguage(string[] segments, string targetLanguage)
    {
        foreach (var segment in segments)
        {
            // Check if segment matches pattern something[lang]
            var lang = ExtractLangKey(segment);
            if (lang == targetLanguage) return true;
        }

        return false;
    }

    private static (string jsonPropertyPath, int foundIndex) BuildJsonPropertyPath(JObject current, string[] segments,
        string targetLanguage, out bool shouldInsert)
    {
        shouldInsert = false;
        // We'll build a path like "localized[0].value"
        // Steps:
        // 1. For each segment, if it's "something[lang]", find the index in current JArray by _key=lang.
        // 2. If not found, mark shouldInsert = true and guess where to insert.
        // 3. For normal segments, just append ".propertyName".

        var pathParts = new List<string>();
        JObject currentObj = current;
        for (int i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            var lang = ExtractLangKey(segment);
            if (lang != null)
            {
                // It's an array segment
                var arrayName = segment.Substring(0, segment.IndexOf('['));
                // Navigate currentObj to arrayName
                var arrayToken = currentObj[arrayName];
                if (arrayToken is JArray arr)
                {
                    // find index by _key
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
                        // Not found, we must insert
                        shouldInsert = true;
                        // We'll just assume index after last
                        idx = arr.Count;
                    }

                    pathParts.Add($"{arrayName}[{idx}]");

                    // Move currentObj to that item, if it exists:
                    JObject foundItem = null;
                    if (idx < arr.Count) foundItem = arr[idx] as JObject;
                    if (foundItem != null)
                    {
                        currentObj = foundItem;
                    }
                    else
                    {
                        // We are going to insert later, so no currentObj
                        currentObj = null;
                    }
                }
                else
                {
                    // Array not found, must insert
                    shouldInsert = true;
                    pathParts.Add($"{arrayName}[0]"); // placeholder
                    currentObj = null;
                }
            }
            else
            {
                // Normal segment
                pathParts.Add(segment);
                // If we can, navigate deeper into currentObj
                if (currentObj != null && currentObj[segment] is JObject childObj)
                {
                    currentObj = childObj;
                }
                else
                {
                    // Might be final property or doesn't exist yet
                    currentObj = null;
                }
            }
        }

        // Join by '.'
        return (string.Join(".", pathParts), 0);
    }

    private static string ExtractLangKey(string segment)
    {
        // segment looks like "localized[en]" or "artist[en]"
        // or could be "artist" without brackets
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
        // Find the segment that had language.
        foreach (var s in segments)
        {
            var lang = ExtractLangKey(s);
            if (lang != null)
            {
                return s.Substring(0, s.IndexOf('['));
            }
        }

        // fallback
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

        // default fallback
        return "internationalizedArrayStringValue";
    }

    private static void SetNestedProperty(JObject obj, string propertyPath, string value)
    {
        // propertyPath might be "description" or "name"
        // or could be nested like "value.description" but we split at ".value." already.
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
