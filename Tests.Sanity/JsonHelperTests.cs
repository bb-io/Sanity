using Apps.Sanity.Utils;
using FluentAssertions;
using Newtonsoft.Json.Linq;

namespace Tests.Sanity;

[TestClass]
public class JsonHelperTests
{
    private const string FieldLocalizedDocument = """
        {
            "_type": "article",
            "language": "it",
            "title": [
                { "_type": "internationalizedArrayArticleTitleValue", "_key": "EN", "value": "Title" },
                { "_type": "internationalizedArrayArticleTitleValue", "_key": "generated-key", "language": "fr-CA", "value": "Titre" }
            ],
            "sections": [
                {
                    "_type": "section",
                    "_key": "section-1",
                    "content": [
                        {
                            "_type": "internationalizedArrayArticleContentValue",
                            "_key": "en",
                            "value": [
                                { "_type": "block", "_key": "block-1", "children": [{ "_type": "span", "_key": "span-1" }] },
                                { "_type": "snippet-ref", "_key": "ref-1", "_ref": "snippet-id" }
                            ]
                        },
                        { "_type": "internationalizedArrayArticleContentValue", "_key": "es", "value": [] }
                    ]
                }
            ]
        }
        """;

    [TestMethod]
    public void GetLocales_ReturnsDistinctLocalesFromNestedAndTopLevelFields()
    {
        var document = JObject.Parse(FieldLocalizedDocument);

        var locales = JsonHelper.GetLocales(document);

        locales.Should().Equal("EN", "fr-CA", "es");
    }

    [TestMethod]
    public void GetLocales_OmitsExcludedLocalesRegardlessOfCase()
    {
        var document = JObject.Parse(FieldLocalizedDocument);

        var locales = JsonHelper.GetLocales(document, ["en", "FR-ca", "de"]);

        locales.Should().Equal("es");
    }

    [TestMethod]
    public void GetLocales_ReturnsEmptyListWhenNoLocalizedEntriesExist()
    {
        var document = JObject.Parse("""{ "language": "en", "title": [], "tags": [{ "_type": "reference", "_key": "tag-1" }] }""");

        var locales = JsonHelper.GetLocales(document);

        locales.Should().BeEmpty();
    }

    [TestMethod]
    public void TranslationForSpecificLanguageExist_TranslationDontExist_ShouldReturnFalse()
    {
        var json = @"{
            ""lastName"": [
                {
                    ""_key"": ""en"",
                    ""value"": ""Empty last name"",
                    ""_type"": ""internationalizedArrayStringValue""
                }
            ],
            ""_createdAt"": ""2024-12-16T15:12:23Z"",
            ""_rev"": ""YQFK2aeNLI48tHQQD0WJmN"",
            ""_type"": ""developer"",
            ""_id"": ""d3136b37-85af-47ed-b59d-75ad95ebaca3"",
            ""_updatedAt"": ""2024-12-16T15:22:05Z"",
            ""expirience"": [
                {
                    ""_type"": ""internationalizedArrayStringValue"",
                    ""_key"": ""en"",
                    ""value"": ""test""
                }
            ],
            ""firstName"": [
                {
                    ""_type"": ""internationalizedArrayStringValue"",
                    ""_key"": ""en"",
                    ""value"": ""Empty""
                }
            ]
        }";
        var translationLanguage = "fr";
        
        var result = JsonHelper.TranslationForSpecificLanguageExist(json, translationLanguage);
        result.Should().Be(false);
    }
    
    [TestMethod]
    public void TranslationForSpecificLanguageExist_TranslationExist_ShouldReturnTrue()
    {
        var json = @"        {
            ""lastName"": [
                {
                    ""_type"": ""internationalizedArrayStringValue"",
                    ""_key"": ""en"",
                    ""value"": ""Empty last name""
                }
            ],
            ""_createdAt"": ""2024-12-16T15:12:23Z"",
            ""_rev"": ""YQFK2aeNLI48tHQQD0WrEv"",
            ""_type"": ""developer"",
            ""_id"": ""d3136b37-85af-47ed-b59d-75ad95ebaca3"",
            ""_updatedAt"": ""2024-12-16T15:24:30Z"",
            ""expirience"": [
                {
                    ""_key"": ""en"",
                    ""value"": ""test"",
                    ""_type"": ""internationalizedArrayStringValue""
                }
            ],
            ""firstName"": [
                {
                    ""_type"": ""internationalizedArrayStringValue"",
                    ""_key"": ""en"",
                    ""value"": ""Empty""
                },
                {
                    ""_key"": ""fr"",
                    ""value"": ""Vide"",
                    ""_type"": ""internationalizedArrayStringValue""
                }
            ]
        }";
        var translationLanguage = "fr";
        
        var result = JsonHelper.TranslationForSpecificLanguageExist(json, translationLanguage);
        result.Should().Be(true);
    }
}
