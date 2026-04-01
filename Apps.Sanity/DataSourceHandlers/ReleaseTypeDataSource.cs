using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.Sanity.DataSourceHandlers;

public class ReleaseTypeDataSource : IStaticDataSourceItemHandler
{
    public IEnumerable<DataSourceItem> GetData()
    {
        return new List<DataSourceItem>
        {
            new("asap", "asap"),
            new("scheduled", "scheduled"),
            new("undecided", "undecided")
        };
    }
}
