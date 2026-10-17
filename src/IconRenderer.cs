using Prowl.Runtime;
using Prowl.Runtime.Rendering;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// Renders a 3/4-view thumbnail of every prop into its own RenderTexture, once, after the
/// window and GL context exist. Uses a private off-screen scene per icon (same technique as
/// Prowl's editor asset previews). If anything fails, the spawn menu falls back to drawn icons.
/// </summary>
public sealed class IconRenderer
{
    public const int Size = 192;
    private readonly Dictionary<PropKind, RenderTexture> _icons = [];
    private bool _attempted;

    public bool Ready { get; private set; }
    public string? Error { get; private set; }

    public RenderTexture? Get(PropKind kind) => _icons.GetValueOrDefault(kind);

    public void RenderAll(Shader shader)
    {
        if (_attempted) return;
        _attempted = true;
        try
        {
            foreach (PropKind kind in PropManager.AllKinds)
                _icons[kind] = RenderOne(kind, shader);
            Ready = true;
            Console.WriteLine($"Rendered {_icons.Count} prop thumbnails.");
        }
        catch (Exception e)
        {
            Error = e.Message;
            Console.WriteLine($"Prop thumbnails unavailable, using drawn icons: {e.Message}");
        }
    }

    private static RenderTexture RenderOne(PropKind kind, Shader shader)
    {
        var scene = new Scene { Name = $"Icon {kind}" };

        var camGo = new GameObject("IconCamera");
        Camera cam = camGo.AddComponent<Camera>();
        cam.FieldOfView = 30f;
        cam.NearClipPlane = 0.05f;
        cam.FarClipPlane = 50f;
        cam.ClearFlags = CameraClearFlags.SolidColor;
        cam.ClearColor = new Color(0f, 0f, 0f, 0f);
        scene.Add(camGo);

        var lightGo = new GameObject("IconLight");
        lightGo.Transform.LocalEulerAngles = new Float3(-40f, 35f, 0f);
        DirectionalLight light = lightGo.AddComponent<DirectionalLight>();
        light.Intensity = 1.6f;
        light.CastShadows = false;
        scene.Add(lightGo);

        var subject = new GameObject("IconSubject");
        MeshRenderer r = subject.AddComponent<MeshRenderer>();
        (Mesh mesh, float extent) = MeshFor(kind);
        r.Mesh = mesh;
        var mat = new Material(shader);
        mat.SetColor("_MainColor", PropManager.IconColor(kind));
        SurfaceMaterial.Get(PropManager.DefaultSurface(kind)).ApplyVisual(mat);
        r.Material = mat;
        subject.Transform.LocalEulerAngles = new Float3(kind == PropKind.Ramp ? -20f : 0f, 25f, 0f);
        scene.Add(subject);

        scene.Enable();

        Float3 dir = Float3.Normalize(new Float3(1f, 0.75f, -1.2f));
        float dist = extent / MathF.Tan(15f * MathF.PI / 180f) * 1.15f;
        camGo.Transform.Position = dir * dist;
        camGo.Transform.Rotation = Quaternion.LookRotation(-dir, Float3.UnitY);

        var rt = new RenderTexture(Size, Size, true, [TextureImageFormat.Color4b]);
        cam.Target = rt;
        cam.UpdateRenderData();
        (cam.Pipeline ?? DefaultRenderPipeline.Default).Render(cam, new RenderingData());
        scene.Disable();
        return rt;
    }

    private static (Mesh mesh, float extent) MeshFor(PropKind kind) => kind switch
    {
        PropKind.Cube => (PropManager.MeshFor(PropKind.Cube, PropManager.SizeOf(PropKind.Cube)), 0.9f),
        PropKind.Sphere => (PropManager.MeshFor(PropKind.Sphere, PropManager.SizeOf(PropKind.Sphere)), 0.6f),
        PropKind.Plank => (PropManager.MeshFor(PropKind.Plank, PropManager.SizeOf(PropKind.Plank)), 1.6f),
        PropKind.Barrel => (PropManager.MeshFor(PropKind.Barrel, PropManager.SizeOf(PropKind.Barrel)), 0.75f),
        PropKind.BigCrate => (PropManager.MeshFor(PropKind.BigCrate, PropManager.SizeOf(PropKind.BigCrate)), 1.8f),
        PropKind.Ramp => (PropManager.MeshFor(PropKind.Ramp, PropManager.SizeOf(PropKind.Ramp)), 2.0f),
        PropKind.SmallCrate => (PropManager.MeshFor(PropKind.SmallCrate, PropManager.SizeOf(PropKind.SmallCrate)), 0.55f),
        PropKind.Pole => (PropManager.MeshFor(PropKind.Pole, PropManager.SizeOf(PropKind.Pole)), 1.6f),
        PropKind.Panel => (PropManager.MeshFor(PropKind.Panel, PropManager.SizeOf(PropKind.Panel)), 1.5f),
        PropKind.BigBall => (PropManager.MeshFor(PropKind.BigBall, PropManager.SizeOf(PropKind.BigBall)), 1.2f),
        _ => (Mesh.CreateCube(Float3.One), 0.9f),
    };
}
