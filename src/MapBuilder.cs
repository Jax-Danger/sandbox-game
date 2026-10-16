using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

/// <summary>Builds the static test map: a big floor, ramps, walls, and a few platforms.</summary>
public static class MapBuilder
{
    public static DirectionalLight Build(Scene scene, Shader shader)
    {
        GameObject lightGo = new("Directional Light");
        DirectionalLight light = lightGo.AddComponent<DirectionalLight>();
        light.Color = Color.White;
        light.Intensity = 1.4f;
        light.ShadowQuality = ShadowQuality.Soft;
        lightGo.Transform.LocalEulerAngles = new Float3(-50f, 30f, 0f);
        scene.Add(lightGo);

        var floor = new Color(0.78f, 0.80f, 0.82f, 1f);
        var concrete = new Color(0.62f, 0.62f, 0.60f, 1f);
        var accent = new Color(0.80f, 0.55f, 0.25f, 1f);

        // 120 x 120 m floor with a procedural dev-texture grid.
        Box(scene, shader, "Floor", new Float3(0f, -0.5f, 0f), new Float3(120f, 1f, 120f), Float3.Zero, floor);

        // Perimeter walls.
        const float half = 60f, h = 4f;
        Box(scene, shader, "Wall N", new Float3(0f, h / 2f, half), new Float3(120f, h, 1f), Float3.Zero, concrete);
        Box(scene, shader, "Wall S", new Float3(0f, h / 2f, -half), new Float3(120f, h, 1f), Float3.Zero, concrete);
        Box(scene, shader, "Wall E", new Float3(half, h / 2f, 0f), new Float3(1f, h, 120f), Float3.Zero, concrete);
        Box(scene, shader, "Wall W", new Float3(-half, h / 2f, 0f), new Float3(1f, h, 120f), Float3.Zero, concrete);

        // Ramps (tilted slabs) at different angles.
        Box(scene, shader, "Ramp gentle", new Float3(-14f, 1.2f, 10f), new Float3(5f, 0.4f, 10f), new Float3(-14f, 0f, 0f), accent);
        Box(scene, shader, "Ramp steep", new Float3(14f, 2.2f, 10f), new Float3(5f, 0.4f, 10f), new Float3(-28f, 0f, 0f), accent);
        Box(scene, shader, "Ramp side", new Float3(0f, 1.5f, 24f), new Float3(10f, 0.4f, 5f), new Float3(0f, 0f, 18f), accent);

        // A platform at the top of the gentle ramp, and a free-standing wall to throw things at.
        Box(scene, shader, "Platform", new Float3(-14f, 2.2f, 19f), new Float3(8f, 0.6f, 8f), Float3.Zero, concrete);
        Box(scene, shader, "Wall mid", new Float3(10f, 1.5f, -12f), new Float3(12f, 3f, 0.6f), Float3.Zero, concrete);
        Box(scene, shader, "Step 1", new Float3(-10f, 0.25f, -12f), new Float3(3f, 0.5f, 3f), Float3.Zero, concrete);
        Box(scene, shader, "Step 2", new Float3(-13f, 0.5f, -12f), new Float3(3f, 1f, 3f), Float3.Zero, concrete);
        Box(scene, shader, "Step 3", new Float3(-16f, 0.75f, -12f), new Float3(3f, 1.5f, 3f), Float3.Zero, concrete);
        return light;
    }

    private static void Box(Scene scene, Shader shader, string name, Float3 position, Float3 size, Float3 euler, Color color, bool collide = true)
    {
        GameObject go = new(name);
        go.Transform.Position = position;
        go.Transform.LocalEulerAngles = euler;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.Mesh = Mesh.CreateCube(size);
        var material = new Material(shader);
        material.SetColor("_MainColor", color);
        if (!Application.IsHeadless && Environment.GetEnvironmentVariable("SANDBOX_PROCTEX") != "0")
        {
            bool floor = name == "Floor";
            material.SetTexture("_MainTex", floor ? ProcTex.DevFloor() : ProcTex.Concrete());
            material.SetVector("_Tiling", floor ? new Float2(size.X / 4f, size.Z / 4f) : new Float2(MathF.Max(1f, MathF.Max(size.X, size.Z) / 3f), MathF.Max(1f, size.Y / 3f)));
            if (Environment.GetEnvironmentVariable("SANDBOX_PROCTEX_SURF") == "1")
            material.SetTexture("_SurfaceTex", ProcTex.Surface(floor ? 0.85f : 0.9f, 0f));
        }
        renderer.Material = material;
        if (collide)
            go.AddComponent<BoxCollider>().Size = size;
        scene.Add(go);
    }
}
