﻿using Apps.Sanity.Models.Identifiers;
using Blackbird.Applications.Sdk.Common;

namespace Apps.Sanity.Models.Requests;

public class CreateContentRequest : DatasetIdentifier
{
    [Display("Content type", Description = "The type of content you want to create (e.g., article, image, video).")]
    public string Type { get; set; } = string.Empty;

    [Display("Properties", Description = "The list of properties you want to specify for the content.")]
    public IEnumerable<string>? Properties { get; set; }

    [Display("Property values", Description = "The values corresponding to the specified properties. Ensure the number of values matches the number of properties, and the order is maintained. Key-value pairs will be constructed from Properties and Property Values and sent to the Sanity API.")]
    public IEnumerable<string>? PropertyValues { get; set; }
}
