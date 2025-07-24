using Apps.Sanity.Models.Identifiers;
using Blackbird.Applications.SDK.Blueprints.Interfaces.CMS;
using Blackbird.Applications.Sdk.Common;

namespace Apps.Sanity.Models.Requests;

public class GetContentAsHtmlRequest : ContentIdentifier, IDownloadContentInput
{
    [Display("Source language")]
    public string SourceLanguage { get; set; } = string.Empty;

    [Display("Include reference entries", Description = "Whether to include reference entries (e.g., in related_articles field) in the HTML output. References will be added to a special section at the end of the document.")]
    public bool? IncludeReferenceEntries { get; set; }

    [Display("Include rich text reference entries", Description = "Whether to include references that appear inside rich text fields in the HTML output. These will also be added to the references section.")]
    public bool? IncludeRichTextReferenceEntries { get; set; }
    
    [Display("Order of fields", Description = "The order in which the fields should be rendered in the HTML output. If not specified, the order will be determined by the schema.")]
    public IEnumerable<string>? OrderOfFields { get; set; }
}