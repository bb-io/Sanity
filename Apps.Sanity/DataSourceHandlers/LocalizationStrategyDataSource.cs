using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.Sanity.DataSourceHandlers;

public class LocalizationStrategyDataSource : IStaticDataSourceItemHandler
{
    public IEnumerable<DataSourceItem> GetData()
    {
        return new List<DataSourceItem>
        {
            new("FieldLevel", "Field level localization"),
            new("DocumentLevel", "Document level localization")
        };
    }
}
