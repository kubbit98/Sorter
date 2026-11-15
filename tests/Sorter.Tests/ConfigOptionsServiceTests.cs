using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Sorter.Data;

namespace Sorter.Tests;

public class ConfigOptionsServiceTests
{
    private readonly TestOptionsMonitor<ConfigOptions> _configMonitor = new(new ConfigOptions { Source = "/a", Destination = "/b" });
    private readonly TestOptionsMonitor<KeyBindsOptions> _keyBindsMonitor = new(new KeyBindsOptions());
    private readonly ConfigOptionsService _service;

    public ConfigOptionsServiceTests()
    {
        _service = new ConfigOptionsService(
            new DestinationDFP(null, NullLogger.Instance),
            new SourceDFP(null, NullLogger.Instance),
            _configMonitor,
            _keyBindsMonitor,
            new ConfigurationBuilder().Build());
    }

    [Fact]
    public async Task WaitUntilApplied_ReturnsAsSoonAsMonitorHasTheValues()
    {
        var saved = new ConfigOptions { Source = "/a", Destination = "/b" };

        Assert.True(await _service.WaitUntilAppliedAsync(saved, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task WaitUntilApplied_WaitsForTheMonitorToReload()
    {
        var saved = new ConfigOptions { Source = "/new", Destination = "/b" };
        _ = Task.Run(async () =>
        {
            await Task.Delay(200);
            _configMonitor.CurrentValue = new ConfigOptions { Source = "/new", Destination = "/b" };
        });

        Assert.True(await _service.WaitUntilAppliedAsync(saved, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task WaitUntilApplied_GivesUpAfterTimeoutInsteadOfHanging()
    {
        var saved = new ConfigOptions { Source = "/never", Destination = "/b" };
        var stopwatch = Stopwatch.StartNew();

        Assert.False(await _service.WaitUntilAppliedAsync(saved, TimeSpan.FromMilliseconds(200)));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task WaitUntilApplied_ForKeyBinds_ComparesBindings()
    {
        var saved = new KeyBindsOptions { KeyBinds = new() { ["a"] = "/folder" } };

        Assert.False(await _service.WaitUntilAppliedAsync(saved, TimeSpan.FromMilliseconds(100)));
        _keyBindsMonitor.CurrentValue = new KeyBindsOptions { KeyBinds = new() { ["a"] = "/folder" } };
        Assert.True(await _service.WaitUntilAppliedAsync(saved, TimeSpan.FromMilliseconds(100)));
    }
}
