using Apps.Sanity.Actions;
using Apps.Sanity.Models.Requests;
using Apps.Sanity.Models.Responses.Content;
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

        await VerifySearchContentAsync(request, result =>
        {
            return Task.Run(() => result.Items.Should().AllSatisfy(x => x.Type.Should().Be("event")));
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

        await VerifySearchContentAsync(request, result =>
        {
            return Task.Run(() => result.Items.Should().AllSatisfy(x => x.CreatedAt.Should().BeAfter(createdAfter)));
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
    
    private async Task VerifySearchContentAsync(SearchContentRequest request, Func<SearchContentResponse, Task>? additionalAssertions = null)
    {
        var datasetDataHandler = new ContentActions(InvocationContext);
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
}