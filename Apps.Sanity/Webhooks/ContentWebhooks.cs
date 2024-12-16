using Apps.Sanity.Invocables;
using Apps.Sanity.Models.Responses.Content;
using Apps.Sanity.Utils;
using Apps.Sanity.Webhooks.Handlers.Content;
using Apps.Sanity.Webhooks.Models.Requests;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common.Webhooks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Apps.Sanity.Webhooks;

[WebhookList]
public class ContentWebhooks(InvocationContext invocationContext) : AppInvocable(invocationContext)
{
    [Webhook("On content updated", typeof(ContentUpdatedHandler), 
        Description = "This webhook is triggered when a content is updated.")]
    public Task<WebhookResponse<ContentResponse>> OnContentUpdated(WebhookRequest request,
        [WebhookParameter] WebhookFilterRequest filterRequest) => HandleWebhookRequest(request, filterRequest);

    [Webhook("On content created", typeof(ContentCreatedHandler),
        Description = "This webhook is triggered when a new content is created.")]
    public Task<WebhookResponse<ContentResponse>> OnContentCreated(WebhookRequest request,
        [WebhookParameter] WebhookFilterRequest filterRequest) => HandleWebhookRequest(request, filterRequest);

    private Task<WebhookResponse<ContentResponse>> HandleWebhookRequest(WebhookRequest request, WebhookFilterRequest filterRequest)
    {
        var body = request.Body.ToString()!;
        var content = JsonConvert.DeserializeObject<ContentResponse>(body)
                      ?? throw new Exception($"Cannot deserialize body to a content object. Body: {body}");

        if (filterRequest.Types != null)
        {
            if (!filterRequest.Types.Contains(content.Type))
            {
                return Task.Run(() => new WebhookResponse<ContentResponse>
                {
                    ReceivedWebhookRequestType = WebhookRequestType.Preflight,
                    Result = null
                });
            }
        }

        if (!string.IsNullOrEmpty(filterRequest.TranslationLanguage) &&
            filterRequest.TriggerIfAllLanguageFieldsAreEmpty.HasValue)
        {
            if (JsonHelper.TranslationForSpecificLanguageExist(body, filterRequest.TranslationLanguage))
            {
                return Task.Run(() => new WebhookResponse<ContentResponse>
                {
                    ReceivedWebhookRequestType = WebhookRequestType.Preflight,
                    Result = null
                });
            }
        }
        
        return Task.Run(() => new WebhookResponse<ContentResponse>
        {
            ReceivedWebhookRequestType = WebhookRequestType.Default,
            Result = content
        });
    }
}