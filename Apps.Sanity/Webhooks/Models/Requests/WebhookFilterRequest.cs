using Blackbird.Applications.Sdk.Common;

namespace Apps.Sanity.Webhooks.Models.Requests;

public class WebhookFilterRequest
{
    [Display("Content types")] 
    public IEnumerable<string>? Types { get; set; }

    [Display("Translation language", Description = "Only applies if 'Trigger if all language fields are not exists' is enabled")]
    public string? TranslationLanguage { get; set; }

    [Display("Trigger if all language fields are missing", Description = "If true, the event will trigger only if the specified 'Translation language' does not appear in the content at all.")]
    public bool? TriggerIfAllLanguageFieldsAreEmpty { get; set; }
}