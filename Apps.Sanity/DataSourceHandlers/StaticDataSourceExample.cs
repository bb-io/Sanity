using System;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.Sanity.DataSourceHandlers;

public class StaticDataSourceExample : IStaticDataSourceItemHandler
{
    public IEnumerable<DataSourceItem> GetData()
    {
        return new List<DataSourceItem>
        {
            new("item1", "Item 1"),
            new("item2", "Item 2"),
            new("item3", "Item 3"),
        };
    }
}
