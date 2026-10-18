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
    private readonly Dictionary<string, RenderTexture> _models = [];
    public RenderTexture? Get(string spawnId) => _models.GetValueOrDefault(spawnId);

    public void RenderAll(Shader shader, IEnumerable<(string Id, string? Vm)>? weapons = null)
    {
        if (_attempted) return;
        _attempted = true;
        try
        {
            foreach (PropKind kind in PropManager.AllKinds)
                _icons[kind] = RenderOne(kind, shader);
            foreach (ModelDef def in ModelProps.All)
                _models["prop_" + def.Id] = RenderOne(PropKind.Cube, shader, def);
            foreach (var (id, vm) in weapons ?? [])
                if (IconSubjects.Weapon(id, vm) is { } wd) _models[id] = RenderOne(PropKind.Cube, shader, wd, weapon: !IconSubjects.IsCustom(id));
            foreach (SpawnEntry e in SpawnCatalog.Entries.Where(x => x.Tab == MenuTab.Entities))
                if (IconSubjects.Entity(e.Id) is { } ed) _models[e.Id] = RenderOne(PropKind.Cube, shader, ed);
            foreach (SpawnEntry e in SpawnCatalog.Entries.Where(x => x.Tab == MenuTab.Vehicles))
                if (IconSubjects.Vehicle(e.Id) is { } vd) _models[e.Id] = RenderOne(PropKind.Cube, shader, vd);
            Ready = true;
            Console.WriteLine($"Rendered {_icons.Count + _models.Count} thumbnails.");
        }
        catch (Exception e)
        {
            Error = e.Message;
            Console.WriteLine($"Prop thumbnails unavailable, using drawn icons: {e.Message}");
        }
    }

    private static RenderTexture RenderOne(PropKind kind, Shader shader, ModelDef? model = null, bool weapon = false)
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

        // Soft fill from the opposite side and a cool rim from behind, so dark guns and props don't read as silhouettes.
        var fillGo = new GameObject("IconFill");
        fillGo.Transform.LocalEulerAngles = new Float3(-15f, -140f, 0f);
        DirectionalLight fill = fillGo.AddComponent<DirectionalLight>();
        fill.Intensity = 0.55f; fill.CastShadows = false; fill.Color = new Color(0.85f, 0.9f, 1f, 1f);
        scene.Add(fillGo);
        var rimGo = new GameObject("IconRim");
        rimGo.Transform.LocalEulerAngles = new Float3(-25f, 200f, 0f);
        DirectionalLight rim = rimGo.AddComponent<DirectionalLight>();
        rim.Intensity = weapon ? 1.1f : 0.6f; rim.CastShadows = false; rim.Color = new Color(0.8f, 0.9f, 1f, 1f);
        scene.Add(rimGo);

        var subject = new GameObject("IconSubject");
        (Mesh mesh, float extent) = MeshFor(kind);
        var mat = Mats.New(shader);
        mat.SetColor("_MainColor", PropManager.IconColor(kind));
        SurfaceMaterial.Get(PropManager.DefaultSurface(kind)).ApplyVisual(mat);
        if (model is null)
        {
            MeshRenderer r = subject.AddComponent<MeshRenderer>();
            r.Mesh = mesh;
            r.Material = mat;
        }
        else
        {
            if (model.Parts.FirstOrDefault(p => p.Paint) is { } paint) mat.SetColor("_MainColor", paint.Color);
            ModelProps.BuildParts(subject, model, shader, mat);
            Float3 c = model.Collider;
            extent = MathF.Sqrt(c.X * c.X + c.Y * c.Y + c.Z * c.Z) * 0.5f;
        }
        subject.Transform.LocalEulerAngles = weapon ? new Float3(0f, 105f, 0f) : new Float3(kind == PropKind.Ramp ? -20f : 0f, model?.Category == "Signs" && model.Id != "street_lamp" ? 205f : 25f, kind == PropKind.Pole && model is null ? 42f : 0f); // pole lies diagonal: fills the tile instead of a hairline
        scene.Add(subject);

        scene.Enable();

        Float3 dir = Float3.Normalize(weapon ? new Float3(0.45f, 0.35f, 1f) : new Float3(1f, 0.75f, -1.2f));
        // Tight framing: bounding sphere of the actual rendered vertices (thin weapons and tall props fill the tile),
        // re-centred on the origin.
        try
        {
            var pts = new List<Float3>();
            foreach (MeshRenderer mr in subject.GetComponentsInChildren<MeshRenderer>())
                if (mr.Mesh.Res?.Vertices is { } vs)
                    for (int i = 0; i < vs.Length; i += Math.Max(1, vs.Length / 400)) pts.Add(mr.GameObject.Transform.TransformPoint(vs[i]));
            if (pts.Count > 3)
            {
                Float3 lo = pts[0], hi = pts[0];
                foreach (Float3 q in pts) { lo = new Float3(MathF.Min(lo.X, q.X), MathF.Min(lo.Y, q.Y), MathF.Min(lo.Z, q.Z)); hi = new Float3(MathF.Max(hi.X, q.X), MathF.Max(hi.Y, q.Y), MathF.Max(hi.Z, q.Z)); }
                Float3 ctr = (lo + hi) * 0.5f;
                // Radius of the silhouette as seen along the view axis (not the 3D bounding sphere, which over-shrank
                // thin subjects and clipped round ones like the bouncy ball).
                float rr = 0f; foreach (Float3 q in pts) { Float3 d = q - ctr; rr = MathF.Max(rr, Float3.Length(d - dir * Float3.Dot(d, dir))); }
                if (rr > 0.01f && float.IsFinite(rr)) { subject.Transform.Position = subject.Transform.Position - ctr; extent = rr * 0.98f; }
            }
        }
        catch { }

        float dist = extent / MathF.Tan(15f * MathF.PI / 180f) * 1.08f;
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
