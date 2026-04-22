namespace Apps.Sanity.Utils;

public static class ReleaseContentHelper
{
    private const string VersionsPrefix = "versions.";

    public static bool IsVersionId(string contentId)
    {
        return !string.IsNullOrWhiteSpace(contentId) && contentId.StartsWith(VersionsPrefix, StringComparison.Ordinal);
    }

    public static string? GetReleaseName(string contentId)
    {
        if (!IsVersionId(contentId))
        {
            return null;
        }

        var releaseSeparatorIndex = contentId.IndexOf('.', VersionsPrefix.Length);
        if (releaseSeparatorIndex < 0)
        {
            return string.Empty;
        }

        return contentId[VersionsPrefix.Length..releaseSeparatorIndex];
    }

    public static string BuildVersionId(string releaseName, string contentId)
    {
        var publishedId = GetPublishedId(contentId);
        return $"{VersionsPrefix}{releaseName}.{publishedId}";
    }

    public static string GetUploadArtifactSourceUcid(string sourceContentId, bool publishedDocumentExists)
    {
        var publishedId = GetPublishedId(sourceContentId);
        return IsVersionId(sourceContentId) && !publishedDocumentExists
            ? sourceContentId
            : publishedId;
    }

    public static string GetPublishedId(string contentId)
    {
        if (string.IsNullOrWhiteSpace(contentId))
        {
            return contentId;
        }

        if (contentId.StartsWith("drafts.", StringComparison.Ordinal))
        {
            return contentId["drafts.".Length..];
        }
        
        if (!contentId.StartsWith(VersionsPrefix, StringComparison.Ordinal))
        {
            return contentId;
        }

        var releaseSeparatorIndex = contentId.IndexOf('.', VersionsPrefix.Length);
        if (releaseSeparatorIndex < 0 || releaseSeparatorIndex == contentId.Length - 1)
        {
            return contentId;
        }

        return contentId[(releaseSeparatorIndex + 1)..];
    }
}
