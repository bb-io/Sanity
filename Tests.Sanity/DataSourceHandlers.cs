using Apps.Sanity.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Exceptions;
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
    
    [TestMethod]
    public async Task ReferenceFieldDataHandler_WithoutContentId_ShouldThrowError()
    {
        var referenceFieldDataHandler = new ReferenceFieldDataHandler(InvocationContext, new());
        await Assert.ThrowsExceptionAsync<PluginMisconfigurationException>(async () => await TestDataHandler(referenceFieldDataHandler));
    }
    
    [TestMethod]
    public async Task ReferenceFieldDataHandler_WithValidContentId_Success()
    {
        var referenceFieldDataHandler = new ReferenceFieldDataHandler(InvocationContext, new() { ContentId = "cedad9bf-b99b-41b7-b5cd-0cf1b4a6be24" });
        await TestDataHandler(referenceFieldDataHandler);
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