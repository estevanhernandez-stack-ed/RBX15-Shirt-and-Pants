using System.Collections.Immutable;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>Shared builders for the Home tab tests (Task 3).</summary>
internal static class HomeTestKit
{
    public static SKImage Solid(int w, int h, SKColor c)
    {
        using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(c);
        return surface.Snapshot();
    }

    public static Layer LayerOf(int id, int w = 200, int h = 100) =>
        new() { Id = id, Image = Solid(w, h, SKColors.Red), Name = "Image " + id };

    /// <summary>A session whose document holds the given layers (no history, nothing selected).</summary>
    public static EditorSession SessionWith(params Layer[] layers) =>
        new(new Document { Layers = layers.ToImmutableList() });

    public static HomeViewModel Home(EditorSession session, FakeColorPicker? picker = null)
    {
        var status = new FakeStatus();
        return new(session, picker ?? new FakeColorPicker())
        {
            File = new FileGroupViewModel(session, new FakeFileDialogs(), new FakeDialogService(), status, new ImportService(session, status)),
        };
    }

    public static RegionTileViewModel Tile(RegionsGroupViewModel regions, string region) =>
        regions.Body.Concat(regions.Limb1).Concat(regions.Limb2).Single(t => t.Region == region);

    /// <summary>Records every PropertyChanged name raised by <paramref name="source"/>.</summary>
    public static List<string?> Track(System.ComponentModel.INotifyPropertyChanged source)
    {
        var names = new List<string?>();
        source.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        return names;
    }
}
