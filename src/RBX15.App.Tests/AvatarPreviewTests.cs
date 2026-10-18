using System.Collections.Immutable;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using RBX15.App.ViewModels;
using RBX15.App.Views;
using RBX15.Engine;

namespace RBX15.App.Tests;

/// <summary>
/// The 3D preview (v5.1 spec, App and Testing > App; ledger avatar-preview-3d, new). The view-model is checked for its
/// coalescing, its hidden state, the turn methods and the idle spin (with a fake clock). The view is rendered through
/// <see cref="RenderTargetBitmap"/> at a fixed size and read at the projection of a 3D point worked out from the
/// camera and the model transform, never at a hard-coded pixel, so no result depends on the display scale.
/// </summary>
public class AvatarPreviewTests
{
    const int PanelWidth = 260, PanelHeight = 360;

    // ---- builders ----

    static Document DocWith(TemplateMode mode, params (string Region, string Color)[] fills) => new()
    {
        Mode = mode,
        BgOpacity = 0,   // the default navy background would cover every region
        RegionFills = fills.Select(f => (f.Region, Fill.Solid(f.Color))).ToImmutableList(),
    };

    /// <summary>A change that always differs in content, so each call is a real document change.</summary>
    static void Touch(EditorSession session, int n) => session.Apply(d => d with { BgOpacity = 0.01 * (n + 1) });

    sealed class Clock
    {
        public TimeSpan Now;
        public bool Animations = true;
    }

    static AvatarPreviewViewModel Vm(EditorSession session, Clock? clock = null)
    {
        clock ??= new Clock();
        return new AvatarPreviewViewModel(session, () => clock.Animations, () => clock.Now);
    }

    static (AvatarPreviewViewModel Vm, AvatarPreviewPanel View) Panel(Document doc)
    {
        var vm = Vm(new EditorSession(doc));
        var view = new AvatarPreviewPanel { DataContext = vm };
        Layout(view);
        return (vm, view);
    }

    static void Layout(FrameworkElement view)
    {
        StaThread.Pump();
        view.Measure(new Size(PanelWidth, PanelHeight));
        view.Arrange(new Rect(0, 0, PanelWidth, PanelHeight));
        view.UpdateLayout();
    }

    static string[] DressedParts(AvatarPreviewViewModel vm) =>
        vm.Boxes.Where(b => b.Dressed).Select(b => b.Part.ToString()).Order().ToArray();

    // ---- rendering helpers ----

    /// <summary>
    /// Where <paramref name="p"/> (studs, before the yaw / pitch) lands in the panel, in DIPs: the model transform, then the
    /// camera's perspective (WPF's FieldOfView is horizontal, so the vertical scale is the viewport's aspect).
    /// </summary>
    static Point Project(AvatarPreviewPanel view, Point3D p)
    {
        var camera = view.CameraElement;
        var world = view.BodyGroup.Transform.Value.Transform(p);
        double depth = camera.Position.Z - world.Z;   // the camera looks straight down -Z
        double half = depth * Math.Tan(camera.FieldOfView * Math.PI / 360);
        double w = view.ViewportElement.ActualWidth, h = view.ViewportElement.ActualHeight;
        double nx = (world.X - camera.Position.X) / half;
        double ny = (world.Y - camera.Position.Y) / half * (w / h);
        var inViewport = new Point((nx + 1) / 2 * w, (1 - ny) / 2 * h);
        return view.ViewportElement.TransformToAncestor(view).Transform(inViewport);
    }

    static RenderTargetBitmap Render(AvatarPreviewPanel view)
    {
        Layout(view);
        var bitmap = new RenderTargetBitmap(PanelWidth, PanelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        return bitmap;
    }

    /// <summary>The average color of the 5 x 5 patch around <paramref name="at"/>.</summary>
    static (double R, double G, double B) Patch(BitmapSource image, Point at)
    {
        int x0 = (int)Math.Round(at.X) - 2, y0 = (int)Math.Round(at.Y) - 2;
        var px = new byte[5 * 5 * 4];
        image.CopyPixels(new Int32Rect(x0, y0, 5, 5), px, 5 * 4, 0);
        double b = 0, g = 0, r = 0;
        for (int i = 0; i < 25; i++) { b += px[i * 4]; g += px[i * 4 + 1]; r += px[i * 4 + 2]; }
        return (r / 25, g / 25, b / 25);
    }

    static (double R, double G, double B) ColorAt(AvatarPreviewPanel view, BitmapSource image, double x, double y, double z) =>
        Patch(image, Project(view, new Point3D(x, y, z)));

    const double Margin = 60;   // "clearly dominant" on a 0..255 channel

    static void AssertRed((double R, double G, double B) c) { Assert.True(c.R > c.G + Margin && c.R > c.B + Margin, $"not red-dominant: {c}"); }
    static void AssertGreen((double R, double G, double B) c) { Assert.True(c.G > c.R + Margin && c.G > c.B + Margin, $"not green-dominant: {c}"); }
    static void AssertBlue((double R, double G, double B) c) { Assert.True(c.B > c.R + Margin && c.B > c.G + Margin, $"not blue-dominant: {c}"); }
    static void AssertGrey((double R, double G, double B) c)
    {
        Assert.True(Math.Abs(c.R - c.G) < 20 && Math.Abs(c.G - c.B) < 20 && c.R > 60, $"not body-color grey: {c}");
    }

    // ---- done-when 1: coalescing, mode, hidden ----

    [Fact]
    public void Many_document_changes_in_one_turn_cost_one_texture_rebuild() => StaThread.Run(() =>
    {
        var session = new EditorSession(DocWith(TemplateMode.Shirt));
        var vm = Vm(session);
        int before = vm.TextureBuilds;
        Assert.NotNull(vm.Texture);
        Assert.True(vm.Texture!.IsFrozen);
        for (int i = 0; i < 7; i++) Touch(session, i);
        Assert.Equal(before, vm.TextureBuilds);   // nothing rebuilds inside the turn
        StaThread.Pump();
        Assert.Equal(before + 1, vm.TextureBuilds);
        StaThread.Pump();
        Assert.Equal(before + 1, vm.TextureBuilds);   // and nothing is left queued
    });

    [Fact]
    public void A_selection_change_does_not_rebuild() => StaThread.Run(() =>
    {
        var session = new EditorSession(DocWith(TemplateMode.Shirt));
        var vm = Vm(session);
        int before = vm.TextureBuilds;
        session.Select(null);
        session.TemplateTitle = "x";
        StaThread.Pump();
        Assert.Equal(before, vm.TextureBuilds);
    });

    [Fact]
    public void A_mode_change_rebuilds_the_boxes_from_arms_to_legs_and_back() => StaThread.Run(() =>
    {
        var session = new EditorSession(DocWith(TemplateMode.Shirt));
        var vm = Vm(session);
        Assert.Equal(["LeftArm", "RightArm", "Torso"], DressedParts(vm));
        var shirtBoxes = vm.Boxes;

        session.Apply(d => d with { Mode = TemplateMode.Pants });
        StaThread.Pump();
        Assert.Equal(TemplateMode.Pants, vm.Mode);
        Assert.NotSame(shirtBoxes, vm.Boxes);
        Assert.Equal(["LeftLeg", "RightLeg", "Torso"], DressedParts(vm));

        session.Apply(d => d with { Mode = TemplateMode.Shirt });
        StaThread.Pump();
        Assert.Equal(["LeftArm", "RightArm", "Torso"], DressedParts(vm));
    });

    [Fact]
    public void Nothing_rebuilds_while_hidden_and_showing_rebuilds_once() => StaThread.Run(() =>
    {
        var session = new EditorSession(DocWith(TemplateMode.Shirt));
        var vm = Vm(session);
        int before = vm.TextureBuilds;
        vm.IsVisible = false;
        for (int i = 0; i < 4; i++) Touch(session, i);
        session.Apply(d => d with { Mode = TemplateMode.Pants });
        StaThread.Pump();
        Assert.Equal(before, vm.TextureBuilds);
        Assert.Equal(TemplateMode.Shirt, vm.Mode);   // the boxes wait too

        vm.IsVisible = true;
        StaThread.Pump();
        Assert.Equal(before + 1, vm.TextureBuilds);
        Assert.Equal(TemplateMode.Pants, vm.Mode);
        StaThread.Pump();
        Assert.Equal(before + 1, vm.TextureBuilds);
    });

    [Fact]
    public void Showing_a_panel_whose_document_did_not_move_does_not_rebuild() => StaThread.Run(() =>
    {
        var vm = Vm(new EditorSession(DocWith(TemplateMode.Shirt)));
        int before = vm.TextureBuilds;
        vm.IsVisible = false;
        vm.IsVisible = true;
        StaThread.Pump();
        Assert.Equal(before, vm.TextureBuilds);
    });

    [Fact]
    public void A_change_queued_while_visible_and_hidden_before_it_runs_does_not_rebuild() => StaThread.Run(() =>
    {
        var session = new EditorSession(DocWith(TemplateMode.Shirt));
        var vm = Vm(session);
        int before = vm.TextureBuilds;
        Touch(session, 0);
        vm.IsVisible = false;
        StaThread.Pump();
        Assert.Equal(before, vm.TextureBuilds);
    });

    // ---- done-when 2 and 3: the rendered result ----

    [Fact]
    public void Torso_front_red_reads_red_at_yaw_0_and_torso_back_blue_reads_blue_at_yaw_180() => StaThread.Run(() =>
    {
        var (vm, view) = Panel(DocWith(TemplateMode.Shirt, ("torso_front", "#ff0000"), ("torso_back", "#0000ff")));

        var front = Render(view);
        AssertRed(ColorAt(view, front, 0, 3, 0.5));

        vm.Yaw = 180;
        var back = Render(view);
        AssertBlue(ColorAt(view, back, 0, 3, -0.5));
    });

    [Fact]
    public void Shirt_mode_dresses_the_arms_and_the_right_arm_is_on_the_viewers_left() => StaThread.Run(() =>
    {
        var (_, view) = Panel(DocWith(TemplateMode.Shirt, ("rarm_front", "#00ff00")));
        var image = Render(view);

        var arm = Project(view, new Point3D(-1.5, 3, 0.5));
        var torso = Project(view, new Point3D(0, 3, 0.5));
        Assert.True(arm.X < torso.X, "the character's right arm should be left of the torso on screen");
        AssertGreen(Patch(image, arm));
        AssertGrey(Patch(image, torso));   // nothing filled on the torso: body color
    });

    [Fact]
    public void Pants_mode_dresses_the_legs_and_leaves_the_arms_body_color() => StaThread.Run(() =>
    {
        var (_, view) = Panel(DocWith(TemplateMode.Pants, ("rleg_front", "#00ff00")));
        var image = Render(view);

        AssertGreen(ColorAt(view, image, -0.5, 1, 0.5));   // right leg front
        AssertGrey(ColorAt(view, image, -1.5, 3, 0.5));    // right arm front
    });

    [Fact]
    public void The_texture_follows_a_document_change_in_the_rendered_panel() => StaThread.Run(() =>
    {
        var session = new EditorSession(DocWith(TemplateMode.Shirt));
        var vm = Vm(session);
        var view = new AvatarPreviewPanel { DataContext = vm };
        Layout(view);
        AssertGrey(ColorAt(view, Render(view), 0, 3, 0.5));

        session.Apply(d => d with { RegionFills = ImmutableList.Create(("torso_front", Fill.Solid("#ff0000"))) });
        StaThread.Pump();
        AssertRed(ColorAt(view, Render(view), 0, 3, 0.5));
    });

    // ---- done-when 4: turning ----

    [Fact]
    public void Drag_turns_yaw_and_pitch_half_a_degree_per_dip_and_pitch_clamps() => StaThread.Run(() =>
    {
        var vm = Vm(new EditorSession(DocWith(TemplateMode.Shirt)));
        vm.BeginDrag();
        vm.DragTo(40, 10);
        Assert.Equal(20, vm.Yaw, 6);
        Assert.Equal(5, vm.Pitch, 6);
        vm.DragTo(-100, 0);
        Assert.Equal(-30, vm.Yaw, 6);
        vm.DragTo(0, 1000);
        Assert.Equal(30, vm.Pitch, 6);
        vm.DragTo(0, -5000);
        Assert.Equal(-30, vm.Pitch, 6);
        vm.EndDrag();
    });

    [Fact]
    public void Yaw_wraps_so_a_long_spin_stays_in_range() => StaThread.Run(() =>
    {
        var vm = Vm(new EditorSession(DocWith(TemplateMode.Shirt)));
        vm.Yaw = 350;
        Assert.Equal(-10, vm.Yaw, 6);
        vm.Yaw = 180;
        Assert.Equal(180, vm.Yaw, 6);
        vm.Yaw = -540;
        Assert.Equal(180, vm.Yaw, 6);
    });

    [Fact]
    public void Arrow_keys_turn_15_degrees_and_Home_returns_to_the_front_through_the_view() => StaThread.Run(() =>
    {
        var (vm, view) = Panel(DocWith(TemplateMode.Shirt));
        using var source = new HwndSource(new HwndSourceParameters("avatar-preview-test"));

        void Press(Key key)
        {
            var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.KeyDownEvent };
            view.ViewportElement.RaiseEvent(args);
        }

        Press(Key.Right);
        Assert.Equal(15, vm.Yaw, 6);
        Press(Key.Right);
        Assert.Equal(30, vm.Yaw, 6);
        Press(Key.Left);
        Press(Key.Left);
        Press(Key.Left);
        Assert.Equal(-15, vm.Yaw, 6);
        vm.DragTo(0, 20);
        Assert.NotEqual(0, vm.Pitch);
        Press(Key.Home);
        Assert.Equal(0, vm.Yaw, 6);
        Assert.Equal(0, vm.Pitch, 6);
    });

    [Fact]
    public void Other_keys_are_left_alone() => StaThread.Run(() =>
    {
        var (_, view) = Panel(DocWith(TemplateMode.Shirt));
        Assert.False(view.HandleKey(Key.A));
        Assert.True(view.HandleKey(Key.Left));
    });

    [Fact]
    public void ResetView_is_the_double_click_return_to_the_front() => StaThread.Run(() =>
    {
        var vm = Vm(new EditorSession(DocWith(TemplateMode.Shirt)));
        vm.Yaw = 123;
        vm.Pitch = -20;
        vm.ResetView();
        Assert.Equal(0, vm.Yaw);
        Assert.Equal(0, vm.Pitch);
    });

    [Fact]
    public void The_viewport_is_focusable_and_named() => StaThread.Run(() =>
    {
        var (_, view) = Panel(DocWith(TemplateMode.Shirt));
        Assert.True(view.ViewportElement.Focusable);
        Assert.Equal("3D preview", System.Windows.Automation.AutomationProperties.GetName(view.ViewportElement));
    });

    // ---- done-when 5: the idle spin ----

    [Fact]
    public void Advance_spins_20_degrees_a_second() => StaThread.Run(() =>
    {
        var vm = Vm(new EditorSession(DocWith(TemplateMode.Shirt)));
        vm.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(20, vm.Yaw, 6);
        vm.Advance(TimeSpan.FromMilliseconds(500));
        Assert.Equal(30, vm.Yaw, 6);
    });

    [Fact]
    public void Advance_waits_while_dragging_and_for_2_seconds_after_release() => StaThread.Run(() =>
    {
        var clock = new Clock { Now = TimeSpan.FromSeconds(10) };
        var vm = Vm(new EditorSession(DocWith(TemplateMode.Shirt)), clock);

        vm.BeginDrag();
        clock.Now = TimeSpan.FromSeconds(30);
        vm.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(0, vm.Yaw);   // dragging, however long

        vm.EndDrag();              // released at t = 30
        clock.Now = TimeSpan.FromSeconds(31.9);
        vm.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(0, vm.Yaw);

        clock.Now = TimeSpan.FromSeconds(32);
        vm.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(20, vm.Yaw, 6);
    });

    [Fact]
    public void A_key_press_pauses_the_spin_for_2_seconds_too() => StaThread.Run(() =>
    {
        var clock = new Clock { Now = TimeSpan.FromSeconds(5) };
        var vm = Vm(new EditorSession(DocWith(TemplateMode.Shirt)), clock);
        vm.TurnBy(15);
        clock.Now = TimeSpan.FromSeconds(6);
        vm.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(15, vm.Yaw, 6);
        clock.Now = TimeSpan.FromSeconds(7);
        vm.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(35, vm.Yaw, 6);
    });

    [Fact]
    public void Advance_does_nothing_while_hidden_or_with_animations_off() => StaThread.Run(() =>
    {
        var clock = new Clock();
        var vm = Vm(new EditorSession(DocWith(TemplateMode.Shirt)), clock);

        vm.IsVisible = false;
        vm.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(0, vm.Yaw);
        vm.IsVisible = true;

        clock.Animations = false;
        vm.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(0, vm.Yaw);

        clock.Animations = true;
        vm.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(20, vm.Yaw, 6);
    });

    [Fact]
    public void The_spin_turns_the_rendered_avatar() => StaThread.Run(() =>
    {
        var (vm, view) = Panel(DocWith(TemplateMode.Shirt, ("torso_front", "#ff0000"), ("torso_back", "#0000ff")));
        vm.Advance(TimeSpan.FromSeconds(9));   // 180 degrees
        Assert.Equal(180, vm.Yaw, 6);
        AssertBlue(ColorAt(view, Render(view), 0, 3, -0.5));
    });
}
