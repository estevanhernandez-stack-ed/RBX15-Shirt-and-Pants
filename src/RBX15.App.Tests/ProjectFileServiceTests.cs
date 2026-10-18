using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows.Threading;
using RBX15.App.Services;
using RBX15.App.Tests.Fakes;
using RBX15.Engine;
using RBX15.Parity;
using SkiaSharp;

namespace RBX15.App.Tests;

/// <summary>
/// Save work, Save As, Open and the unsaved-changes guard (spec §3 "Saving"; saveProject and loadProject,
/// editor.js:2550-2687; interfaces R1, R2, R4, R11-R13; ledger project-save-format, project-save-filename,
/// project-load-flow, project-load-layers, project-load-error-path-bugs, project-load-history-interplay,
/// project-load-no-imgsrc-hang, project-load-untrusted-src, io-download-save-dialog, shell-no-unsaved-changes-guard).
/// Real files in a temp folder; fake dialogs; no window.
/// </summary>
public sealed class ProjectFileServiceTests : IDisposable
{
    static readonly string Root = RepoRoot.Find(AppContext.BaseDirectory);
    static readonly DateTimeOffset Noon = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    readonly TestFiles _files = new();

    public void Dispose() => _files.Dispose();

    sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    sealed class Rig
    {
        public Rig(EditorSession? session = null, Func<string, CancellationToken, Task<ProjectLoad>>? read = null)
        {
            Session = session ?? new EditorSession();
            Service = new ProjectFileService(Session, Files, Dialogs, Status, Settings, new FixedClock(Noon), read, Released.Add);
        }

        public EditorSession Session { get; }
        public FakeFileDialogs Files { get; } = new();
        public FakeDialogService Dialogs { get; } = new();
        public FakeStatus Status { get; } = new();
        public FakeSettingsStore Settings { get; } = new();
        public List<IReadOnlyCollection<SKImage>> Released { get; } = [];
        public ProjectFileService Service { get; }

        public IEnumerable<SKImage> AllReleased => Released.SelectMany(r => r);
    }

    static SKImage Img(SKColor color, int w = 4, int h = 4)
    {
        using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(color);
        return surface.Snapshot();
    }

    static string Url(SKImage image)
    {
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return "data:image/png;base64," + Convert.ToBase64String(data.AsSpan());
    }

    /// <summary>A copy of a frozen 4.1 fixture (tests/goldens/projects) in the temp folder.</summary>
    string Fixture(string name)
    {
        string path = Path.Combine(_files.Dir, name + ".r15proj");
        File.Copy(Path.Combine(Root, "tests", "goldens", "projects", name + ".r15proj"), path);
        return path;
    }

    string Text(string name, string json) => _files.Write(name, Encoding.UTF8.GetBytes(json));

    /// <summary>A session with one layer added through Apply, so it is dirty and has one step.</summary>
    static EditorSession Dirty(out SKImage image)
    {
        var s = new EditorSession();
        var img = Img(SKColors.Red);
        Assert.True(s.ApplyWithSelection(x => LayerOps.AddImage(x, img, s.TakeId(), "art")));
        image = img;
        return s;
    }

    // ---- Save (saveProject, editor.js:2550-2591) ----

    [Theory] // project-save-filename (keep) parityCheck: 'My Shirt v2!' -> 'My_Shirt_v2_.r15proj'; empty -> 'untitled.r15proj' (editor.js:2586-2587)
    [InlineData("My Shirt v2!", "My_Shirt_v2_.r15proj")]
    [InlineData("", "untitled.r15proj")]
    public void Save_work_with_no_current_file_is_save_as_with_4_1s_name(string title, string suggested)
    {
        var rig = new Rig(Dirty(out _));
        rig.Session.TemplateTitle = title;
        string remembered = Directory.CreateDirectory(Path.Combine(_files.Dir, "remembered")).FullName;
        rig.Settings.Folders[SettingsFolder.Project] = remembered;
        rig.Files.SaveProjectResult = Path.Combine(_files.Dir, suggested);

        Assert.True(rig.Service.Save());

        Assert.Equal([(suggested, (string?)remembered)], rig.Files.SaveProjectCalls);
        Assert.True(File.Exists(rig.Files.SaveProjectResult));
    }

    [Fact] // project-save-format: ProjectIO.Write's bytes, UTF-8 without a BOM, no title (R1); then clean, current file, status, folder
    public void Save_writes_the_project_marks_the_session_saved_and_says_so()
    {
        var rig = new Rig(Dirty(out _));
        rig.Session.TemplateTitle = "Hoodie";
        string path = Path.Combine(_files.Dir, "hoodie.r15proj");
        rig.Files.SaveProjectResult = path;

        Assert.True(rig.Service.SaveAs());

        byte[] bytes = File.ReadAllBytes(path);
        Assert.Equal(Encoding.UTF8.GetBytes(ProjectIO.Write(rig.Session.Document, Noon, title: null)), bytes);
        Assert.NotEqual(0xEF, bytes[0]);                                   // no BOM
        var json = JsonNode.Parse(Encoding.UTF8.GetString(bytes))!.AsObject();
        Assert.Equal("5.0", json["version"]!.GetValue<string>());
        Assert.Equal("2026-10-16T12:00:00.000Z", json["savedAt"]!.GetValue<string>());
        Assert.False(json.ContainsKey("title"));                          // project-template-name-not-saved (keep)
        Assert.Equal(path, rig.Session.CurrentFile);
        Assert.False(rig.Session.IsDirty);
        Assert.Equal(1, rig.Session.UndoCount);                           // saving keeps history
        Assert.Equal("Saved hoodie.r15proj.", rig.Status.Current);        // io-download-save-dialog fix: feedback after saving
        Assert.Equal([(SettingsFolder.Project, _files.Dir)], rig.Settings.FolderSaves);
        Assert.Equal(new[] { "hoodie.r15proj" }, Directory.GetFiles(_files.Dir).Select(Path.GetFileName));   // no temp file left
    }

    [Fact] // spec §3: Ctrl+S saves to the current file with no dialog
    public void Save_work_with_a_current_file_writes_there_without_a_dialog()
    {
        var rig = new Rig(Dirty(out _));
        string path = Path.Combine(_files.Dir, "a.r15proj");
        rig.Files.SaveProjectResult = path;
        Assert.True(rig.Service.Save());
        Assert.True(rig.Session.Apply(d => d with { BgColor = "#123456" }));
        Assert.True(rig.Session.IsDirty);

        Assert.True(rig.Service.Save());

        Assert.Single(rig.Files.SaveProjectCalls);
        Assert.False(rig.Session.IsDirty);
        Assert.Equal("#123456", ProjectIO.Read(File.ReadAllText(path)).Document.BgColor);
    }

    [Fact] // Save As suggests the current file's name in its own folder
    public void Save_as_suggests_the_current_name_in_the_current_folder()
    {
        var rig = new Rig(Dirty(out _));
        string sub = Directory.CreateDirectory(Path.Combine(_files.Dir, "sub")).FullName;
        rig.Files.SaveProjectResult = Path.Combine(sub, "first.r15proj");
        Assert.True(rig.Service.Save());
        rig.Settings.Folders[SettingsFolder.Project] = _files.Dir;            // the current file's folder wins over the remembered one
        rig.Files.SaveProjectResult = Path.Combine(sub, "second.r15proj");

        Assert.True(rig.Service.SaveAs());

        Assert.Equal(("first.r15proj", (string?)sub), rig.Files.SaveProjectCalls[1]);
        Assert.Equal(Path.Combine(sub, "second.r15proj"), rig.Session.CurrentFile);
    }

    [Fact] // io-download-save-dialog parityCheck: cancel produces no file and no error
    public void Cancelling_save_as_writes_nothing_and_stays_dirty()
    {
        var rig = new Rig(Dirty(out _));
        Assert.False(rig.Service.Save());
        Assert.Single(rig.Files.SaveProjectCalls);
        Assert.Empty(Directory.GetFiles(_files.Dir));
        Assert.True(rig.Session.IsDirty);
        Assert.Null(rig.Session.CurrentFile);
        Assert.Empty(rig.Status.Shown);
        Assert.Empty(rig.Settings.FolderSaves);
    }

    [Fact] // Review Focus 2: a locked target. The write is atomic: the old file survives, no temp is left, the error is named, still dirty
    public void A_failed_write_keeps_the_existing_file_and_stays_dirty()
    {
        var rig = new Rig(Dirty(out _));
        string path = _files.Write("locked.r15proj", [1, 2, 3]);
        rig.Files.SaveProjectResult = path;
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.False(rig.Service.Save());
        Assert.StartsWith("Couldn't save locked.r15proj: ", rig.Status.Current);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
        Assert.Equal(new[] { path }, Directory.GetFiles(_files.Dir));
        Assert.True(rig.Session.IsDirty);
        Assert.Null(rig.Session.CurrentFile);
        Assert.Empty(rig.Settings.FolderSaves);
    }

    [Fact] // M3b carry-forward: Save raises Changing before its snapshot, so an open wheel burst closes and its change is saved as one step
    public void Save_closes_a_pending_gesture_before_it_reads_the_document()
    {
        var rig = new Rig();
        IDisposable? burst = rig.Session.BeginGesture();
        Assert.True(rig.Session.Apply(d => d with { BgOpacity = 0.5 }));
        Assert.Equal(0, rig.Session.UndoCount);
        rig.Session.Changing += (_, _) => { var b = burst; burst = null; b?.Dispose(); };   // what CanvasInteraction does for a burst
        rig.Files.SaveProjectResult = Path.Combine(_files.Dir, "w.r15proj");

        Assert.True(rig.Service.Save());

        Assert.False(rig.Session.InGesture);
        Assert.Equal(1, rig.Session.UndoCount);
        Assert.Equal(0.5, ProjectIO.Read(File.ReadAllText(rig.Files.SaveProjectResult)).Document.BgOpacity);
        Assert.False(rig.Session.IsDirty);
    }

    // ---- Open (loadProject, editor.js:2593-2680) ----

    [Fact] // project-load-flow, project-load-history-interplay (fix): a 4.1 file opens all at once, history cleared, clean, nothing selected
    public async Task Open_swaps_in_a_4_1_file_clears_history_and_reads_clean()
    {
        var rig = new Rig(Dirty(out var old));
        rig.Session.TemplateTitle = "Hoodie";
        string path = Fixture("proj-shirt-rich");
        rig.Dialogs.SaveChoiceAnswer = SaveChoice.DontSave;
        var busy = new List<bool>();
        rig.Service.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ProjectFileService.IsBusy)) busy.Add(rig.Service.IsBusy); };

        Assert.True(await rig.Service.OpenAsync(path));

        var expected = ProjectIO.Read(File.ReadAllText(path));
        Assert.Equal(ProjectIO.Write(expected.Document, Noon), ProjectIO.Write(rig.Session.Document, Noon));
        Assert.Equal(new[] { "L1", "L2", "L3", "L4" }, rig.Session.Document.Layers.Select(l => l.Name));
        Assert.Equal(expected.NextId, rig.Session.NextId);
        Assert.Equal(rig.Session.Document.Layers[^1].Id, rig.Session.SelectedLayerId);   // T1-R2
        Assert.Equal((0, 0), (rig.Session.UndoCount, rig.Session.RedoCount));
        Assert.False(rig.Session.Undo());                                 // project-load-history-interplay parityCheck: Ctrl+Z after Open does nothing
        Assert.Equal(path, rig.Session.CurrentFile);
        Assert.False(rig.Session.IsDirty);
        Assert.Equal("Hoodie", rig.Session.TemplateTitle);                // R1
        Assert.Equal(new[] { true, false }, busy);                        // R4: busy only while the read runs
        Assert.Equal((1, (string?)null), (rig.Status.Clears, rig.Status.Current));
        Assert.Equal([(SettingsFolder.Project, _files.Dir)], rig.Settings.FolderSaves);
        Assert.Equal(new[] { old }, rig.AllReleased);                     // R11: the outgoing document's image
    }

    [Fact] // project-load-no-imgsrc-hang (fix): records without imgSrc are named, and the document still swaps and refreshes
    public async Task A_file_whose_layers_all_lack_images_opens_empty_and_names_them()
    {
        var rig = new Rig();
        string path = Text("bare.r15proj", """{ "mode": "Pants", "layers": [ { "id": 1, "name": "A" }, { "id": 2, "name": "B" } ] }""");
        Assert.True(await rig.Service.OpenAsync(path));
        Assert.Empty(rig.Session.Document.Layers);
        Assert.Equal(TemplateMode.Pants, rig.Session.Document.Mode);
        Assert.Equal("Opened bare.r15proj. Couldn't load: A, B.", rig.Status.Current);
    }

    [Fact] // project-load-error-path-bugs, project-load-layers (fix): a corrupt last image keeps the file order and ids, and is named
    public async Task Dropped_layers_are_named_on_the_status_strip_and_the_rest_keep_file_order()
    {
        var rig = new Rig();
        using var red = Img(SKColors.Red);
        string json = $$"""
            { "layers": [
              { "id": 5, "name": "Good", "imgSrc": "{{Url(red)}}" },
              { "id": 2, "name": "Also good", "imgSrc": "{{Url(red)}}" },
              { "id": 9, "name": "Broken", "imgSrc": "data:image/png;base64,AAAA" } ] }
            """;
        string path = Text("mixed.r15proj", json);
        Assert.True(await rig.Service.OpenAsync(path));
        Assert.Equal(new[] { 5, 2 }, rig.Session.Document.Layers.Select(l => l.Id));
        Assert.Equal(6, rig.Session.NextId);
        Assert.Equal("Opened mixed.r15proj. Couldn't load: Broken.", rig.Status.Current);
    }

    [Fact] // contract (R12): out-of-range stored values load raw, as 4.1 kept them; nothing is normalized
    public async Task Out_of_range_values_load_raw()
    {
        var rig = new Rig();
        using var red = Img(SKColors.Red);
        string path = Text("wild.r15proj", $$"""
            { "layers": [ { "id": 1, "imgSrc": "{{Url(red)}}", "rotation": 720, "scale": 900, "opacity": 3,
                            "brightness": 999, "contrast": -50, "saturate": 1000, "hueRotate": -720, "rShift": 400 } ] }
            """);
        Assert.True(await rig.Service.OpenAsync(path));
        var l = Assert.Single(rig.Session.Document.Layers);
        Assert.Equal((720.0, 900.0, 3.0, 999.0, -50.0, 1000.0, -720.0, 400), (l.Rotation, l.Scale, l.Opacity, l.Brightness, l.Contrast, l.Saturate, l.HueRotate, l.RShift));
    }

    public static TheoryData<string, string> BadFiles => new()
    {
        { "[]", "Failed to load project. the file is a JSON array, not a project object" },
        { """{ "mode": "Hoodie" }""", "Failed to load project. unknown mode \"Hoodie\" (expected \"Shirt\" or \"Pants\")" },
        { """{ "bgImageSrc": "http://example.com/a.png" }""", "Failed to load project. bgImageSrc must be a data: URL, not \"http://example.com/a.png\"" },
        { """{ "layers": [ { "id": 0 } ] }""", "Failed to load project. layers[0].id must be a positive whole number" },
    };

    [Theory, MemberData(nameof(BadFiles))] // Review Focus 1; project-load-untrusted-src, project-load-flow: 4.1's alert text, all or nothing, no prompt
    public async Task A_file_that_cannot_open_alerts_4_1s_text_and_changes_nothing(string json, string alert)
    {
        var rig = new Rig(Dirty(out _));
        var before = rig.Session.Document;
        string path = Text("bad.r15proj", json);

        Assert.False(await rig.Service.OpenAsync(path));

        Assert.Equal(new[] { alert }, rig.Dialogs.Alerts);
        Assert.Empty(rig.Dialogs.SaveChanges);                            // R4: nobody is asked to save for a file that cannot open
        Assert.Same(before, rig.Session.Document);
        Assert.Equal(1, rig.Session.UndoCount);
        Assert.Null(rig.Session.CurrentFile);
        Assert.True(rig.Session.IsDirty);
        Assert.False(rig.Service.IsBusy);
        Assert.Empty(rig.Released);
        Assert.Empty(rig.Settings.FolderSaves);
    }

    [Fact] // Review Focus 1: text that is not JSON, and a file that is gone, alert with the reader's own message
    public async Task Not_json_and_a_missing_file_alert_too()
    {
        var rig = new Rig();
        Assert.False(await rig.Service.OpenAsync(Text("trunc.r15proj", "{ \"layers\": [")));
        Assert.StartsWith("Failed to load project. the file is not JSON: ", Assert.Single(rig.Dialogs.Alerts));
        string gone = Path.Combine(_files.Dir, "gone.r15proj");
        Assert.False(await rig.Service.OpenAsync(gone));
        Assert.Equal($"Failed to load project. Could not find file '{gone}'.", rig.Dialogs.Alerts[1]);
        Assert.Null(rig.Session.CurrentFile);
    }

    [Fact] // critic M1, T1-R14 (project-load-untrusted-src): a file over the size limit is refused before it is read, with 4.1's alert prefix, and nothing changes
    public async Task A_file_over_the_size_limit_alerts_and_changes_nothing()
    {
        Assert.Equal(512L * 1024 * 1024, ProjectFileService.MaxProjectBytes);
        var rig = new Rig(Dirty(out _), read: (p, c) => Task.Run(() => ProjectFileService.ReadFile(p, maxBytes: 1024 * 1024), c));
        var before = rig.Session.Document;
        string path = Text("big.r15proj", "{ \"layers\": [] }" + new string(' ', 1536 * 1024 - 16));   // 1.5 MiB of valid JSON

        Assert.False(await rig.Service.OpenAsync(path));

        Assert.Equal(new[] { "Failed to load project. the file is 1.5 MB; the limit is 1 MB" }, rig.Dialogs.Alerts);
        Assert.Empty(rig.Dialogs.SaveChanges);
        Assert.Same(before, rig.Session.Document);
        Assert.True(rig.Session.IsDirty);
        Assert.Null(rig.Session.CurrentFile);
        Assert.Empty(rig.Released);
    }

    [Fact] // R4: the read is awaited off the caller, the service is busy until it lands, and a second open is refused meanwhile
    public async Task While_the_read_runs_the_service_is_busy_and_refuses_a_second_open()
    {
        var pending = new TaskCompletionSource<ProjectLoad>();
        var rig = new Rig(read: (_, _) => pending.Task);
        var open = rig.Service.OpenAsync("C:\\p\\slow.r15proj");
        Assert.False(open.IsCompleted);
        Assert.True(rig.Service.IsBusy);
        Assert.False(await rig.Service.OpenAsync("C:\\p\\other.r15proj"));
        rig.Files.OpenProjectResult = "C:\\p\\third.r15proj";
        Assert.False(await rig.Service.OpenAsync());
        Assert.Empty(rig.Files.OpenProjectDirs);                          // no dialog while busy

        pending.SetResult(new ProjectLoad(new Document { BgColor = "#010203" }, 1, []));
        Assert.True(await open);
        Assert.False(rig.Service.IsBusy);
        Assert.Equal("#010203", rig.Session.Document.BgColor);
    }

    [Fact] // R4/R11: closing the window cancels a pending read; its result is released, never applied
    public async Task A_cancelled_read_is_released_and_never_applied()
    {
        var pending = new TaskCompletionSource<ProjectLoad>();
        var rig = new Rig(read: (_, _) => pending.Task);
        var open = rig.Service.OpenAsync("C:\\p\\slow.r15proj");
        rig.Service.CancelPendingOpen();
        var img = Img(SKColors.Blue);
        pending.SetResult(new ProjectLoad(new Document { Layers = [new Layer { Id = 1, Image = img }] }, 2, []));

        Assert.False(await open);
        Assert.Empty(rig.Session.Document.Layers);
        Assert.Null(rig.Session.CurrentFile);
        Assert.Equal(new[] { img }, rig.AllReleased);
        Assert.Empty(rig.Dialogs.Alerts);
        rig.Service.CancelPendingOpen();                                  // nothing pending: a no-op
    }

    [Fact] // R13: the guard asks only after a good read; Cancel keeps everything and releases what was loaded
    public async Task Cancel_in_the_guard_keeps_the_document_and_releases_the_load()
    {
        var rig = new Rig(Dirty(out _));
        var before = rig.Session.Document;
        string path = Fixture("proj-shirt-rich");

        Assert.False(await rig.Service.OpenAsync(path));

        Assert.Equal(new[] { "Untitled" }, rig.Dialogs.SaveChanges);
        Assert.Same(before, rig.Session.Document);
        Assert.Equal(1, rig.Session.UndoCount);
        Assert.Equal(4, rig.AllReleased.Count());                         // the four decoded fixture layers
        Assert.DoesNotContain(rig.AllReleased, i => before.Layers.Any(l => ReferenceEquals(l.Image, i)));
    }

    [Fact] // R13: Save in the guard saves first; a cancelled Save As cancels the whole open
    public async Task Save_in_the_guard_saves_first_and_a_cancelled_save_as_cancels_the_open()
    {
        var rig = new Rig(Dirty(out _));
        rig.Dialogs.SaveChoiceAnswer = SaveChoice.Save;
        string path = Fixture("proj-shirt-rich");

        Assert.False(await rig.Service.OpenAsync(path));                  // Save As cancelled
        Assert.Single(rig.Files.SaveProjectCalls);
        Assert.Null(rig.Session.CurrentFile);
        Assert.True(rig.Session.IsDirty);

        string saved = Path.Combine(_files.Dir, "work.r15proj");
        rig.Files.SaveProjectResult = saved;
        Assert.True(await rig.Service.OpenAsync(path));
        Assert.Single(ProjectIO.Read(File.ReadAllText(saved)).Document.Layers);   // the work was saved before the swap
        Assert.Equal(path, rig.Session.CurrentFile);
        Assert.Equal(new[] { "Untitled", "Untitled" }, rig.Dialogs.SaveChanges);
    }

    [Fact] // shell-no-unsaved-changes-guard: never on a pristine, just-saved, just-opened or undone-to-clean document
    public async Task The_guard_never_asks_for_a_clean_document()
    {
        var rig = new Rig();
        Assert.True(rig.Service.ConfirmDiscardChanges());                 // pristine
        Assert.True(rig.Session.Apply(d => d with { ShowGrid = true }));
        Assert.True(rig.Session.Undo());
        Assert.True(rig.Service.ConfirmDiscardChanges());                 // undone back to clean (R2)
        Assert.True(await rig.Service.OpenAsync(Fixture("proj-pants-legacy")));
        Assert.True(rig.Service.ConfirmDiscardChanges());                 // just opened
        Assert.True(rig.Session.Apply(d => d with { ShowGrid = !d.ShowGrid }));
        Assert.True(rig.Service.Save());                                  // to the current file
        Assert.True(rig.Service.ConfirmDiscardChanges());                 // just saved
        Assert.Empty(rig.Dialogs.SaveChanges);

        Assert.True(rig.Session.Apply(d => d with { ShowGrid = !d.ShowGrid }));
        rig.Dialogs.SaveChoiceAnswer = SaveChoice.DontSave;
        Assert.True(rig.Service.ConfirmDiscardChanges());
        Assert.Equal(new[] { "proj-pants-legacy.r15proj" }, rig.Dialogs.SaveChanges);   // named by its file
        rig.Dialogs.SaveChoiceAnswer = SaveChoice.Cancel;
        Assert.False(rig.Service.ConfirmDiscardChanges());
    }

    [Fact] // the Open dialog starts in the remembered folder; a cancel does nothing
    public async Task Open_with_no_pick_does_nothing()
    {
        var rig = new Rig(Dirty(out _));
        rig.Settings.Folders[SettingsFolder.Project] = _files.Dir;
        Assert.False(await rig.Service.OpenAsync());
        Assert.Equal(new[] { (string?)_files.Dir }, rig.Files.OpenProjectDirs);
        Assert.Empty(rig.Dialogs.SaveChanges);
        Assert.True(rig.Session.IsDirty);
    }

    [Fact] // file-project-format parityCheck: a round trip save -> load is stable
    public async Task A_round_trip_through_save_and_open_is_stable()
    {
        var rig = new Rig();
        Assert.True(await rig.Service.OpenAsync(Fixture("proj-shirt-rich")));
        string again = Path.Combine(_files.Dir, "again.r15proj");
        rig.Files.SaveProjectResult = again;
        Assert.True(rig.Service.SaveAs());
        string first = ProjectIO.Write(rig.Session.Document, Noon);

        Assert.True(await rig.Service.OpenAsync(again));

        Assert.Equal(first, ProjectIO.Write(rig.Session.Document, Noon));
        Assert.Equal(again, rig.Session.CurrentFile);
    }

    [Fact] // R11's default: the outgoing images are freed at ApplicationIdle, after the new document has rendered; kept ones are not
    public void The_default_release_frees_outgoing_images_at_application_idle() => StaThread.Run(() =>
    {
        var old = Img(SKColors.Red);
        var shared = Img(SKColors.Green);
        var session = new EditorSession(new Document { Layers = [new Layer { Id = 1, Image = old }, new Layer { Id = 2, Image = shared }] });
        var load = new ProjectLoad(new Document { Layers = [new Layer { Id = 1, Image = shared }] }, 2, []);
        var service = new ProjectFileService(session, new FakeFileDialogs(), new FakeDialogService(), new FakeStatus(), new FakeSettingsStore(),
            read: (_, _) => Task.FromResult(load));

        Assert.True(service.OpenAsync("C:\\p\\x.r15proj").GetAwaiter().GetResult());
        Assert.NotEqual(IntPtr.Zero, old.Handle);                         // not yet: the frame for the new document comes first
        Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
        Assert.Equal(IntPtr.Zero, old.Handle);
        Assert.NotEqual(IntPtr.Zero, shared.Handle);
    });

    // ---- names and strings ----

    [Fact] // dialogs-inventory, io-alert-inventory: 4.1's alert strings verbatim (editor.js:2344, :2364, :2385, :2676)
    public void Dialog_text_is_4_1s_verbatim()
    {
        string js = File.ReadAllText(Path.Combine(Root, "editor.js"));
        Assert.Contains("alert('Built-in pack \"' + packName + '\" not found. Reinstall the app to restore it.');", js);
        Assert.Contains("alert('Built-in pack \"' + packName + '\" is empty.');", js);
        Assert.Contains("alert('" + DialogText.NoImagesInFolder + "');", js);
        Assert.Contains("alert('Failed to load project. ' + err.message);", js);
        Assert.Equal("Built-in pack \"drips\" not found. Reinstall the app to restore it.", DialogText.PackNotFound("drips"));
        Assert.Equal("Built-in pack \"word_art\" is empty.", DialogText.PackEmpty("word_art"));
        Assert.Equal("Failed to load project. boom", DialogText.LoadFailed("boom"));
        Assert.Equal("Do you want to save changes to a.r15proj?", DialogText.SaveChanges("a.r15proj"));
        Assert.Equal("Untitled", DialogText.Untitled);
    }

    [Fact] // interfaces §2.2: .r15proj first, JSON keeps 4.1's reach (accept=".json,.r15proj", editor.html:108); Save writes .r15proj only
    public void The_dialog_filters_keep_4_1s_reach()
    {
        Assert.Contains("<input type=\"file\" id=\"projectFileInput\" accept=\".json,.r15proj\" hidden>", File.ReadAllText(Path.Combine(Root, "editor.html")));
        Assert.Equal("RBX15 project (*.r15proj)|*.r15proj|JSON project (*.json)|*.json", Win32FileDialogs.ProjectOpenFilter);
        Assert.Equal("RBX15 project (*.r15proj)|*.r15proj", Win32FileDialogs.ProjectSaveFilter);
    }

    [Theory]
    [InlineData(null, "Untitled")]
    [InlineData("", "Untitled")]
    [InlineData("C:\\work\\my shirt.r15proj", "my shirt.r15proj")]
    public void Document_name_is_the_file_name_or_untitled(string? file, string expected) =>
        Assert.Equal(expected, ProjectFileService.DocumentName(file));
}
