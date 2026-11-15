using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sorter.Data;

namespace Sorter.Tests;

// SaveOptions writes config.json to the current directory, which is shared by the whole process
[CollectionDefinition(nameof(CurrentDirectoryCollection), DisableParallelization = true)]
public class CurrentDirectoryCollection;

[Collection(nameof(CurrentDirectoryCollection))]
public class ConfigSaveIntegrationTests : IDisposable
{
    private readonly TempDirectory _root = new();
    private readonly string _previousDirectory = Directory.GetCurrentDirectory();

    public ConfigSaveIntegrationTests()
    {
        Directory.SetCurrentDirectory(_root.Path);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_previousDirectory);
        _root.Dispose();
    }

    [Fact]
    public async Task SavedOptions_AreAppliedWithoutFileSystemNotifications()
    {
        string source = Directory.CreateDirectory(_root.Combine("src")).FullName;
        string destination = Directory.CreateDirectory(_root.Combine("dst")).FullName;
        var configuration = new ConfigurationManager();
        configuration.AddJsonFile(_root.Combine("config.json"), optional: true, reloadOnChange: false);
        var services = new ServiceCollection();
        services.AddOptions<ConfigOptions>().Bind(configuration.GetSection(ConfigOptions.config));
        services.AddOptions<KeyBindsOptions>();
        using var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<ConfigOptions>>();
        var service = new ConfigOptionsService(
            new DestinationDFP(null, NullLogger.Instance), new SourceDFP(null, NullLogger.Instance),
            monitor, provider.GetRequiredService<IOptionsMonitor<KeyBindsOptions>>(), configuration);
        var previous = monitor.CurrentValue;
        var saved = new ConfigOptions { Source = source, Destination = destination, AllowRename = false, BlackList = ["mp4"] };

        service.SaveOptions(saved);

        Assert.True(await service.WaitUntilAppliedAsync(saved, TimeSpan.FromSeconds(1)));
        Assert.Equal(source, monitor.CurrentValue.Source);
        Assert.False(monitor.CurrentValue.AllowRename);
        Assert.NotSame(previous, monitor.CurrentValue);
        Assert.Equal(string.Empty, previous.Source);
    }
}
