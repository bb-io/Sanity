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
        
        var richTextTargetId = publish
            ? DraftContentHelper.GetPublishedId(contentId)
            : DraftContentHelper.GetDraftId(DraftContentHelper.GetPublishedId(contentId));

        var richTextNodes = contentRoot.SelectNodes(".//*[@data-rich-text='true']");
        if (richTextNodes != null)
        {
            foreach (var richTextNode in richTextNodes)
            {
                var richTextPatch = RichTextToJsonConvertor.CreatePatchObject(
                    richTextNode,
                    contentObj,
                    richTextTargetId,
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
                var id = publish ? 
                    DraftContentHelper.GetPublishedId(contentId): 
                    DraftContentHelper.GetDraftId(DraftContentHelper.GetPublishedId(contentId));

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
            var arrayName = GetArrayName(parsedPathSegments);
            var usesLanguageField = InferUsesLanguageField(contentObj, arrayName);

            // Self-heal: remove stale duplicate keys shaped like "<lang>_deduped_N".
            // Sanity's content lake auto-renames a duplicate array _key to this form when a
            // previous sync inserted an item whose _key collided with an existing one. Such
            // keys are not valid registered languages and block publishing in Studio with
            // "Array item keys must be valid languages registered to the field type".
            AddDedupedKeyCleanup(patchContent, contentObj, arrayName, targetLanguage, usesLanguageField);

            if (shouldInsertAfter)
            {
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
                    .FirstOrDefault(i => (i["language"]?.ToString() ?? i["_key"]?.ToString()) == targetLanguage);

                if (existingItem == null)
                {
                    existingItem = usesLanguageField
                        ? new JObject
                        {
                            ["_key"] = Guid.NewGuid().ToString("N")[..32],
                            ["_type"] = itemType,
                            ["language"] = targetLanguage,
                            ["value"] = new JObject()
                        }
                        : new JObject
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
            if (lang != null && !int.TryParse(lang, out _)) return true;
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
                            var itemLang = itemObj?["language"]?.ToString() ?? itemObj?["_key"]?.ToString();
                            if (itemLang?.Equals(lang, StringComparison.OrdinalIgnoreCase) == true)
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
        var pathParts = new List<string>();
        foreach (var s in segments)
        {
            var lang = ExtractLangKey(s);
            if (lang != null && !int.TryParse(lang, out _))
            {
                // This is the language-keyed segment; return the full path up to this array
                pathParts.Add(s.Substring(0, s.IndexOf('[')));
                return string.Join(".", pathParts);
            }
            pathParts.Add(s);
        }

        return segments[0];
    }

    private static void AddDedupedKeyCleanup(JObject patchContent, JObject contentObj, string arrayName,
        string targetLanguage, bool usesLanguageField)
    {
        // Deduped-key corruption only happens with the "_key == language" convention.
        // When a random _key + language field is used, duplicates don't collide on language.
        if (usesLanguageField || string.IsNullOrEmpty(arrayName) || string.IsNullOrEmpty(targetLanguage))
        {
            return;
        }

        if (ResolveTokenAtPath(contentObj, arrayName) is not JArray array)
        {
            return;
        }

        var dedupedPrefix = $"{targetLanguage}_deduped";
        var staleKeys = array
            .OfType<JObject>()
            .Select(item => item["_key"]?.ToString())
            .Where(key => !string.IsNullOrEmpty(key)
                && key!.StartsWith(dedupedPrefix, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (staleKeys.Count == 0)
        {
            return;
        }

        if (patchContent["unset"] is not JArray unsetArray)
        {
            unsetArray = new JArray();
            patchContent["unset"] = unsetArray;
        }

        foreach (var key in staleKeys)
        {
            var selector = $"{arrayName}[_key==\"{key}\"]";
            if (unsetArray.All(existing => existing.ToString() != selector))
            {
                unsetArray.Add(selector);
            }
        }
    }

    private static string InferInternationalizedType(JObject current, string arrayPath)
    {
        var token = ResolveTokenAtPath(current, arrayPath);
        if (token is JArray arr && arr.Count > 0 && arr[0] is JObject firstItem)
        {
            var typeVal = firstItem["_type"]?.ToString();
            if (!string.IsNullOrEmpty(typeVal))
            {
                return typeVal;
            }
        }

        return "internationalizedArrayStringValue";
    }

    private static bool InferUsesLanguageField(JObject current, string arrayPath)
    {
        var token = ResolveTokenAtPath(current, arrayPath);
        if (token is JArray arr && arr.Count > 0 && arr[0] is JObject firstItem)
        {
            return firstItem["language"] != null;
        }

        return false;
    }

    private static JToken? ResolveTokenAtPath(JObject root, string path)
    {
        var parts = path.Split('.');
        JToken? current = root;

        foreach (var part in parts)
        {
            if (current == null) return null;

            int bracketIndex = part.IndexOf('[');
            if (bracketIndex > 0 && part.EndsWith("]"))
            {
                var propName = part.Substring(0, bracketIndex);
                var indexText = part.Substring(bracketIndex + 1, part.Length - bracketIndex - 2);

                if (current is JObject obj)
                {
                    current = obj[propName];
                }
                else return null;

                if (current is JArray arr && int.TryParse(indexText, out int idx) && idx < arr.Count)
                {
                    current = arr[idx];
                }
                else return null;
            }
            else
            {
                if (current is JObject obj)
                {
                    current = obj[part];
                }
                else return null;
            }
        }

        return current;
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