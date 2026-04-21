using Newtonsoft.Json.Linq;

namespace Apps.Sanity.Models;

internal sealed record UploadContentResult(
    string ContentId,
    JObject Content,
    Dictionary<string, string> ReferenceIdMapping,
    Dictionary<string, JObject> ReferencedContents);
