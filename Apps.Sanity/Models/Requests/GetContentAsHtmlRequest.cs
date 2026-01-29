using Apps.Sanity.DataSourceHandlers;
using Apps.Sanity.Models.Identifiers;
using Blackbird.Applications.SDK.Blueprints.Interfaces.CMS;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;

namespace Apps.Sanity.Models.Requests;

public class GetContentAsHtmlRequest : ContentIdentifier, IDownloadContentInput
{
    [Display("Localization strategy"), StaticDataSource(typeof(LocalizationStrategyDataSource))]
    public string LocalizationStrategy { get; set; } = default!;
    
    [Display("Source language")]
    public string SourceLanguage { get; set; } = string.Empty;

    [Display("Include reference entries", Description = "Whether to include reference entries (e.g., in related_articles field) in the HTML output. References will be added to a special section at the end of the document.")]
    public bool? IncludeReferenceEntries { get; set; }

    [Display("Include rich text reference entries", Description = "Whether to include references that appear inside rich text fields in the HTML output. These will also be added to the references section.")]
    public bool? IncludeRichTextReferenceEntries { get; set; }
    
    [Display("Order of fields", Description = "The order in which the fields should be rendered in the HTML output. If not specified, the order will be determined by the schema.")]
    public IEnumerable<string>? OrderOfFields { get; set; }
    
    [Display("Reference field names", Description = "Reference field names by default should be called 'reference' but if your schema uses different names for reference fields, you can specify them here. Please note that 'reference' is always included even if not specified here.")]
    public IEnumerable<string>? ReferenceFieldNames { get; set; }
    
    [Display("Field names", Description = "Names of fields for which to apply character limits. Should correspond with 'Field max length' values.")]
    public IEnumerable<string>? FieldNames { get; set; }
    
    [Display("Field max length", Description = "Maximum character length for each field specified in 'Field names'. Values should be provided in the same order.")]
    public IEnumerable<int>? FieldMaxLength { get; set; }
    
    [Display("Excluded fields", Description = "Additional field names to exclude from translation (beyond the default system fields: _createdAt, _id, _rev, _type, _updatedAt, language). Only applicable for document level localization.")]
    public IEnumerable<string>? ExcludedFields { get; set; }
}