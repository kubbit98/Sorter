using Microsoft.Extensions.Configuration;
using Sorter.Data;

namespace Sorter.Tests;

public class SorterSettingsTests : IDisposable
{
    private readonly TempDirectory _root = new();
    private readonly string _source;
    private readonly string _destination;

    public SorterSettingsTests()
    {
        _source = Directory.CreateDirectory(_root.Combine("src")).FullName;
        _destination = Directory.CreateDirectory(_root.Combine("dst")).FullName;
    }

    public void Dispose() => _root.Dispose();

    private static IConfiguration Configuration(string? useThumbnails = null)
    {
        var values = new Dictionary<string, string?>();
        if (useThumbnails != null) values["UseThumbnails"] = useThumbnails;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private ConfigOptions ValidOptions() => new() { Source = _source, Destination = _destination };

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("missing")]
    public void InvalidSource_Fails(string source)
    {
        var options = ValidOptions();
        options.Source = source == "missing" ? _root.Combine("missing") : source;

        Assert.False(SorterSettings.TryCreate(options, Configuration(), out var settings));
        Assert.Null(settings);
    }

    [Theory]
    [InlineData("")]
    [InlineData("missing")]
    public void InvalidDestination_Fails(string destination)
    {
        var options = ValidOptions();
        options.Destination = destination == "missing" ? _root.Combine("missing") : destination;

        Assert.False(SorterSettings.TryCreate(options, Configuration(), out _));
    }

    [Fact]
    public void RelativePaths_BecomeAbsolute()
    {
        var options = new ConfigOptions
        {
            Source = Path.GetRelativePath(Directory.GetCurrentDirectory(), _source),
            Destination = Path.GetRelativePath(Directory.GetCurrentDirectory(), _destination),
            ExcludeDirsSource = [Path.GetRelativePath(Directory.GetCurrentDirectory(), Path.Combine(_source, "skip"))]
        };

        Assert.True(SorterSettings.TryCreate(options, Configuration(), out var settings));
        Assert.Equal(_source, settings.Source);
        Assert.Equal(_destination, settings.Destination);
        Assert.Equal([Path.Combine(_source, "skip")], settings.ExcludedSourceDirectories);
    }

    [Fact]
    public void BlankAndNullLists_AreTreatedAsEmpty()
    {
        var options = ValidOptions();
        options.ExcludeDirsSource = ["", "   "];
        options.ExcludeDirsDestination = null!;
        options.WhiteList = null!;
        options.BlackList = null!;

        Assert.True(SorterSettings.TryCreate(options, Configuration(), out var settings));
        Assert.Empty(settings.ExcludedSourceDirectories);
        Assert.Empty(settings.ExcludedDestinationDirectories);
        Assert.True(settings.Filter.IsAllowed("jpg"));
    }

    [Fact]
    public void Filter_UsesListsFromOptions()
    {
        var options = ValidOptions();
        options.UseWhiteListInsteadOfBlackList = true;
        options.WhiteList = ["txt"];

        Assert.True(SorterSettings.TryCreate(options, Configuration(), out var settings));
        Assert.True(settings.Filter.IsAllowed("txt"));
        Assert.False(settings.Filter.IsAllowed("jpg"));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    public void UseThumbnails_ComesFromAppSettings(string? value, bool expected)
    {
        Assert.True(SorterSettings.TryCreate(ValidOptions(), Configuration(value), out var settings));
        Assert.Equal(expected, settings.UseThumbnails);
    }

    [Fact]
    public void ToFullPaths_SkipsNullAndBlankEntries()
    {
        Assert.Empty(SorterSettings.ToFullPaths(null));
        Assert.Equal([_source], SorterSettings.ToFullPaths(["", "  ", _source]));
    }
}
