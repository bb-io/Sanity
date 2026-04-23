namespace Apps.Sanity.Models;

public record BlackbirdExportMetadata
{
    public string HtmlLanguage { get; init; } = string.Empty;

    public string Ucid { get; init; } = string.Empty;

    public string? ContentName { get; init; }

    public string SystemName { get; init; } = string.Empty;

    public string SystemRef { get; init; } = string.Empty;
}
