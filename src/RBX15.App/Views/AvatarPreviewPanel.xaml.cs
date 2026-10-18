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
        foreach (var box in _vm.Boxes)
        {
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
