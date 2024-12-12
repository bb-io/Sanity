using Apps.Sanity.DataSourceHandlers;
using Tests.Sanity.Base;

namespace Tests.Sanity;

[TestClass]
public class DataSourceHandlers : TestBase
{
    [TestMethod]
    public async Task DatasetDataHandler_WithoutSearchString_ShouldNotThrowError()
    {
        var datasetDataHandler = new DatasetDataHandler(InvocationContext);
        var result = await datasetDataHandler.GetDataAsync(new(), default)
                     ?? throw new Exception("Data handler should not return null");

        foreach (var item in result)
        {
            Console.WriteLine(item.Value);
        }
    }
}