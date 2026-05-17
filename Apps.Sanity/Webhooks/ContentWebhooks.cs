using Apps.Sanity.Invocables;
using Apps.Sanity.Models.Responses.Content;
using Apps.Sanity.Utils;
using Apps.Sanity.Webhooks.Handlers.Content;
using Apps.Sanity.Webhooks.Models.Requests;
using Blackbird.Applications.SDK.Blueprints;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common.Webhooks;
using Newtonsoft.Json;
using Blackbird.Applications.Sdk.Common.Exceptions;

namespace Apps.Sanity.Webhooks;

[WebhookList]
public class ContentWebhooks(InvocationContext invocationContext) : AppInvocable(invocationContext)
{
    [Webhook("On content updated", typeof(ContentUpdatedHandler), 
        Description = "This event is triggered when a content is updated.")]
    [BlueprintEventDefinition(BlueprintEvent.ContentCreatedOrUpdated)]
    public Task<WebhookResponse<ContentResponse>> OnContentUpdated(WebhookRequest request,
        [WebhookParameter] WebhookFilterRequest filterRequest) => HandleWebhookRequest(request, filterRequest);

    [Webhook("On content created", typeof(ContentCreatedHandler),
        Description = "This event is triggered when a new content is created.")]
    public Task<WebhookResponse<ContentResponse>> OnContentCreated(WebhookRequest request,
        [WebhookParameter] WebhookFilterRequest filterRequest) => HandleWebhookRequest(request, filterRequest);

    private Task<WebhookResponse<ContentResponse>> HandleWebhookRequest(WebhookRequest request, WebhookFilterRequest filterRequest)
    {
        if (string.IsNullOrEmpty(filterRequest.CustomHeaderName) != string.IsNullOrEmpty(filterRequest.CustomHeaderValue))
            throw new PluginMisconfigurationException("Both 'Custom header name' and 'Custom header value' must be either provided together or omitted.");

        if (!string.IsNullOrEmpty(filterRequest.CustomHeaderName))
        {
            if (!filterRequest.CustomHeaderName.StartsWith("blackbird"))
                throw new PluginMisconfigurationException("Custom header name must start with 'blackbird'.");

            var headerValue = string.Empty;
            request.Headers?.TryGetValue(filterRequest.CustomHeaderName, out headerValue);
            var headerValueContains = headerValue?.Contains(filterRequest.CustomHeaderValue!, StringComparison.OrdinalIgnoreCase);

            if (headerValueContains != true)
            {
                return Task.Run(() => new WebhookResponse<ContentResponse>
                {
                    ReceivedWebhookRequestType = WebhookRequestType.Preflight,
                    Result = null
                });
            }
        }

        var body = request.Body.ToString()!;
        var content = JsonConvert.DeserializeObject<ContentResponse>(body)
                      ?? throw new Exception($"Cannot deserialize body to a content object. Body: {body}");

        if (filterRequest.CustomHeaderName != null &&
            request.Headers?.TryGetValue(filterRequest.CustomHeaderName, out var receivedHeaderValue) == true)
        {
            content.CustomHeaderValue = receivedHeaderValue ?? string.Empty;
        }

        if (filterRequest.Types != null &&
            filterRequest.Types.Contains(content.Type) != true)
        {
            return Task.Run(() => new WebhookResponse<ContentResponse>
            {
                ReceivedWebhookRequestType = WebhookRequestType.Preflight,
                Result = null
            });
        }

        if (!string.IsNullOrEmpty(filterRequest.TranslationLanguage) &&
            filterRequest.TriggerIfAllLanguageFieldsAreEmpty is true)
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