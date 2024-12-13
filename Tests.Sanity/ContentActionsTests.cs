using Apps.Sanity.Actions;
using Apps.Sanity.Models.Requests;
using Apps.Sanity.Models.Responses.Content;
using Blackbird.Applications.Sdk.Common.Exceptions;
using FluentAssertions;
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
            Types = new[] { "event" }
        };

        await VerifySearchContentAsync(request,
            result => { return Task.Run(() => result.Items.Should().AllSatisfy(x => x.Type.Should().Be("event"))); });
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
        var contentId = "1jzxHAeumuCFu7uTFF2ZAQ";
        var datasetDataHandler = new ContentActions(InvocationContext, FileManager);
        var content = await datasetDataHandler.GetContentAsync(new() { ContentId = contentId });

        content.Id.Should().NotBeNullOrEmpty();
        Console.WriteLine($"{content.Id}: {content.Type}");
    }

    [TestMethod]
    public async Task GetContentAsHtml_ExistingContent_ShouldNotThrowError()
    {
        var contentId = "273a4464-4363-4aef-92b8-fc828ef60396";
        var datasetDataHandler = new ContentActions(InvocationContext, FileManager);
        var content =
            await datasetDataHandler.GetContentAsHtmlAsync(new() { ContentId = contentId, SourceLanguage = "en" });

        content.File.Name.Should().NotBeNullOrEmpty();
        Console.WriteLine(content.File.Name);
    }

    [TestMethod]
    public async Task UpdateContentFromHtml_ExistingContent_ShouldNotThrowError()
    {
        var datasetDataHandler = new ContentActions(InvocationContext, FileManager);
        await datasetDataHandler.UpdateContentFromHtmlAsync(new()
        {
            TargetLanguage = "fr", 
            File = new()
            {
                Name = "273a4464-4363-4aef-92b8-fc828ef60396.html",
                ContentType = "text/html"
            }
        });
    }

    [TestMethod]
    public async Task CreateContent_EmptyContent_ShouldNotThrowError()
    {
        var datasetDataHandler = new ContentActions(InvocationContext, FileManager);
        var content = await datasetDataHandler.CreateContentAsync(new() { Type = "event" });

        content.Id.Should().NotBeNullOrEmpty();
        Console.WriteLine($"{content.Id}: {content.Type}");

        await DeleteContentAsync(content.Id);
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

        content.Id.Should().NotBeNullOrEmpty();
        Console.WriteLine($"{content.Id}: {content.Type}");

        await DeleteContentAsync(content.Id);
    }

    [TestMethod]
    public async Task DeleteContent_NotExistingContent_ShouldThrowError()
    {
        var contentId = "not valid";
        var datasetDataHandler = new ContentActions(InvocationContext, FileManager);
        await Assert.ThrowsExceptionAsync<PluginMisconfigurationException>(async () =>
            await datasetDataHandler.DeleteContentAsync(new() { ContentId = contentId }));
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
        foreach (var item in result.Items)
        {
            Console.WriteLine($"{item.Id}: {item.Type}");
        }
    }

    private async Task DeleteContentAsync(string contentID)
    {
        var datasetDataHandler = new ContentActions(InvocationContext, FileManager);
        await datasetDataHandler.DeleteContentAsync(new() { ContentId = contentID });
        Console.WriteLine("Content was successfully deleted");
    }
}