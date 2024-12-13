using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.Sanity.Models.Responses.Content;

public class GetContentAsHtmlResponse
{
    public FileReference File { get; set; } = default!;
}