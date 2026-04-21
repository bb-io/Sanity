using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.Sanity.Models.Responses.Content;

public class UploadContentResponse
{
    public FileReference Content { get; set; } = default!;
}
