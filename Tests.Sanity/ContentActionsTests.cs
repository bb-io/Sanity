using Apps.Sanity.Actions;
using Apps.Sanity.Models.Requests;
using Apps.Sanity.Models.Responses.Content;
using Blackbird.Applications.Sdk.Common.Exceptions;
using FluentAssertions;
using Newtonsoft.Json;
using Tests.Sanity.Base;

namespace Tests.Sanity;

[TestClass]
public class ContentActionsTests : TestBase
{
    [TestMethod]
    public async Task SearchContent_WithoutInputParameters_ShouldNotThrowAnError()
    {
        var request = new SearchContentRequest();
        await VerifySearchContentAsync(request);
    }

    [TestMethod]
    public async Task SearchContent_WithTypesInputParameters_ShouldNotThrowAnError()
    {
        var request = new SearchContentRequest
        {
            Types = ["article", "snippet"]
        };

        await VerifySearchContentAsync(request,
            result =>
            {
                return Task.Run(() =>
                    result.Items.Should().AllSatisfy(x => x.Type.Should().BeOneOf("article", "snippet")));
            });
    }

    [TestMethod]
    public async Task SearchContent_WithCreatedAtInputParameters_ShouldNotThrowAnError()
    {
        var createdAfter = DateTime.Parse("2024-12-12T09:31:18Z").ToUniversalTime();
        var request = new SearchContentRequest
        {
            CreatedAfter = createdAfter
        };

        await VerifySearchContentAsync(request,
            result =>
            {
                return Task.Run(() =>
                    result.Items.Should().AllSatisfy(x => x.CreatedAt.Should().BeAfter(createdAfter)));
            });
    }

    [TestMethod]
    public async Task SearchContent_WithGroqAtInputParameters_ShouldNotThrowAnError()
    {
        var request = new SearchContentRequest
        {
            GroqQuery = $"dateTime(_createdAt) > dateTime('2024-12-12T09:31:18Z') && _type == \"event\""
        };

        await VerifySearchContentAsync(request, result => Task.Delay(1));
    }

    [TestMethod]
    public async Task GetContent_ExistingContent_ShouldNotThrowError()
    {
        var contentId = "drafts.896c8360-d864-4435-bf69-7d0e29dae2bf";
        var datasetDataHandler = new ContentActions(InvocationContext, FileManager);
        var content = await datasetDataHandler.GetContentAsync(new() { ContentId = contentId });

        content.ContentId.Should().NotBeNullOrEmpty();
        Console.WriteLine($"{content.ContentId}: {content.Type}");
    }

    [TestMethod]
    public async Task GetContentAsHtml_ExistingContent_ShouldNotThrowError()
    {
        var contentId = "cc2cab03-88d1-4fec-a6e4-01536b642f92";
        var datasetDataHandler = new ContentActions(InvocationContext, FileManager);
        var content =
            await datasetDataHandler.GetContentAsHtmlAsync(new()
            {
                ContentId = contentId, 
                SourceLanguage = "EN", 
                IncludeReferenceEntries = true,
                IncludeRichTextReferenceEntries = true,
                ReferenceFieldNames = ["snippet-ref"]
            });

        content.Content.Name.Should().NotBeNullOrEmpty();
        Console.WriteLine(content.Content.Name);
    }

    [TestMethod]
    public async Task UpdateContentFromHtml_ExistingContent_ShouldNotThrowError()
    {
        var datasetDataHandler = new ContentActions(InvocationContext, FileManager);
        await datasetDataHandler.UpdateContentFromHtmlAsync(new()
        {
            Locale = "en",
            Content = new()
            {
                Name = "drafts.13cf55b0-c6c1-4602-b95c-54f54987b9b5.html",
                ContentType = "text/html"
            },
            //Publish = false
        });
    }

    [TestMethod]
    public async Task CreateContent_EmptyContent_ShouldNotThrowError()
    {
        var datasetDataHandler = new ContentActions(InvocationContext, FileManager);
        var content = await datasetDataHandler.CreateContentAsync(new() { Type = "event" });

        content.ContentId.Should().NotBeNullOrEmpty();
        Console.WriteLine($"{content.ContentId}: {content.Type}");

        await DeleteContentAsync(content.ContentId);
    }

    [TestMethod]
    public async Task CreateContent_ContentWithName_ShouldNotThrowError()
    {
        var datasetDataHandler = new ContentActions(InvocationContext, FileManager);
        var content = await datasetDataHandler.CreateContentAsync(new()
        {
            Type = "event",
            Properties = new[] { "Name" },
            PropertyValues = new[] { $"Test event {Guid.NewGuid()}" }
        });

        content.ContentId.Should().NotBeNullOrEmpty();
        Console.WriteLine($"{content.ContentId}: {content.Type}");

        await DeleteContentAsync(content.ContentId);
    }

    [TestMethod]
    public async Task DeleteContent_NotExistingContent_ShouldThrowError()
    {
        var contentId = "not valid";
        var datasetDataHandler = new ContentActions(InvocationContext, FileManager);
        await Assert.ThrowsExceptionAsync<PluginMisconfigurationException>(async () =>
            await datasetDataHandler.DeleteContentAsync(new() { ContentId = contentId }));
    }
    
    [TestMethod]
    public async Task AddReferenceToContentAsync_ValidInput_ShouldNotThrowError()
    {
        var datasetDataHandler = new ContentActions(InvocationContext, FileManager);
        await datasetDataHandler.AddReferenceToContentAsync(new()
        {
            ContentId = "cedad9bf-b99b-41b7-b5cd-0cf1b4a6be24",
            ReferenceFieldName = "Artist",
            ReferenceContentId = "522472d5-456a-46d5-a44d-f251036a5b17"
        });
    }
    
    [TestMethod]
    public async Task RemoveReferenceFromContentAsync_ValidInput_ShouldNotThrowError()
    {
        var datasetDataHandler = new ContentActions(InvocationContext, FileManager);
        await datasetDataHandler.RemoveReferenceFromContentAsync(new()
        {
            ContentId = "cedad9bf-b99b-41b7-b5cd-0cf1b4a6be24",
            ReferenceFieldName = "Artist",
            ReferenceContentId = "522472d5-456a-46d5-a44d-f251036a5b17"
        });
    }

    private async Task VerifySearchContentAsync(SearchContentRequest request,
        Func<SearchContentResponse, Task>? additionalAssertions = null)
    {
        var datasetDataHandler = new ContentActions(InvocationContext, FileManager);
        var result = await datasetDataHandler.SearchContentAsync(request);

        result.Items.Should().NotBeNull("Action should not return null collection");

        result.TotalCount.Should().Be(result.Items.Count);

        if (additionalAssertions != null)
        {
            await additionalAssertions(result);
        }

        Console.WriteLine(result.TotalCount);
        Console.WriteLine(JsonConvert.SerializeObject(result.Items, Formatting.Indented));
    }

    private async Task DeleteContentAsync(string contentID)
    {
        var datasetDataHandler = new ContentActions(InvocationContext, FileManager);
        await datasetDataHandler.DeleteContentAsync(new() { ContentId = contentID });
        Console.WriteLine("Content was successfully deleted");
    }
}