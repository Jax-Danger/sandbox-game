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
        _ => Price > 0 ? $"For sale · ${Price:N0}" : "",
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
        var walk = new Color(0.66f, 0.65f, 0.62f, 1f);
        var paver = new Color(0.72f, 0.66f, 0.58f, 1f);
        var line = new Color(0.92f, 0.80f, 0.30f, 1f);

        // Ground.
        GameObject ground = new("Ground");
        ground.Transform.Position = new Float3(0f, -0.5f, 0f);
        MeshRenderer gr = ground.AddComponent<MeshRenderer>();
        gr.Mesh = Mesh.CreateCube(new Float3(200f, 1f, 200f));
        var gm = Mats.New(shader);
        gm.SetColor("_MainColor", new Color(0.48f, 0.64f, 0.38f, 1f));
        if (!Application.IsHeadless && Environment.GetEnvironmentVariable("SANDBOX_PROCTEX") != "0") { gm.SetTexture("_MainTex", ProcTex.Grass()); gm.SetVector("_Tiling", new Float2(50f, 50f)); }
        gr.Material = gm;
        ground.AddComponent<BoxCollider>().Size = new Float3(200f, 1f, 200f);
        Add(scene, ground);

        // Plaza (centre) and four avenues running out of it, with sidewalks and centre lines.
        Box(scene, shader, "Plaza", new Float3(0f, 0.03f, 0f), new Float3(34f, 0.06f, 34f), Float3.Zero, paver);
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
        Shape(scene, shader, "~Fountain water", Meshes.RoundedCylinder(2.4f, 0.04f, 0.01f, 48), new Float3(0f, 0.67f, 4f), new Color(0.12f, 0.32f, 0.48f, 1f));
        Shape(scene, shader, "Fountain column", Meshes.Lathe("fcol", [(0.0001f, -0.7f), (0.4f, -0.7f), (0.3f, -0.55f), (0.22f, -0.2f), (0.2f, 0.4f), (0.55f, 0.55f), (0.6f, 0.62f), (0.5f, 0.66f), (0.12f, 0.7f), (0.0001f, 0.72f)], 32), new Float3(0f, 1.3f, 4f), stone, new Float3(0.6f, 1.4f, 0.6f));
        foreach (var (x, z, yaw) in new[] { (-7f, 4f, 90f), (7f, 4f, 90f), (-4f, -12f, 0f), (4f, -12f, 0f) })
        {
            Box(scene, shader, "Bench seat", new Float3(x, 0.45f, z), yaw == 0f ? new Float3(2.2f, 0.12f, 0.6f) : new Float3(0.6f, 0.12f, 2.2f), Float3.Zero, new Color(0.55f, 0.38f, 0.22f, 1f));
            Box(scene, shader, "~Bench legs", new Float3(x, 0.2f, z), yaw == 0f ? new Float3(1.9f, 0.4f, 0.4f) : new Float3(0.4f, 0.4f, 1.9f), Float3.Zero, new Color(0.15f, 0.15f, 0.16f, 1f));
        }
        foreach (var (x, z) in new[] { (-14f, -14f), (14f, -14f), (-14f, 14f), (14f, 14f) })
        {
            Shape(scene, shader, "Tree trunk", Meshes.Lathe("trunk", [(0.0001f, -1.2f), (0.34f, -1.2f), (0.24f, -0.9f), (0.2f, 0f), (0.17f, 1.0f), (0.12f, 1.4f), (0.0001f, 1.45f)], 20), new Float3(x, 1.2f, z), new Color(0.40f, 0.28f, 0.18f, 1f), new Float3(0.4f, 2.4f, 0.4f));
            Shape(scene, shader, "~Tree crown", Crown(0), new Float3(x, 3.4f, z), new Color(0.28f, 0.52f, 0.26f, 1f));
            Shape(scene, shader, "~Tree crown b", Meshes.Sphere(0.85f), new Float3(x + 0.8f, 3.0f, z - 0.5f), new Color(0.24f, 0.47f, 0.23f, 1f));
            Shape(scene, shader, "~Tree crown c", Meshes.Sphere(0.75f), new Float3(x - 0.7f, 3.1f, z + 0.6f), new Color(0.31f, 0.56f, 0.28f, 1f));
            Shape(scene, shader, "Lamp post", PropMeshes.Post(0.07f, 4f), new Float3(x * 0.7f, 2f, z * 0.7f), new Color(0.12f, 0.12f, 0.13f, 1f), new Float3(0.14f, 4f, 0.14f));
            Shape(scene, shader, "~Lamp head", Meshes.Lathe("lantern", [(0.05f, -0.2f), (0.2f, -0.12f), (0.22f, 0.02f), (0.3f, 0.06f), (0.26f, 0.12f), (0.08f, 0.2f), (0.0001f, 0.24f)], 8), new Float3(x * 0.7f, 4.15f, z * 0.7f), new Color(0.15f, 0.15f, 0.16f, 1f));
            Shape(scene, shader, "~Lamp glass", Meshes.Sphere(0.15f), new Float3(x * 0.7f, 4.12f, z * 0.7f), new Color(1f, 0.92f, 0.70f, 1f));
        }
        // Laws board (north edge of the plaza, facing spawn).
        Box(scene, shader, "Board posts", new Float3(0f, 1.1f, 12.5f), new Float3(4.4f, 2.2f, 0.18f), Float3.Zero, new Color(0.30f, 0.22f, 0.15f, 1f));
        GameObject board = Box(scene, shader, "~Laws board", new Float3(0f, 2.0f, 12.38f), new Float3(4f, 2.2f, 0.06f), Float3.Zero, new Color(0.10f, 0.13f, 0.17f, 1f));
        board.AddComponent<LawsBoard>();
        Town.Interactive.Add((board, new Float3(4f, 2.2f, 0.06f)));
        Town.Board = board;
        Box(scene, shader, "~Board header", new Float3(0f, 3.25f, 12.36f), new Float3(4f, 0.3f, 0.06f), Float3.Zero, new Color(0.78f, 0.22f, 0.22f, 1f), collide: false);
        // ATM (east edge of the plaza).
        GameObject atm = Box(scene, shader, "ATM", new Float3(15.6f, 1.0f, -4f), new Float3(0.8f, 2.0f, 0.9f), Float3.Zero, new Color(0.24f, 0.30f, 0.40f, 1f));
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
            Box(scene, shader, "~Bar", new Float3(cell.X - 2.6f + 0f, 1.4f, cell.Z - 2.6f + 0.3f + i * 0.6f), new Float3(0.08f, 2.8f, 0.08f), Float3.Zero, bars);
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
        Box(scene, shader, $"~{tag} base", W(0f, 0.2f, -hd - 0.17f), Sz(s.X, 0.4f, 0.06f), Float3.Zero, trim, collide: false);
        // Windows on the front and sides.
        int floors = h > 6f ? 2 : 1;
        for (int f = 0; f < floors; f++)
        {
            float wy = 1.6f + f * 3f;
            foreach (float wx in new[] { -hw * 0.6f, hw * 0.6f })
                Box(scene, shader, $"~{tag} window", W(wx, wy, -hd - 0.16f), Sz(1.6f, 1.1f, 0.04f), Float3.Zero, glass, collide: false);
            foreach (float sx in new[] { -hw - 0.16f, hw + 0.16f })
                Box(scene, shader, $"~{tag} side window", W(sx, wy, 0f), Sz(0.04f, 1.1f, 1.6f), Float3.Zero, glass, collide: false);
        }
        // Sign plate over the door (the colour tells the building's role).
        Color plate = owner switch { "police" => new Color(0.20f, 0.36f, 0.85f, 1f), "city" => new Color(0.85f, 0.45f, 0.18f, 1f), _ => new Color(0.18f, 0.62f, 0.40f, 1f) };
        Box(scene, shader, $"~{tag} sign", W(0f, dh + 0.45f, -hd - 0.18f), Sz(2.4f, 0.5f, 0.05f), Float3.Zero, plate, collide: false);
        if (owner == "police")
        {
            Box(scene, shader, $"~{tag} light R", W(-1.2f, dh + 0.9f, -hd - 0.2f), Sz(0.3f, 0.2f, 0.1f), Float3.Zero, new Color(0.95f, 0.2f, 0.2f, 1f), collide: false);
            Box(scene, shader, $"~{tag} light B", W(1.2f, dh + 0.9f, -hd - 0.2f), Sz(0.3f, 0.2f, 0.1f), Float3.Zero, new Color(0.2f, 0.4f, 1f, 1f), collide: false);
        }
        // Path from the door to the sidewalk.
        Box(scene, shader, $"~{tag} path", W(0f, 0.05f, -hd - 2.5f), Sz(2f, 0.1f, 5f), Float3.Zero, new Color(0.64f, 0.62f, 0.58f, 1f));
        MakeDoor(scene, shader, "door_" + tag.ToLowerInvariant(), title, W(-dw / 2f + 0.05f, 0f, -hd), yaw, dw - 0.1f, dh - 0.05f, new Color(0.42f, 0.28f, 0.18f, 1f), owner, price, locked: false);
    }

    private static void MakeDoor(Scene scene, Shader shader, string id, string title, Float3 hinge, float yaw, float width, float height, Color color, string owner, int price, bool locked)
    {
        GameObject go = Box(scene, shader, $"~Door {title}", hinge, new Float3(width, height, 0.1f), Float3.Zero, color);
        Door d = go.AddComponent<Door>();
        d.Id = id; d.Title = title; d.Owner = owner; d.Price = price; d.Locked = locked;
        d.Hinge = hinge + new Float3(0f, height / 2f, 0f); d.Yaw = yaw; d.Width = width;
        d.Pose();
        d.Rebuild();
        Town.Doors.Add(d);
        Town.Interactive.Add((go, new Float3(width, height, 0.1f)));
    }
}
