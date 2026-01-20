using Blackbird.Filters.Shared;

namespace Apps.Sanity.Models;

public class FieldSizeRestriction
{
    public string FieldName { get; set; } = string.Empty;
    public SizeRestrictions Restrictions { get; set; } = new();
}
