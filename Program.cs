using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// Slice 1: a window with a ground plane, a light, a fly camera, and a few
/// dynamic rigid bodies that fall and collide.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var game = new SandboxGame();
        bool headless = args.Any(static a => string.Equals(a, "--headless", StringComparison.OrdinalIgnoreCase));
        if (headless)
        {
            game.RunHeadless(new HeadlessRunOptions
            {
                MaxSeconds = 2.5,
                TargetFps = 60,
            });
            return game.HeadlessExitCode;
        }

        game.Run("Sandbox", 1280, 720);
        return 0;
    }
}

public sealed class SandboxGame : Game
{
    private const float MoveSpeed = 8f;

    private GameObject _camera = null!;
    private readonly List<DynamicBody> _bodies = [];
    private bool _reportedPhysics;
    private int _frames;

    /// <summary>0 after a headless run that saw bodies fall. 1 if they never moved.</summary>
    public int HeadlessExitCode { get; private set; }

    public override void Initialize()
    {
        var scene = new Scene();
        Shader shader = Shader.LoadDefault(DefaultShader.Standard);

        GameObject lightGo = new("Directional Light");
        DirectionalLight light = lightGo.AddComponent<DirectionalLight>();
        light.Color = Color.White;
        light.Intensity = 1.4f;
        light.ShadowQuality = ShadowQuality.Soft;
        lightGo.Transform.LocalEulerAngles = new Float3(-50f, 30f, 0f);
        scene.Add(lightGo);

        _camera = new GameObject("Main Camera");
        _camera.Tag = "Main Camera";
        _camera.Transform.Position = new Float3(7f, 4.5f, -10f);
        _camera.Transform.Forward = new Float3(0f, 1.2f, 0f) - _camera.Transform.Position;
        Camera camera = _camera.AddComponent<Camera>();
        camera.ClearFlags = CameraClearFlags.SolidColor;
        camera.ClearColor = new Color(0.53f, 0.70f, 0.86f, 1f);
        camera.FieldOfView = 60f;
        camera.NearClipPlane = 0.1f;
        camera.FarClipPlane = 200f;
        scene.Add(_camera);

        AddStaticBox(scene, shader, "Ground", new Float3(0f, -0.5f, 0f), new Float3(40f, 1f, 40f), new Color(0.45f, 0.48f, 0.42f, 1f));

        // Offsets and tilts so the pile topples instead of landing in a perfect column.
        AddDynamicBox(scene, shader, "Cube A", new Float3(0f, 2.2f, 0f), new Float3(8f, 12f, 4f), new Color(0.85f, 0.28f, 0.22f, 1f));
        AddDynamicBox(scene, shader, "Cube B", new Float3(0.25f, 3.5f, 0.15f), new Float3(4f, -18f, 10f), new Color(0.93f, 0.62f, 0.18f, 1f));
        AddDynamicBox(scene, shader, "Cube C", new Float3(-0.2f, 4.8f, -0.1f), new Float3(-12f, 6f, 16f), new Color(0.95f, 0.84f, 0.25f, 1f));
        AddDynamicBox(scene, shader, "Cube D", new Float3(0.55f, 6.2f, -0.35f), new Float3(15f, 20f, -8f), new Color(0.25f, 0.48f, 0.86f, 1f));
        AddDynamicSphere(scene, shader, "Sphere", new Float3(-0.45f, 7.6f, 0.4f), 0.5f, new Color(0.72f, 0.32f, 0.78f, 1f));

        Scene.Load(scene);

        foreach (DynamicBody body in _bodies)
            body.StartY = body.Object.Transform.Position.Y;

        Console.WriteLine($"Sandbox ready: ground, light, camera, {_bodies.Count} dynamic bodies.");
        Console.WriteLine("Fly: WASD, Q/E up/down, Shift sprint. Hold right mouse to look.");
    }

    public override void EndUpdate()
    {
        _frames++;
        if (Application.IsHeadless)
            ReportPhysicsIfReady();
        else
            FlyCamera();
    }

    private void ReportPhysicsIfReady()
    {
        if (_reportedPhysics || _frames < 100)
            return;

        _reportedPhysics = true;
        float lowest = float.MaxValue;
        float maxDrop = 0f;
        foreach (DynamicBody body in _bodies)
        {
            float y = body.Object.Transform.Position.Y;
            lowest = MathF.Min(lowest, y);
            maxDrop = MathF.Max(maxDrop, body.StartY - y);
            Console.WriteLine($"  {body.Object.Name}: y {body.StartY:0.00} -> {y:0.00}");
        }

        // Bodies start a few meters up. After ~1.5s they should be on the slab (top at y = 0).
        bool fell = maxDrop > 1f && lowest < 2f && lowest > -2f;
        if (fell)
        {
            HeadlessExitCode = 0;
            Console.WriteLine($"Sandbox physics check passed (lowest y {lowest:0.00}, drop {maxDrop:0.00}).");
        }
        else
        {
            HeadlessExitCode = 1;
            Console.WriteLine($"Sandbox physics check failed (lowest y {lowest:0.00}, drop {maxDrop:0.00}).");
        }
    }

    private void FlyCamera()
    {
        float speed = MoveSpeed * (float)Time.DeltaTime;
        if (Input.GetKey(KeyCode.ShiftLeft) || Input.GetKey(KeyCode.ShiftRight))
            speed *= 2.5f;

        Float2 move = Float2.Zero;
        if (Input.GetKey(KeyCode.W)) move += Float2.UnitY;
        if (Input.GetKey(KeyCode.S)) move -= Float2.UnitY;
        if (Input.GetKey(KeyCode.D)) move += Float2.UnitX;
        if (Input.GetKey(KeyCode.A)) move -= Float2.UnitX;

        _camera.Transform.Position += _camera.Transform.Forward * move.Y * speed;
        _camera.Transform.Position += _camera.Transform.Right * move.X * speed;

        float vertical = 0f;
        if (Input.GetKey(KeyCode.E)) vertical += 1f;
        if (Input.GetKey(KeyCode.Q)) vertical -= 1f;
        _camera.Transform.Position += Float3.UnitY * vertical * speed;

        if (Input.GetMouseButton(1))
        {
            Float2 delta = Input.MouseDelta;
            _camera.Transform.LocalEulerAngles += new Float3(delta.Y, delta.X, 0f) * 0.1f;
        }
    }

    private void AddStaticBox(Scene scene, Shader shader, string name, Float3 position, Float3 size, Color color)
    {
        GameObject go = new(name);
        go.Transform.Position = position;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.Mesh = Mesh.CreateCube(size);
        renderer.Material = CreateMaterial(shader, color);
        BoxCollider collider = go.AddComponent<BoxCollider>();
        collider.Size = size;
        scene.Add(go);
    }

    private void AddDynamicBox(Scene scene, Shader shader, string name, Float3 position, Float3 eulerDegrees, Color color)
    {
        GameObject go = new(name);
        go.Transform.Position = position;
        go.Transform.LocalEulerAngles = eulerDegrees;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.Mesh = Mesh.CreateCube(Float3.One);
        renderer.Material = CreateMaterial(shader, color);
        Rigidbody3D body = go.AddComponent<Rigidbody3D>();
        body.Mass = 1f;
        BoxCollider collider = go.AddComponent<BoxCollider>();
        collider.Size = Float3.One;
        scene.Add(go);
        _bodies.Add(new DynamicBody(go));
    }

    private void AddDynamicSphere(Scene scene, Shader shader, string name, Float3 position, float radius, Color color)
    {
        GameObject go = new(name);
        go.Transform.Position = position;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.Mesh = Mesh.CreateSphere(radius, 16, 24);
        renderer.Material = CreateMaterial(shader, color);
        Rigidbody3D body = go.AddComponent<Rigidbody3D>();
        body.Mass = 1f;
        SphereCollider collider = go.AddComponent<SphereCollider>();
        collider.Radius = radius;
        scene.Add(go);
        _bodies.Add(new DynamicBody(go));
    }

    private static Material CreateMaterial(Shader shader, Color color)
    {
        var material = new Material(shader);
        material.SetColor("_MainColor", color);
        return material;
    }

    private sealed class DynamicBody(GameObject obj)
    {
        public GameObject Object { get; } = obj;
        public float StartY { get; set; }
    }
}
