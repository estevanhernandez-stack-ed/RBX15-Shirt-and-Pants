using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace RBX15.Engine;

/// <summary>
/// The vendored Word Art and UI faces (assets/fonts, pinned by its README manifest), resolved the way
/// Chromium resolved the @font-face rules 4.1 loaded. tools/parity-ref/fonts.js declares one
/// @font-face per family and per weight 4.1 requests (editor.html:11), and the manifest's "4.1
/// requests" column lists exactly those weights.
/// <list type="bullet">
/// <item>A request picks a declared face by the CSS weight-matching rule (CSS Fonts 4, 5.2).</item>
/// <item>A variable file has its wght axis set to the request clamped to the face's declared weight
/// (Blink FontCustomPlatformData), so a 900 request draws Dancing Script at 700.</item>
/// <item>A request of 600 or more on a face declared below 600 is synthetic bold (Blink
/// CSSSegmentedFontFace sets SyntheticBold when the face's maximum weight is under BoldThreshold),
/// drawn with SkFont embolden. Advances do not change.</item>
/// </list>
/// Files load from <c>assets/fonts</c> under <see cref="AppContext.BaseDirectory"/>. RBX15.Engine.csproj
/// copies them there for every project that references the engine.
/// </summary>
public static partial class Fonts
{
    /// <summary>The Word Art families in the order of 4.1's waFont select (editor.html:230-246).</summary>
    public static readonly IReadOnlyList<string> WordArtFamilies = new[]
    {
        "Anton", "Bebas Neue", "Dancing Script", "Great Vibes", "Pacifico", "Creepster", "Poppins", "Lato",
        "Bangers", "Permanent Marker", "Bungee Shade", "Rubik Mono One", "Press Start 2P", "Righteous", "Orbitron",
    };

    /// <summary>Blink's BoldThreshold: a request at or above it on a lighter face is emboldened.</summary>
    public const int BoldThreshold = 600;

    /// <summary>One declared face: a family, the weight its @font-face declares, and its file under <see cref="Root"/>.</summary>
    internal sealed record Face(string Family, int Weight, string File);

    /// <summary>The directory holding the manifest (README.md) and the font files.</summary>
    public static string Root { get; } = Path.Combine(AppContext.BaseDirectory, "assets", "fonts");

    static readonly Lazy<IReadOnlyList<Face>> Declared = new(() => ParseManifest(File.ReadAllText(Path.Combine(Root, "README.md"))));
    static readonly ConcurrentDictionary<string, byte[]> Files = new(StringComparer.Ordinal);
    // Lazy so the GetOrAdd factory runs once per face: ConcurrentDictionary may run a factory on several
    // threads and keep one result, which would leak the losers' native typefaces.
    static readonly ConcurrentDictionary<Face, Lazy<SKTypeface>> Typefaces = new();

    internal static readonly uint WghtTag = 'w' << 24 | 'g' << 16 | 'h' << 8 | 't';

    [GeneratedRegex(@"^\| `(?<file>[^`]+\.ttf)` \| (?<family>[^|]+?) \| (?<weights>[0-9]+(?:, [0-9]+)*) \|", RegexOptions.Multiline)]
    private static partial Regex FaceRow();

    /// <summary>One face per weight listed in each font row's "4.1 requests" cell.</summary>
    internal static IReadOnlyList<Face> ParseManifest(string readme)
    {
        var faces = new List<Face>();
        foreach (Match m in FaceRow().Matches(readme.Replace("\r\n", "\n")))
            foreach (var w in m.Groups["weights"].Value.Split(", "))
                faces.Add(new Face(m.Groups["family"].Value, int.Parse(w, CultureInfo.InvariantCulture), m.Groups["file"].Value));
        return faces;
    }

    /// <summary>
    /// The declared face a request matches (CSS Fonts 4, 5.2, font-weight): above 500, the lightest
    /// at or above it, else the heaviest below; 400 to 500, the lightest from it up to 500, else the
    /// heaviest below, else the lightest above 500; below 400, the heaviest at or below it, else the
    /// lightest above.
    /// </summary>
    internal static Face Match(string family, int weight)
    {
        var faces = Declared.Value.Where(f => f.Family == family).ToList();
        if (faces.Count == 0) throw new ArgumentException($"no vendored font family '{family}'", nameof(family));
        Face? Lightest(Func<int, bool> p) => faces.Where(f => p(f.Weight)).MinBy(f => f.Weight);
        Face? Heaviest(Func<int, bool> p) => faces.Where(f => p(f.Weight)).MaxBy(f => f.Weight);
        if (weight > 500) return Lightest(w => w >= weight) ?? Heaviest(w => w < weight)!;
        if (weight >= 400) return Lightest(w => w >= weight && w <= 500) ?? Heaviest(w => w < weight) ?? Lightest(w => w > 500)!;
        return Heaviest(w => w <= weight) ?? Lightest(w => w > weight)!;
    }

    /// <summary>True when Chromium draws this request with synthetic bold.</summary>
    public static bool IsSyntheticBold(string family, int weight) =>
        weight >= BoldThreshold && Match(family, weight).Weight < BoldThreshold;

    /// <summary>
    /// The typeface for a request: the matched file, its wght axis (when it has one) at the face's
    /// declared weight. Cache-owned; callers must not dispose.
    /// </summary>
    public static SKTypeface Get(string family, int weight) =>
        Typefaces.GetOrAdd(Match(family, weight), face => new Lazy<SKTypeface>(() => Load(face))).Value;

    static SKTypeface Load(Face face)
    {
        using var data = SKData.CreateCopy(FileBytes(face.File));
        var typeface = SKTypeface.FromData(data) ?? throw new InvalidDataException($"not a font: {face.File}");
        if (!typeface.VariationDesignParameters.Any(a => a.Tag == WghtTag)) return typeface;
        var instanced = typeface.Clone(new SKFontArguments
        {
            VariationDesignPosition = new[] { new SKFontVariationPositionCoordinate { Axis = WghtTag, Value = face.Weight } },
        });
        typeface.Dispose();
        return instanced;
    }

    /// <summary>The wght axis value a request draws at, or null for a static file.</summary>
    internal static float? AxisWeight(string family, int weight) =>
        Get(family, weight).VariationDesignParameters.Any(a => a.Tag == WghtTag) ? Match(family, weight).Weight : null;

    /// <summary>The matched file's bytes, which HarfBuzz shapes from. Cache-owned.</summary>
    internal static byte[] FileBytes(string family, int weight) => FileBytes(Match(family, weight).File);

    static byte[] FileBytes(string file) =>
        Files.GetOrAdd(file, f => File.ReadAllBytes(Path.Combine(Root, f.Replace('/', Path.DirectorySeparatorChar))));

    /// <summary>
    /// The SkFont Chromium's canvas draws a request with (Blink FontPlatformData::SetupSkFont): the
    /// typeface and size, embolden for synthetic bold, grayscale antialiasing (canvas text on a
    /// transparent canvas is never LCD), subpixel positioning, no hinting and linear metrics.
    /// Caller disposes.
    /// </summary>
    public static SKFont CreateFont(string family, int weight, float size) => new(Get(family, weight), size)
    {
        Embolden = IsSyntheticBold(family, weight),
        Edging = SKFontEdging.Antialias,
        Subpixel = true,
        Hinting = SKFontHinting.None,
        LinearMetrics = true,
    };
}
