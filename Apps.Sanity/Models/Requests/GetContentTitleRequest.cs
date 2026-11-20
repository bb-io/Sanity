using Apps.Sanity.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.Sanity.Models.Requests;

public class GetContentTitleRequest
{
    [Display("Title field", Description = "Root-level text field to use as the content title."),
     DataSource(typeof(TitleFieldDataHandler))]
    public string? TitleField { get; set; }

    [Display("Locale", Description = "Language to read from a localized title field.")]
    public string? Locale { get; set; }
}
