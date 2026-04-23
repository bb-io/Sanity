using Apps.Sanity.Models;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.Sanity.DataSourceHandlers;

public class TranslationMetadataSchemaDataSource : IStaticDataSourceItemHandler
{
    public IEnumerable<DataSourceItem> GetData()
    {
        return new List<DataSourceItem>
        {
            new(nameof(TranslationMetadataSchema.Default), "Default (v2 - @sanity/document-internationalization v2+)"),
            new(nameof(TranslationMetadataSchema.Legacy), "Legacy v1 (typed entries with 'language' field)")
        };
    }
}
