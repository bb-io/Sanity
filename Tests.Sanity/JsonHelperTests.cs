using Apps.Sanity.Utils;
using FluentAssertions;

namespace Tests.Sanity;

[TestClass]
public class JsonHelperTests
{
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