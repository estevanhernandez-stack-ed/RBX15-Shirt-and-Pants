using System.Text.Json;
using RBX15.Engine;
using RBX15.Parity;

namespace RBX15.Parity.Tests;

public class SceneLoaderProjectTests
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);

    static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void The_project_op_is_supported()
    {
        var scene = Json("""{ "name": "p", "actions": [{ "op": "project", "file": "tests/goldens/projects/proj-shirt-drawn.r15proj" }] }""");
        Assert.Empty(SceneLoader.UnsupportedOps(scene));
    }

    [Fact] // loadProject replaces mode, background, fills and layers (editor.js:2599-2627)
    public void A_project_op_replaces_the_scene_document_with_the_file()
    {
        var scene = Json("""
            { "name": "p", "bgColor": "#ffffff", "regionColors": { "torso_back": "#000000" },
              "actions": [{ "op": "project", "file": "tests/goldens/projects/proj-pants-legacy.r15proj" }] }
            """);

        var doc = SceneLoader.Parse(scene, Root).Document;

        Assert.Equal(TemplateMode.Pants, doc.Mode);
        Assert.Equal("#192e44", doc.BgColor);
        Assert.NotNull(doc.BgImage);
        Assert.Equal(new[] { "waist_front", "waist_back", "rleg_front", "lleg_front" }, doc.RegionFills.Select(f => f.Region));
        Assert.Equal(new[] { "L1" }, doc.Layers.Select(l => l.Name));
    }

    [Fact] // the harness froze only projects whose every layer loaded
    public void A_project_that_drops_a_layer_refuses_to_parse()
    {
        var file = Path.Combine(Path.GetTempPath(), $"rbx15-drop-{Guid.NewGuid():N}.r15proj");
        File.WriteAllText(file, """{ "layers": [{ "id": 1, "name": "Broken", "imgSrc": "data:image/png;base64,AAAA" }] }""");
        try
        {
            var scene = Json($$"""{ "name": "p", "actions": [{ "op": "project", "file": {{JsonSerializer.Serialize(file)}} }] }""");
            var ex = Assert.Throws<InvalidDataException>(() => SceneLoader.Parse(scene, Root));
            Assert.Contains("Broken", ex.Message);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
