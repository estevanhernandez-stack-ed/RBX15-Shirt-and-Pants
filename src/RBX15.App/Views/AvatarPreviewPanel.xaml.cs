using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using RBX15.App.ViewModels;
using RBX15.Engine;

namespace RBX15.App.Views;

/// <summary>
/// The 3D preview panel (v5.1 spec, App). One <see cref="GeometryModel3D"/> per face: four positions and two triangles
/// (TL, BL, BR and TL, BR, TR, counter-clockwise from outside, so each normal points out), texture coordinates the
/// template-pixel UVs over 585 x 559. A dressed box takes an <see cref="ImageBrush"/> over the whole texture with
/// <c>ViewportUnits=Absolute, Viewport=0,0,1,1</c>: left to the default, WPF maps the brush to the mesh's own UV
/// bounding box and the texture lands wrong. An undressed box takes the body color. Rotation is the view-model's yaw
/// then pitch, through the body's middle. Mouse and keys only call the view-model's turn methods.
/// </summary>
public partial class AvatarPreviewPanel : UserControl
{
    static readonly Color BodyColor = Color.FromRgb(Avatar.BodyColor.Red, Avatar.BodyColor.Green, Avatar.BodyColor.Blue);
    static readonly TimeSpan MaxFrame = TimeSpan.FromMilliseconds(100);

    AvatarPreviewViewModel? _vm;
    Window? _window;
    Point _dragFrom;
    TimeSpan? _lastFrame;
    bool _rendering;

    public AvatarPreviewPanel()
    {
        InitializeComponent();
        DataContextChanged += (_, e) => Attach(e.NewValue as AvatarPreviewViewModel);
        Loaded += (_, _) =>
        {
            _window = Window.GetWindow(this);
            if (_window is not null) _window.StateChanged += OnWindowStateChanged;
            UpdateRendering();
        };
        Unloaded += (_, _) =>
        {
            if (_window is not null) _window.StateChanged -= OnWindowStateChanged;
            _window = null;
            UpdateRendering();
        };
        IsVisibleChanged += (_, _) => UpdateRendering();
        DragSurface.MouseLeftButtonDown += OnMouseDown;
        DragSurface.MouseMove += OnMouseMove;
        DragSurface.MouseLeftButtonUp += OnMouseUp;
        DragSurface.LostMouseCapture += (_, _) => _vm?.EndDrag();
        Viewport.KeyDown += (_, e) => e.Handled = HandleKey(e.Key);
    }

    /// <summary>The viewport, for the render tests.</summary>
    internal Viewport3D ViewportElement => Viewport;

    /// <summary>The group the yaw and pitch turn, for the render tests.</summary>
    internal Model3DGroup BodyGroup => Body;

    internal PerspectiveCamera CameraElement => Camera;

    /// <summary>Left / Right turn 15 degrees, Home returns to the front (spec, Panel). Returns whether the key was taken.</summary>
    internal bool HandleKey(Key key)
    {
        if (_vm is null) return false;
        switch (key)
        {
            case Key.Left: _vm.TurnBy(-AvatarPreviewViewModel.KeyTurnDegrees); return true;
            case Key.Right: _vm.TurnBy(AvatarPreviewViewModel.KeyTurnDegrees); return true;
            case Key.Home: _vm.ResetView(); return true;
            default: return false;
        }
    }

    void Attach(AvatarPreviewViewModel? vm)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
        _vm = vm;
        if (vm is not null) vm.PropertyChanged += OnVmChanged;
        SyncRotation();
        RebuildModels();
        UpdateRendering();
    }

    void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AvatarPreviewViewModel.Yaw):
            case nameof(AvatarPreviewViewModel.Pitch):
                SyncRotation();
                break;
            case nameof(AvatarPreviewViewModel.Boxes):
            case nameof(AvatarPreviewViewModel.Texture):
                RebuildModels();
                break;
            case nameof(AvatarPreviewViewModel.IsVisible):
                UpdateRendering();
                break;
        }
    }

    void SyncRotation()
    {
        YawRotation.Angle = _vm?.Yaw ?? 0;
        PitchRotation.Angle = _vm?.Pitch ?? 0;
    }

    void RebuildModels()
    {
        Body.Children.Clear();
        if (_vm is null) return;
        Brush? textureBrush = null;
        if (_vm.Texture is BitmapSource texture)
        {
            var image = new ImageBrush(texture)
            {
                ViewportUnits = BrushMappingMode.Absolute,
                Viewport = new Rect(0, 0, 1, 1),
                Stretch = Stretch.Fill,
                TileMode = TileMode.None,
            };
            image.Freeze();
            textureBrush = image;
        }
        var body = new SolidColorBrush(BodyColor);
        body.Freeze();
        Body.Children.Add(Stage.Value);
        foreach (var box in _vm.Boxes)
        {
            if (box.Part == AvatarPart.Head)
            {
                Body.Children.Add(Head.Value);
                continue;
            }
            bool textured = box.Dressed && textureBrush is not null;
            var material = new DiffuseMaterial(textured ? textureBrush! : body);
            material.Freeze();
            foreach (var face in box.Faces)
            {
                var model = new GeometryModel3D(Mesh(face, textured), material);
                model.Freeze();
                Body.Children.Add(model);
            }
        }
    }

    // ---- the look: the stage under the feet and the classic head (frozen once, shared by every rebuild) ----

    static readonly Lazy<Model3DGroup> Stage = new(BuildStage);
    static readonly Lazy<Model3DGroup> Head = new(BuildHead);

    /// <summary>
    /// A floor at the feet, inside the turning group so the body never lifts off it when pitched: a soft dark shadow
    /// (alpha; diffuse black reads black under any light) and a faint brand-cyan ring (emissive, so it glows on the dark
    /// stage). Both are radial, so the yaw leaves them unchanged. Drawn before the body, so their alpha blends over the
    /// background only.
    /// </summary>
    static Model3DGroup BuildStage()
    {
        var shadow = new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0x90, 0, 0, 0), 0),
                new GradientStop(Color.FromArgb(0x50, 0, 0, 0), 0.45),
                new GradientStop(Color.FromArgb(0x00, 0, 0, 0), 1),
            },
        };
        var ring = new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0x00, 0x17, 0xD4, 0xFA), 0),
                new GradientStop(Color.FromArgb(0x00, 0x17, 0xD4, 0xFA), 0.70),
                new GradientStop(Color.FromArgb(0x60, 0x17, 0xD4, 0xFA), 0.86),
                new GradientStop(Color.FromArgb(0x00, 0x17, 0xD4, 0xFA), 1),
            },
        };
        var group = new Model3DGroup
        {
            Children =
            {
                Floor(2.4, -0.01, new DiffuseMaterial(shadow)),
                Floor(2.6, -0.005, new EmissiveMaterial(ring)),
            },
        };
        group.Freeze();
        return group;
    }

    /// <summary>A square on the ground plane, its brush stretched over it, visible from above and below.</summary>
    static GeometryModel3D Floor(double half, double y, Material material)
    {
        var mesh = new MeshGeometry3D
        {
            Positions = { new(-half, y, -half), new(half, y, -half), new(half, y, half), new(-half, y, half) },
            TriangleIndices = { 0, 3, 2, 0, 2, 1 },
            TextureCoordinates = { new(0, 0), new(1, 0), new(1, 1), new(0, 1) },
            Normals = { new(0, 1, 0), new(0, 1, 0), new(0, 1, 0), new(0, 1, 0) },
        };
        return new GeometryModel3D(mesh, material) { BackMaterial = material };
    }

    const int HeadSegments = 40;

    /// <summary>
    /// The classic head: a cylinder 1.2 studs across and 1.2 tall on the torso (the rig's head box), its rims rounded by
    /// a 0.1 stud bevel and its sides smooth-shaded, wearing the classic smile on its front (+Z). Body color, with a soft
    /// specular sheen that the flat-faced boxes do not carry.
    /// </summary>
    static Model3DGroup BuildHead()
    {
        const double r = 0.6, bevel = 0.1, y0 = 4.0, y1 = 5.2;
        // The profile from the bottom center up to the top center: (radius, height).
        (double R, double Y)[] profile =
        [
            (0, y0), (r - bevel, y0), (r - bevel * 0.29, y0 + bevel * 0.29), (r, y0 + bevel),
            (r, y1 - bevel), (r - bevel * 0.29, y1 - bevel * 0.29), (r - bevel, y1), (0, y1),
        ];
        var mesh = new MeshGeometry3D();
        // One ring of vertices per profile point; smooth normals around the head and along the bevel.
        for (int i = 0; i < profile.Length; i++)
        {
            var (pr, py) = profile[i];
            var (nr, ny) = ProfileNormal(profile, i);
            for (int k = 0; k <= HeadSegments; k++)
            {
                // Around from the back (-Z) through +X to the front (+Z) at t 0.5 and on to the back again, so u = t
                // runs on without a jump: the seam is the first and last column, two vertices in one place, both at
                // the back. Wrapping from the front instead made one strip at the back sweep u from 1 back to 0 and
                // drew a squeezed second face there.
                double t = k / (double)HeadSegments, a = Math.PI + 2 * Math.PI * t;
                double sx = Math.Sin(a), cz = Math.Cos(a);
                mesh.Positions.Add(new Point3D(pr * sx, py, pr * cz));
                mesh.Normals.Add(new Vector3D(nr * sx, ny, nr * cz));
                mesh.TextureCoordinates.Add(new Point(t, 1 - (py - y0) / (y1 - y0)));
            }
        }
        int row = HeadSegments + 1;
        for (int i = 0; i < profile.Length - 1; i++)
            for (int k = 0; k < HeadSegments; k++)
            {
                int a = i * row + k, b = a + 1, c = a + row, d = c + 1;
                foreach (int n in new[] { a, b, d, a, d, c }) mesh.TriangleIndices.Add(n);
            }
        mesh.Freeze();
        var face = new ImageBrush(FaceTexture()) { ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 1, 1) };
        var material = new MaterialGroup
        {
            Children =
            {
                new DiffuseMaterial(face),
                new SpecularMaterial(new SolidColorBrush(Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF)), 28),
            },
        };
        var group = new Model3DGroup { Children = { new GeometryModel3D(mesh, material) } };
        group.Freeze();
        return group;
    }

    /// <summary>The outward normal at a profile point as (radial, up), averaged from its two profile edges.</summary>
    static (double R, double Y) ProfileNormal((double R, double Y)[] profile, int i)
    {
        static (double R, double Y) Edge((double R, double Y) a, (double R, double Y) b)
        {
            double dr = b.R - a.R, dy = b.Y - a.Y, len = Math.Sqrt(dr * dr + dy * dy);
            return (dy / len, -dr / len);   // the edge turned a quarter outward
        }
        if (i == 0) return (0, -1);
        if (i == profile.Length - 1) return (0, 1);
        var e1 = Edge(profile[i - 1], profile[i]);
        var e2 = Edge(profile[i], profile[i + 1]);
        double x = e1.R + e2.R, y = e1.Y + e2.Y, l = Math.Sqrt(x * x + y * y);
        return (x / l, y / l);
    }

    /// <summary>The classic smile on body color: two upright oval eyes and a thin smile, centered on u 0.5.</summary>
    static BitmapSource FaceTexture()
    {
        const int w = 512, h = 128;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(BodyColor), null, new Rect(0, 0, w, h));
            var ink = new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1F));
            dc.DrawEllipse(ink, null, new Point(w / 2 - 22, 50), 6.5, 12);
            dc.DrawEllipse(ink, null, new Point(w / 2 + 22, 50), 6.5, 12);
            var smile = new StreamGeometry();
            using (var g = smile.Open())
            {
                g.BeginFigure(new Point(w / 2 - 34, 78), false, false);
                g.QuadraticBezierTo(new Point(w / 2, 108), new Point(w / 2 + 34, 78), true, true);
            }
            dc.DrawGeometry(null, new Pen(ink, 4.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, smile);
        }
        var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    static MeshGeometry3D Mesh(AvatarFace face, bool textured)
    {
        var mesh = new MeshGeometry3D();
        foreach (var c in face.Corners) mesh.Positions.Add(new Point3D(c.X, c.Y, c.Z));
        foreach (int i in new[] { 0, 3, 2, 0, 2, 1 }) mesh.TriangleIndices.Add(i);
        var normal = Vector3D.CrossProduct(mesh.Positions[3] - mesh.Positions[0], mesh.Positions[2] - mesh.Positions[3]);
        normal.Normalize();
        for (int i = 0; i < 4; i++) mesh.Normals.Add(normal);
        if (textured)
            foreach (var (u, v) in face.Uv) mesh.TextureCoordinates.Add(new Point(u / TemplateSize.Width, v / TemplateSize.Height));
        mesh.Freeze();
        return mesh;
    }

    void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null) return;
        Viewport.Focus();
        e.Handled = true;
        if (e.ClickCount >= 2)
        {
            _vm.ResetView();
            return;
        }
        _dragFrom = e.GetPosition(DragSurface);
        _vm.BeginDrag();
        DragSurface.CaptureMouse();
    }

    void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_vm is null || !_vm.IsDragging) return;
        var at = e.GetPosition(DragSurface);
        _vm.DragTo(at.X - _dragFrom.X, at.Y - _dragFrom.Y);
        _dragFrom = at;
    }

    void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null || !_vm.IsDragging) return;
        DragSurface.ReleaseMouseCapture();   // LostMouseCapture ends the drag
        _vm.EndDrag();
    }

    void OnWindowStateChanged(object? sender, EventArgs e) => UpdateRendering();

    /// <summary>Subscribes to the frame clock only while loaded, shown, enabled in the view-model and not minimized.</summary>
    void UpdateRendering()
    {
        bool want = IsLoaded && IsVisible && _vm is { IsVisible: true } && _window?.WindowState != WindowState.Minimized;
        if (want == _rendering) return;
        _rendering = want;
        _lastFrame = null;
        if (want) CompositionTarget.Rendering += OnFrame;
        else CompositionTarget.Rendering -= OnFrame;
    }

    void OnFrame(object? sender, EventArgs e)
    {
        if (_vm is null || e is not RenderingEventArgs frame) return;
        var now = frame.RenderingTime;
        var delta = _lastFrame is { } last ? now - last : TimeSpan.Zero;
        _lastFrame = now;
        if (delta > TimeSpan.Zero) _vm.Advance(delta > MaxFrame ? MaxFrame : delta);
    }
}
