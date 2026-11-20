using Apps.Sanity.Invocables;
using Apps.Sanity.Models.Identifiers;
using Apps.Sanity.Utils;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;

namespace Apps.Sanity.DataSourceHandlers;

public class TitleFieldDataHandler(InvocationContext invocationContext, [ActionParameter] ContentIdentifier identifier)
    : AppInvocable(invocationContext), IAsyncDataSourceItemHandler
{
    public async Task<IEnumerable<DataSourceItem>> GetDataAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(identifier.ContentId))
            throw new PluginMisconfigurationException("Select a Content ID to load its title fields.");

        var helper = new DraftContentHelper(Client, Creds);
        var documents = await helper.GetContentWithDraftFallbackAsync(identifier.ContentId, identifier.GetDatasetIdOrDefault());
        var content = documents.FirstOrDefault() ?? throw new PluginMisconfigurationException("No content found for the provided ID. Please verify that the ID is correct and try again.");

        return BlackbirdExportMetadataFactory.GetTitleFields(content)
            .Where(field => string.IsNullOrEmpty(context.SearchString) ||
                            field.Contains(context.SearchString, StringComparison.OrdinalIgnoreCase))
            .OrderBy(field => field, StringComparer.OrdinalIgnoreCase)
            .Select(field => new DataSourceItem(field, field));
    }
}
