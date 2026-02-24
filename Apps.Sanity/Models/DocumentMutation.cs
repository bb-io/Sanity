using Newtonsoft.Json.Linq;

namespace Apps.Sanity.Models;

public class DocumentMutation
{
    /// <summary>
    /// Original document ID from base language (e.g., English version)
    /// </summary>
    public string OriginalDocumentId { get; set; } = default!;
    
    /// <summary>
    /// Target document ID for the localized version (will be determined during processing)
    /// </summary>
    public string? TargetDocumentId { get; set; }
    
    /// <summary>
    /// Content of the document with translated fields
    /// </summary>
    public JObject Content { get; set; } = default!;
    
    /// <summary>
    /// True for the main document, false for referenced documents
    /// </summary>
    public bool IsMainDocument { get; set; }
    
    /// <summary>
    /// Mapping of JSON paths to original reference IDs that need to be updated
    /// Key: JSON path (e.g., "hero._ref", "sections[0].image._ref")
    /// Value: Original document ID being referenced
    /// </summary>
    public Dictionary<string, string> ReferenceMapping { get; set; } = new();
}
