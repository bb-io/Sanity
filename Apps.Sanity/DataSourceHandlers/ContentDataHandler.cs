using Apps.Sanity.Actions;
using Apps.Sanity.Invocables;
using Apps.Sanity.Models.Identifiers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Invocation;
using Newtonsoft.Json.Linq;

namespace Apps.Sanity.DataSourceHandlers;

public class ContentDataHandler(InvocationContext invocationContext, [ActionParameter] DatasetIdentifier datasetIdentifier)
    : AppInvocable(invocationContext), IAsyncDataSourceItemHandler
{
    public async Task<IEnumerable<DataSourceItem>> GetDataAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        var contentActions = new ContentActions(InvocationContext);
        var content = await contentActions.SearchContentAsJObjectAsync(new()
        {
            DatasetId = datasetIdentifier.DatasetId
        });

        return content
            .Where(x => context.SearchString == null || GetContentReadableName(x).Contains(context.SearchString))
            .Select(x => new DataSourceItem(x["_id"]!.ToString(), GetContentReadableName(x)));
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