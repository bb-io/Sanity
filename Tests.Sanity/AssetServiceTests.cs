using Apps.Sanity.Services;
using FluentAssertions;
using Tests.Sanity.Base;

namespace Tests.Sanity;

[TestClass]
public class AssetServiceTests : TestBase
{
    [TestMethod]
    public async Task GetAssetUrlAsync_ValidAssetId_ShouldReturnAssetUrl()
    {
        // Arrange
        var assetService = new AssetService(InvocationContext);
        var datasetId = "production";
        var assetId = "image-627f114524d7288dd02c709bc1389dc59877186a-848x841-png";

        // Act
        var assetUrl = await assetService.GetAssetUrlAsync(datasetId, assetId);

        // Assert
        assetUrl.Should().NotBeNullOrEmpty();
        assetUrl.Should().StartWith("https://");
        Console.WriteLine($"Asset URL: {assetUrl}");
    }
}