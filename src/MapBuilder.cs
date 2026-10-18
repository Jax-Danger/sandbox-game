using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// Builds the static world. Maps are rebuildable (every session picks one): "flatgrass" (the test arena),
/// "hills" (outdoor terrain with a lake) and "custom" (a user model imported with Prowl.Clay, with static mesh colliders).
/// </summary>
public static partial class MapBuilder
{
    public static readonly (string Id, string Name, string Description)[] Maps =
    [
        ("flatgrass", "Flatgrass", "Classic flat arena with ramps, steps and walls."),
        ("hills", "Rolling Hills", "Outdoor terrain with hills and a lake."),
        ("town", "RP Town", "Streets, a spawn plaza, police station, shops and houses with doors."),
        ("custom", "Custom map", "Import your own model (glb, gltf, fbx, obj, vrm)."),
    ];
    public static readonly string[] ImportExtensions = [".glb", ".gltf", ".fbx", ".obj", ".vrm"];

    private static readonly List<GameObject> _objects = [];
    public static string Current { get; private set; } = "flatgrass";
    /// <summary>Where the player should appear on this map.</summary>
    public static Float3 SpawnPoint { get; private set; } = new(0f, 0.1f, -10f);

    /// <summary>Approximate walkable ground height for NPC steering.</summary>
    public static float GroundHeight(float x, float z, float fallback) => Current switch
    {
        "hills" => HillsHeight(x, z),
        "flatgrass" => 0f,
        "town" => 0f,
        _ => fallback,
    };

    public static DirectionalLight CreateSun(Scene scene)
    {
        GameObject lightGo = new("Directional Light");
        DirectionalLight light = lightGo.AddComponent<DirectionalLight>();
        light.Color = Color.White;
        light.Intensity = 1.4f;
        light.ShadowQuality = ShadowQuality.Soft;
        lightGo.Transform.LocalEulerAngles = new Float3(-50f, 30f, 0f);
        scene.Add(lightGo);
        return light;
    }

    /// <summary>Back-compat: sun + default map.</summary>
    public static DirectionalLight Build(Scene scene, Shader shader)
    {
        DirectionalLight sun = CreateSun(scene);
        Load(scene, shader, "flatgrass", null);
        return sun;
    }

    public static void Clear(Scene scene)
    {
        foreach (GameObject go in _objects) scene.Remove(go);
        _objects.Clear();
        _batch.Clear();
    }

    /// <summary>Replaces the current map. Returns an error message for a failed custom import (falls back to Flatgrass).</summary>
    public static string? Load(Scene scene, Shader shader, string map, string? customFile)
    {
        Clear(scene);
        Town.Reset();
        Water.Clear();
        SpawnPoints.Clear();
        Current = map;
        string? error = null;
        switch (map)
        {
            case "hills": BuildHills(scene, shader); break;
            case "town": BuildTown(scene, shader); break;
            case "custom":
                error = BuildCustom(scene, shader, customFile);
                if (error is not null) { Current = "flatgrass"; BuildFlatgrass(scene, shader); }
                break;
            default: Current = "flatgrass"; BuildFlatgrass(scene, shader); break;
        }
        BuildBatches(scene, shader);
        return error;
    }

    private static void BuildFlatgrass(Scene scene, Shader shader)
    {
        SpawnPoint = new Float3(0f, 0.1f, -10f);
        var floor = new Color(0.78f, 0.80f, 0.82f, 1f);
        var concrete = new Color(0.62f, 0.62f, 0.60f, 1f);
        var accent = new Color(0.80f, 0.55f, 0.25f, 1f);

        Box(scene, shader, "Floor", new Float3(0f, -0.5f, 0f), new Float3(120f, 1f, 120f), Float3.Zero, floor);
        const float half = 60f, h = 4f;
        Box(scene, shader, "Wall N", new Float3(0f, h / 2f, half), new Float3(120f, h, 1f), Float3.Zero, concrete);
        Box(scene, shader, "Wall S", new Float3(0f, h / 2f, -half), new Float3(120f, h, 1f), Float3.Zero, concrete);
        Box(scene, shader, "Wall E", new Float3(half, h / 2f, 0f), new Float3(1f, h, 120f), Float3.Zero, concrete);
        Box(scene, shader, "Wall W", new Float3(-half, h / 2f, 0f), new Float3(1f, h, 120f), Float3.Zero, concrete);
        Ramp(scene, shader, "Ramp gentle", new Float3(-14f, 1.2f, 10f), new Float3(5f, 0.4f, 10f), new Float3(-14f, 0f, 0f), accent);
        Ramp(scene, shader, "Ramp steep", new Float3(14f, 2.2f, 10f), new Float3(5f, 0.4f, 10f), new Float3(-28f, 0f, 0f), accent);
        Ramp(scene, shader, "Ramp side", new Float3(0f, 1.5f, 24f), new Float3(10f, 0.4f, 5f), new Float3(0f, 0f, 18f), accent);
        Box(scene, shader, "Platform", new Float3(-14f, 2.2f, 19f), new Float3(8f, 0.6f, 8f), Float3.Zero, concrete);
        Box(scene, shader, "Wall mid", new Float3(10f, 1.5f, -12f), new Float3(12f, 3f, 0.6f), Float3.Zero, concrete);
        // Coping caps with a drip edge on every wall top, and a plinth on the mid wall.
        var cap = new Color(0.72f, 0.72f, 0.70f, 1f);
        foreach (var (p, sz) in new[] { (new Float3(0f, h, half), new Float3(120f, 0.16f, 1.3f)), (new Float3(0f, h, -half), new Float3(120f, 0.16f, 1.3f)), (new Float3(half, h, 0f), new Float3(1.3f, 0.16f, 120f)), (new Float3(-half, h, 0f), new Float3(1.3f, 0.16f, 120f)), (new Float3(10f, 3f, -12f), new Float3(12.3f, 0.14f, 0.85f)) })
            Shape(scene, shader, "~Coping", Meshes.RoundedBox(sz, 0.05f), p + new Float3(0f, 0.08f, 0f), cap);
        Shape(scene, shader, "~Plinth", Meshes.RoundedBox(new Float3(12.2f, 0.25f, 0.8f), 0.05f), new Float3(10f, 0.125f, -12f), cap);
        {
            // Perimeter walls read as poured panels: pilasters every 12 m, a formwork joint every 3 m, a dark skirting and
            // a painted orange band at shoulder height on the inside faces.
            var pil = new List<Mesh>(); var joint = new List<Mesh>(); var skirt = new List<Mesh>(); var band = new List<Mesh>();
            foreach (var (nx, nz) in new[] { (0f, -1f), (0f, 1f), (-1f, 0f), (1f, 0f) })
            {
                bool alongX = nx == 0f; float wallC = (alongX ? -nz : -nx) * half, face = wallC + (alongX ? nz : nx) * 0.5f;
                Float3 P(float t, float y, float d) => alongX ? new Float3(t, y, face + nz * d) : new Float3(face + nx * d, y, t);
                Float3 S(float along, float y, float d) => alongX ? new Float3(along, y, d) : new Float3(d, y, along);
                for (float t = -54f; t <= 54.1f; t += 12f) pil.Add(Off(Meshes.RoundedBox(S(0.7f, h, 0.3f), 0.05f), P(t, h / 2f, 0.15f).X, h / 2f, P(t, h / 2f, 0.15f).Z));
                for (float t = -58.5f; t <= 58.6f; t += 3f) joint.Add(Off(Meshes.RoundedBox(S(0.05f, h - 0.4f, 0.03f), 0.01f), P(t, 0f, 0.01f).X, h / 2f + 0.1f, P(t, 0f, 0.01f).Z));
                skirt.Add(Off(Meshes.RoundedBox(S(118.5f, 0.3f, 0.08f), 0.02f), P(0f, 0f, 0.04f).X, 0.15f, P(0f, 0f, 0.04f).Z));
                band.Add(Off(Meshes.RoundedBox(S(118.5f, 0.22f, 0.02f), 0.005f), P(0f, 0f, 0.01f).X, 1.5f, P(0f, 0f, 0.01f).Z));
            }
            Shape(scene, shader, "~Wall pilasters", Meshes.Merge(pil.ToArray()), Float3.Zero, new Color(0.66f, 0.66f, 0.64f, 1f));
            Shape(scene, shader, "~Wall joints", Meshes.Merge(joint.ToArray()), Float3.Zero, new Color(0.45f, 0.45f, 0.44f, 1f));
            Shape(scene, shader, "~Wall skirting", Meshes.Merge(skirt.ToArray()), Float3.Zero, new Color(0.3f, 0.3f, 0.31f, 1f));
            Shape(scene, shader, "~Wall band", Meshes.Merge(band.ToArray()), Float3.Zero, accent);
        }
        Box(scene, shader, "Step 1", new Float3(-10f, 0.25f, -12f), new Float3(3f, 0.5f, 3f), Float3.Zero, concrete);
        Box(scene, shader, "Step 2", new Float3(-13f, 0.5f, -12f), new Float3(3f, 1f, 3f), Float3.Zero, concrete);
        Box(scene, shader, "Step 3", new Float3(-16f, 0.75f, -12f), new Float3(3f, 1.5f, 3f), Float3.Zero, concrete);
    }

    /// <summary>Terrain height (m) for the hills map: flat-ish play area near spawn, hills further out, a lake bowl.</summary>
    public static float HillsHeight(float x, float z)
    {
        float r = MathF.Sqrt(x * x + z * z);
        float hills = 4.5f * MathF.Sin(x * 0.045f) * MathF.Cos(z * 0.038f) + 2.2f * MathF.Sin(x * 0.11f + z * 0.07f) + 1.1f * MathF.Cos(z * 0.16f - x * 0.05f);
        float flat = Math.Clamp((r - 18f) / 30f, 0f, 1f);                    // spawn plateau
        float lakeD = MathF.Sqrt((x - 45f) * (x - 45f) + (z - 35f) * (z - 35f));
        float lake = -5f * MathF.Max(0f, 1f - lakeD / 26f);                    // lake bowl
        float rim = 6f * Math.Clamp((r - 95f) / 25f, 0f, 1f);                 // raised rim keeps players in
        return hills * flat + lake * flat + rim;
    }

    private static void BuildHills(Scene scene, Shader shader)
    {
        SpawnPoint = new Float3(0f, HillsHeight(0f, -10f) + 0.2f, -10f);
        const float size = 240f; const int n = 120;
        var verts = new Float3[(n + 1) * (n + 1)];
        var uv = new Float2[verts.Length];
        for (int j = 0; j <= n; j++)
            for (int i = 0; i <= n; i++)
            {
                float x = -size / 2f + size * i / n, z = -size / 2f + size * j / n;
                verts[j * (n + 1) + i] = new Float3(x, HillsHeight(x, z), z);
                uv[j * (n + 1) + i] = new Float2(x / 4f, z / 4f);
            }
        var idx = new uint[n * n * 6];
        int k = 0;
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                uint a = (uint)(j * (n + 1) + i), b = a + 1, c = a + (uint)(n + 1), d = c + 1;
                idx[k++] = a; idx[k++] = c; idx[k++] = b;
                idx[k++] = b; idx[k++] = c; idx[k++] = d;
            }
        var mesh = new Mesh { IndexFormat = IndexFormat.UInt32, Vertices = verts, UV = uv, Indices = idx };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (mesh.UV is { Length: > 0 }) mesh.RecalculateTangents();

        GameObject terrain = new("Terrain");
        MeshRenderer r = terrain.AddComponent<MeshRenderer>();
        r.Mesh = mesh;
        var m = Mats.New(shader);
        m.SetColor("_MainColor", new Color(0.46f, 0.5f, 0.36f, 1f));
        if (!Application.IsHeadless && Environment.GetEnvironmentVariable("SANDBOX_PROCTEX") != "0") m.SetTexture("_MainTex", ProcTex.Grass());
        r.Material = m;
        terrain.AddComponent<MeshCollider>().Mesh = mesh;
        Add(scene, terrain);

        // Lake: visual surface + a buoyancy volume (props float by material; the airboat skims it).
        Water.Add(new Float3(45f, -1.575f - 6f, 35f), new Float3(24f, 6f, 24f));
        GameObject water = new("Water");
        water.Transform.Position = new Float3(45f, -1.6f, 35f);
        MeshRenderer wr = water.AddComponent<MeshRenderer>();
        wr.Mesh = Mesh.CreateCube(new Float3(48f, 0.05f, 48f));
        var wm = Mats.New(shader);
        wm.SetColor("_MainColor", new Color(0.14f, 0.34f, 0.46f, 1f));
        if (!Application.IsHeadless) { wm.SetTexture("_MainTex", ProcTex.Water()); wm.SetVector("_Tiling", new Float2(9f, 9f)); wm.SetColor("_MainColor", new Color(0.62f, 0.68f, 0.74f, 1f)); wm.SetTexture("_SurfaceTex", ProcTex.Surface(0.08f, 0.1f)); }
        wr.Material = wm;
        Add(scene, water);

        {
            // Shoreline: a sandy/muddy band draped over the terrain where it meets the water, reed clumps and a jetty.
            var sv = new List<Float3>(); var su = new List<Float2>(); var si = new List<uint>();
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    Float3 a = verts[j * (n + 1) + i], b = verts[j * (n + 1) + i + 1], c = verts[(j + 1) * (n + 1) + i], d = verts[(j + 1) * (n + 1) + i + 1];
                    foreach (var (p0, p1, p2) in new[] { (a, c, b), (b, c, d) })
                    {
                        float lo = MathF.Min(p0.Y, MathF.Min(p1.Y, p2.Y)), hi = MathF.Max(p0.Y, MathF.Max(p1.Y, p2.Y));
                        Float3 m3 = (p0 + p1 + p2) / 3f;
                        float ld = MathF.Sqrt((m3.X - 45f) * (m3.X - 45f) + (m3.Z - 35f) * (m3.Z - 35f));
                        if (ld > 25f || hi < -1.75f || lo > -1.38f) continue;
                        // Clip the triangle at the band's top contour (y = -1.38) so the sand edge follows the terrain
                        // smoothly instead of stair-stepping along whole grid triangles.
                        const float top = -1.38f;
                        var poly = new List<Float3>();
                        Float3[] tri = [p0, p1, p2];
                        for (int e = 0; e < 3; e++)
                        {
                            Float3 A = tri[e], B = tri[(e + 1) % 3];
                            bool ain = A.Y <= top, bin = B.Y <= top;
                            if (ain) poly.Add(A);
                            if (ain != bin) { float t = (top - A.Y) / (B.Y - A.Y); poly.Add(A + (B - A) * t); }
                        }
                        if (poly.Count < 3) continue;
                        uint o = (uint)sv.Count; Float3 up = new(0f, 0.03f, 0f);
                        foreach (Float3 q in poly) { sv.Add(q + up); su.Add(new(q.X / 3f, q.Z / 3f)); }
                        for (int e = 1; e + 1 < poly.Count; e++) si.AddRange([o, o + (uint)e, o + (uint)e + 1]);
                    }
                }
            if (sv.Count > 0)
            {
                var shore = new Mesh { IndexFormat = IndexFormat.UInt32, Vertices = sv.ToArray(), UV = su.ToArray(), Indices = si.ToArray() };
                shore.RecalculateNormals(); shore.RecalculateBounds();
                Shape(scene, shader, "~Shore", shore, Float3.Zero, new Color(0.4f, 0.36f, 0.27f, 1f));
            }
            var reeds = new List<Mesh>(); var heads = new List<Mesh>();
            for (int i = 0; i < 160; i++)
            {
                float a = i * 1.31f, rr = 17f + (i * 7 % 11) * 0.8f;
                float x = 45f + MathF.Cos(a) * rr, z = 35f + MathF.Sin(a) * rr, y = HillsHeight(x, z);
                if (y < -1.9f || y > -0.9f) continue;
                for (int q = 0; q < 7; q++)
                {
                    float qa = q * 2.1f + i, qr = 0.08f + (q % 3) * 0.12f, h = 0.9f + (q * 13 % 7) * 0.12f;
                    Float3 b0 = new(x + MathF.Cos(qa) * qr, y - 0.05f, z + MathF.Sin(qa) * qr), tip = b0 + new Float3(MathF.Cos(qa) * 0.18f, h, MathF.Sin(qa) * 0.18f);
                    reeds.Add(PropMeshes.Rod(b0, tip, 0.022f, 0.006f));
                    if (q % 3 == 0) heads.Add(PropMeshes.Rod(tip - new Float3(0f, 0.22f, 0f) + (tip - b0) * 0f, tip - new Float3(0f, 0.04f, 0f), 0.022f));
                }
            }
            if (reeds.Count > 0) Shape(scene, shader, "~Reeds", Meshes.Merge(reeds.ToArray()), Float3.Zero, new Color(0.42f, 0.55f, 0.26f, 1f));
            if (heads.Count > 0) Shape(scene, shader, "~Reed heads", Meshes.Merge(heads.ToArray()), Float3.Zero, new Color(0.4f, 0.27f, 0.15f, 1f));
            // Jetty: plank deck on round piles running from the west shore out over the water.
            var planks = new List<Mesh>(); var piles = new List<Mesh>();
            float jx0 = 45f - 27f, jz = 35f, jy = -1.15f;
            for (int i = 0; i < 40; i++) planks.Add(Off(Meshes.RoundedBox(new Float3(0.2f, 0.05f, 1.6f), 0.01f), jx0 + i * 0.22f, jy, jz + ((i * 7) % 3 - 1) * 0.015f));
            for (float px = jx0 + 0.1f; px <= jx0 + 8.7f; px += 2.15f)
                foreach (float pz in new[] { -0.75f, 0.75f }) piles.Add(Off(Meshes.Lathe("pile", [(0.0001f, -2.4f), (0.09f, -2.4f), (0.09f, 0.08f), (0.07f, 0.12f), (0.0001f, 0.13f)], 12), px, jy, jz + pz));
            foreach (float pz in new[] { -0.72f, 0.72f }) planks.Add(Off(Meshes.RoundedBox(new Float3(8.8f, 0.14f, 0.07f), 0.015f), jx0 + 4.4f, jy - 0.09f, jz + pz));
            Shape(scene, shader, "Jetty", Meshes.Merge(planks.ToArray()), Float3.Zero, new Color(0.56f, 0.42f, 0.29f, 1f), new Float3(8.8f, 0.1f, 1.6f));
            Shape(scene, shader, "~Jetty piles", Meshes.Merge(piles.ToArray()), Float3.Zero, new Color(0.36f, 0.27f, 0.19f, 1f));
        }
        {
            // Grass tufts: clumps of tapered blades (two-sided, up-facing normals so they light like the turf) scattered
            // over the hills; skips the lake, the deck and the rim.
            var gv = new List<Float3>(); var gn = new List<Float3>(); var gu = new List<Float2>(); var gi = new List<uint>();
            var rng = new Random(7);
            for (int t = 0; t < 4500; t++)
            {
                float x = (float)(rng.NextDouble() * 160 - 80), z = (float)(rng.NextDouble() * 160 - 80);
                if (MathF.Sqrt((x - 45f) * (x - 45f) + (z - 35f) * (z - 35f)) < 25f || (MathF.Abs(x - 6f) < 4f && MathF.Abs(z - 4f) < 3f)) continue;
                float y = HillsHeight(x, z);
                int nb = 8 + rng.Next(8);
                for (int kb = 0; kb < nb; kb++)
                {
                    float a = (float)(rng.NextDouble() * MathF.Tau), rb = (float)rng.NextDouble() * 0.18f, h = 0.18f + (float)rng.NextDouble() * 0.28f, w = 0.025f;
                    Float3 b = new(x + MathF.Cos(a) * rb, y - 0.02f, z + MathF.Sin(a) * rb);
                    Float3 side = new(-MathF.Sin(a) * w, 0f, MathF.Cos(a) * w), lean = new(MathF.Cos(a) * h * 0.35f, h, MathF.Sin(a) * h * 0.35f);
                    uint o = (uint)gv.Count;
                    gv.AddRange([b - side, b + side, b + lean]); gn.AddRange([Float3.UnitY, Float3.UnitY, Float3.UnitY]); gu.AddRange([new(0f, 0f), new(1f, 0f), new(0.5f, 1f)]);
                    gi.AddRange([o, o + 1, o + 2, o, o + 2, o + 1]);
                }
            }
            var gm = new Mesh { IndexFormat = IndexFormat.UInt32, Vertices = gv.ToArray(), Normals = gn.ToArray(), UV = gu.ToArray(), Indices = gi.ToArray() };
            gm.RecalculateBounds();
            Shape(scene, shader, "~Grass tufts", gm, Float3.Zero, new Color(0.17f, 0.29f, 0.09f, 1f));
        }
        // A few rocks and a wooden deck near spawn for scale.
        var rock = new Color(0.31f, 0.3f, 0.28f, 1f);
        for (int i = 0; i < 14; i++)
        {
            float a = i * 2.4f, d = 24f + (i * 37 % 50);
            float x = MathF.Cos(a) * d, z = MathF.Sin(a) * d;
            float s = 1.2f + (i % 4) * 0.7f;
            // Lumpy boulders (were rotated boxes); a box collider sized to the core keeps physics simple.
            Shape(scene, shader, $"~Rock {i}", Meshes.Rock(s * 0.62f, 20 + i % 6), new Float3(x, HillsHeight(x, z) + s * 0.2f, z), rock, new Float3(s * 0.9f, s * 0.7f, s * 0.9f), new Float3(0f, i * 37f, 0f));
            if (!Application.IsHeadless) Shape(scene, shader, $"~Rock moss {i}", Meshes.RockMoss(s * 0.62f, 20 + i % 6), new Float3(x, HillsHeight(x, z) + s * 0.2f, z), new Color(0.14f, 0.2f, 0.08f, 1f), euler: new Float3(0f, i * 37f, 0f));
        }
        // Scattered trees on the hills (none on the spawn plateau, the lake or the rim).
        var bark = new Color(0.38f, 0.27f, 0.17f, 1f);
        for (int i = 0; i < 90; i++)
        {
            float a = i * 2.39996f, d = 24f + (i * 53 % 78);
            float x = MathF.Cos(a) * d, z = MathF.Sin(a) * d;
            if (MathF.Sqrt((x - 45f) * (x - 45f) + (z - 35f) * (z - 35f)) < 30f) continue;
            bool onRock = false; // keep trunks out of the boulders
            for (int ri = 0; ri < 14; ri++) { float ra = ri * 2.4f, rd = 24f + (ri * 37 % 50); float rx = MathF.Cos(ra) * rd - x, rz = MathF.Sin(ra) * rd - z; if (rx * rx + rz * rz < 9f) onRock = true; }
            if (onRock) continue;
            float y = HillsHeight(x, z), tk = 0.8f + (i % 5) * 0.12f;
            Shape(scene, shader, "Tree trunk", Meshes.Lathe("htrunk", [(0.0001f, -0.3f), (0.3f, -0.3f), (0.22f, 0f), (0.17f, 1.6f), (0.12f, 2.6f), (0.0001f, 2.7f)], 16), new Float3(x, y, z), bark, new Float3(0.36f, 5.4f, 0.36f));
            int sd = i % 9;
            Shape(scene, shader, "~Tree crown", Meshes.Blob(1.5f * tk, sd), new Float3(x, y + 3.6f * tk, z), new Color(0.2f, 0.34f, 0.16f, 1f));
            Shape(scene, shader, "~Tree crown b", Meshes.Blob(1.05f * tk, sd + 1), new Float3(x + 0.9f * tk, y + 3.0f * tk, z - 0.4f), new Color(0.16f, 0.29f, 0.13f, 1f));
            Shape(scene, shader, "~Tree crown c", Meshes.Blob(0.95f * tk, sd + 2), new Float3(x - 0.8f * tk, y + 3.2f * tk, z + 0.6f), new Color(0.26f, 0.4f, 0.18f, 1f));
            Shape(scene, shader, "~Tree branches", Meshes.Merge(PropMeshes.Rod(new Float3(0f, 1.9f * tk, 0f), new Float3(0.85f * tk, 2.9f * tk, -0.35f), 0.1f, 0.04f), PropMeshes.Rod(new Float3(0f, 2.1f * tk, 0f), new Float3(-0.75f * tk, 3.0f * tk, 0.5f), 0.09f, 0.035f), PropMeshes.Rod(new Float3(0f, 2.4f * tk, 0f), new Float3(0.1f, 3.4f * tk, 0.15f), 0.08f, 0.035f)), new Float3(x, y, z), bark);
        }
        {
            // Deck near spawn: gapped boards on joists, rim boards and corner posts (one box collider).
            float dy = HillsHeight(6f, 4f) + 0.2f;
            var boards = new List<Mesh>(); var frame = new List<Mesh>();
            for (int i = 0; i < 27; i++) boards.Add(Off(Meshes.RoundedBox(new Float3(0.2f, 0.05f, 3.9f), 0.012f), -2.9f + i * 0.223f, 0.125f, ((i * 5) % 3 - 1) * 0.01f));
            foreach (float z in new[] { -1.98f, 1.98f }) frame.Add(Off(Meshes.RoundedBox(new Float3(6.05f, 0.22f, 0.06f), 0.012f), 0f, 0.02f, z));
            foreach (float x in new[] { -2.98f, 2.98f }) frame.Add(Off(Meshes.RoundedBox(new Float3(0.06f, 0.22f, 4.0f), 0.012f), x, 0.02f, 0f));
            foreach (float x in new[] { -2.9f, 2.9f }) foreach (float z in new[] { -1.9f, 1.9f }) frame.Add(Off(Meshes.RoundedBox(new Float3(0.14f, 0.6f, 0.14f), 0.02f), x, -0.25f, z));
            Shape(scene, shader, "Deck", Meshes.Merge(boards.ToArray()), new Float3(6f, dy, 4f), new Color(0.62f, 0.44f, 0.28f, 1f), new Float3(6f, 0.3f, 4f));
            Shape(scene, shader, "~Deck frame", Meshes.Merge(frame.ToArray()), new Float3(6f, dy, 4f), new Color(0.45f, 0.32f, 0.2f, 1f));
        }
    }

    private static string? BuildCustom(Scene scene, Shader shader, string? file)
    {
        SpawnPoint = new Float3(0f, 2f, 0f);
        if (file is null || !File.Exists(file)) return "Custom map file not found.";
        try
        {
            var result = new Prowl.Runtime.AssetImporting.ModelImporter().Import(new FileInfo(file));
            if (result.RootGO is not { } root) return "The model has no scene.";
            int colliders = 0;
            Float3 min = new(float.MaxValue), max = new(float.MinValue);
            var all = root.GetChildrenDeep().Prepend(root).ToList();
            foreach (GameObject go in all)
                if (!IsMarker(go.Name) && go.GetComponent<MeshRenderer>() is { } mr && mr.Mesh.Res is { } mesh)
                {
                    go.AddComponent<MeshCollider>().Mesh = mesh;
                    colliders++;
                    foreach (Float3 v in mesh.Vertices)
                    {
                        Float3 w = go.Transform.TransformPoint(v);
                        min = new Float3(MathF.Min(min.X, w.X), MathF.Min(min.Y, w.Y), MathF.Min(min.Z, w.Z)); max = new Float3(MathF.Max(max.X, w.X), MathF.Max(max.Y, w.Y), MathF.Max(max.Z, w.Z));
                    }
                }
            Add(scene, root);
            // Spawn above the middle of the model's bounds unless the map has spawn markers.
            if (colliders > 0) SpawnPoint = new Float3((min.X + max.X) / 2f, max.Y + 1f, (min.Z + max.Z) / 2f);
            MarkerReport markers = ApplyMarkers(scene, shader, all);
            Console.WriteLine($"Custom map: {Path.GetFileName(file)} with {colliders} mesh colliders; markers: {markers}.");
            return null;
        }
        catch (Exception e) { return $"Import failed: {e.Message}"; }
    }

    private static void Add(Scene scene, GameObject go) { scene.Add(go); _objects.Add(go); }

    // ------------------------------------------------------------------ static batching
    // Every map piece used to be its own draw call (the town had ~1000, each drawn again per shadow cascade),
    // which kept the render thread and the GL driver busy. Pieces sharing a look are merged into one mesh.
    private static readonly Dictionary<GameObject, (Color C, string Look, Float2 Tiling)> _batch = [];
    public static int BatchedPieces { get; private set; }
    public static int BatchMeshes { get; private set; }
    private static void Batchable(GameObject go, Color c, string look, Float2 tiling) => _batch[go] = (c, look, tiling);

    private static void BuildBatches(Scene scene, Shader shader)
    {
        BatchedPieces = 0; BatchMeshes = 0;
        if (Environment.GetEnvironmentVariable("SANDBOX_BATCH") == "0") { _batch.Clear(); return; }
        var skip = new HashSet<GameObject>(Town.Interactive.Select(i => i.Go));
        if (Town.Atm is { } atm) skip.Add(atm);
        if (Town.Board is { } bd) skip.Add(bd);
        var groups = new Dictionary<(int, int, int, int, string), List<GameObject>>();
        foreach (var (go, info) in _batch)
        {
            if (!go.IsValid() || go.Transform.Parent != null || skip.Contains(go) || go.GetComponent<Door>() is not null) continue;
            if (go.GetComponent<MeshRenderer>() is not { Enabled: true } mr0 || mr0.Mesh.Res is not { } m || m.Vertices.Length == 0) continue;
            var key = ((int)(info.C.R * 255), (int)(info.C.G * 255), (int)(info.C.B * 255), (int)(info.C.A * 255), info.Look);
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = [];
            list.Add(go);
        }
        int n = 0;
        foreach (var (key, list) in groups)
        {
            if (list.Count < 2) continue;
            var v = new List<Float3>(); var nr = new List<Float3>(); var uv = new List<Float2>(); var idx = new List<uint>();
            foreach (GameObject go in list)
            {
                MeshRenderer r = go.GetComponent<MeshRenderer>()!;
                Mesh m = r.Mesh.Res!; Transform t = go.Transform; Float2 tile = _batch[go].Tiling;
                uint b = (uint)v.Count;
                Float3[] mv = m.Vertices, mn = m.Normals; Float2[] mu = m.UV; uint[] mi = m.Indices;
                for (int i = 0; i < mv.Length; i++)
                {
                    v.Add(t.TransformPoint(mv[i]));
                    nr.Add(i < mn.Length ? Float3.Normalize(t.TransformDirection(mn[i])) : Float3.UnitY);
                    uv.Add(i < mu.Length ? new Float2(mu[i].X * tile.X, mu[i].Y * tile.Y) : Float2.Zero);
                }
                foreach (uint ii in mi) idx.Add(ii + b);
                r.Enabled = false;
                BatchedPieces++;
            }
            var mesh = new Mesh { IndexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16, Vertices = v.ToArray(), Normals = nr.ToArray(), UV = uv.ToArray(), Indices = idx.ToArray() };
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            var bgo = new GameObject($"~Batch {n++} {key.Item5}");
            MeshRenderer br = bgo.AddComponent<MeshRenderer>();
            br.Mesh = mesh;
            var bm = Mats.New(shader);
            bm.SetColor("_MainColor", new Color(key.Item1 / 255f, key.Item2 / 255f, key.Item3 / 255f, key.Item4 / 255f));
            if (!Application.IsHeadless && Environment.GetEnvironmentVariable("SANDBOX_PROCTEX") != "0")
                try
                {
                    if (key.Item5 == "detail") bm.SetTexture("_SurfaceTex", ProcTex.Surface(0.85f, 0f));
                    else bm.SetTexture("_MainTex", key.Item5 == "floor" ? ProcTex.DevFloor() : key.Item5 == "asphalt" ? ProcTex.Asphalt() : key.Item5 == "pavers" ? ProcTex.Pavers() : ProcTex.Concrete());
                }
                catch { }
            br.Material = bm;
            Add(scene, bgo);
            BatchMeshes++;
        }
        _batch.Clear();
    }

    private static GameObject Box(Scene scene, Shader shader, string name, Float3 position, Float3 size, Float3 euler, Color color, bool collide = true)
    {
        GameObject go = new(name);
        go.Transform.Position = position;
        go.Transform.LocalEulerAngles = euler;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.Mesh = Meshes.RoundedBox(size, MathF.Min(0.06f, MathF.Min(size.X, MathF.Min(size.Y, size.Z)) * 0.2f));
        var material = Mats.New(shader);
        material.SetColor("_MainColor", color);
        if (!Application.IsHeadless && Environment.GetEnvironmentVariable("SANDBOX_PROCTEX") != "0")
        {
            bool floor = name == "Floor", road = name.StartsWith("Road ") || name.StartsWith("Country road"), plaza = name == "Plaza";
            if (name.StartsWith("~")) { material.SetTexture("_SurfaceTex", ProcTex.Surface(0.85f, 0f)); renderer.Material = material; if (collide) go.AddComponent<BoxCollider>().Size = size; Add(scene, go); Batchable(go, color, "detail", Float2.One); return go; }
            // RoundedBox UVs are in metres (top face U = Z, V = X), so roads/plaza tile at a fixed world size: 6 m asphalt, 1.5 m flags.
            Float2 tiling = road ? new Float2(1f / 6f, 1f / 6f) : plaza ? new Float2(1f / 1.5f, 1f / 1.5f) : floor ? new Float2(0.25f, 0.25f) : new Float2(1f / 3f, 1f / 3f);
            material.SetTexture("_MainTex", plaza ? ProcTex.Pavers() : road ? ProcTex.Asphalt() : floor ? ProcTex.DevFloor() : ProcTex.Concrete());
            material.SetVector("_Tiling", tiling);
            renderer.Material = material;
            if (collide) go.AddComponent<BoxCollider>().Size = size;
            Add(scene, go);
            Batchable(go, color, plaza ? "pavers" : road ? "asphalt" : floor ? "floor" : "concrete", tiling);
            return go;
        }
        renderer.Material = material;
        if (collide) go.AddComponent<BoxCollider>().Size = size;
        Add(scene, go);
        return go;
    }

    /// <summary>A ramp: the painted deck (rounded slab, collides) on a solid concrete wedge down to the ground, inset so the
    /// deck reads as an overhanging lip, with a dark kick plate at the low edge. No more floating planks.</summary>
    private static void Ramp(Scene scene, Shader shader, string name, Float3 position, Float3 size, Float3 euler, Color color)
    {
        GameObject deck = Box(scene, shader, name, position, size, euler, color);
        float hx = size.X / 2f - 0.12f, hz = size.Z / 2f - 0.12f, hy = -size.Y / 2f;
        Float3 W(float x, float z) => deck.Transform.TransformPoint(new Float3(x, hy, z));
        Float3[] top = [W(-hx, -hz), W(hx, -hz), W(hx, hz), W(-hx, hz)];
        Float3 G(Float3 p) => new(p.X, 0f, p.Y < 0.05f ? p.Z : p.Z);
        var faces = new List<Float3[]>();
        // Underside of the deck (facing down) is hidden; build the walls from each deck-bottom edge to the ground.
        for (int i = 0; i < 4; i++)
        {
            Float3 a = top[i], b = top[(i + 1) % 4];
            if (a.Y < 0.02f && b.Y < 0.02f) continue; // edge already on the ground
            faces.Add([a, G(a), G(b), b]);
        }
        foreach (var f in faces) { Float3 c = (f[0] + f[2]) * 0.5f, ctr = (top[0] + top[2]) * 0.5f; if (Float3.Dot(Float3.Cross(f[1] - f[0], f[2] - f[0]), c - ctr) < 0f) Array.Reverse(f); }
        Shape(scene, shader, name + " base", Meshes.Faces(faces.ToArray()), Float3.Zero, new Color(0.6f, 0.6f, 0.58f, 1f));
        // Deck dressing: dark anti-slip strips across the deck and a yellow edge band along each side.
        var grip = new Color(0.2f, 0.2f, 0.22f, 1f);
        int strips = (int)(size.Z / 1.2f);
        for (int i = 0; i < strips; i++)
        {
            float z = -size.Z / 2f + 0.6f + i * (size.Z - 1.2f) / MathF.Max(1, strips - 1);
            Shape(scene, shader, "~" + name + " grip", Meshes.RoundedBox(new Float3(size.X - 0.6f, 0.02f, 0.14f), 0.008f), deck.Transform.TransformPoint(new Float3(0f, size.Y / 2f + 0.005f, z)), grip, euler: euler);
        }
        foreach (float sx in new[] { -1f, 1f })
            Shape(scene, shader, "~" + name + " edge", Meshes.RoundedBox(new Float3(0.12f, 0.02f, size.Z - 0.1f), 0.008f), deck.Transform.TransformPoint(new Float3(sx * (size.X / 2f - 0.12f), size.Y / 2f + 0.006f, 0f)), new Color(0.95f, 0.75f, 0.15f, 1f), euler: euler);
    }

    /// <summary>Visual-only smooth mesh (swept/lathed map detail); optional box collider.</summary>
    private static GameObject Shape(Scene scene, Shader shader, string name, Mesh mesh, Float3 position, Color color, Float3? collider = null, Float3 euler = default)
    {
        GameObject go = new(name);
        go.Transform.Position = position;
        go.Transform.LocalEulerAngles = euler;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.Mesh = mesh;
        var material = Mats.New(shader);
        material.SetColor("_MainColor", color);
        if (!Application.IsHeadless && Environment.GetEnvironmentVariable("SANDBOX_PROCTEX") != "0")
            try
            {
                if (name.StartsWith("~")) material.SetTexture("_SurfaceTex", ProcTex.Surface(0.85f, 0f));
                else material.SetTexture("_MainTex", ProcTex.Concrete());
            }
            catch { }
        renderer.Material = material;
        if (collider is { } c) go.AddComponent<BoxCollider>().Size = c;
        Add(scene, go);
        Batchable(go, color, name.StartsWith("~") ? "detail" : "concrete", Float2.One);
        return go;
    }

    /// <summary>Soft leafy crown: a few overlapping lumpy blobs.</summary>
    private static Mesh Crown(int seed) => Meshes.Sweep([
        new Meshes.Sec(new Float3(0f, -0.9f, 0f), 0.9f, 0.85f, 2f), new Meshes.Sec(new Float3(0.1f, -0.3f, 0.05f), 1.45f, 1.4f, 2f),
        new Meshes.Sec(new Float3(-0.05f, 0.4f, -0.05f), 1.35f, 1.3f, 2f), new Meshes.Sec(new Float3(0f, 1.0f, 0f), 0.8f, 0.85f, 2f)], Float3.UnitX, 28, 0.9f);
}
