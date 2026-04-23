using Apps.Sanity.DataSourceHandlers;
using Apps.Sanity.Models.Identifiers;
using Blackbird.Applications.SDK.Blueprints.Interfaces.CMS;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Files;

namespace Apps.Sanity.Models.Requests;

public class UpdateContentFromHtmlRequest : DatasetIdentifier, IUploadContentInput
{
    public FileReference Content { get; set; } = default!;

    [Display("Target language")]
    public string Locale { get; set; } = string.Empty;

    [Display("Content ID"), DataSource(typeof(ContentDataHandler))]
    public string? ContentId { get; set; }

    [Display("Release name", Description = "If specified, the translated content is added to the selected release instead of being written directly to the live document."), DataSource(typeof(ReleaseDataHandler))]
    public string? ReleaseName { get; set; }

    [Display("Publish after update")]
    public bool? Publish { get; set; }

    [Display("Translation metadata schema", Description = "Shape used when writing translation.metadata documents. Leave empty to use the default v2 format ({ _key: '<lang>', value: reference }) matching @sanity/document-internationalization v2+. Select 'Legacy v1' if your studio schema expects typed entries ({ _key: <uuid>, _type: ..., language: <lang>, value: reference }). Only applies to document level localization.")]
    [StaticDataSource(typeof(TranslationMetadataSchemaDataSource))]
    public string? TranslationMetadataSchema { get; set; }
}
