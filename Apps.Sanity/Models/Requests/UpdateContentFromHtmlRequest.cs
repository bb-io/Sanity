using Apps.Sanity.DataSourceHandlers;
using Apps.Sanity.Models.Identifiers;
using Blackbird.Applications.SDK.Blueprints.Interfaces.CMS;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.Sanity.Models.Requests;

public class UpdateContentFromHtmlRequest : DatasetIdentifier, IUploadContentInput
{
    public FileReference Content { get; set; } = default!;

    [Display("Target language")]
    public string Locale { get; set; } = string.Empty;
    
    [Display("Content ID"), DataSource(typeof(ContentDataHandler))]
    public string? ContentId { get; set; }
}