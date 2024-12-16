using Blackbird.Applications.Sdk.Common.Invocation;

namespace Apps.Sanity.Webhooks.Handlers.Content;

public class ContentCreatedHandler(InvocationContext invocationContext) : BaseWebhookHandler(invocationContext)
{
    protected override string Event => "create";
}