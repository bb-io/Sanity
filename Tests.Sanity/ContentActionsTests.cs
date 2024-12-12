using Apps.Sanity.Actions;
using FluentAssertions;
using Tests.Sanity.Base;

namespace Tests.Sanity;

[TestClass]
public class ContentActionsTests : TestBase
{
    [TestMethod]
    public async Task ValidateSearchContentAction()
    {
        var datasetDataHandler = new ContentActions(InvocationContext);
        var result = await datasetDataHandler.SearchContentAsync(new());

        if (result.Items == null!)
        {
            throw new Exception("Action should not return null collection");
        }

        result.TotalCount.Should().Be(result.Items.Count);

        foreach (var item in result.Items)
        {
            Console.WriteLine($"{item.Id}: {item.Type}");
        }
    }
}