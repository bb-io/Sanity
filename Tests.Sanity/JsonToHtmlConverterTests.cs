using Apps.Sanity.Utils;
using FluentAssertions;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using Tests.Sanity.Base;

namespace Tests.Sanity;

[TestClass]
public class JsonToHtmlConverterTests : TestBase
{
    [TestMethod]
    public void ToHtml_WithArticleContent_ShouldGenerateValidHtml()
    {
        // Arrange
        var jsonContent = GetSampleArticleJson();
        var jObject = JObject.Parse(jsonContent);
        var contentId = "673eebc6-3b2a-4448-9a25-a481d57c61d6";
        var sourceLanguage = "en";

        // Act
        var htmlOutput = jObject.ToHtml(contentId, sourceLanguage);
        
        // Assert
        htmlOutput.Should().NotBeNullOrEmpty("HTML output should not be empty");
        Console.WriteLine(htmlOutput);
        
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(htmlOutput);

        // Verify basic HTML structure
        var htmlNode = htmlDoc.DocumentNode.SelectSingleNode("//html");
        htmlNode.Should().NotBeNull("HTML document should have an html element");
        htmlNode.GetAttributeValue("lang", "").Should().Be("en");
        
        // Verify head content
        var headNode = htmlDoc.DocumentNode.SelectSingleNode("//head");
        headNode.Should().NotBeNull("HTML document should have a head element");
        
        var metaCharset = headNode.SelectSingleNode("meta[@charset='UTF-8']");
        metaCharset.Should().NotBeNull("Head should contain charset meta tag");
        
        var metaBlackbird = headNode.SelectSingleNode("meta[@name='blackbird-content-id']");
        metaBlackbird.Should().NotBeNull("Head should contain blackbird-content-id meta tag");
        metaBlackbird.GetAttributeValue("content", "").Should().Be(contentId);
        
        // Verify body content
        var bodyNode = htmlDoc.DocumentNode.SelectSingleNode("//body");
        bodyNode.Should().NotBeNull("HTML document should have a body element");
        
        // Verify content paths are correctly set
        var contentPaths = htmlDoc.DocumentNode.SelectNodes("//*[@data-json-path]");
        contentPaths.Should().NotBeNull("Content paths should be set on elements");
        contentPaths.Count.Should().BeGreaterThan(0, "There should be elements with data-json-path attributes");
        
        // Verify specific content is present
        var bodyText = bodyNode.InnerText;
        bodyText.Should().Contain("In Blackbird, a Bird refers to an automated workflow", "Content from JSON should be in HTML");
        bodyText.Should().Contain("Multi-engine AI Translation", "List item content should be in HTML");
        
        // Verify formatting
        var codeElements = htmlDoc.DocumentNode.SelectNodes("//span[contains(text(), 'Bird') or contains(text(), 'Checkpoints')]");
        codeElements.Should().NotBeNull("Elements with code formatting should be present");
    }
    
    private string GetSampleArticleJson()
    {
        return @"{
            ""_createdAt"": ""2025-07-08T11:34:29Z"",
            ""_id"": ""673eebc6-3b2a-4448-9a25-a481d57c61d6"",
            ""_rev"": ""1z0QQUaLWItqH7ig93oUGA"",
            ""_system"": {
                ""base"": {
                    ""id"": ""673eebc6-3b2a-4448-9a25-a481d57c61d6"",
                    ""rev"": ""tat7ccgP6WW4mPqXfgbwmT""
                }
            },
            ""_type"": ""article"",
            ""_updatedAt"": ""2025-07-17T11:27:51Z"",
            ""contentMultilingual"": [
                {
                    ""_key"": ""en"",
                    ""_type"": ""internationalizedArrayBlockAndSnippetArrayValue"",
                    ""value"": [
                        {
                            ""_key"": ""997798550125"",
                            ""_type"": ""block"",
                            ""children"": [
                                {
                                    ""_key"": ""faa80561e79d"",
                                    ""_type"": ""span"",
                                    ""marks"": [],
                                    ""text"": ""In Blackbird, a ""
                                },
                                {
                                    ""_key"": ""d28ca511f072"",
                                    ""_type"": ""span"",
                                    ""marks"": [
                                        ""code""
                                    ],
                                    ""text"": ""Bird""
                                },
                                {
                                    ""_key"": ""81b96035f5a3"",
                                    ""_type"": ""span"",
                                    ""marks"": [],
                                    ""text"": "" refers to an automated ""
                                },
                                {
                                    ""_key"": ""bd81aa4568c7"",
                                    ""_type"": ""span"",
                                    ""marks"": [
                                        ""strong""
                                    ],
                                    ""text"": ""workflow""
                                },
                                {
                                    ""_key"": ""4ab56178560b"",
                                    ""_type"": ""span"",
                                    ""marks"": [],
                                    ""text"": "", designed to streamline tasks by connecting various applications seamlessly. Blackbird users design Birds to perform specific sequences of operations automatically — saving time, reducing manual work, and ensuring consistency across processes.""
                                }
                            ],
                            ""markDefs"": [],
                            ""style"": ""normal""
                        },
                        {
                            ""_key"": ""aa65ec623299"",
                            ""_ref"": ""8c8882d8-b8c1-420b-9d72-6ae469003edf"",
                            ""_type"": ""reference""
                        },
                        {
                            ""_key"": ""7c6b4d70eb90"",
                            ""_ref"": ""44aa3089-6162-45e1-b89f-5812aed0d7d0"",
                            ""_type"": ""reference""
                        },
                        {
                            ""_key"": ""c47869348869"",
                            ""_type"": ""image"",
                            ""asset"": {
                                ""_ref"": ""image-042d251974d2a49c5eb54ba69d29506fe3e2a572-704x316-png"",
                                ""_type"": ""reference""
                            }
                        },
                        {
                            ""_key"": ""e5c3ff6dfaac"",
                            ""_type"": ""block"",
                            ""children"": [
                                {
                                    ""_key"": ""e1ef3d1ced69"",
                                    ""_type"": ""span"",
                                    ""marks"": [],
                                    ""text"": """"
                                }
                            ],
                            ""markDefs"": [],
                            ""style"": ""normal""
                        },
                        {
                            ""_key"": ""95f9da561252"",
                            ""_type"": ""block"",
                            ""children"": [
                                {
                                    ""_key"": ""7fa0c461d93a"",
                                    ""_type"": ""span"",
                                    ""marks"": [],
                                    ""text"": ""Use cases from""
                                },
                                {
                                    ""_key"": ""e72d53b23d86"",
                                    ""_type"": ""span"",
                                    ""marks"": [
                                        ""90323af2b033""
                                    ],
                                    ""text"": "" the home page""
                                },
                                {
                                    ""_key"": ""bde7d5ff3457"",
                                    ""_type"": ""span"",
                                    ""marks"": [],
                                    ""text"": "":""
                                }
                            ],
                            ""markDefs"": [
                                {
                                    ""_key"": ""90323af2b033"",
                                    ""_type"": ""link"",
                                    ""href"": ""https://www.blackbird.io/""
                                }
                            ],
                            ""style"": ""h3""
                        },
                        {
                            ""_key"": ""35ed0b4c10a9"",
                            ""_type"": ""block"",
                            ""children"": [
                                {
                                    ""_key"": ""4a47cf4cd6bf"",
                                    ""_type"": ""span"",
                                    ""marks"": [],
                                    ""text"": ""Multi-engine AI Translation""
                                }
                            ],
                            ""level"": 1,
                            ""listItem"": ""bullet"",
                            ""markDefs"": [],
                            ""style"": ""normal""
                        },
                        {
                            ""_key"": ""1410ff0bc8a6"",
                            ""_type"": ""block"",
                            ""children"": [
                                {
                                    ""_key"": ""fd1a38e39b08"",
                                    ""_type"": ""span"",
                                    ""marks"": [],
                                    ""text"": ""Multilingual Content Repurposing""
                                }
                            ],
                            ""level"": 1,
                            ""listItem"": ""bullet"",
                            ""markDefs"": [],
                            ""style"": ""normal""
                        },
                        {
                            ""_key"": ""658a2dc08472"",
                            ""_type"": ""block"",
                            ""children"": [
                                {
                                    ""_key"": ""2563057fa6e9"",
                                    ""_type"": ""span"",
                                    ""marks"": [],
                                    ""text"": ""Multilingual SEO & UX""
                                }
                            ],
                            ""level"": 1,
                            ""listItem"": ""bullet"",
                            ""markDefs"": [],
                            ""style"": ""normal""
                        },
                        {
                            ""_key"": ""2acf6bcf584f"",
                            ""_type"": ""block"",
                            ""children"": [
                                {
                                    ""_key"": ""fe15f75b4bb2"",
                                    ""_type"": ""span"",
                                    ""marks"": [],
                                    ""text"": ""Business Process & Approval Automation""
                                }
                            ],
                            ""level"": 1,
                            ""listItem"": ""bullet"",
                            ""markDefs"": [],
                            ""style"": ""normal""
                        },
                        {
                            ""_key"": ""7b4a7ccb771c"",
                            ""_type"": ""block"",
                            ""children"": [
                                {
                                    ""_key"": ""229a81f7de9d"",
                                    ""_type"": ""span"",
                                    ""marks"": [],
                                    ""text"": ""Human-in-the-Loop""
                                }
                            ],
                            ""level"": 1,
                            ""listItem"": ""bullet"",
                            ""markDefs"": [],
                            ""style"": ""normal""
                        },
                        {
                            ""_key"": ""5d92e398fc16"",
                            ""_type"": ""block"",
                            ""children"": [
                                {
                                    ""_key"": ""e772abd808fb"",
                                    ""_type"": ""span"",
                                    ""marks"": [],
                                    ""text"": ""see our ""
                                },
                                {
                                    ""_key"": ""68df7cb4ac2b"",
                                    ""_type"": ""span"",
                                    ""marks"": [
                                        ""code""
                                    ],
                                    ""text"": ""Checkpoints""
                                },
                                {
                                    ""_key"": ""6f558d347249"",
                                    ""_type"": ""span"",
                                    ""marks"": [],
                                    ""text"": "" feature!""
                                }
                            ],
                            ""level"": 2,
                            ""listItem"": ""bullet"",
                            ""markDefs"": [],
                            ""style"": ""normal""
                        },
                        {
                            ""_key"": ""9f6513452785"",
                            ""_type"": ""block"",
                            ""children"": [
                                {
                                    ""_key"": ""5cf1008ddcc1"",
                                    ""_type"": ""span"",
                                    ""marks"": [],
                                    ""text"": ""AI Risk Prediction and Analytics-based Decisions""
                                }
                            ],
                            ""level"": 1,
                            ""listItem"": ""bullet"",
                            ""markDefs"": [],
                            ""style"": ""normal""
                        }
                    ]
                }
            ],
            ""slug"": {
                ""_type"": ""slug"",
                ""current"": ""bird""
            },
            ""title"": [
                {
                    ""_key"": ""en"",
                    ""_type"": ""internationalizedArrayStringValue"",
                    ""value"": ""Bird""
                }
            ]
        }";
    }
}
