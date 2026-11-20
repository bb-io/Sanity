using Apps.Sanity.Models.Identifiers;
using Blackbird.Applications.Sdk.Common;

namespace Apps.Sanity.Models.Requests;

public class GetLocalesRequest : ContentIdentifier
{
    [Display("Exclude locales", Description = "Locale codes to omit from the result. Matching is case-insensitive.")]
    public IEnumerable<string>? ExcludeLocales { get; set; }
}
