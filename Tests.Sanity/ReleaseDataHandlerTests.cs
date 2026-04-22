using Apps.Sanity.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Newtonsoft.Json;
using Tests.Sanity.Base;

namespace Tests.Sanity;

[TestClass]
public class ReleaseDataHandlerTests : TestBase
{
    [TestMethod]
    public async Task GetData_Success()
    {
        var dataHandler = new ReleaseDataHandler(InvocationContext, new());
        
        var result = await dataHandler.GetDataAsync(new DataSourceContext(), CancellationToken.None);
        
        Assert.IsNotNull(result);
        Console.WriteLine(JsonConvert.SerializeObject(result));
    }
}