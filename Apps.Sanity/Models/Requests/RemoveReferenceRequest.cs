using Apps.Sanity.DataSourceHandlers;
using Apps.Sanity.Models.Identifiers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.Sanity.Models.Requests;

public class RemoveReferenceRequest : ContentIdentifier
{
    [Display("Reference field name", Description = "The ID of the reference field to which the reference will be removed."), DataSource(typeof(ReferenceFieldDataHandler))]
    public string ReferenceFieldName { get; set; } = string.Empty;
    
    [Display("Referenced content ID", Description = "The ID of the content to be removed as a reference."), DataSource(typeof(ContentDataHandler))]
    public string ReferenceContentId { get; set; } = string.Empty;
    
    [Display("Update as draft", Description = "Whether to save the changes as a draft. If false, the changes will be published immediately.")]
    public bool? UpdateAsDraft { get; set; }
    
    public bool ShouldUpdateAsDraft() => UpdateAsDraft ?? true;
}