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
    }

    /// <summary>Replaces the current map. Returns an error message for a failed custom import (falls back to Flatgrass).</summary>
    public static string? Load(Scene scene, Shader shader, string map, string? customFile)
    {
        Clear(scene);
        Town.Reset();
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
        Box(scene, shader, "Ramp gentle", new Float3(-14f, 1.2f, 10f), new Float3(5f, 0.4f, 10f), new Float3(-14f, 0f, 0f), accent);
        Box(scene, shader, "Ramp steep", new Float3(14f, 2.2f, 10f), new Float3(5f, 0.4f, 10f), new Float3(-28f, 0f, 0f), accent);
        Box(scene, shader, "Ramp side", new Float3(0f, 1.5f, 24f), new Float3(10f, 0.4f, 5f), new Float3(0f, 0f, 18f), accent);
        Box(scene, shader, "Platform", new Float3(-14f, 2.2f, 19f), new Float3(8f, 0.6f, 8f), Float3.Zero, concrete);
        Box(scene, shader, "Wall mid", new Float3(10f, 1.5f, -12f), new Float3(12f, 3f, 0.6f), Float3.Zero, concrete);
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
        m.SetColor("_MainColor", new Color(0.55f, 0.72f, 0.42f, 1f));
        if (!Application.IsHeadless && Environment.GetEnvironmentVariable("SANDBOX_PROCTEX") != "0") m.SetTexture("_MainTex", ProcTex.Grass());
        r.Material = m;
        terrain.AddComponent<MeshCollider>().Mesh = mesh;
        Add(scene, terrain);

        // Lake surface (visual only; the bowl floor is the collider).
        GameObject water = new("Water");
        water.Transform.Position = new Float3(45f, -1.6f, 35f);
        MeshRenderer wr = water.AddComponent<MeshRenderer>();
        wr.Mesh = Mesh.CreateCube(new Float3(48f, 0.05f, 48f));
        var wm = Mats.New(shader);
        wm.SetColor("_MainColor", new Color(0.18f, 0.42f, 0.62f, 1f));
        if (!Application.IsHeadless) { wm.SetTexture("_MainTex", ProcTex.Solid(40, 96, 140)); wm.SetTexture("_SurfaceTex", ProcTex.Surface(0.08f, 0.1f)); }
        wr.Material = wm;
        Add(scene, water);

        // A few rocks and a wooden deck near spawn for scale.
        var rock = new Color(0.52f, 0.50f, 0.48f, 1f);
        for (int i = 0; i < 14; i++)
        {
            float a = i * 2.4f, d = 24f + (i * 37 % 50);
            float x = MathF.Cos(a) * d, z = MathF.Sin(a) * d;
            float s = 1.2f + (i % 4) * 0.7f;
            Box(scene, shader, $"Rock {i}", new Float3(x, HillsHeight(x, z) + s * 0.3f, z), new Float3(s, s * 0.8f, s * 1.1f), new Float3(i * 11f, i * 37f, i * 7f), rock);
        }
        Box(scene, shader, "Deck", new Float3(6f, HillsHeight(6f, 4f) + 0.2f, 4f), new Float3(6f, 0.3f, 4f), Float3.Zero, new Color(0.62f, 0.44f, 0.28f, 1f));
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
            foreach (GameObject go in root.GetChildrenDeep().Prepend(root).ToList())
                if (go.GetComponent<MeshRenderer>() is { } mr && mr.Mesh.Res is { } mesh)
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
            if (colliders == 0) return null;
            // Spawn above the middle of the model's bounds.
            SpawnPoint = new Float3((min.X + max.X) / 2f, max.Y + 1f, (min.Z + max.Z) / 2f);
            Console.WriteLine($"Custom map: {Path.GetFileName(file)} with {colliders} mesh colliders.");
            return null;
        }
        catch (Exception e) { return $"Import failed: {e.Message}"; }
    }

    private static void Add(Scene scene, GameObject go) { scene.Add(go); _objects.Add(go); }

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
            bool floor = name == "Floor";
            if (name.StartsWith("~")) { material.SetTexture("_SurfaceTex", ProcTex.Solid(0, 215, 255)); renderer.Material = material; if (collide) go.AddComponent<BoxCollider>().Size = size; Add(scene, go); return go; }
            material.SetTexture("_MainTex", floor ? ProcTex.DevFloor() : ProcTex.Concrete());
            material.SetVector("_Tiling", floor ? new Float2(size.X / 4f, size.Z / 4f) : new Float2(MathF.Max(1f, MathF.Max(size.X, size.Z) / 3f), MathF.Max(1f, size.Y / 3f)));
        }
        renderer.Material = material;
        if (collide) go.AddComponent<BoxCollider>().Size = size;
        Add(scene, go);
        return go;
    }
}
