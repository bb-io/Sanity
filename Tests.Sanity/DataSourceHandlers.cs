using Apps.Sanity.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Tests.Sanity.Base;

namespace Tests.Sanity;

[TestClass]
public class DataSourceHandlers : TestBase
{
    [TestMethod]
    public async Task DatasetDataHandler_WithoutSearchString_ShouldNotThrowError()
    {
        var datasetDataHandler = new DatasetDataHandler(InvocationContext);
        await TestDataHandler(datasetDataHandler);
    }
    
    [TestMethod]
    public async Task ContentDataHandler_WithoutSearchString_ShouldNotThrowError()
    {
        var datasetDataHandler = new ContentDataHandler(InvocationContext, new());
        await TestDataHandler(datasetDataHandler);
    }

    private async Task TestDataHandler(IAsyncDataSourceItemHandler dataSourceItemHandler)
    {
        var result = await dataSourceItemHandler.GetDataAsync(new(), default)
                     ?? throw new Exception("Data handler should not return null");

        foreach (var item in result)
        {
            Console.WriteLine($"{item.Value}: {item.DisplayName}");
        }
    }
}