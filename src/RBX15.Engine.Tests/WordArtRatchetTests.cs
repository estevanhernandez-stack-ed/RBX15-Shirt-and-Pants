using System.Text.Json;
using RBX15.Parity;

namespace RBX15.Engine.Tests;

/// <summary>
/// The Word Art regression ratchet. The Word Art rule leaves glyph edges unbounded and the swap guard
/// replaces the engine's bitmap, so an edge-only change (retro stripe colors, the neon blur sigma, a
/// sub-pixel glyph shift) would pass both. This compares the engine's own Word Art bitmap (the layer
/// image Add to canvas makes, through the same SceneLoader path as WordArtSwapTests) directly with
/// 4.1's frozen bitmap in tests/goldens/app/wordart-ref, and holds each scene to the numbers measured
/// on 2026-10-14 (ImageDiff.Compare: max delta, differing pixels, interior differences).
/// <para>
/// The ceilings are a ratchet: they move only by a deliberate update that is logged (decision log and
/// the wordart-glyph-rasterization ledger row), never to make a failing change pass. A change that
/// lowers a number should lower its ceiling in the same commit.
/// </para>
/// </summary>
public class WordArtRatchetTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static readonly string Dir = Path.Combine(Root, "tests", "goldens", "app");
    static readonly string RefDir = Path.Combine(Dir, "wordart-ref");

    /// <summary>
    /// Scene: max delta, differing pixels, interior differences, as measured on 2026-10-14. Interior is 0
    /// on every scene at bitmap level (outline-anton's 7 one-level pixels appear only after the export's
    /// downscale). A max delta of 255 is an un-premultiplied edge pixel of alpha 1 against alpha 0, so on
    /// those scenes the differing count is what bites.
    /// </summary>
    static readonly Dictionary<string, (int MaxDelta, int Differing, int Interior)> Ceilings = new()
    {
        ["wordart-gradient-135-poppins"] = (92, 1951, 0),
        ["wordart-gradient-anton"] = (71, 1746, 0),
        ["wordart-gradient-dancing"] = (94, 3076, 0),
        ["wordart-gradient-poppins"] = (92, 1946, 0),
        ["wordart-multiline-orbitron"] = (79, 3884, 0),
        ["wordart-neon-anton"] = (255, 4420, 0),
        ["wordart-neon-dancing"] = (255, 3856, 0),
        ["wordart-neon-poppins"] = (255, 5877, 0),
        ["wordart-outline-anton"] = (255, 2146, 0),
        ["wordart-outline-dancing"] = (255, 2961, 0),
        ["wordart-outline-poppins"] = (255, 2367, 0),
        ["wordart-outline-stroke-0-dancing"] = (255, 2961, 0),
        ["wordart-retro-anton"] = (255, 2429, 0),
        ["wordart-retro-dancing"] = (255, 5848, 0),
        ["wordart-retro-poppins"] = (255, 2780, 0),
        ["wordart-shadow-anton"] = (83, 1987, 0),
        ["wordart-shadow-dancing"] = (88, 3645, 0),
        ["wordart-shadow-poppins"] = (60, 2152, 0),
        ["wordart-solid-anton"] = (83, 1745, 0),
        ["wordart-solid-dancing"] = (94, 3113, 0),
        ["wordart-solid-poppins"] = (92, 1955, 0),
        ["wordart-stroke-12-anton"] = (255, 1592, 0),
    };

    static readonly Lazy<IReadOnlyList<JsonElement>> Raw = new(() =>
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, "scenes.json")));
        return doc.RootElement.EnumerateArray()
            .Where(e => e.TryGetProperty("actions", out var a) && a.EnumerateArray().Any(x => x.GetProperty("op").GetString() == "wordart"))
            .Select(e => e.Clone())
            .ToList();
    });

    static string NameOf(JsonElement e) => e.GetProperty("name").GetString()!;

    public static TheoryData<string> WordArtScenes()
    {
        var data = new TheoryData<string>();
        foreach (var e in Raw.Value) data.Add(NameOf(e));
        return data;
    }

    [Fact]
    public void Every_wordart_scene_has_a_ceiling()
    {
        Assert.Equal(Raw.Value.Select(NameOf).Order(StringComparer.Ordinal), Ceilings.Keys.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(WordArtScenes))]
    public void The_engine_word_art_bitmap_stays_within_its_measured_distance_of_4_1s(string name)
    {
        var scene = SceneLoader.Parse(Raw.Value.Single(e => NameOf(e) == name), Root);
        using var engine = ImageDiff.FromImage(scene.Document.Layers[^1].Image);
        using var reference = ImageDiff.Load(Path.Combine(RefDir, name + ".png"));
        var result = ImageDiff.Compare(reference, engine);
        Assert.True(result.SameSize, $"{name}: {result}");
        Assert.True(Ceilings.TryGetValue(name, out var ceiling), $"{name}: no ceiling; measured {result}");
        Assert.True(result.MaxDelta <= ceiling.MaxDelta && result.DifferingPixels <= ceiling.Differing && result.InteriorDifferences <= ceiling.Interior,
            $"{name}: {result} exceeds the ratchet (max delta {ceiling.MaxDelta}, differing {ceiling.Differing}, interior {ceiling.Interior})");
    }
}
