using Apps.Sanity.Models.Identifiers;
using Blackbird.Applications.Sdk.Common;

namespace Apps.Sanity.Models.Requests;

public class GetContentAsHtmlRequest : ContentIdentifier
{
    [Display("Source language")]
    public string SourceLanguage { get; set; } = string.Empty;
}