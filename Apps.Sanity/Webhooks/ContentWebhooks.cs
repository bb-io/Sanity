using Apps.Sanity.Invocables;
using Apps.Sanity.Models.Responses.Content;
using Apps.Sanity.Webhooks.Handlers.Content;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common.Webhooks;
using Newtonsoft.Json;

namespace Apps.Sanity.Webhooks;

[WebhookList]
public class ContentWebhooks(InvocationContext invocationContext) : AppInvocable(invocationContext)
{
    [Webhook("On content updated", typeof(ContentUpdatedHandler), 
        Description = "This webhook is triggered when a content is updated.")]
    public Task<WebhookResponse<ContentResponse>> OnContentUpdated(WebhookRequest request) => HandleWebhookRequest(request);

    [Webhook("On content created", typeof(ContentCreatedHandler),
        Description = "This webhook is triggered when a new content is created.")]
    public Task<WebhookResponse<ContentResponse>> OnContentCreated(WebhookRequest request) => HandleWebhookRequest(request);

    private Task<WebhookResponse<ContentResponse>> HandleWebhookRequest(WebhookRequest request)
    {
        var body = request.Body.ToString()!;
        var content = JsonConvert.DeserializeObject<ContentResponse>(body)
                      ?? throw new Exception($"Cannot deserialize body to a content object. Body: {body}");
        
        return Task.Run(() => new WebhookResponse<ContentResponse>
        {
            ReceivedWebhookRequestType = WebhookRequestType.Default,
            Result = content
        });
    }
}