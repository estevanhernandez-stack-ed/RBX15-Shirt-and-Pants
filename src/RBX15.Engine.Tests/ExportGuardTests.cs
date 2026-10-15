using SkiaSharp;

namespace RBX15.Engine.Tests;

/// <summary>exportPNG's blank-template guard (editor.js:1500-1506; export-png-guard, io-empty-export-confirm).</summary>
public class ExportGuardTests
{
    static Layer Layer(bool visible) => new() { Id = 1, Image = Px.Solid(4, 4, SKColors.Red), Visible = visible };

    [Fact]
    public void The_message_is_4_1_verbatim_with_its_em_dash() =>
        Assert.Equal("The template is empty — download a blank PNG anyway?", ExportGuard.EmptyTemplateMessage);

    [Fact]
    public void A_fresh_document_has_no_art() => Assert.False(ExportGuard.HasArt(new Document()));

    [Fact] // a background color alone, at any opacity, is not art (ledger io-empty-export-confirm)
    public void A_background_color_alone_is_not_art() =>
        Assert.False(ExportGuard.HasArt(new Document { BgColor = "#b71c1c", BgOpacity = 0.5, ShowMask = false, ShowGrid = true }));

    [Fact] // l.visible && l.img: hidden layers do not count (ledger canvas-export-pipeline quirk)
    public void Hidden_layers_only_are_not_art() => Assert.False(ExportGuard.HasArt(new Document { Layers = [Layer(false)] }));

    [Fact]
    public void A_visible_layer_is_art() => Assert.True(ExportGuard.HasArt(new Document { Layers = [Layer(false), Layer(true) with { Id = 2 }] }));

    [Fact] // Object.keys(regionColors).length > 0
    public void A_region_fill_is_art() =>
        Assert.True(ExportGuard.HasArt(new Document { RegionFills = [("torso_front", new Fill("solid", "#17d4fa"))] }));

    [Fact] // !!bgImage
    public void A_background_image_is_art() => Assert.True(ExportGuard.HasArt(new Document { BgImage = Px.Solid(2, 2, SKColors.Blue) }));
}
