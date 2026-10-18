using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// Interactive bits of the RP town: doors (buy / own / lock), the ATM, the laws board, the jail cell and
/// sidewalk waypoints the NPCs walk between. Filled in by <see cref="MapBuilder"/> when the "town" map loads.
/// </summary>
public static class Town
{
    public static List<Door> Doors { get; } = [];
    public static List<Float3> Waypoints { get; } = [];
    public static GameObject? Atm { get; set; }
    public static GameObject? Board { get; set; }
    public static Float3 CellCenter { get; set; }
    public static Float3 CellHalf { get; set; }
    public static Float3 ReleasePoint { get; set; }
    /// <summary>True when a custom map supplied RP markers (jail, ATM, doors, board or NPC paths).</summary>
    public static bool Markers { get; set; }
    public static bool Active => MapBuilder.Current == "town" || Markers;
    public static bool HasJail => CellHalf.X > 0f;

    public static void Reset() { Interactive.Clear(); Doors.Clear(); Waypoints.Clear(); Atm = null; Board = null; Markers = false; CellHalf = Float3.Zero; }

    public static bool InCell(Float3 p) => MathF.Abs(p.X - CellCenter.X) < CellHalf.X && MathF.Abs(p.Z - CellCenter.Z) < CellHalf.Z && p.Y < CellCenter.Y + 3f;

    /// <summary>Interactive map objects with their box sizes. Static hits carry no Transform, so we resolve them by point.</summary>
    public static List<(GameObject Go, Float3 Size)> Interactive { get; } = [];

    public static GameObject? At(RaycastHit hit)
    {
        if (hit.Transform is { } t && (t.GameObject.GetComponent<Door>() is not null || t.GameObject.GetComponent<AtmMachine>() is not null || t.GameObject.GetComponent<LawsBoard>() is not null)) return t.GameObject;
        foreach (var (go, size) in Interactive)
        {
            if (go.GetComponent<Door>() is { Open: true }) continue;
            Float3 l = go.Transform.InverseTransformPoint(hit.Point);
            if (MathF.Abs(l.X) <= size.X / 2f + 0.06f && MathF.Abs(l.Y) <= size.Y / 2f + 0.06f && MathF.Abs(l.Z) <= size.Z / 2f + 0.06f) return go;
        }
        return null;
    }

    public static Door? DoorFrom(RaycastHit hit) => At(hit)?.GetComponent<Door>();
    public static Door? Find(string id) => Doors.FirstOrDefault(d => d.Id == id);
}

/// <summary>
/// A hinged door. Static collider while closed; swings open (collider off) with E. In Roleplay, unowned doors with a
/// price can be bought (F2), owned doors locked/unlocked (L), and thieves can pick locked ones.
/// </summary>
public sealed class Door : MonoBehaviour
{
    public string Id = "", Title = "Door";
    public int Price;
    /// <summary>"" = for sale / public, "you" = the player, "city" / "police" = map-owned.</summary>
    public string Owner = "";
    public bool Locked, Open;
    /// <summary>Jobs that hold keys to a map-owned door (e.g. police for the station).</summary>
    public string[] Jobs = [];
    /// <summary>Names the owner shared keys with.</summary>
    public readonly HashSet<string> SharedKeys = [];
    public bool ForSale => Owner == "" && Price > 0;
    public Float3 Hinge;
    public float Yaw, Width = 1.3f;
    private float _angle;
    private BoxCollider? _col;

    public override void OnEnable() => _col = GameObject.GetComponent<BoxCollider>();

    public string Status => Owner switch
    {
        "you" => Locked ? "Your door · locked" : "Your door",
        "police" => "Police property",
        "city" => "City property",
        _ => Price > 0 ? $"For sale · ${Price:N0} · press F2 to buy" : "",
    };

    public void SetOpen(bool open)
    {
        Open = open;
        _col ??= GameObject.GetComponent<BoxCollider>();
        if (open && _col is not null) _col.Enabled = false;
        if (Application.IsHeadless) { _angle = open ? 1f : 0f; Pose(); if (!open) Rebuild(); }
    }

    public override void Update()
    {
        float target = Open ? 1f : 0f;
        if (MathF.Abs(_angle - target) < 0.001f) return;
        _angle += Math.Clamp(target - _angle, -Time.DeltaTime * 2.5f, Time.DeltaTime * 2.5f);
        Pose();
        if (!Open && _angle <= 0.001f) { _angle = 0f; Pose(); Rebuild(); }
    }

    /// <summary>Re-creates the static collider at the current pose (static shapes don't follow their transform).</summary>
    public void Rebuild()
    {
        _col ??= GameObject.GetComponent<BoxCollider>();
        if (_col is null) return;
        _col.Enabled = false;
        _col.Enabled = true;
    }

    public void Pose()
    {
        float a = Yaw + Theme.Ease(_angle) * 95f;
        float r = a * MathF.PI / 180f;
        Float3 along = new(MathF.Cos(r), 0f, -MathF.Sin(r));
        Transform.Position = Hinge + along * (Width / 2f);
        Transform.LocalEulerAngles = new Float3(0f, a, 0f);
    }
}

public sealed class AtmMachine : MonoBehaviour { }
public sealed class LawsBoard : MonoBehaviour { }

public static partial class MapBuilder
{
    private static void BuildTown(Scene scene, Shader shader)
    {
        SpawnPoint = new Float3(0f, 0.2f, -9f);
        var asphalt = new Color(0.20f, 0.21f, 0.23f, 1f);
        var walk = new Color(0.58f, 0.57f, 0.55f, 1f);
        var paver = new Color(0.72f, 0.66f, 0.58f, 1f);
        var line = new Color(0.92f, 0.80f, 0.30f, 1f);

        // Ground.
        GameObject ground = new("Ground");
        ground.Transform.Position = new Float3(0f, -0.5f, 0f);
        MeshRenderer gr = ground.AddComponent<MeshRenderer>();
        gr.Mesh = Mesh.CreateCube(new Float3(200f, 1f, 200f));
        var gm = Mats.New(shader);
        gm.SetColor("_MainColor", new Color(0.43f, 0.48f, 0.34f, 1f));
        if (!Application.IsHeadless && Environment.GetEnvironmentVariable("SANDBOX_PROCTEX") != "0") { gm.SetTexture("_MainTex", ProcTex.Grass()); gm.SetVector("_Tiling", new Float2(50f, 50f)); }
        gr.Material = gm;
        ground.AddComponent<BoxCollider>().Size = new Float3(200f, 1f, 200f);
        Add(scene, ground);

        // Outskirts (render only): farmland past the 200 m play square, a ragged tree line and low hills fading into
        // the fog, so the map edge no longer drops off into the sky.
        if (!Application.IsHeadless)
        {
            var outer = Shape(scene, shader, "~Outskirts", Mesh.CreateCube(new Float3(1400f, 0.02f, 1400f)), new Float3(0f, -0.04f, 0f), new Color(0.4f, 0.46f, 0.31f, 1f));
            if (outer.GetComponent<MeshRenderer>() is { } om && Environment.GetEnvironmentVariable("SANDBOX_PROCTEX") != "0")
            { om.Material.Res?.SetTexture("_MainTex", ProcTex.Grass()); om.Material.Res?.SetVector("_Tiling", new Float2(350f, 350f)); }
            var rng = new Random(77);
            var crowns = new[] { new List<Mesh>(), new List<Mesh>(), new List<Mesh>() }; var trunks = new List<Mesh>(); // merged: 4 draws, not 300
            for (int i = 0; i < 150; i++)
            {
                float a = i / 150f * MathF.Tau + (float)rng.NextDouble() * 0.03f, r = 118f + (float)rng.NextDouble() * 22f;
                float cx = MathF.Cos(a), cz = MathF.Sin(a), sq = MathF.Max(MathF.Abs(cx), MathF.Abs(cz));
                float x = cx / sq * r * 0.9f, z = cz / sq * r * 0.9f; // follows the square edge, ~6-26 m outside it
                if (MathF.Abs(x) < 9f || MathF.Abs(z) < 9f) continue; // keep the country roads clear
                float k = 0.9f + (float)rng.NextDouble() * 0.9f;
                crowns[i % 3].Add(Off(Meshes.Blob(2.4f * k, i % 9), x, 3.4f * k, z));
                trunks.Add(Off(Mesh.CreateCube(new Float3(0.45f, 3.2f * k, 0.45f)), x, 1.4f * k, z));
            }
            Color[] cc = [new(0.24f, 0.34f, 0.18f, 1f), new(0.28f, 0.38f, 0.2f, 1f), new(0.2f, 0.3f, 0.16f, 1f)];
            for (int c = 0; c < 3; c++) Shape(scene, shader, "~Far trees", Meshes.Merge(crowns[c].ToArray()), Float3.Zero, cc[c]);
            Shape(scene, shader, "~Far trunks", Meshes.Merge(trunks.ToArray()), Float3.Zero, new Color(0.3f, 0.22f, 0.15f, 1f));
            // Country roads carry each avenue on out of town (no kerbs), with a dashed centre line that fades into the fog.
            for (int dir = 0; dir < 4; dir++)
            {
                bool ax = dir < 2; float sg = dir % 2 == 0 ? 1f : -1f;
                Float3 At(float along, float y) => ax ? new Float3(sg * along, y, 0f) : new Float3(0f, y, sg * along);
                Shape(scene, shader, "~Country road", Mesh.CreateCube(ax ? new Float3(360f, 0.04f, 8f) : new Float3(8f, 0.04f, 360f)), At(83f + 180f, 0.0f), new Color(0.33f, 0.33f, 0.34f, 1f));
                var dash = new List<Mesh>();
                for (float t = 86f; t < 300f; t += 6f) dash.Add(Off(Meshes.RoundedBox(ax ? new Float3(2.6f, 0.01f, 0.18f) : new Float3(0.18f, 0.01f, 2.6f), 0.003f), ax ? sg * t : 0f, 0.025f, ax ? 0f : sg * t));
                Shape(scene, shader, "~Country road line", Meshes.Merge(dash.ToArray()), Float3.Zero, new Color(0.92f, 0.82f, 0.36f, 1f));
            }
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * MathF.Tau + 0.2f, r = 210f + (i * 37 % 60);
                var h = Shape(scene, shader, "~Far hill", Meshes.Blob(1f, 30 + i, 0.08f), new Float3(MathF.Cos(a) * r, -2f, MathF.Sin(a) * r), new Color(0.36f, 0.43f, 0.3f, 1f));
                h.Transform.LocalScale = new Float3(70f + i % 3 * 20f, 14f + i % 4 * 5f, 60f);
                h.Transform.LocalEulerAngles = new Float3(0f, -a * 180f / MathF.PI, 0f);
            }
        }

        // Plaza (centre) and four avenues running out of it, with sidewalks and centre lines.
        Box(scene, shader, "Plaza", new Float3(0f, 0.03f, 0f), new Float3(34f, 0.06f, 34f), Float3.Zero, paver);
        {
            // Paving: a darker kerb border, a 1.5 m joint grid, and two concentric granite rings around the fountain.
            var joints = new List<Mesh>();
            for (float t = -15.75f; t <= 15.8f; t += 1.5f)
            {
                joints.Add(Off(Meshes.RoundedBox(new Float3(32f, 0.006f, 0.035f), 0.002f), 0f, 0.061f, t));
                joints.Add(Off(Meshes.RoundedBox(new Float3(0.035f, 0.006f, 32f), 0.002f), t, 0.061f, 0f));
            }
            Shape(scene, shader, "~Paving joints", Meshes.Merge(joints.ToArray()), Float3.Zero, new Color(0.36f, 0.33f, 0.3f, 1f));
            var kerb = new List<Mesh>();
            foreach (float sg in new[] { -1f, 1f })
            {
                kerb.Add(Off(Meshes.RoundedBox(new Float3(34f, 0.03f, 0.9f), 0.01f), 0f, 0.07f, sg * 16.55f));
                kerb.Add(Off(Meshes.RoundedBox(new Float3(0.9f, 0.03f, 32.2f), 0.01f), sg * 16.55f, 0.07f, 0f));
            }
            Shape(scene, shader, "~Plaza kerb", Meshes.Merge(kerb.ToArray()), Float3.Zero, new Color(0.6f, 0.58f, 0.55f, 1f));
            var setts = new List<Mesh>();
            foreach (var (rr, n, wdt) in new[] { (3.3f, 40, 0.7f), (6.85f, 80, 0.45f) })
                for (int i = 0; i < n; i++)
                {
                    float ang = i * MathF.Tau / n, ca = MathF.Cos(ang), sa = MathF.Sin(ang), len = MathF.Tau * rr / n - 0.04f;
                    Mesh m = Meshes.RoundedBox(new Float3(wdt, 0.02f, len), 0.008f);
                    var mm = new Mesh { IndexFormat = m.IndexFormat, Vertices = m.Vertices.Select(v => new Float3(v.X * ca - v.Z * sa + ca * rr, v.Y + 0.062f, v.X * sa + v.Z * ca + sa * rr + 4f)).ToArray(), Normals = m.Normals.Select(v => new Float3(v.X * ca - v.Z * sa, v.Y, v.X * sa + v.Z * ca)).ToArray(), UV = m.UV, Indices = m.Indices };
                    mm.RecalculateBounds(); setts.Add(mm);
                }
            Shape(scene, shader, "~Paving rings", Meshes.Merge(setts.ToArray()), Float3.Zero, new Color(0.5f, 0.47f, 0.44f, 1f));
        }
        foreach (int dir in new[] { 0, 1, 2, 3 })
        {
            bool alongX = dir < 2; float sgn = dir % 2 == 0 ? 1f : -1f;
            Float3 c = alongX ? new Float3(sgn * 50f, 0.02f, 0f) : new Float3(0f, 0.02f, sgn * 50f);
            Float3 road = alongX ? new Float3(66f, 0.04f, 10f) : new Float3(10f, 0.04f, 66f);
            Box(scene, shader, $"Road {dir}", c, road, Float3.Zero, asphalt);
            for (int k = 0; k < 11; k++)
            {
                float t = 20f + k * 6f;
                Box(scene, shader, $"~Line {dir}.{k}", alongX ? new Float3(sgn * t, 0.045f, 0f) : new Float3(0f, 0.045f, sgn * t), alongX ? new Float3(3f, 0.01f, 0.25f) : new Float3(0.25f, 0.01f, 3f), Float3.Zero, line, collide: false);
            }
            foreach (float side in new[] { -1f, 1f })
            {
                Float3 sc = alongX ? new Float3(sgn * 50f, 0.08f, side * 6.5f) : new Float3(side * 6.5f, 0.08f, sgn * 50f);
                Box(scene, shader, $"Sidewalk {dir}{side}", sc, alongX ? new Float3(66f, 0.16f, 3f) : new Float3(3f, 0.16f, 66f), Float3.Zero, walk);
                for (int k = 0; k < 6; k++)
                {
                    float t = 22f + k * 11f;
                    Town.Waypoints.Add(alongX ? new Float3(sgn * t, 0f, side * 6.5f) : new Float3(side * 6.5f, 0f, sgn * t));
                }
            }
        }
        // Crosswalk stripes at each plaza exit.
        foreach (int dir in new[] { 0, 1, 2, 3 })
            for (int k = -3; k <= 3; k++)
            {
                bool alongX = dir < 2; float sgn = dir % 2 == 0 ? 1f : -1f;
                Box(scene, shader, $"~Zebra {dir}{k}", alongX ? new Float3(sgn * 18.5f, 0.045f, k * 1.3f) : new Float3(k * 1.3f, 0.045f, sgn * 18.5f), alongX ? new Float3(2.4f, 0.01f, 0.6f) : new Float3(0.6f, 0.01f, 2.4f), Float3.Zero, Color.White, collide: false);
            }
        for (int i = 0; i < 8; i++) { float a = i * MathF.Tau / 8f; Town.Waypoints.Add(new Float3(MathF.Cos(a) * 11f, 0f, MathF.Sin(a) * 11f)); }

        // Plaza furniture: fountain, benches, lamps, trees, laws board, ATM.
        var stone = new Color(0.78f, 0.76f, 0.72f, 1f);
        Shape(scene, shader, "Fountain base", Meshes.Lathe("fbasin", [(0.0001f, -0.35f), (2.6f, -0.35f), (2.62f, 0.2f), (2.75f, 0.28f), (2.72f, 0.36f), (2.45f, 0.38f), (2.4f, 0.3f), (0.0001f, 0.3f)], 48), new Float3(0f, 0.35f, 4f), stone, new Float3(5f, 0.7f, 5f));
        Shape(scene, shader, "~Fountain water", Meshes.RoundedCylinder(2.4f, 0.04f, 0.01f, 48), new Float3(0f, 0.67f, 4f), new Color(0.07f, 0.24f, 0.32f, 1f));
        Shape(scene, shader, "Fountain column", Meshes.Lathe("fcol", [(0.0001f, -0.7f), (0.4f, -0.7f), (0.3f, -0.55f), (0.22f, -0.2f), (0.2f, 0.4f), (0.55f, 0.55f), (0.6f, 0.62f), (0.5f, 0.66f), (0.12f, 0.7f), (0.0001f, 0.72f)], 32), new Float3(0f, 1.3f, 4f), stone, new Float3(0.6f, 1.4f, 0.6f));
        {
            // Upper tier: a slim stem, small bowl and pineapple finial; water spilling from the lower bowl in eight sheets,
            // a pool in the bowl, and ripple rings where the sheets land.
            Float3 fc = new(0f, 0f, 4f);
            Shape(scene, shader, "~Fountain upper", Meshes.Lathe("fupper", [(0.0001f, 0f), (0.14f, 0f), (0.1f, 0.08f), (0.08f, 0.4f), (0.3f, 0.5f), (0.32f, 0.55f), (0.26f, 0.57f), (0.06f, 0.6f), (0.06f, 0.72f), (0.1f, 0.8f), (0.07f, 0.9f), (0.0001f, 0.95f)], 28), fc + new Float3(0f, 2.0f, 0f), stone);
            var pale = new Color(0.62f, 0.8f, 0.86f, 1f);
            Shape(scene, shader, "~Fountain bowl water", Meshes.Merge(Off(Meshes.RoundedCylinder(0.5f, 0.02f, 0.005f, 32), 0f, 1.975f, 0f), Off(Meshes.RoundedCylinder(0.25f, 0.02f, 0.005f, 24), 0f, 2.545f, 0f)), fc, new Color(0.1f, 0.3f, 0.38f, 1f));
            var sheets = new List<Mesh>(); var ripples = new List<Mesh>();
            for (int i = 0; i < 8; i++)
            {
                float a = i * MathF.Tau / 8f, cx = MathF.Cos(a), cz = MathF.Sin(a);
                Float3 P(float r, float y) => new(cx * r, y, cz * r);
                sheets.Add(Meshes.Sweep([new Meshes.Sec(P(0.6f, 1.96f), 0.07f, 0.008f, 2f, Float3.UnitY), new Meshes.Sec(P(0.68f, 1.85f), 0.06f, 0.01f, 2f, Float3.UnitY), new Meshes.Sec(P(0.76f, 1.4f), 0.05f, 0.012f, 2f, Float3.UnitY), new Meshes.Sec(P(0.8f, 0.7f), 0.06f, 0.015f, 2f, Float3.UnitY)], new Float3(-cz, 0f, cx), 8, 0.6f));
                ripples.Add(Off(Meshes.Lathe("fripple", [(0.06f, 0f), (0.1f, 0.006f), (0.14f, 0f)], 16), cx * 0.8f, 0.69f, cz * 0.8f));
            }
            Shape(scene, shader, "~Fountain sheets", Meshes.Merge(sheets.ToArray()), fc, pale);
            Shape(scene, shader, "~Fountain ripples", Meshes.Merge(ripples.Concat([Meshes.Lathe("fripple2", [(1.2f, 0f), (1.25f, 0.005f), (1.3f, 0f)], 48) is var rr ? Off(rr, 0f, 0.69f, 0f) : null!]).ToArray()), fc, new Color(0.3f, 0.5f, 0.58f, 1f));
        }
        foreach (var (x, z, yaw) in new[] { (-7f, 4f, 90f), (7f, 4f, 90f), (-4f, -12f, 0f), (4f, -12f, 0f) })
        {
            // Park bench model (slats, backrest, cast-iron sides) facing the fountain; one box collider.
            ModelDef bench = ModelProps.All.First(m => m.Id == "bench");
            GameObject bgo = new("Bench");
            bgo.Transform.Position = new Float3(x, bench.Collider.Y / 2f, z);
            bgo.Transform.LocalEulerAngles = new Float3(0f, yaw == 0f ? (z < 0f ? 0f : 180f) : (x < 0f ? 90f : -90f), 0f);
            bgo.AddComponent<BoxCollider>().Size = bench.Collider;
            var bmat = Mats.New(shader); bmat.SetColor("_MainColor", new Color(0.55f, 0.38f, 0.22f, 1f));
            ModelProps.BuildParts(bgo, bench, shader, bmat);
            Add(scene, bgo);
        }
        foreach (var (x, z) in new[] { (-14f, -14f), (14f, -14f), (-14f, 14f), (14f, 14f) })
        {
            Shape(scene, shader, "Tree trunk", Meshes.Lathe("trunk", [(0.0001f, -1.2f), (0.34f, -1.2f), (0.24f, -0.9f), (0.2f, 0f), (0.17f, 1.0f), (0.12f, 1.4f), (0.0001f, 1.45f)], 20), new Float3(x, 1.2f, z), new Color(0.40f, 0.28f, 0.18f, 1f), new Float3(0.4f, 2.4f, 0.4f));
            // Clustered leaf clumps in three greens plus a couple of visible branches.
            int sd = (int)(MathF.Abs(x * 7f + z * 3f)) % 9;
            Shape(scene, shader, "~Tree crown", Meshes.Blob(1.35f, sd), new Float3(x, 3.5f, z), new Color(0.3f, 0.4f, 0.24f, 1f));
            Shape(scene, shader, "~Tree crown b", Meshes.Blob(0.95f, sd + 1), new Float3(x + 0.9f, 3.0f, z - 0.4f), new Color(0.25f, 0.34f, 0.2f, 1f));
            Shape(scene, shader, "~Tree crown c", Meshes.Blob(0.9f, sd + 2), new Float3(x - 0.8f, 3.1f, z + 0.5f), new Color(0.35f, 0.45f, 0.26f, 1f));
            Shape(scene, shader, "~Tree crown d", Meshes.Blob(0.8f, sd + 3), new Float3(x + 0.1f, 4.4f, z + 0.2f), new Color(0.33f, 0.43f, 0.25f, 1f));
            Shape(scene, shader, "~Branch", PropMeshes.Rod(new Float3(0f, 0f, 0f), new Float3(0.8f, 0.7f, -0.35f), 0.09f, 0.04f), new Float3(x, 2.3f, z), new Color(0.40f, 0.28f, 0.18f, 1f));
            Shape(scene, shader, "~Branch", PropMeshes.Rod(new Float3(0f, 0f, 0f), new Float3(-0.7f, 0.8f, 0.45f), 0.08f, 0.035f), new Float3(x, 2.4f, z), new Color(0.40f, 0.28f, 0.18f, 1f));
            Shape(scene, shader, "Lamp post", PropMeshes.Post(0.07f, 4f), new Float3(x * 0.7f, 2f, z * 0.7f), new Color(0.12f, 0.12f, 0.13f, 1f), new Float3(0.14f, 4f, 0.14f));
            // Victorian lantern: fluted cast foot, collar, ladder bar, a four-post lantern with glowing glass under a peaked cap.
            Float3 lp = new(x * 0.7f, 0f, z * 0.7f);
            Shape(scene, shader, "~Lamp foot", Meshes.Lathe("tlfoot", [(0.0001f, 0f), (0.22f, 0f), (0.22f, 0.08f), (0.18f, 0.12f), (0.17f, 0.5f), (0.13f, 0.6f), (0.11f, 0.85f), (0.0001f, 0.85f)], 8), lp, new Color(0.12f, 0.12f, 0.13f, 1f));
            Shape(scene, shader, "~Lamp collar", Meshes.Lathe("tlcol", [(0.06f, -0.04f), (0.1f, -0.02f), (0.1f, 0.02f), (0.06f, 0.04f)], 20), lp + new Float3(0f, 3.75f, 0f), new Color(0.12f, 0.12f, 0.13f, 1f));
            Shape(scene, shader, "~Lamp ladder", Meshes.RoundedBox(new Float3(0.7f, 0.04f, 0.04f), 0.012f), lp + new Float3(0f, 3.55f, 0f), new Color(0.12f, 0.12f, 0.13f, 1f));
            Shape(scene, shader, "~Lamp head", Meshes.Merge(
                Meshes.Lathe("tlbase", [(0.04f, -0.04f), (0.16f, -0.02f), (0.17f, 0.02f), (0.0001f, 0.03f)], 4),
                Meshes.Lathe("tlcap", [(0.0001f, 0.33f), (0.24f, 0.33f), (0.25f, 0.35f), (0.12f, 0.46f), (0.03f, 0.5f), (0.03f, 0.56f), (0.0001f, 0.58f)], 4),
                Meshes.RoundedBox(new Float3(0.025f, 0.32f, 0.025f), 0.006f) is var lpost ? Meshes.Merge(Off(lpost, 0.12f, 0.17f, 0f), Off(lpost, -0.12f, 0.17f, 0f), Off(lpost, 0f, 0.17f, 0.12f), Off(lpost, 0f, 0.17f, -0.12f)) : null!),
                lp + new Float3(0f, 3.95f, 0f), new Color(0.12f, 0.12f, 0.13f, 1f), euler: new Float3(0f, 45f, 0f));
            Shape(scene, shader, "~Lamp glass", Meshes.Lathe("tlglass", [(0.0001f, 0f), (0.1f, 0f), (0.15f, 0.31f), (0.0001f, 0.31f)], 4), lp + new Float3(0f, 3.97f, 0f), new Color(1f, 0.92f, 0.70f, 1f), euler: new Float3(0f, 45f, 0f));
        }
        // Laws board (north edge of the plaza, facing spawn).
        Box(scene, shader, "Board posts", new Float3(0f, 1.1f, 12.5f), new Float3(4.4f, 2.2f, 0.18f), Float3.Zero, new Color(0.30f, 0.22f, 0.15f, 1f)).GetComponent<MeshRenderer>()!.Enabled = false;
        // Visible: two turned posts with ball finials, a framed back panel and a little roof over the notice.
        var post = new Color(0.30f, 0.22f, 0.15f, 1f);
        foreach (float px in new[] { -2.2f, 2.2f })
        {
            Shape(scene, shader, "~Board post", Meshes.Lathe("bpost", [(0.0001f, 0f), (0.14f, 0f), (0.14f, 0.12f), (0.1f, 0.2f), (0.09f, 3.3f), (0.12f, 3.36f), (0.12f, 3.42f), (0.0001f, 3.44f)], 20), new Float3(px, 0f, 12.5f), post);
            Shape(scene, shader, "~Board finial", Meshes.Sphere(0.1f), new Float3(px, 3.55f, 12.5f), post);
        }
        Shape(scene, shader, "~Board back", PropMeshes.InsetPanel(4.3f, 2.6f, 0.12f, 0.12f), new Float3(0f, 2.05f, 12.48f), post, euler: new Float3(0f, 180f, 0f));
        Shape(scene, shader, "~Board roof", Meshes.Sweep([new Meshes.Sec(new Float3(0f, 3.5f, 12.15f), 2.5f, 0.04f, 4f), new Meshes.Sec(new Float3(0f, 3.7f, 12.6f), 2.5f, 0.04f, 4f), new Meshes.Sec(new Float3(0f, 3.5f, 12.95f), 2.5f, 0.04f, 4f)], Float3.UnitX, 12, 0.3f), Float3.Zero, new Color(0.36f, 0.22f, 0.18f, 1f));
        GameObject board = Box(scene, shader, "~Laws board", new Float3(0f, 2.0f, 12.38f), new Float3(4f, 2.2f, 0.06f), Float3.Zero, new Color(0.10f, 0.13f, 0.17f, 1f));
        board.AddComponent<LawsBoard>();
        Town.Interactive.Add((board, new Float3(4f, 2.2f, 0.06f)));
        Town.Board = board;
        Box(scene, shader, "~Board header", new Float3(0f, 3.25f, 12.36f), new Float3(4f, 0.3f, 0.06f), Float3.Zero, new Color(0.78f, 0.22f, 0.22f, 1f), collide: false);
        // ATM (east edge of the plaza).
        GameObject atm = Box(scene, shader, "ATM", new Float3(15.6f, 1.0f, -4f), new Float3(0.8f, 2.0f, 0.9f), Float3.Zero, new Color(0.24f, 0.30f, 0.40f, 1f));
        atm.GetComponent<MeshRenderer>()!.Mesh = Meshes.RoundedBox(new Float3(0.8f, 2.0f, 0.9f), 0.12f, 4);
        // Kiosk detail: recessed screen bezel, keypad, card slot, cash slot and a hood over the screen.
        var kdark = new Color(0.1f, 0.12f, 0.15f, 1f);
        Shape(scene, shader, "~ATM bezel", PropMeshes.InsetPanel(0.72f, 0.62f, 0.05f, 0.06f), new Float3(15.2f, 1.35f, -4f), kdark, euler: new Float3(0f, -90f, 0f));
        Shape(scene, shader, "~ATM hood", Meshes.RoundedBox(new Float3(0.28f, 0.04f, 0.78f), 0.015f), new Float3(15.08f, 1.72f, -4f), kdark, euler: new Float3(0f, 0f, 14f));
        Shape(scene, shader, "~ATM shelf", Meshes.RoundedBox(new Float3(0.3f, 0.05f, 0.7f), 0.02f), new Float3(15.08f, 0.98f, -4f), new Color(0.6f, 0.62f, 0.65f, 1f), euler: new Float3(0f, 0f, -12f));
        for (int ky = 0; ky < 4; ky++)
            for (int kx = 0; kx < 3; kx++)
                Shape(scene, shader, "~ATM key", Meshes.RoundedBox(new Float3(0.03f, 0.03f, 0.05f), 0.01f), new Float3(15.02f + ky * 0.02f, 1.02f + ky * 0.005f, -4.06f + kx * 0.06f), new Color(0.75f, 0.76f, 0.78f, 1f), euler: new Float3(0f, 0f, -12f));
        Shape(scene, shader, "~ATM card", Meshes.RoundedBox(new Float3(0.02f, 0.03f, 0.12f), 0.008f), new Float3(15.19f, 1.05f, -3.75f), new Color(0.2f, 0.85f, 0.4f, 1f));
        Shape(scene, shader, "~ATM cash", Meshes.RoundedBox(new Float3(0.02f, 0.04f, 0.3f), 0.01f), new Float3(15.19f, 0.8f, -4f), kdark);
        atm.AddComponent<AtmMachine>();
        Town.Interactive.Add((atm, new Float3(0.8f, 2.0f, 0.9f)));
        Town.Atm = atm;
        Box(scene, shader, "~ATM screen", new Float3(15.18f, 1.35f, -4f), new Float3(0.04f, 0.45f, 0.6f), Float3.Zero, new Color(0.35f, 0.85f, 0.65f, 1f), collide: false);
        Box(scene, shader, "~ATM sign", new Float3(15.18f, 1.86f, -4f), new Float3(0.04f, 0.2f, 0.7f), Float3.Zero, new Color(0.30f, 0.65f, 0.95f, 1f), collide: false);

        // Buildings: (title, centre, size, yaw where the front faces the road, wall colour, door owner, price).
        var buildings = new (string Title, Float3 C, Float3 S, float Yaw, Color Wall, string Owner, int Price)[]
        {
            ("Police Station", new(30f, 0f, 28f), new(20f, 5f, 14f), 0f, new Color(0.62f, 0.68f, 0.80f, 1f), "police", 0),
            ("General Store", new(30f, 0f, -26f), new(16f, 4.2f, 12f), 180f, new Color(0.86f, 0.80f, 0.66f, 1f), "city", 0),
            ("House 1", new(-26f, 0f, 26f), new(11f, 4f, 10f), 0f, new Color(0.84f, 0.70f, 0.62f, 1f), "", 300),
            ("House 2", new(-46f, 0f, 26f), new(11f, 4f, 10f), 0f, new Color(0.70f, 0.78f, 0.70f, 1f), "", 350),
            ("House 3", new(-26f, 0f, -26f), new(11f, 4f, 10f), 180f, new Color(0.78f, 0.74f, 0.86f, 1f), "", 400),
            ("Apartment", new(-48f, 0f, -27f), new(14f, 7f, 12f), 180f, new Color(0.74f, 0.62f, 0.52f, 1f), "", 650),
            ("Garage", new(54f, 0f, -26f), new(12f, 4.5f, 12f), 180f, new Color(0.66f, 0.66f, 0.68f, 1f), "", 450),
        };
        foreach (var b in buildings) Building(scene, shader, b.Title, b.C, b.S, b.Yaw, b.Wall, b.Owner, b.Price);

        // Jail cell inside the police station (back-right corner), with bars and a locked police door.
        Float3 cell = new(36f, 0f, 31f);
        Town.CellCenter = cell; Town.CellHalf = new Float3(2.4f, 0f, 2.4f);
        Town.ReleasePoint = new Float3(30f, 0.2f, 17f);
        var bars = new Color(0.20f, 0.20f, 0.22f, 1f);
        for (int i = 0; i < 9; i++)
            Shape(scene, shader, "~Bar", Meshes.RoundedCylinder(0.04f, 2.8f, 0.012f, 16), new Float3(cell.X - 2.6f, 1.4f, cell.Z - 2.6f + 0.3f + i * 0.6f), bars);
        for (int k = 0; k < 2; k++)
            Box(scene, shader, "~BarRail", new Float3(cell.X - 2.6f, 0.3f + k * 2.3f, cell.Z), new Float3(0.07f, 0.07f, 5.2f), Float3.Zero, bars);
        Box(scene, shader, "Cell wall W", new Float3(cell.X - 2.6f, 1.4f, cell.Z), new Float3(0.1f, 2.8f, 5.2f), Float3.Zero, new Color(0, 0, 0, 0), collide: true).GetComponent<MeshRenderer>()!.Enabled = false;
        Box(scene, shader, "Cell wall S L", new Float3(cell.X - 1.7f, 1.4f, cell.Z - 2.6f), new Float3(1.8f, 2.8f, 0.2f), Float3.Zero, new Color(0.55f, 0.58f, 0.64f, 1f));
        Box(scene, shader, "Cell wall S R", new Float3(cell.X + 1.7f, 1.4f, cell.Z - 2.6f), new Float3(1.8f, 2.8f, 0.2f), Float3.Zero, new Color(0.55f, 0.58f, 0.64f, 1f));
        Box(scene, shader, "~Cell bench", new Float3(cell.X + 1.6f, 0.4f, cell.Z + 1.2f), new Float3(1.4f, 0.1f, 2.2f), Float3.Zero, bars);
        MakeDoor(scene, shader, "door_cell", "Jail Cell", new Float3(cell.X - 0.8f, 0f, cell.Z - 2.6f), 0f, 1.6f, 2.6f, bars, "police", 0, locked: true);
    }

    /// <summary>Four walls (front has a doorway), windows, roof with parapet, a sign plate and a door.</summary>
    private static void Building(Scene scene, Shader shader, string title, Float3 c, Float3 s, float yaw, Color wall, string owner, int price)
    {
        float r = yaw * MathF.PI / 180f;
        Float3 W(float lx, float ly, float lz) => c + new Float3(lx * MathF.Cos(r) + lz * MathF.Sin(r), ly, -lx * MathF.Sin(r) + lz * MathF.Cos(r));
        Float3 Sz(float x, float y, float z) => MathF.Abs(MathF.Sin(r)) > 0.5f ? new Float3(z, y, x) : new Float3(x, y, z);
        const float t = 0.3f, dw = 1.4f, dh = 2.5f;
        float hw = s.X / 2f, hd = s.Z / 2f, h = s.Y;
        string tag = title.Replace(" ", "");
        var trim = new Color(wall.R * 0.7f, wall.G * 0.7f, wall.B * 0.7f, 1f);
        var glass = new Color(0.40f, 0.55f, 0.68f, 1f);
        // Front wall faces local -Z (towards the road); doorway at local x = 0.
        float side = (s.X - dw) / 2f;
        Box(scene, shader, $"{tag} front L", W(-(dw / 2f + side / 2f), h / 2f, -hd), Sz(side, h, t), Float3.Zero, wall);
        Box(scene, shader, $"{tag} front R", W(dw / 2f + side / 2f, h / 2f, -hd), Sz(side, h, t), Float3.Zero, wall);
        Box(scene, shader, $"{tag} lintel", W(0f, dh + (h - dh) / 2f, -hd), Sz(dw, h - dh, t), Float3.Zero, wall);
        Box(scene, shader, $"{tag} back", W(0f, h / 2f, hd), Sz(s.X, h, t), Float3.Zero, wall);
        Box(scene, shader, $"{tag} side L", W(-hw, h / 2f, 0f), Sz(t, h, s.Z), Float3.Zero, wall);
        Box(scene, shader, $"{tag} side R", W(hw, h / 2f, 0f), Sz(t, h, s.Z), Float3.Zero, wall);
        Box(scene, shader, $"~{tag} floor", W(0f, 0.04f, 0f), Sz(s.X - 0.2f, 0.08f, s.Z - 0.2f), Float3.Zero, new Color(0.55f, 0.42f, 0.30f, 1f));
        Box(scene, shader, $"{tag} roof", W(0f, h + 0.1f, 0f), Sz(s.X + 0.6f, 0.2f, s.Z + 0.6f), Float3.Zero, trim);
        Box(scene, shader, $"~{tag} parapet", W(0f, h + 0.35f, -hd - 0.2f), Sz(s.X + 0.6f, 0.3f, 0.2f), Float3.Zero, trim, collide: false);
        Color plate = owner switch { "police" => new Color(0.20f, 0.36f, 0.85f, 1f), "city" => new Color(0.85f, 0.45f, 0.18f, 1f), _ => new Color(0.18f, 0.62f, 0.40f, 1f) };
        DressBuilding(scene, shader, tag, title, c, s, yaw, wall, trim, plate, dw, dh);
        if (owner == "police")
        {
            Box(scene, shader, $"~{tag} light R", W(-2.0f, dh + 1.35f, -hd - 0.24f), Sz(0.22f, 0.4f, 0.14f), Float3.Zero, new Color(0.95f, 0.2f, 0.2f, 1f), collide: false);
            Box(scene, shader, $"~{tag} light B", W(2.0f, dh + 1.35f, -hd - 0.24f), Sz(0.22f, 0.4f, 0.14f), Float3.Zero, new Color(0.2f, 0.4f, 1f, 1f), collide: false);
        }
        // Door casing and a cornice band under the roof.
        Box(scene, shader, $"~{tag} casing l", W(-dw / 2f - 0.07f, dh / 2f, -hd - 0.17f), Sz(0.14f, dh + 0.1f, 0.06f), Float3.Zero, Color.White, collide: false);
        Box(scene, shader, $"~{tag} casing r", W(dw / 2f + 0.07f, dh / 2f, -hd - 0.17f), Sz(0.14f, dh + 0.1f, 0.06f), Float3.Zero, Color.White, collide: false);
        Box(scene, shader, $"~{tag} casing t", W(0f, dh + 0.07f, -hd - 0.17f), Sz(dw + 0.28f, 0.14f, 0.06f), Float3.Zero, Color.White, collide: false);
        Box(scene, shader, $"~{tag} cornice", W(0f, h - 0.12f, -hd - 0.19f), Sz(s.X + 0.1f, 0.18f, 0.1f), Float3.Zero, trim, collide: false);
        Float3 eul = new(0f, yaw, 0f);
        if (title.StartsWith("House"))
        {
            // Pitched gable roof with eaves overhang, gable-end walls, fascia boards and a brick chimney.
            float ov = 0.5f, rise = 2.2f, ry = h + 0.2f, rx = hw + ov, rz = hd + ov;
            Float3 L(float x, float y, float z) => new(x, y, z);
            var roof = Meshes.Faces(
                [L(-rx, ry, -rz), L(rx, ry, -rz), L(rx, ry + rise, 0f), L(-rx, ry + rise, 0f)],
                [L(rx, ry, rz), L(-rx, ry, rz), L(-rx, ry + rise, 0f), L(rx, ry + rise, 0f)],
                [L(-rx, ry - 0.12f, -rz), L(-rx, ry, -rz), L(-rx, ry + rise, 0f), L(-rx, ry + rise - 0.12f, 0f)],
                [L(rx, ry, -rz), L(rx, ry - 0.12f, -rz), L(rx, ry + rise - 0.12f, 0f), L(rx, ry + rise, 0f)],
                [L(-rx, ry, rz), L(-rx, ry - 0.12f, rz), L(-rx, ry + rise - 0.12f, 0f), L(-rx, ry + rise, 0f)],
                [L(rx, ry - 0.12f, rz), L(rx, ry, rz), L(rx, ry + rise, 0f), L(rx, ry + rise - 0.12f, 0f)],
                [L(-rx, ry - 0.12f, -rz), L(-rx, ry + rise - 0.12f, 0f), L(rx, ry + rise - 0.12f, 0f), L(rx, ry - 0.12f, -rz)],
                [L(rx, ry - 0.12f, rz), L(rx, ry + rise - 0.12f, 0f), L(-rx, ry + rise - 0.12f, 0f), L(-rx, ry - 0.12f, rz)],
                [L(-rx, ry - 0.12f, -rz), L(rx, ry - 0.12f, -rz), L(rx, ry, -rz), L(-rx, ry, -rz)],
                [L(rx, ry - 0.12f, rz), L(-rx, ry - 0.12f, rz), L(-rx, ry, rz), L(rx, ry, rz)]);
            var shingle = new Color(0.36f, 0.22f, 0.18f, 1f);
            Shape(scene, shader, $"~{tag} pitched roof", roof, c, shingle, euler: eul);
            {
                // Shingle courses: a shadow line every ~30 cm up both slopes, with staggered butt joints on alternate rows.
                var courses = new List<Mesh>();
                int nk = 9;
                for (int k = 1; k < nk; k++)
                {
                    float tk = k / (float)nk, yy = ry + rise * tk + 0.012f, zz = rz * (1f - tk);
                    foreach (float sg in new[] { -1f, 1f })
                    {
                        courses.Add(Off(Meshes.RoundedBox(new Float3(rx * 2f, 0.03f, 0.05f), 0.01f), 0f, yy, sg * zz));
                        for (float jx = -rx + (k % 2 == 0 ? 0.45f : 0.9f); jx < rx; jx += 0.9f)
                            courses.Add(Off(Meshes.RoundedBox(new Float3(0.025f, 0.02f, rz / nk * 1.05f), 0.006f), jx, yy - rise / nk * 0.5f + 0.004f, sg * (zz + rz / nk * 0.5f)));
                    }
                }
                Shape(scene, shader, $"~{tag} shingle courses", Meshes.Merge(courses.ToArray()), c, new Color(0.28f, 0.17f, 0.14f, 1f), euler: eul);
            }
            var gable = Meshes.Faces([L(-hw, ry, -hd + 0.05f), L(-hw, ry, hd - 0.05f), L(-hw, ry + rise * (1f - ov / rz) , 0f)], [L(hw, ry, hd - 0.05f), L(hw, ry, -hd + 0.05f), L(hw, ry + rise * (1f - ov / rz), 0f)]);
            Shape(scene, shader, $"~{tag} gables", gable, c, wall, euler: eul);
            Shape(scene, shader, $"~{tag} ridge", Meshes.Sweep([new Meshes.Sec(L(-rx - 0.05f, ry + rise + 0.02f, 0f), 0.09f, 0.07f, 2.4f, Float3.UnitY), new Meshes.Sec(L(rx + 0.05f, ry + rise + 0.02f, 0f), 0.09f, 0.07f, 2.4f, Float3.UnitY)], Float3.UnitY, 12, 0.5f), c, trim, euler: eul);
            Shape(scene, shader, $"~{tag} chimney", Meshes.RoundedBox(new Float3(0.7f, 2.2f, 0.7f), 0.04f), W(hw * 0.55f, ry + rise * 0.55f + 0.6f, hd * 0.35f), new Color(0.55f, 0.3f, 0.24f, 1f), euler: eul);
            Shape(scene, shader, $"~{tag} chimney cap", Meshes.RoundedBox(new Float3(0.86f, 0.12f, 0.86f), 0.03f), W(hw * 0.55f, ry + rise * 0.55f + 1.76f, hd * 0.35f), trim, euler: eul);
            // Porch canopy on brackets over the door.
            {
                // Porch canopy: a solid shingled slab on a fascia board, carried on two turned brackets off the facade.
                float th = MathF.Atan2(0.4f, 1.1f), cs = MathF.Cos(th), sn = MathF.Sin(th), fz = -hd - 0.15f;
                Mesh Tilt(Mesh m, Float3 o) { var mm = new Mesh { IndexFormat = m.IndexFormat, Vertices = m.Vertices.Select(v => new Float3(v.X, v.Y * cs + v.Z * sn, -v.Y * sn + v.Z * cs) + o).ToArray(), Normals = m.Normals.Select(v => new Float3(v.X, v.Y * cs + v.Z * sn, -v.Y * sn + v.Z * cs)).ToArray(), UV = m.UV, Indices = m.Indices }; mm.RecalculateBounds(); return mm; }
                Float3 co = L(0f, dh + 0.72f, fz - 0.56f);
                Shape(scene, shader, $"~{tag} canopy", Tilt(Meshes.RoundedBox(new Float3(2.8f, 0.07f, 1.2f), 0.02f), co), c, shingle, euler: eul);
                Shape(scene, shader, $"~{tag} canopy fascia", Meshes.Merge(Tilt(Meshes.RoundedBox(new Float3(2.84f, 0.16f, 0.04f), 0.012f), co + new Float3(0f, -0.11f, -0.56f)), Tilt(Meshes.RoundedBox(new Float3(2.7f, 0.04f, 1.1f), 0.01f), co + new Float3(0f, -0.06f, 0f))), c, Color.White, euler: eul);
                foreach (float bx in new[] { -1.2f, 1.2f })
                    Shape(scene, shader, $"~{tag} bracket", Meshes.Merge(PropMeshes.Rod(L(bx, dh + 0.05f, fz - 0.02f), L(bx, dh + 0.5f, fz - 0.95f), 0.035f), PropMeshes.Rod(L(bx, dh + 0.62f, fz - 0.02f), L(bx, dh + 0.5f, fz - 0.95f), 0.03f), Meshes.RoundedBox(new Float3(0.1f, 0.7f, 0.05f), 0.015f) is var pl ? Off(pl, bx, dh + 0.35f, fz - 0.02f) : null!), c, Color.White, euler: eul);
                // Front step and a door mat.
                Shape(scene, shader, $"~{tag} step", Meshes.RoundedBox(new Float3(2.0f, 0.16f, 0.7f), 0.03f), W(0f, 0.08f, fz - 0.35f), new Color(0.7f, 0.68f, 0.64f, 1f), euler: eul);
                Shape(scene, shader, $"~{tag} mat", Meshes.RoundedBox(new Float3(1.0f, 0.02f, 0.5f), 0.008f), W(0f, 0.17f, fz - 0.33f), new Color(0.35f, 0.24f, 0.16f, 1f), euler: eul);
            }
        }
        else
        {
            // Flat-roof commercial: coping all round, a mid-height string course, rooftop AC unit and vents.
            foreach (var (px, pz, sx, sz) in new[] { (0f, hd + 0.2f, s.X + 0.6f, 0.2f), (-hw - 0.2f, 0f, 0.2f, s.Z + 0.6f), (hw + 0.2f, 0f, 0.2f, s.Z + 0.6f) })
                Box(scene, shader, $"~{tag} parapet", W(px, h + 0.35f, pz), Sz(sx, 0.3f, sz), Float3.Zero, trim, collide: false);
            Shape(scene, shader, $"~{tag} coping", Meshes.Sweep([new Meshes.Sec(new Float3(-hw - 0.35f, h + 0.52f, -hd - 0.35f), 0.03f, 0.05f, 2.4f), new Meshes.Sec(new Float3(hw + 0.35f, h + 0.52f, -hd - 0.35f), 0.03f, 0.05f, 2.4f)], Float3.UnitY, 10, 0.5f), c, new Color(0.85f, 0.85f, 0.82f, 1f), euler: eul);
                Shape(scene, shader, $"~{tag} ac", Meshes.RoundedBox(new Float3(1.6f, 0.9f, 1.1f), 0.08f), W(hw * 0.4f, h + 0.65f, hd * 0.3f), new Color(0.72f, 0.73f, 0.74f, 1f), euler: eul);
            Shape(scene, shader, $"~{tag} ac fan", Meshes.Lathe("acfan", [(0.0001f, 0f), (0.36f, 0f), (0.38f, 0.03f), (0.36f, 0.05f), (0.0001f, 0.05f)], 28), W(hw * 0.4f, h + 1.1f, hd * 0.3f), new Color(0.25f, 0.26f, 0.27f, 1f), euler: eul);
            foreach (float vx in new[] { -hw * 0.5f, -hw * 0.3f })
                Shape(scene, shader, $"~{tag} vent", Meshes.Lathe("vent", [(0.0001f, 0f), (0.14f, 0f), (0.14f, 0.5f), (0.2f, 0.55f), (0.2f, 0.62f), (0.0001f, 0.66f)], 20), W(vx, h + 0.2f, -hd * 0.2f), new Color(0.6f, 0.6f, 0.62f, 1f), euler: eul);
            // Awning over the entrance.
            Shape(scene, shader, $"~{tag} awning", Meshes.Sweep([new Meshes.Sec(new Float3(0f, dh + 0.85f, -hd - 0.15f), 1.7f, 0.04f, 4f), new Meshes.Sec(new Float3(0f, dh + 0.5f, -hd - 1.2f), 1.7f, 0.04f, 4f)], Float3.UnitX, 12, 0.3f), c, plate, euler: eul);
        }
        // Path from the door to the sidewalk.
        // Path from the door to the sidewalk: five concrete flags with dark joints on a gravel bed.
        Box(scene, shader, $"~{tag} path", W(0f, 0.04f, -hd - 2.5f), Sz(2.1f, 0.08f, 5f), Float3.Zero, new Color(0.3f, 0.29f, 0.27f, 1f));
        for (int i = 0; i < 5; i++)
            Box(scene, shader, $"~{tag} flag", W(0f, 0.055f, -hd - 0.5f - i), Sz(1.94f, 0.1f, 0.94f), Float3.Zero, i % 2 == 0 ? new Color(0.55f, 0.53f, 0.5f, 1f) : new Color(0.51f, 0.5f, 0.47f, 1f));
        MakeDoor(scene, shader, "door_" + tag.ToLowerInvariant(), title, W(-dw / 2f + 0.05f, 0f, -hd), yaw, dw - 0.1f, dh - 0.05f, new Color(0.42f, 0.28f, 0.18f, 1f), owner, price, locked: false);
    }

    private static void MakeDoor(Scene scene, Shader shader, string id, string title, Float3 hinge, float yaw, float width, float height, Color color, string owner, int price, bool locked)
    {
        GameObject go = Box(scene, shader, $"~Door {title}", hinge, new Float3(width, height, 0.1f), Float3.Zero, color);
        // Raised panels and brass knobs (visual children swing with the door).
        void Child(string n, Mesh m, Float3 lp, Color c)
        {
            var k = new GameObject(n); k.SetParent(go, false); k.Transform.LocalPosition = lp;
            var r = k.AddComponent<MeshRenderer>(); r.Mesh = m; var mt = Mats.New(shader); mt.SetColor("_MainColor", c); r.Material = mt;
        }
        var panel = Meshes.RoundedBox(new Float3(width * 0.62f, height * 0.36f, 0.03f), 0.012f, 3);
        var dark = new Color(color.R * 0.85f, color.G * 0.85f, color.B * 0.85f, 1f);
        foreach (float zz in new[] { -0.055f, 0.055f })
        {
            Child("DoorPanel", panel, new Float3(-width * 0.04f, height * 0.2f, zz), dark);
            Child("DoorPanel", panel, new Float3(-width * 0.04f, -height * 0.22f, zz), dark);
            Child("DoorKnob", Meshes.Sphere(0.035f), new Float3(width * 0.4f, -0.08f, zz * 1.5f), new Color(0.78f, 0.62f, 0.25f, 1f));
        }
        Door d = go.AddComponent<Door>();
        d.Id = id; d.Title = title; d.Owner = owner; d.Price = price; d.Locked = locked || (owner == "" && price > 0);
        d.Jobs = owner == "police" ? ["police"] : owner == "city" && title.Contains("Hall") ? ["mayor"] : [];
        d.Hinge = hinge + new Float3(0f, height / 2f, 0f); d.Yaw = yaw; d.Width = width;
        d.Pose();
        d.Rebuild();
        Town.Doors.Add(d);
        Town.Interactive.Add((go, new Float3(width, height, 0.1f)));
    }
    private static Mesh Off(Mesh m, float x, float y, float z)
    {
        var mm = new Mesh { IndexFormat = m.IndexFormat, Vertices = m.Vertices.Select(v => v + new Float3(x, y, z)).ToArray(), Normals = m.Normals, UV = m.UV, Indices = m.Indices };
        mm.RecalculateBounds(); return mm;
    }
}
