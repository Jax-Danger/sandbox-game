// Legacy code-built game objects are never destroyed while cached; ?./?? on them predates the 1.0 analyzer (revisit in stage 6).
#pragma warning disable PROWLEO001, PROWLEO002
using Prowl.PaperUI;
using Prowl.Runtime;
using Prowl.Runtime.Rendering;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// Placeable security camera (gmod_cameraprop style). The newest camera renders into a
/// RenderTexture every other frame and is shown as a picture-in-picture monitor on the HUD.
/// </summary>
public static class CameraFeeds
{
    public const int Width = 384, Height = 216;
    private static readonly List<Prop> _cams = [];
    private static RenderTexture? _rt;
    private static GameObject? _camGo;
    private static Camera? _cam;
    private static int _frame;
    public static bool Visible = true;
    public static int Renders { get; private set; }

    public static IReadOnlyList<Prop> All => _cams;
    public static Prop? Active => _cams.LastOrDefault(c => c.Go.IsValid());

    public static Prop Spawn(SandboxGame g, Float3 pos, float yaw, bool recordUndo = true)
    {
        Prop p = g.Props.Spawn(PropKind.SmallCrate, pos, yaw, recordUndo);
        Make(g, p);
        return p;
    }

    public static void Make(SandboxGame g, Prop p)
    {
        p.Entity = "ent_camera";
        p.Go.Name = p.Go.Name.Replace("Small Crate", "Camera");
        p.SetColor(new Color(0.18f, 0.19f, 0.21f, 1f));
        var lens = new GameObject("CameraLens");
        lens.SetParent(p.Go, false);
        // Smooth camcorder body (swept) on the crate collider, lathed lens barrel with a hood, glowing glass.
        if (p.Go.GetComponent<MeshRenderer>() is { } body)
            body.Mesh = Meshes.Sweep([new Meshes.Sec(new Float3(0f, 0f, -0.29f), 0.2f, 0.17f, 3.2f), new Meshes.Sec(new Float3(0f, 0.01f, -0.1f), 0.24f, 0.2f, 3.6f),
                new Meshes.Sec(new Float3(0f, 0.01f, 0.12f), 0.24f, 0.2f, 3.6f), new Meshes.Sec(new Float3(0f, 0f, 0.26f), 0.2f, 0.17f, 3.2f)], Float3.UnitX, 28, 0.35f);
        var barrel = new GameObject("CameraBarrel");
        barrel.SetParent(p.Go, false);
        barrel.Transform.LocalPosition = new Float3(0f, 0f, 0.2f);
        MeshRenderer br = barrel.AddComponent<MeshRenderer>();
        br.Mesh = Meshes.LatheZ([(0.0001f, 0f), (0.15f, 0f), (0.155f, 0.03f), (0.14f, 0.1f), (0.17f, 0.16f), (0.18f, 0.2f), (0.15f, 0.2f), (0.13f, 0.12f), (0.0001f, 0.12f)]);
        var bm = Mats.New(g.Shader); bm.SetColor("_MainColor", new Color(0.08f, 0.08f, 0.09f, 1f)); br.Material = bm;
        lens.Transform.LocalPosition = new Float3(0f, 0f, 0.322f);
        lens.Transform.LocalEulerAngles = new Float3(90f, 0f, 0f);
        MeshRenderer r = lens.AddComponent<MeshRenderer>();
        r.Mesh = Meshes.RoundedCylinder(0.125f, 0.012f, 0.005f, 32);
        var m = Mats.New(g.Shader);
        m.SetColor("_MainColor", new Color(0.05f, 0.06f, 0.08f, 1f));
        if (!Application.IsHeadless) { m.SetTexture("_EmissionTex", ProcTex.Solid(30, 70, 150)); m.SetFloat("_EmissionIntensity", 0.7f); }
        r.Material = m;
        if (!Application.IsHeadless && p.Go.Transform.Find("CamHandle") is null)
        {
            try { p.Go.GetComponent<MeshRenderer>()?.Material?.SetTexture("_MainTex", Texture2D.LoadDefault(DefaultTexture.White)); } catch { }
            var dark = Mats.New(g.Shader); dark.SetColor("_MainColor", new Color(0.07f, 0.07f, 0.08f, 1f));
            var rub = Mats.New(g.Shader); rub.SetColor("_MainColor", new Color(0.12f, 0.12f, 0.13f, 1f));
            var stl = Mats.New(g.Shader); stl.SetColor("_MainColor", new Color(0.62f, 0.63f, 0.66f, 1f));
            var red = Mats.New(g.Shader); red.SetColor("_MainColor", new Color(1f, 0.12f, 0.08f, 1f));
            if (!Application.IsHeadless) { red.SetTexture("_EmissionTex", ProcTex.Solid(255, 30, 20)); red.SetFloat("_EmissionIntensity", 2f); }
            void Part(string n, Mesh mesh, Float3 at, Material mt, Float3 eul = default)
            {
                var c = new GameObject(n); c.SetParent(p.Go, false); c.Transform.LocalPosition = at; c.Transform.LocalEulerAngles = eul;
                MeshRenderer mr = c.AddComponent<MeshRenderer>(); mr.Mesh = mesh; mr.Material = mt;
            }
            // Carry handle on two posts, knurled focus + zoom rings on the barrel, tally LED, flip-out side monitor,
            // rear battery pack, and a tripod plate underneath.
            Part("CamHandle", VehicleModels.BentPipe(0.022f, 0.05f, Float3.UnitX, new Float3(0f, 0.2f, -0.18f), new Float3(0f, 0.29f, -0.14f), new Float3(0f, 0.29f, 0.12f), new Float3(0f, 0.2f, 0.16f)), Float3.Zero, rub);
            for (int i = 0; i < 9; i++) Part("Knurl", Meshes.LatheZ([(0.157f, 0f), (0.161f, 0.004f), (0.157f, 0.008f)], 32), new Float3(0f, 0f, 0.235f + i * 0.009f), rub);
            Part("ZoomRing", Meshes.LatheZ([(0.176f, 0f), (0.184f, 0.01f), (0.184f, 0.03f), (0.176f, 0.04f)], 32), new Float3(0f, 0f, 0.345f), stl);
            Part("Tally", Meshes.Sphere(0.018f), new Float3(0.09f, 0.21f, 0.2f), red);
            Part("Monitor", Meshes.RoundedBox(new Float3(0.02f, 0.15f, 0.2f), 0.008f, 3), new Float3(-0.26f, 0.04f, 0.04f), dark, new Float3(0f, -18f, 0f));
            var scr = Mats.New(g.Shader); scr.SetColor("_MainColor", new Color(0.15f, 0.35f, 0.55f, 1f));
            if (!Application.IsHeadless) { scr.SetTexture("_EmissionTex", ProcTex.Solid(40, 90, 140)); scr.SetFloat("_EmissionIntensity", 1f); }
            Part("MonitorScreen", Meshes.RoundedBox(new Float3(0.004f, 0.125f, 0.17f), 0.002f, 2), new Float3(-0.273f, 0.04f, 0.042f), scr, new Float3(0f, -18f, 0f));
            Part("Battery", Meshes.RoundedBox(new Float3(0.3f, 0.2f, 0.08f), 0.02f, 3), new Float3(0f, 0f, -0.32f), dark);
            Part("BatteryLatch", Meshes.RoundedBox(new Float3(0.08f, 0.03f, 0.02f), 0.006f, 2), new Float3(0f, 0.06f, -0.365f), stl);
            Part("LensRing", Meshes.LatheZ([(0.1f, 0f), (0.128f, 0f), (0.128f, 0.004f), (0.1f, 0.004f)], 32), new Float3(0f, 0f, 0.329f), dark);
            Part("LensGlint", Meshes.Sphere(0.02f), new Float3(0.045f, 0.045f, 0.326f), stl);
            Part("TripodPlate", Meshes.RoundedBox(new Float3(0.16f, 0.025f, 0.22f), 0.008f, 3), new Float3(0f, -0.21f, 0f), stl);
        }
        _cams.Add(p);
    }

    public static void Clear() => _cams.Clear();

    /// <summary>Renders the active feed. Call once per frame before the main camera draws.</summary>
    public static void Tick(SandboxGame g)
    {
        _cams.RemoveAll(c => !c.Go.IsValid() || !g.Props.Contains(c));
        Prop? src = Active;
        if (src is null || Application.IsHeadless || !Visible) return;
        if (++_frame % 2 != 0) return;
        if (_rt is null)
        {
            _rt = new RenderTexture(Width, Height, true, [TextureFormat.RGBA8]);
            _camGo = new GameObject("FeedCamera");
            _cam = _camGo.AddComponent<Camera>();
            _cam.FieldOfView = 70f;
            _cam.NearClipPlane = 0.05f;
            _cam.FarClipPlane = 300f;
            _cam.Background = CameraBackground.Skybox;
            _cam.Target = _rt;
            _cam.Tonemapping = Graphics.Tonemapper(); // match the main view's exposure
            _cam.Enabled = false; // rendered manually, never as a scene camera
            g.World.Add(_camGo);
        }
        Transform t = src.Go.Transform;
        _camGo!.Transform.Position = t.Position + t.Forward * 0.42f;
        _camGo.Transform.Rotation = t.Rotation;
        _cam!.UpdateRenderData();
        _cam.Render();
        Renders++;
    }

    public static void Draw(Paper paper, float w, float h)
    {
        if (Active is null || !Visible || _rt?.MainTexture is not { } tex) return;
        float pw = Theme.S(300), ph = pw * Height / (float)Width, x = w - pw - Theme.S(20), y = Theme.S(20), pad = Theme.S(5);
        Ui.Glass(paper, "cam_frame", x - pad, y - pad, pw + pad * 2, ph + pad * 2 + Theme.S(20), Theme.S(Theme.RadiusSm), strong: true).IsNotInteractable();
        paper.Box("cam_feed").PositionType(PositionType.SelfDirected).Position(x, y).Size(pw, ph).IsNotInteractable()
            .OnPostLayout((handle, rect) => paper.Draw(ref handle, (canvas, r) =>
            {
                float rx = (float)r.Min.X, ry = (float)r.Min.Y, rw = (float)r.Size.X, rh = (float)r.Size.Y;
                canvas.SetBrushTexture(tex);
                canvas.SetBrushTextureTransform(
                    Prowl.Vector.Spatial.Transform2D.CreateTranslation(rx, ry + rh) *
                    Prowl.Vector.Spatial.Transform2D.CreateScale(rw, -rh));
                canvas.RoundedRectFilled(rx, ry, rw, rh, Theme.S(4), new Color32(255, 255, 255, 255));
                canvas.ClearBrushTexture();
            }));
        float dot = Theme.S(6);
        Ui.Panel(paper, "cam_rec", x + Theme.S(2), y + ph + Theme.S(7), dot, dot, new Color(0.95f, 0.25f, 0.25f, 1f), dot / 2f, shadow: false).IsNotInteractable();
        Ui.Text(paper, "cam_lbl", x + Theme.S(14), y + ph + Theme.S(2), pw, Theme.S(16),
            $"{Active.Go.Name}  ·  {_cams.Count} camera{(_cams.Count == 1 ? "" : "s")}", Theme.Micro, Theme.TextMuted);
    }
}
