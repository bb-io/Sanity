using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.Sanity.DataSourceHandlers;

public class ReleaseStateDataSource : IStaticDataSourceItemHandler
{
    public IEnumerable<DataSourceItem> GetData()
    {
        return new List<DataSourceItem>
        {
            new("active", "active"),
            new("scheduled", "scheduled"),
            new("published", "published"),
            new("archived", "archived"),
            new("deleted", "deleted"),
            new("scheduling", "scheduling"),
            new("unscheduling", "unscheduling"),
            new("archiving", "archiving"),
            new("unarchiving", "unarchiving"),
            new("publishing", "publishing")
        };
    }
}
