namespace Apps.Sanity.Models;

public class DocumentMutationResult
{
    /// <summary>
    /// List of all mutations to apply (main document + referenced documents)
    /// </summary>
    public List<DocumentMutation> Mutations { get; set; } = new();
    
    /// <summary>
    /// ID of the main document being translated
    /// </summary>
    public string MainDocumentId { get; set; } = default!;
}
