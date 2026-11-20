using Blackbird.Applications.SDK.Blueprints.Interfaces.CMS;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.Sanity.Models.Responses.Content;

public class GetContentAsHtmlResponse : IDownloadContentOutput
{
    public FileReference Content { get; set; } = default!;

    [Display("Content title")]
    public string? ContentTitle { get; set; }

    [Display("Content type")]
    public string Type { get; set; } = string.Empty;
}
