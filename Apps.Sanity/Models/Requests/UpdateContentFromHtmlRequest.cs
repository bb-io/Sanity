using Apps.Sanity.Models.Identifiers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.Sanity.Models.Requests;

public class UpdateContentFromHtmlRequest : DatasetIdentifier
{
    public FileReference File { get; set; } = default!;

    [Display("Target language")]
    public string TargetLanguage { get; set; } = string.Empty;
}