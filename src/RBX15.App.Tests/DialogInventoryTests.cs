using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using RBX15.App.Canvas;
using RBX15.App.Controls;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.App.ViewModels;
using RBX15.Engine;
using RBX15.Parity;
using SkiaSharp;
using Point = System.Windows.Point;

namespace RBX15.App.Tests;

/// <summary>
/// The dialog and status inventory (ledger dialogs-inventory, io-alert-inventory, diag-console-only-errors,
/// file-input-same-file-reselect). 4.1 had five alert() sites and one confirm() (editor.js:1270, :1506, :2344,
/// :2364, :2385, :2676), and wrote every other failure to the developer console only. Each test names the 4.1
/// line it maps. The 4.1 strings are read from the untouched editor.js itself, so a typo on either side fails.
/// Dialogs and pickers are local recording fakes: no real dialog opens and no input is synthesized.
/// </summary>
public sealed class DialogInventoryTests : IDisposable
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static readonly string EditorJs = Path.Combine(Root, "editor.js");
    readonly string _dir = Directory.CreateTempSubdirectory("rbx15-m4a-t3-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    // ------------------------------------------------------------------ the six 4.1 sites, verbatim

    [Fact] // dialogs-inventory: "verify no other modal text appears" (the 4.1 side)
    public void Editor_js_has_exactly_the_six_dialog_sites_of_the_inventory()
    {
        var sites = File.ReadLines(EditorJs)
            .Select((text, i) => (text, line: i + 1))
            .Where(x => Regex.IsMatch(x.text, @"\b(alert|confirm|prompt)\("))
            .Select(x => x.line);
        Assert.Equal([1270, 1506, 2344, 2364, 2385, 2676], sites);
        foreach (var other in new[] { "editor.html", "main.js" })
        {
            string text = File.ReadAllText(Path.Combine(Root, other));
            Assert.DoesNotMatch(@"\b(alert|confirm|prompt)\(|showMessageBox|showErrorBox", text);
        }
    }

    [Theory] // dialogs-inventory, io-alert-inventory: "compare text exactly"
    [InlineData(1270, "eraser")]          // v5: the status strip (eraser-selection-semantics, closed in M3b)
    [InlineData(1506, "empty-export")]    // v5: the themed confirm (io-empty-export-confirm, closed in M3a)
    [InlineData(2344, "pack-not-found")]
    [InlineData(2364, "pack-empty")]
    [InlineData(2385, "no-images")]
    [InlineData(2676, "load-failed")]
    public void Each_4_1_message_is_kept_verbatim(int line, string site)
    {
        const string reason = "layers must be an array, not a string";
        var (bindings, v5) = site switch
        {
            "eraser" => (Bind(), CanvasInteraction.NoLayerToErase),
            "empty-export" => (Bind(), ExportGuard.EmptyTemplateMessage),
            "pack-not-found" => (Bind(("packName", PackSource.Drips)), DialogText.PackNotFound(PackSource.Drips)),
            "pack-empty" => (Bind(("packName", PackSource.WordArt)), DialogText.PackEmpty(PackSource.WordArt)),
            "no-images" => (Bind(), DialogText.NoImagesInFolder),
            "load-failed" => (Bind(("err.message", reason)), DialogText.LoadFailed(reason)),
            _ => throw new ArgumentOutOfRangeException(nameof(site)),
        };
        Assert.Equal(Evaluate41(line, bindings), v5);
    }

    [Fact] // dialogs-inventory: "verify no other modal text appears" (the v5 side)
    public void Every_v5_dialog_shows_an_inventory_string()
    {
        string app = Path.Combine(Root, "src", "RBX15.App");
        string sep = Path.DirectorySeparatorChar.ToString();
        var sources = Directory.EnumerateFiles(app, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(sep + "obj" + sep) && !f.Contains(sep + "bin" + sep))
            .ToList();
        var shown = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in sources)
        {
            string text = File.ReadAllText(file);
            string name = Path.GetFileName(file);
            Assert.False(text.Contains("MessageBox.Show(", StringComparison.Ordinal), $"{name} opens a MessageBox");
            if (text.Contains("new ThemedDialog(", StringComparison.Ordinal)) Assert.Equal("ThemedDialogService.cs", name);
            foreach (Match m in Regex.Matches(text, @"\.(Alert|Confirm)\(\s*([A-Za-z_.]+)"))
            {
                string arg = m.Groups[2].Value;
                Assert.True(arg.StartsWith("DialogText.", StringComparison.Ordinal) || arg == "ExportGuard.EmptyTemplateMessage",
                    $"{name}: {m.Value} shows text outside the inventory");
                shown.Add(arg);
            }
        }
        Assert.Superset(new HashSet<string>
        {
            "ExportGuard.EmptyTemplateMessage", "DialogText.PackNotFound", "DialogText.PackEmpty",
            "DialogText.NoImagesInFolder", "DialogText.LoadFailed",
        }, shown);

        // The two v5 prompts with their own buttons (the unsaved-changes guard, M5's restore prompt) take their text from DialogText
        // inside ThemedDialogService; nothing else builds a dialog (checked above), so these are all the other modal texts there are.
        var prompts = Regex.Matches(File.ReadAllText(Path.Combine(app, "Services", "ThemedDialogService.cs")), @"Show\(\s*(DialogText\.[A-Za-z]+)")
            .Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(new HashSet<string> { "DialogText.SaveChanges", "DialogText.RestorePrompt" }, prompts);
    }

    // ------------------------------------------------------------------ the alerts, triggered

    [Fact] // io-alert-inventory, editor.js:2342-2346
    public void A_missing_built_in_pack_alerts_with_the_4_1_text() => StaThread.Run(() =>
    {
        var (library, dialogs, _) = Library(packsDirectory: _dir);
        library.LoadDripsCommand.Execute(null);
        Assert.Equal(["Built-in pack \"drips\" not found. Reinstall the app to restore it."], dialogs.Alerts);
        Assert.False(library.IsBrowserVisible);
    });

    [Fact] // io-alert-inventory, editor.js:2347-2366: the pack's folder holds no image
    public void An_empty_built_in_pack_alerts_with_the_4_1_text() => StaThread.Run(() =>
    {
        Directory.CreateDirectory(Path.Combine(_dir, PackSource.WordArt));
        File.WriteAllText(Path.Combine(_dir, PackSource.WordArt, "readme.txt"), "not an image");
        var (library, dialogs, _) = Library(packsDirectory: _dir);
        library.LoadWordArtCommand.Execute(null);
        Assert.Equal(["Built-in pack \"word_art\" is empty."], dialogs.Alerts);
        Assert.False(library.IsBrowserVisible);
    });

    [Fact] // io-alert-inventory, editor.js:2380-2387
    public void A_folder_with_no_images_alerts_with_the_4_1_text() => StaThread.Run(() =>
    {
        string folder = Directory.CreateDirectory(Path.Combine(_dir, "notes")).FullName;
        File.WriteAllText(Path.Combine(folder, "todo.txt"), "x");
        Directory.CreateDirectory(Path.Combine(folder, "sub"));
        File.WriteAllText(Path.Combine(folder, "sub", "logo.svg"), "<svg/>");
        var (library, dialogs, picks) = Library(packsDirectory: _dir);
        picks.Folders.Enqueue(folder);
        library.OpenFolderCommand.Execute(null);
        Assert.Equal(["No images found in that folder. Pick a folder with PNG, JPG, GIF, or WEBP files."], dialogs.Alerts);
        Assert.False(library.IsBrowserVisible);
    });

    [Fact] // diag-console-only-errors (new v5 path: the folder vanished between the pick and the read)
    public void A_folder_that_cannot_be_read_goes_to_the_status_strip_not_a_dialog() => StaThread.Run(() =>
    {
        var status = new FakeStatus();
        var (library, dialogs, picks) = Library(packsDirectory: _dir, status: status);
        picks.Folders.Enqueue(Path.Combine(_dir, "gone"));
        library.OpenFolderCommand.Execute(null);
        Assert.Empty(dialogs.Alerts);
        Assert.NotNull(status.Current);
        Assert.StartsWith("Couldn't open gone: ", status.Current);   // Task 2's FolderFailedMessage (T2-R8; critic L2)
        Assert.False(library.IsBrowserVisible);
    });

    [Theory] // io-alert-inventory parityCheck: a non-JSON file and a truncated JSON; editor.js:2675-2677
    [InlineData("not-json", "the file is not JSON: ")]
    [InlineData("truncated", "the file is not JSON: ")]
    [InlineData("missing", "Could not find file")]
    public void A_project_that_cannot_be_read_alerts_with_the_4_1_prefix_asks_nothing_and_changes_nothing(string kind, string reasonStart) => StaThread.Run(() =>
    {
        string path = Path.Combine(_dir, kind + ".r15proj");
        if (kind == "not-json") File.WriteAllText(path, "hello");
        if (kind == "truncated") File.WriteAllText(path, "{\"layers\":[");
        var (session, main, dialogs, picks) = Shell();
        using var red = Solid(SKColors.Red);
        Assert.True(session.ApplyWithSelection(s => LayerOps.AddImage(s, red, session.TakeId(), "Mine")));
        var before = session.Document;
        picks.Projects.Enqueue(path);

        Assert.False(Await(main.Files.OpenAsync()));

        string alert = Assert.Single(dialogs.Alerts);
        Assert.StartsWith("Failed to load project. " + reasonStart, alert);
        Assert.Empty(dialogs.SaveChanges);                   // a file that cannot open never asks to save (R4)
        Assert.Same(before, session.Document);
        Assert.Equal(1, session.UndoCount);
        Assert.True(session.IsDirty);
        Assert.Null(session.CurrentFile);
        Assert.False(main.Files.IsBusy);
    });

    // ------------------------------------------------------------------ console-only failures, now on the status strip

    [Theory] // diag-console-only-errors: an image that does not decode, through every route that adds one
    [InlineData("add-image")]     // editor.js:2700-2703, no onerror
    [InlineData("zone-click")]    // editor.js:954, :958 -> handleFiles :1006 'Image load failed' (console)
    [InlineData("zone-drop")]     // editor.js:957 -> handleFiles :1006
    [InlineData("canvas-drop")]   // editor.js:985 -> handleFiles :1006
    [InlineData("asset-click")]   // editor.js:2502-2510, no onerror
    public void An_undecodable_image_is_named_on_the_status_strip_by_every_route(string route) => StaThread.Run(() =>
    {
        string folder = Directory.CreateDirectory(Path.Combine(_dir, "art")).FullName;
        string bad = Path.Combine(folder, "broken.png");
        File.WriteAllBytes(bad, [1, 2, 3, 4, 5, 6, 7, 8]);
        var (session, main, dialogs, picks) = Shell();
        switch (route)
        {
            case "add-image": picks.Images.Enqueue([bad]); main.Home.File.AddImageCommand.Execute(null); break;
            case "zone-click": picks.Images.Enqueue([bad]); main.Layers.BrowseCommand.Execute(null); break;
            case "zone-drop": main.Layers.DropFiles([bad]); break;
            case "canvas-drop": main.Canvas.DropHandler!(new CanvasDrop([bad], new Point(100, 100))); break;
            case "asset-click":
                picks.Folders.Enqueue(folder);
                main.Assets.OpenFolderCommand.Execute(null);
                main.Assets.AddCommand.Execute(Assert.Single(main.Assets.Rows.SelectMany(r => r.Tiles)));
                break;
        }
        Assert.Equal("Couldn't add broken.png: The file isn't a readable image, or it is damaged.", main.Status.Message);
        Assert.Empty(dialogs.Alerts);
        Assert.Empty(session.Document.Layers);
        Assert.Equal(0, session.UndoCount);
    });

    [Theory] // diag-console-only-errors: handleFiles' three console lines (editor.js:1000, :1006, :1009)
    [InlineData("notes.txt", "Not a supported image type. Use PNG, JPEG, WebP, BMP or GIF.")]   // :1000 'Skipped non-image'
    [InlineData("broken.png", "The file isn't a readable image, or it is damaged.")]            // :1006 'Image load failed'
    [InlineData("locked.png", "The file couldn't be read.")]                                     // :1009 'FileReader failed'
    public void Each_console_only_import_failure_has_its_status_text(string fileName, string reason) => StaThread.Run(() =>
    {
        string path = Path.Combine(_dir, fileName);
        File.WriteAllBytes(path, fileName == "locked.png" ? Encoded(SKColors.Red) : [1, 2, 3, 4]);
        var (session, main, dialogs, _) = Shell();
        using (fileName == "locked.png" ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None) : null)
            main.Layers.DropFiles([path]);
        Assert.Equal($"Couldn't add {fileName}: {reason}", main.Status.Message);
        Assert.Empty(dialogs.Alerts);
        Assert.Empty(session.Document.Layers);
    });

    [Fact] // diag-console-only-errors, project-load-error-path-bugs: 4.1's layer img.onerror only counted (editor.js:2669-2672)
    public void A_project_layer_that_cannot_load_is_named_on_the_status_strip() => StaThread.Run(() =>
    {
        string path = Path.Combine(_dir, "half.r15proj");
        File.WriteAllText(path, "{\"layers\":[{\"id\":1,\"name\":\"Bad\",\"imgSrc\":\"data:image/png;base64,AAAA\"}]}");
        var (session, main, dialogs, picks) = Shell();
        picks.Projects.Enqueue(path);
        Assert.True(Await(main.Files.OpenAsync()));
        Assert.Equal("Opened half.r15proj. Couldn't load: Bad.", main.Status.Message);
        Assert.Empty(dialogs.Alerts);
        Assert.Empty(session.Document.Layers);
        Assert.Equal(path, session.CurrentFile);
    });

    [Fact] // diag-console-only-errors: a preview that cannot be shown (editor.js:2473-2475, no onerror)
    public void A_set_with_unreadable_previews_reports_once_and_a_group_switch_does_not_repeat_it() => StaThread.Run(() =>
    {
        string folder = AssetFolder();
        var (_, main, _, picks) = Shell();
        var messages = Messages(main.Status);
        picks.Folders.Enqueue(folder);
        main.Assets.OpenFolderCommand.Execute(null);
        Assert.Equal(4, main.Assets.GroupButtons.Count);      // All, a, b, c: the subfolder buttons show
        LoadThumbnails(main.Assets);

        var report = Assert.Single(messages);
        Assert.Contains(report, new[] { ThumbnailFailureReport.Message("bad1.png"), ThumbnailFailureReport.Message("bad2.png") });

        main.Assets.ShowGroupCommand.Execute(main.Assets.GroupButtons.Single(g => g.Key == "b"));
        LoadThumbnails(main.Assets);
        main.Assets.ShowGroupCommand.Execute(main.Assets.GroupButtons.Single(g => g.Key == AssetScan.AllKey));
        LoadThumbnails(main.Assets);
        Assert.Single(messages);
    });

    [Fact] // diag-console-only-errors: a new load is a new set, so its failures report again
    public void A_new_set_reports_its_unreadable_previews_again() => StaThread.Run(() =>
    {
        string folder = AssetFolder();
        var (_, main, _, picks) = Shell();
        var messages = Messages(main.Status);
        picks.Folders.Enqueue(folder);
        picks.Folders.Enqueue(folder);
        main.Assets.OpenFolderCommand.Execute(null);
        LoadThumbnails(main.Assets);
        main.Assets.OpenFolderCommand.Execute(null);
        LoadThumbnails(main.Assets);
        Assert.Equal(2, messages.Count);
    });

    // ------------------------------------------------------------------ the same file, picked again

    [Theory] // file-input-same-file-reselect: 4.1's drop-zone input never reset its value (editor.js:958)
    [InlineData("add-image")]     // #imageLoadInput reset its value (editor.js:2707)
    [InlineData("zone-click")]    // #fileInput did not (editor.js:954, :958): the 4.1 bug
    [InlineData("open-project")]  // #projectFileInput reset its value (editor.js:2686)
    [InlineData("open-folder")]   // #folderInput reset its value (editor.js:2316)
    public void Picking_the_same_file_again_acts_again(string route) => StaThread.Run(() =>
    {
        var (session, main, dialogs, picks) = Shell();
        string image = Path.Combine(_dir, "star.png");
        File.WriteAllBytes(image, Encoded(SKColors.Red));
        switch (route)
        {
            case "add-image":
            case "zone-click":
                picks.Images.Enqueue([image]);
                picks.Images.Enqueue([image]);
                for (int i = 0; i < 2; i++)
                    (route == "add-image" ? main.Home.File.AddImageCommand : main.Layers.BrowseCommand).Execute(null);
                Assert.Equal(["star", "star"], session.Document.Layers.Select(l => l.Name));
                Assert.Equal(2, session.UndoCount);
                Assert.Equal(2, picks.ImageCalls);
                break;
            case "open-project":
                string project = Path.Combine(_dir, "shirt.r15proj");
                using (var red = Solid(SKColors.Red))
                {
                    var seed = new EditorSession();
                    seed.ApplyWithSelection(s => LayerOps.AddImage(s, red, seed.TakeId(), "Star"));
                    File.WriteAllText(project, ProjectIO.Write(seed.Document, DateTimeOffset.UnixEpoch));
                }
                picks.Projects.Enqueue(project);
                picks.Projects.Enqueue(project);
                Assert.True(Await(main.Files.OpenAsync()));
                double x = session.Document.Layers[0].X;
                Assert.True(session.Apply(d => LayerOps.MoveTo(d, d.Layers[0].Id, x + 40, 0)));
                dialogs.SaveChoiceAnswer = SaveChoice.DontSave;
                Assert.True(Await(main.Files.OpenAsync()));
                Assert.Equal(["shirt.r15proj"], dialogs.SaveChanges);
                Assert.Equal(x, session.Document.Layers[0].X);       // the edit is gone: the file was read again
                Assert.False(session.IsDirty);
                Assert.Equal(0, session.UndoCount);
                Assert.Equal(project, session.CurrentFile);
                Assert.Equal(2, picks.ProjectCalls);
                break;
            case "open-folder":
                string folder = AssetFolder();
                picks.Folders.Enqueue(folder);
                picks.Folders.Enqueue(folder);
                main.Assets.OpenFolderCommand.Execute(null);
                var first = main.Assets.Rows.SelectMany(r => r.Tiles).ToList();
                main.Assets.OpenFolderCommand.Execute(null);
                var second = main.Assets.Rows.SelectMany(r => r.Tiles).ToList();
                Assert.Equal(first.Select(t => t.Entry.FileName), second.Select(t => t.Entry.FileName));
                Assert.DoesNotContain(second, t => first.Contains(t));   // a fresh set, not the old one kept
                Assert.Equal("4 unique images (0 duplicates removed)", main.Assets.CountText);
                Assert.Equal(2, picks.FolderCalls);
                break;
        }
        Assert.Empty(dialogs.Alerts);
    });

    [Fact] // file-input-same-file-reselect: no picker instance outlives a call, so nothing can remember a pick
    public void The_native_pickers_keep_no_dialog_between_calls()
    {
        var fields = typeof(Win32FileDialogs).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.DoesNotContain(fields, f => typeof(Microsoft.Win32.CommonDialog).IsAssignableFrom(f.FieldType));
    }

    // ------------------------------------------------------------------ helpers

    static Dictionary<string, string> Bind(params (string Name, string Value)[] pairs) => pairs.ToDictionary(p => p.Name, p => p.Value);

    /// <summary>
    /// The text 4.1 shows at editor.js:<paramref name="line"/>: the argument of its alert( or confirm(, a chain of
    /// single-quoted literals and identifiers joined by +, with each identifier taken from <paramref name="bindings"/>.
    /// </summary>
    static string Evaluate41(int line, IReadOnlyDictionary<string, string> bindings)
    {
        string src = File.ReadLines(EditorJs).ElementAt(line - 1);
        var call = Regex.Match(src, @"\b(alert|confirm)\(");
        Assert.True(call.Success, $"editor.js:{line} holds no alert( or confirm(");
        int i = call.Index + call.Length;
        var text = new StringBuilder();
        while (true)
        {
            while (src[i] == ' ') i++;
            if (src[i] == '\'')
            {
                for (i++; src[i] != '\''; i++)
                {
                    if (src[i] == '\\') i++;
                    text.Append(src[i]);
                }
                i++;
            }
            else
            {
                string id = Regex.Match(src[i..], @"^[A-Za-z_$][\w$.]*").Value;
                Assert.True(id.Length > 0, $"editor.js:{line}: unexpected '{src[i]}' at column {i + 1}");
                Assert.True(bindings.TryGetValue(id, out var value), $"editor.js:{line}: no binding for {id}");
                text.Append(value);
                i += id.Length;
            }
            while (src[i] == ' ') i++;
            if (src[i] == ')') return text.ToString();
            Assert.Equal('+', src[i]);
            i++;
        }
    }

    (EditorSession Session, MainViewModel Main, RecordingDialogs Dialogs, ScriptedPicks Picks) Shell()
    {
        var session = new EditorSession();
        var dialogs = new RecordingDialogs();
        var picks = new ScriptedPicks();
        var main = TestShell.Quiet(Composition.Build(session, picks, dialogs, new FakeColorPicker(), new FakeSettingsStore(), TestShell.Storage(), new ManualTickSource(), new FakeProcessProbe()));
        return (session, main, dialogs, picks);
    }

    static (AssetLibraryViewModel Library, RecordingDialogs Dialogs, ScriptedPicks Picks) Library(string packsDirectory, FakeStatus? status = null)
    {
        status ??= new FakeStatus();
        var dialogs = new RecordingDialogs();
        var picks = new ScriptedPicks();
        var library = new AssetLibraryViewModel(new ImportService(new EditorSession(), status), picks, dialogs, status,
            new FakeSettingsStore(), new PackSource(packsDirectory), new AssetThumbnailLoader());
        return (library, dialogs, picks);
    }

    /// <summary>root/a/bad1.png, root/b/bad2.png, root/b/good.png, root/c/good2.png: two previews that cannot show.</summary>
    string AssetFolder()
    {
        string root = Path.Combine(_dir, "assets");
        foreach (var sub in new[] { "a", "b", "c" }) Directory.CreateDirectory(Path.Combine(root, sub));
        File.WriteAllBytes(Path.Combine(root, "a", "bad1.png"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(root, "b", "bad2.png"), [4, 5, 6]);
        File.WriteAllBytes(Path.Combine(root, "b", "good.png"), Encoded(SKColors.Blue));
        File.WriteAllBytes(Path.Combine(root, "c", "good2.png"), Encoded(SKColors.Lime));
        return root;
    }

    /// <summary>Reads every shown tile's Thumbnail, as a realized row does (R5), and pumps until each has loaded or failed.</summary>
    static void LoadThumbnails(AssetLibraryViewModel library)
    {
        var tiles = library.Rows.SelectMany(r => r.Tiles).ToList();
        foreach (var tile in tiles) _ = tile.Thumbnail;
        PumpUntil(() => tiles.All(t => t.Thumbnail is not null || t.ThumbnailFailed));
    }

    static List<string> Messages(StatusViewModel status)
    {
        var shown = new List<string>();
        status.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StatusViewModel.Message) && status.Message is { } m) shown.Add(m);
        };
        return shown;
    }

    /// <summary>Runs the STA dispatcher until <paramref name="task"/> completes: its continuations post to it.</summary>
    static T Await<T>(Task<T> task)
    {
        PumpUntil(() => task.IsCompleted);
        return task.GetAwaiter().GetResult();
    }

    static void PumpUntil(Func<bool> done)
    {
        var clock = Stopwatch.StartNew();
        while (!done())
        {
            if (clock.ElapsedMilliseconds > 10_000) throw new TimeoutException("the dispatcher work did not finish in 10 s");
            StaThread.Pump();
            Thread.Sleep(5);
        }
    }

    static SKImage Solid(SKColor color)
    {
        using var surface = SKSurface.Create(new SKImageInfo(8, 8));
        surface.Canvas.Clear(color);
        return surface.Snapshot();
    }

    static byte[] Encoded(SKColor color)
    {
        using var image = Solid(color);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>Records every dialog; AskSaveChanges answers <see cref="SaveChoiceAnswer"/>.</summary>
    sealed class RecordingDialogs : IDialogService
    {
        public List<string> Alerts { get; } = [];
        public List<string> Confirms { get; } = [];
        public List<string> SaveChanges { get; } = [];
        public SaveChoice SaveChoiceAnswer { get; set; } = SaveChoice.Cancel;

        public void Alert(string message) => Alerts.Add(message);

        public bool Confirm(string message)
        {
            Confirms.Add(message);
            return false;
        }

        public SaveChoice AskSaveChanges(string documentName)
        {
            SaveChanges.Add(documentName);
            return SaveChoiceAnswer;
        }

        public RestoreChoice AskRestore(string documentName, DateTimeOffset autosavedAt) => RestoreChoice.Later;
    }

    /// <summary>Answers each picker from its own queue (an empty queue is a cancel) and counts the calls.</summary>
    sealed class ScriptedPicks : IFileDialogs
    {
        public Queue<IReadOnlyList<string>> Images { get; } = new();
        public Queue<string> Projects { get; } = new();
        public Queue<string> Folders { get; } = new();
        public int ImageCalls { get; private set; }
        public int ProjectCalls { get; private set; }
        public int FolderCalls { get; private set; }

        public IReadOnlyList<string> OpenImages()
        {
            ImageCalls++;
            return Images.TryDequeue(out var paths) ? paths : [];
        }

        public string? SavePng(string suggestedFileName) => null;

        public string? OpenProject(string? initialDirectory)
        {
            ProjectCalls++;
            return Projects.TryDequeue(out var path) ? path : null;
        }

        public string? SaveProject(string suggestedFileName, string? initialDirectory) => null;

        public string? PickFolder(string? initialDirectory)
        {
            FolderCalls++;
            return Folders.TryDequeue(out var path) ? path : null;
        }
    }
}
