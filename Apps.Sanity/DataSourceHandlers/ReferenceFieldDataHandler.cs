using Apps.Sanity.Actions;
using Apps.Sanity.Invocables;
using Apps.Sanity.Models.Identifiers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Newtonsoft.Json.Linq;

namespace Apps.Sanity.DataSourceHandlers;

public class ReferenceFieldDataHandler(InvocationContext invocationContext, [ActionParameter] ContentIdentifier contentIdentifier)
    : AppInvocable(invocationContext), IAsyncDataSourceItemHandler
{
    public async Task<IEnumerable<DataSourceItem>> GetDataAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(contentIdentifier.ContentId))
        {
            throw new PluginMisconfigurationException("Please, provide Content ID first, to be able to load reference fields");
        }
        
        var contentActions = new ContentActions(InvocationContext, null!);
        var groqQuery = $"_id == \"{contentIdentifier.ContentId}\"";
        var jObjects = await contentActions.SearchContentAsJObjectAsync(new()
        {
            DatasetId = contentIdentifier.DatasetId,
            GroqQuery = groqQuery
        });

        if (jObjects.Count == 0)
        {
            throw new PluginMisconfigurationException(
                "No content found for the provided ID. Please verify that the ID is correct and try again.");
        }

        var jObject = jObjects.First();
        var referenceFields = jObject.Properties()
            .Where(p =>
            {
                if (p.Value.Type == JTokenType.Array)
                {
                    return p.Value.Any(v => v.Type == JTokenType.Object && v["_type"]?.ToString() == "reference");
                }

                if (p.Value.Type == JTokenType.Object)
                {
                    return p.Value["_type"]?.ToString() == "reference";
                }
                
                return false;
            })
            .Select(p => p.Name);
        
        return referenceFields
            .Select(fieldName => new DataSourceItem(fieldName, fieldName));
    }

    private string GetContentReadableName(JObject jObject)
    {
        if (jObject.TryGetValue("name", StringComparison.InvariantCultureIgnoreCase, out var name) && name.Type == JTokenType.String)
        {
            return name.ToString();
        }
        
        if (jObject.TryGetValue("title", StringComparison.InvariantCultureIgnoreCase, out var title) && title.Type == JTokenType.String)
        {
            return title.ToString();
        }

        return $"[{jObject["_type"]}] {jObject["_id"]}";
    }
}