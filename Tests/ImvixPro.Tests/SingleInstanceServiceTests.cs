using ImvixPro.Services;

namespace ImvixPro.Tests;

public sealed class SingleInstanceServiceTests
{
    [Fact]
    public void ActivationPayload_RoundTripsDistinctTrimmedPaths()
    {
        var serialized = SingleInstanceService.SerializeActivationRequest(
        [
            @"C:\Images\sample.png",
            @"C:\Images\sample.png",
            "  C:\\Docs\\report.pdf  "
        ]);

        var request = SingleInstanceService.DeserializeActivationRequest(serialized);

        Assert.True(request.IsActivationRequested);
        Assert.Equal(
        [
            @"C:\Images\sample.png",
            @"C:\Docs\report.pdf"
        ],
            request.Paths);
    }

    [Fact]
    public void DeserializeActivationRequest_SupportsLegacyActivatePayload()
    {
        var request = SingleInstanceService.DeserializeActivationRequest("activate");

        Assert.True(request.IsActivationRequested);
        Assert.Empty(request.Paths);
    }

    [Fact]
    public void MergeRequests_PreservesActivationAndCombinesDistinctPaths()
    {
        var merged = SingleInstanceService.MergeRequests(
            AppActivationRequest.Activate([@"C:\Images\one.png"]),
            AppActivationRequest.Activate([@"C:\Images\one.png", @"C:\Images\two.png"]));

        Assert.True(merged.IsActivationRequested);
        Assert.Equal(
        [
            @"C:\Images\one.png",
            @"C:\Images\two.png"
        ],
            merged.Paths);
    }
}
