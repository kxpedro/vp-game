using Godot;

// TESTE (nao definitivo): renderiza o mundo 3D em baixa resolucao e amplia sem filtro,
// para o cenario ficar com o mesmo pixel do sprite do Virgil (PlayerSpriteVisual.cs).
// Um SubViewport compartilha o World3D da janela, com uma camera espelhando a IsoCamera;
// a janela principal para de desenhar 3D e o HUD continua em resolucao cheia.
// Liga/desliga em debug com F4.
public partial class PixelRenderTest : Node
{
    public static bool Enabled { get; set; } = true;

    // Camera mais perto no modo pixel: cada pixel do sprite vira 2-3 pixels da janela.
    private const float ZoomMultiplier = 0.6f;

    private Node3D _player;
    private CanvasLayer _layer;
    private SubViewportContainer _container;
    private SubViewport _viewport;
    private Camera3D _mirror;
    private bool _active;

    public override void _Ready()
    {
        _player = GetParent<Node3D>();
        _layer = new CanvasLayer { Name = "PixelRenderLayer", Layer = -1, Visible = false };
        _container = new SubViewportContainer
        {
            Name = "PixelRenderContainer",
            Stretch = true,
            StretchShrink = 2,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _container.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _viewport = new SubViewport
        {
            Name = "PixelRenderViewport",
            HandleInputLocally = false,
            Msaa3D = Viewport.Msaa.Disabled,
            ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Disabled,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
        };
        _mirror = new Camera3D { Name = "PixelRenderCamera" };
        _viewport.AddChild(_mirror);
        _container.AddChild(_viewport);
        _layer.AddChild(_container);
        AddChild(_layer);
    }

    public override void _ExitTree()
    {
        if (_active)
            SetActive(false);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!OS.IsDebugBuild())
            return;
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F4 })
        {
            Enabled = !Enabled;
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (Enabled != _active)
            SetActive(Enabled);
        if (!_active)
            return;

        var main = GetViewport().GetCamera3D();
        if (main == null)
            return;

        _mirror.GlobalTransform = main.GlobalTransform;
        _mirror.Projection = main.Projection;
        _mirror.Fov = main.Fov;
        _mirror.Size = main.Size;
        _mirror.KeepAspect = main.KeepAspect;
        _mirror.Near = main.Near;
        _mirror.Far = main.Far;
        _mirror.HOffset = main.HOffset;
        _mirror.VOffset = main.VOffset;
        _mirror.CullMask = main.CullMask;
        _mirror.Environment = main.Environment;
        _mirror.Attributes = main.Attributes;
        if (!_mirror.Current)
            _mirror.MakeCurrent();

        UpdateShrink(main);
    }

    // Escolhe o fator de reducao para 1 pixel da tela baixa ~ 1 pixel do sprite.
    private void UpdateShrink(Camera3D main)
    {
        var height = GetViewport().GetVisibleRect().Size.Y;
        var distance = main.GlobalPosition.DistanceTo(_player.GlobalPosition);
        if (height <= 0.0f || distance <= 0.01f)
            return;

        var unitsOnScreen = main.Projection == Camera3D.ProjectionType.Orthogonal
            ? main.Size
            : 2.0f * distance * Mathf.Tan(Mathf.DegToRad(main.Fov) * 0.5f);
        var screenPixelsPerSpritePixel = height / unitsOnScreen * PlayerSpriteVisual.PixelSize;
        var shrink = Mathf.Clamp(Mathf.RoundToInt(screenPixelsPerSpritePixel), 1, 8);
        if (_container.StretchShrink != shrink)
            _container.StretchShrink = shrink;
    }

    private void SetActive(bool active)
    {
        _active = active;
        var root = GetViewport();
        if (active)
            _viewport.World3D = root.FindWorld3D();
        _viewport.RenderTargetUpdateMode = active ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        _layer.Visible = active;
        root.Disable3D = active;
        IsoCamera.TestZoomMultiplier = active ? ZoomMultiplier : 1.0f;
    }
}
