using Apps.Sanity.Webhooks;
using Apps.Sanity.Webhooks.Models.Requests;
using Blackbird.Applications.Sdk.Common.Webhooks;
using Newtonsoft.Json;
using Tests.Sanity.Base;

namespace Tests.Sanity;

[TestClass]
public class ContentWebhooksTests : TestBase
{
    private ContentWebhooks _contentWebhooks => new(InvocationContext);

    [TestMethod]
    public async Task OnContentUpdated_WithoutFilters_AlwaysStartsFlight()
    {
        // Arrange

        var request = new WebhookRequest
        {
            Body = await ReadFileFromInput("sample-webhook-oncontentupdated.json")
        };

        var filter = new WebhookFilterRequest();

        // Act
        var response = await _contentWebhooks.OnContentUpdated(request, filter);

        // Assert
        Console.WriteLine(JsonConvert.SerializeObject(response, Formatting.Indented));
        Assert.IsTrue(response.ReceivedWebhookRequestType == WebhookRequestType.Default);
    }

    [TestMethod]
    public async Task OnContentUpdated_WithCustomHeader_StartsFlight()
    {
        // Arrange
        var customHeaderName = "x-custom-header";
        var customHeaderValue = "my-custom-value";

        var request = new WebhookRequest
        {
            Headers = new Dictionary<string, string>
            {
                { customHeaderName, customHeaderValue }
            },
            Body = await ReadFileFromInput("sample-webhook-oncontentupdated.json")
        };

        var filter = new WebhookFilterRequest
        {
            CustomHeaderName = customHeaderName,
            CustomHeaderValue = customHeaderValue
        };

        // Act
        var response = await _contentWebhooks.OnContentUpdated(request, filter);

        // Assert
        Console.WriteLine(JsonConvert.SerializeObject(response, Formatting.Indented));
        Assert.IsTrue(response.ReceivedWebhookRequestType == WebhookRequestType.Default);
    }
}