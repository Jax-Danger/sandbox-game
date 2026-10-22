using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

/// <summary>One visual part of a model prop. Shape: b = bevelled box, c = rounded cylinder, s = sphere, t = tyre, n = cone-ish (tapered cylinder).
/// Paint parts share the prop's material, so the colour tool and the highlight affect them.
/// <paramref name="M"/> = a ready-made mesh (swept/lathed) drawn at scale <paramref name="K"/>; <paramref name="Glow"/> = emissive.</summary>
public sealed record Part(char Shape, Float3 Size, Float3 Offset, Color Color, bool Paint = false, Float3 Rot = default, Mesh? M = null, float K = 1f, bool Glow = false);

/// <summary>A multi-part placeholder model with one box (or cylinder) collider. Real assets can replace it via the manifest id.</summary>
public sealed record ModelDef(string Id, string Name, string Category, Float3 Collider, float Mass, string Surface, Part[] Parts,
    bool Seat = false, bool Round = false, Float3 Center = default, float Ballast = 0f)
{
    public string Entity => "mdl_" + Id;
    public float HalfHeight => Collider.Y / 2f;
}

public static partial class ModelProps
{
    private static Color C(float r, float g, float b) => new(r, g, b, 1f);
    private static readonly Color Wood = C(0.55f, 0.36f, 0.20f), DarkWood = C(0.34f, 0.22f, 0.13f), Steel = C(0.55f, 0.57f, 0.60f),
        Dark = C(0.12f, 0.13f, 0.15f), Fabric = C(0.22f, 0.32f, 0.52f), White = C(0.88f, 0.88f, 0.86f), Concrete = C(0.62f, 0.61f, 0.58f),
        Orange = C(0.95f, 0.42f, 0.08f), Yellow = C(0.95f, 0.75f, 0.12f), Red = C(0.78f, 0.12f, 0.10f), Green = C(0.20f, 0.55f, 0.25f),
        Rubber = C(0.08f, 0.08f, 0.09f), Blue = C(0.15f, 0.35f, 0.75f);

    private static Part B(float x, float y, float z, float ox, float oy, float oz, Color c, bool paint = false, Float3 rot = default) => new('b', new(x, y, z), new(ox, oy, oz), c, paint, rot);
    private static Part Cy(float r, float h, float ox, float oy, float oz, Color c, bool paint = false, Float3 rot = default) => new('c', new(r * 2f, h, r * 2f), new(ox, oy, oz), c, paint, rot);
    private static Part Sp(float r, float ox, float oy, float oz, Color c, bool paint = false) => new('s', new(r * 2f, r * 2f, r * 2f), new(ox, oy, oz), c, paint);
    private static Part Ty(float r, float w, float ox, float oy, float oz, Float3 rot) => new('t', new(r * 2f, w, r * 2f), new(ox, oy, oz), Rubber, false, rot);
    private static readonly Float3 Side = new(0f, 0f, 90f);
    private static Part M(Mesh m, float ox, float oy, float oz, Color c, bool paint = false, Float3 rot = default) => new('m', Float3.One, new(ox, oy, oz), c, paint, rot, m);
    private static Part Cu(float x, float y, float z, float ox, float oy, float oz, Color c, bool paint = false, Float3 rot = default) => M(PropMeshes.Cushion(x, y, z), ox, oy, oz, c, paint, rot);

    private static Part[] Legs(float w, float d, float h, float y, float t, Color c) =>
        [M(PropMeshes.Leg(t / 2, h), -w / 2 + t / 2, y, -d / 2 + t / 2, c), M(PropMeshes.Leg(t / 2, h), w / 2 - t / 2, y, -d / 2 + t / 2, c), M(PropMeshes.Leg(t / 2, h), -w / 2 + t / 2, y, d / 2 - t / 2, c), M(PropMeshes.Leg(t / 2, h), w / 2 - t / 2, y, d / 2 - t / 2, c)];

    public static readonly ModelDef[] All =
    [
        // ---------------- Furniture
        new("chair", "Wooden Chair", "Furniture", new(0.5f, 0.95f, 0.5f), 6f, "Wood",
            [M(Meshes.RoundedBox(new(0.48f, 0.05f, 0.46f), 0.02f), 0, -0.03f, 0.01f, Wood, true), Cu(0.42f, 0.05f, 0.38f, 0, 0.005f, 0.03f, C(0.5f, 0.18f, 0.14f)),
             M(Meshes.Sweep([new Meshes.Sec(new Float3(-0.23f, 0.44f, -0.215f), 0.035f, 0.02f, 2.6f), new Meshes.Sec(new Float3(0f, 0.46f, -0.245f), 0.045f, 0.022f, 2.6f), new Meshes.Sec(new Float3(0.23f, 0.44f, -0.215f), 0.035f, 0.02f, 2.6f)], Float3.UnitY, 16, 0.5f), 0, 0, 0, Wood, true),
             M(PropMeshes.Rod(new(-0.21f, -0.03f, -0.21f), new(-0.215f, 0.45f, -0.225f), 0.022f, 0.018f), 0, 0, 0, DarkWood), M(PropMeshes.Rod(new(0.21f, -0.03f, -0.21f), new(0.215f, 0.45f, -0.225f), 0.022f, 0.018f), 0, 0, 0, DarkWood),
             M(PropMeshes.Rod(new(-0.08f, 0f, -0.215f), new(-0.08f, 0.43f, -0.24f), 0.011f), 0, 0, 0, Wood, true), M(PropMeshes.Rod(new(0f, 0f, -0.215f), new(0f, 0.44f, -0.245f), 0.011f), 0, 0, 0, Wood, true), M(PropMeshes.Rod(new(0.08f, 0f, -0.215f), new(0.08f, 0.43f, -0.24f), 0.011f), 0, 0, 0, Wood, true),
             M(PropMeshes.Rod(new(-0.21f, -0.33f, -0.2f), new(-0.21f, -0.33f, 0.2f), 0.012f), 0, 0, 0, DarkWood), M(PropMeshes.Rod(new(0.21f, -0.33f, -0.2f), new(0.21f, -0.33f, 0.2f), 0.012f), 0, 0, 0, DarkWood), M(PropMeshes.Rod(new(-0.21f, -0.28f, 0f), new(0.21f, -0.28f, 0f), 0.011f), 0, 0, 0, DarkWood),
             .. Legs(0.46f, 0.46f, 0.45f, -0.285f, 0.045f, DarkWood)], Seat: true),
        new("stool", "Stool", "Furniture", new(0.38f, 0.62f, 0.38f), 3f, "Wood",
            [Cy(0.19f, 0.06f, 0, 0.28f, 0, Wood, true), .. Legs(0.3f, 0.3f, 0.56f, -0.03f, 0.04f, DarkWood)], Seat: true, Round: false),
        new("office_chair", "Office Chair", "Furniture", new(0.6f, 1.05f, 0.6f), 12f, "Plastic",
            OfficeChairParts(), Seat: true),
        new("table", "Table", "Furniture", new(1.6f, 0.78f, 0.9f), 25f, "Wood",
            [M(Meshes.RoundedBox(new(1.6f, 0.05f, 0.9f), 0.022f), 0, 0.365f, 0, Wood, true), M(Meshes.RoundedBox(new(1.56f, 0.012f, 0.86f), 0.005f), 0, 0.336f, 0, DarkWood),
             M(Meshes.RoundedBox(new(1.36f, 0.09f, 0.025f), 0.008f), 0, 0.29f, 0.36f, DarkWood), M(Meshes.RoundedBox(new(1.36f, 0.09f, 0.025f), 0.008f), 0, 0.29f, -0.36f, DarkWood),
             M(Meshes.RoundedBox(new(0.025f, 0.09f, 0.66f), 0.008f), 0.71f, 0.29f, 0, DarkWood), M(Meshes.RoundedBox(new(0.025f, 0.09f, 0.66f), 0.008f), -0.71f, 0.29f, 0, DarkWood),
             .. Legs(1.48f, 0.78f, 0.72f, -0.03f, 0.065f, DarkWood)]),
        new("coffee_table", "Coffee Table", "Furniture", new(1.1f, 0.42f, 0.6f), 12f, "Wood",
            [B(1.1f, 0.06f, 0.6f, 0, 0.18f, 0, DarkWood, true), B(1.0f, 0.03f, 0.5f, 0, -0.1f, 0, DarkWood), .. Legs(1.0f, 0.5f, 0.36f, -0.03f, 0.05f, Dark)]),
        new("desk", "Desk", "Furniture", new(1.4f, 0.76f, 0.7f), 30f, "Wood",
            [M(Meshes.RoundedBox(new(1.4f, 0.04f, 0.7f), 0.018f), 0, 0.36f, 0, Wood, true), M(Meshes.RoundedBox(new(0.45f, 0.68f, 0.64f), 0.012f), 0.45f, -0.03f, -0.01f, DarkWood, true),
             M(PropMeshes.InsetPanel(0.42f, 0.2f, 0.02f, 0.018f), 0.45f, 0.2f, 0.32f, Wood), M(PropMeshes.InsetPanel(0.42f, 0.4f, 0.02f, 0.018f), 0.45f, -0.12f, 0.32f, Wood),
             M(PropMeshes.Handle(0.12f), 0.45f, 0.24f, 0.345f, Steel, false, new(0, 0, 90)), M(PropMeshes.Handle(0.12f), 0.45f, 0.0f, 0.345f, Steel, false, new(0, 0, 90)),
             M(Meshes.RoundedBox(new(0.04f, 0.68f, 0.64f), 0.012f), -0.67f, -0.03f, -0.01f, DarkWood, true), M(Meshes.RoundedBox(new(0.9f, 0.3f, 0.015f), 0.005f), -0.22f, 0.15f, -0.3f, DarkWood),
             M(Meshes.RoundedBox(new(0.05f, 0.02f, 0.62f), 0.008f), -0.67f, -0.38f, -0.01f, Dark), M(Meshes.RoundedBox(new(0.46f, 0.02f, 0.62f), 0.008f), 0.45f, -0.38f, -0.01f, Dark),
             M(Meshes.LatheZ([(0.022f, 0f), (0.03f, 0f), (0.03f, 0.004f), (0.022f, 0.004f)], 20), -0.55f, 0.38f, -0.25f, Dark, false, new(-90, 0, 0)), M(Meshes.Sphere(0.0225f), -0.55f, 0.37f, -0.25f, C(0.04f, 0.04f, 0.045f))]),
        new("sofa", "Sofa", "Furniture", new(2.0f, 0.85f, 0.9f), 45f, "Rubber",
            SofaParts(), Seat: true),
        new("bed", "Bed", "Furniture", new(1.1f, 0.6f, 2.1f), 50f, "Wood",
            BedParts()),
        new("bookshelf", "Bookshelf", "Furniture", new(1.0f, 1.9f, 0.35f), 35f, "Wood",
            [M(Meshes.RoundedBox(new(0.94f, 1.84f, 0.02f), 0.006f), 0, 0, -0.16f, DarkWood, true), M(Meshes.RoundedBox(new(0.04f, 1.86f, 0.35f), 0.012f), -0.48f, -0.02f, 0, Wood, true), M(Meshes.RoundedBox(new(0.04f, 1.86f, 0.35f), 0.012f), 0.48f, -0.02f, 0, Wood, true),
             M(Meshes.RoundedBox(new(1.06f, 0.05f, 0.39f), 0.02f), 0, 0.93f, 0.01f, Wood, true), M(Meshes.RoundedBox(new(1.0f, 0.03f, 0.37f), 0.01f), 0, 0.895f, 0.005f, DarkWood),
             M(Meshes.RoundedBox(new(0.96f, 0.09f, 0.33f), 0.01f), 0, -0.905f, 0.0f, DarkWood),
             M(Meshes.RoundedBox(new(0.94f, 0.035f, 0.32f), 0.01f), 0, -0.3f, 0, Wood, true), M(Meshes.RoundedBox(new(0.94f, 0.035f, 0.32f), 0.01f), 0, 0.33f, 0, Wood, true),
             M(Meshes.RoundedBox(new(0.031f, 0.207f, 0.178f), 0.006f), -0.425f, -0.786f, 0.02f, C(0.55f, 0.12f, 0.1f)), M(Meshes.RoundedBox(new(0.025f, 0.238f, 0.220f), 0.006f), -0.393f, -0.771f, 0.02f, C(0.55f, 0.12f, 0.1f)), M(Meshes.RoundedBox(new(0.031f, 0.238f, 0.179f), 0.006f), -0.362f, -0.771f, 0.02f, C(0.55f, 0.12f, 0.1f)), M(Meshes.RoundedBox(new(0.041f, 0.218f, 0.178f), 0.006f), -0.264f, -0.781f, 0.02f, C(0.16f, 0.4f, 0.22f)), M(Meshes.RoundedBox(new(0.041f, 0.251f, 0.214f), 0.006f), -0.220f, -0.764f, 0.02f, C(0.55f, 0.12f, 0.1f)), M(Meshes.RoundedBox(new(0.046f, 0.224f, 0.182f), 0.006f), -0.173f, -0.778f, 0.02f, C(0.12f, 0.25f, 0.5f)), M(Meshes.RoundedBox(new(0.037f, 0.206f, 0.213f), 0.006f), -0.129f, -0.787f, 0.02f, C(0.3f, 0.18f, 0.35f)), M(Meshes.RoundedBox(new(0.046f, 0.201f, 0.205f), 0.006f), -0.084f, -0.790f, 0.02f, C(0.16f, 0.4f, 0.22f)), M(Meshes.RoundedBox(new(0.046f, 0.236f, 0.200f), 0.006f), -0.035f, -0.772f, 0.02f, C(0.75f, 0.6f, 0.3f)), M(Meshes.RoundedBox(new(0.036f, 0.216f, 0.210f), 0.006f), 0.009f, -0.782f, 0.02f, C(0.55f, 0.12f, 0.1f)), M(Meshes.RoundedBox(new(0.026f, 0.244f, 0.197f), 0.006f), 0.043f, -0.768f, 0.02f, C(0.75f, 0.6f, 0.3f)), M(Meshes.RoundedBox(new(0.043f, 0.210f, 0.173f), 0.006f), 0.081f, -0.785f, 0.02f, C(0.12f, 0.25f, 0.5f)), M(Meshes.RoundedBox(new(0.041f, 0.231f, 0.192f), 0.006f), 0.125f, -0.775f, 0.02f, C(0.3f, 0.18f, 0.35f)), M(Meshes.RoundedBox(new(0.033f, 0.239f, 0.175f), 0.006f), 0.165f, -0.770f, 0.02f, C(0.55f, 0.12f, 0.1f)), M(Meshes.RoundedBox(new(0.043f, 0.219f, 0.213f), 0.006f), 0.206f, -0.780f, 0.02f, C(0.55f, 0.12f, 0.1f)), M(Meshes.RoundedBox(new(0.033f, 0.212f, 0.172f), 0.006f), 0.247f, -0.784f, 0.02f, C(0.85f, 0.82f, 0.74f)), M(Meshes.RoundedBox(new(0.027f, 0.268f, 0.204f), 0.006f), 0.280f, -0.756f, 0.02f, C(0.16f, 0.4f, 0.22f)), M(Meshes.RoundedBox(new(0.025f, 0.220f, 0.209f), 0.006f), 0.309f, -0.780f, 0.02f, C(0.75f, 0.6f, 0.3f)), M(Meshes.RoundedBox(new(0.047f, 0.261f, 0.174f), 0.006f), 0.348f, -0.760f, 0.02f, C(0.3f, 0.18f, 0.35f)), M(Meshes.RoundedBox(new(0.030f, 0.231f, 0.189f), 0.006f), 0.390f, -0.774f, 0.02f, C(0.12f, 0.25f, 0.5f)), M(Meshes.RoundedBox(new(0.029f, 0.255f, 0.185f), 0.006f), -0.365f, -0.133f, 0.02f, C(0.55f, 0.12f, 0.1f)), M(Meshes.RoundedBox(new(0.036f, 0.215f, 0.176f), 0.006f), -0.330f, -0.153f, 0.02f, C(0.55f, 0.12f, 0.1f)), M(Meshes.RoundedBox(new(0.026f, 0.212f, 0.177f), 0.006f), -0.296f, -0.154f, 0.02f, C(0.55f, 0.12f, 0.1f)), M(Meshes.RoundedBox(new(0.039f, 0.241f, 0.172f), 0.006f), -0.260f, -0.140f, 0.02f, C(0.1f, 0.1f, 0.12f)), M(Meshes.RoundedBox(new(0.038f, 0.254f, 0.219f), 0.006f), -0.219f, -0.133f, 0.02f, C(0.75f, 0.6f, 0.3f)), M(Meshes.RoundedBox(new(0.030f, 0.231f, 0.181f), 0.006f), -0.181f, -0.145f, 0.02f, C(0.75f, 0.6f, 0.3f)), M(Meshes.RoundedBox(new(0.031f, 0.262f, 0.195f), 0.006f), -0.148f, -0.129f, 0.02f, C(0.55f, 0.12f, 0.1f)), M(Meshes.RoundedBox(new(0.026f, 0.218f, 0.209f), 0.006f), -0.116f, -0.151f, 0.02f, C(0.75f, 0.6f, 0.3f)), M(Meshes.RoundedBox(new(0.048f, 0.240f, 0.190f), 0.006f), -0.016f, -0.140f, 0.02f, C(0.85f, 0.82f, 0.74f)), M(Meshes.RoundedBox(new(0.041f, 0.255f, 0.195f), 0.006f), 0.032f, -0.132f, 0.02f, C(0.55f, 0.12f, 0.1f)), M(Meshes.RoundedBox(new(0.036f, 0.265f, 0.217f), 0.006f), 0.073f, -0.127f, 0.02f, C(0.12f, 0.25f, 0.5f)), M(Meshes.RoundedBox(new(0.033f, 0.246f, 0.202f), 0.006f), 0.111f, -0.137f, 0.02f, C(0.1f, 0.1f, 0.12f)), M(Meshes.RoundedBox(new(0.035f, 0.215f, 0.210f), 0.006f), 0.148f, -0.152f, 0.02f, C(0.16f, 0.4f, 0.22f)), M(Meshes.RoundedBox(new(0.049f, 0.267f, 0.172f), 0.006f), 0.254f, -0.126f, 0.02f, C(0.3f, 0.18f, 0.35f)), M(Meshes.RoundedBox(new(0.028f, 0.268f, 0.173f), 0.006f), 0.295f, -0.126f, 0.02f, C(0.12f, 0.25f, 0.5f)), M(Meshes.RoundedBox(new(0.042f, 0.233f, 0.190f), 0.006f), 0.333f, -0.144f, 0.02f, C(0.75f, 0.6f, 0.3f)), M(Meshes.RoundedBox(new(0.030f, 0.200f, 0.171f), 0.006f), 0.373f, -0.160f, 0.02f, C(0.16f, 0.4f, 0.22f)), M(Meshes.RoundedBox(new(0.028f, 0.257f, 0.214f), 0.006f), 0.405f, -0.132f, 0.02f, C(0.3f, 0.18f, 0.35f)), M(Meshes.RoundedBox(new(0.026f, 0.267f, 0.200f), 0.006f), -0.427f, 0.503f, 0.02f, C(0.1f, 0.1f, 0.12f)), M(Meshes.RoundedBox(new(0.047f, 0.254f, 0.196f), 0.006f), -0.387f, 0.497f, 0.02f, C(0.55f, 0.12f, 0.1f)), M(Meshes.RoundedBox(new(0.039f, 0.218f, 0.172f), 0.006f), -0.281f, 0.479f, 0.02f, C(0.55f, 0.12f, 0.1f)), M(Meshes.RoundedBox(new(0.039f, 0.227f, 0.186f), 0.006f), -0.119f, 0.483f, 0.02f, C(0.12f, 0.25f, 0.5f)), M(Meshes.RoundedBox(new(0.044f, 0.260f, 0.174f), 0.006f), -0.075f, 0.500f, 0.02f, C(0.3f, 0.18f, 0.35f)), M(Meshes.RoundedBox(new(0.045f, 0.226f, 0.191f), 0.006f), -0.027f, 0.483f, 0.02f, C(0.85f, 0.82f, 0.74f)), M(Meshes.RoundedBox(new(0.044f, 0.205f, 0.180f), 0.006f), 0.020f, 0.472f, 0.02f, C(0.16f, 0.4f, 0.22f)), M(Meshes.RoundedBox(new(0.039f, 0.226f, 0.205f), 0.006f), 0.125f, 0.483f, 0.02f, C(0.16f, 0.4f, 0.22f)), M(Meshes.RoundedBox(new(0.037f, 0.224f, 0.215f), 0.006f), 0.166f, 0.482f, 0.02f, C(0.85f, 0.82f, 0.74f)), M(Meshes.RoundedBox(new(0.029f, 0.245f, 0.176f), 0.006f), 0.202f, 0.493f, 0.02f, C(0.1f, 0.1f, 0.12f)), M(Meshes.RoundedBox(new(0.031f, 0.259f, 0.208f), 0.006f), 0.296f, 0.500f, 0.02f, C(0.1f, 0.1f, 0.12f)), M(Meshes.RoundedBox(new(0.041f, 0.247f, 0.186f), 0.006f), 0.335f, 0.494f, 0.02f, C(0.75f, 0.6f, 0.3f)), M(Meshes.RoundedBox(new(0.045f, 0.240f, 0.195f), 0.006f), 0.380f, 0.490f, 0.02f, C(0.75f, 0.6f, 0.3f))]),
        new("cabinet", "Filing Cabinet", "Furniture", new(0.5f, 1.3f, 0.6f), 40f, "Metal",
            [M(Meshes.RoundedBox(new(0.5f, 1.3f, 0.58f), 0.025f), 0, 0, -0.01f, C(0.45f, 0.5f, 0.45f), true),
             M(PropMeshes.InsetPanel(0.44f, 0.38f, 0.025f, 0.03f), 0, 0.42f, 0.29f, C(0.5f, 0.55f, 0.5f)), M(PropMeshes.InsetPanel(0.44f, 0.38f, 0.025f, 0.03f), 0, 0.0f, 0.29f, C(0.5f, 0.55f, 0.5f)), M(PropMeshes.InsetPanel(0.44f, 0.38f, 0.025f, 0.03f), 0, -0.42f, 0.29f, C(0.5f, 0.55f, 0.5f)),
             M(PropMeshes.Handle(0.16f), 0, 0.5f, 0.32f, Steel, false, new(0, 0, 90)), M(PropMeshes.Handle(0.16f), 0, 0.08f, 0.32f, Steel, false, new(0, 0, 90)), M(PropMeshes.Handle(0.16f), 0, -0.34f, 0.32f, Steel, false, new(0, 0, 90)),
             M(Meshes.RoundedBox(new(0.09f, 0.045f, 0.008f), 0.003f), 0, 0.57f, 0.302f, Steel), M(Meshes.RoundedBox(new(0.09f, 0.045f, 0.008f), 0.003f), 0, 0.15f, 0.302f, Steel), M(Meshes.RoundedBox(new(0.09f, 0.045f, 0.008f), 0.003f), 0, -0.27f, 0.302f, Steel),
             M(Meshes.RoundedBox(new(0.075f, 0.03f, 0.004f), 0.002f), 0, 0.57f, 0.306f, White), M(Meshes.RoundedBox(new(0.075f, 0.03f, 0.004f), 0.002f), 0, 0.15f, 0.306f, White), M(Meshes.RoundedBox(new(0.075f, 0.03f, 0.004f), 0.002f), 0, -0.27f, 0.306f, White),
             M(Meshes.RoundedBox(new(0.46f, 0.05f, 0.54f), 0.012f), 0, -0.63f, -0.01f, Dark)]),
        new("fridge", "Fridge", "Furniture", new(0.75f, 1.8f, 0.7f), 70f, "Plastic",
            FridgeParts()),
        new("tv", "TV", "Furniture", new(1.2f, 0.75f, 0.25f), 15f, "Plastic",
            TvParts()),
        new("lamp", "Floor Lamp", "Furniture", new(0.4f, 1.7f, 0.4f), 6f, "Metal",
            LampParts()), // box collider: the tall cylinder tipped over on spawn
        new("bench", "Park Bench", "Furniture", new(1.8f, 0.85f, 0.6f), 30f, "Wood",
            [B(1.8f, 0.05f, 0.14f, 0, 0.02f, 0.2f, Wood, true), B(1.8f, 0.05f, 0.14f, 0, 0.02f, 0.03f, Wood, true), B(1.8f, 0.05f, 0.14f, 0, 0.02f, -0.14f, Wood, true),
             B(1.8f, 0.12f, 0.04f, 0, 0.3f, -0.26f, Wood, true), B(1.8f, 0.12f, 0.04f, 0, 0.18f, -0.26f, Wood, true),
             M(PropMeshes.BenchSide(), -0.8f, 0, 0, Dark), M(PropMeshes.BenchSide(), 0.8f, 0, 0, Dark), M(PropMeshes.BenchLeg(), -0.8f, 0, 0, Dark), M(PropMeshes.BenchLeg(), 0.8f, 0, 0, Dark)], Seat: true),
        new("trash_can", "Trash Can", "Furniture", new(0.5f, 0.8f, 0.5f), 8f, "Metal",
            TrashCanParts(), Round: true),
        new("plant", "Potted Plant", "Furniture", new(0.5f, 1.0f, 0.5f), 10f, "Concrete",
            PlantParts(), Round: true),
        // ---------------- Construction
        new("cinder", "Cinder Block", "Construction", new(0.4f, 0.2f, 0.2f), 14f, "Concrete",
            CinderParts()),
        new("barrier", "Concrete Barrier", "Construction", new(2.0f, 0.8f, 0.6f), 400f, "Concrete",
            BarrierParts()),
        new("cone", "Traffic Cone", "Construction", new(0.36f, 0.7f, 0.36f), 2f, "Rubber",
            [M(PropMeshes.ConeBase(), 0, 0, 0, Orange, true), M(PropMeshes.Cone(), 0, 0, 0, Orange, true), M(PropMeshes.ConeBand(0.02f, 0.097f), 0, 0, 0, White), M(PropMeshes.ConeBand(-0.15f, 0.142f), 0, 0, 0, White)], Round: false, Ballast: 0.24f),
        new("pallet", "Wooden Pallet", "Construction", new(1.2f, 0.15f, 1.0f), 15f, "Wood",
            PalletParts()),
        new("ibeam", "I-Beam", "Construction", new(4.0f, 0.3f, 0.2f), 120f, "Metal",
            IBeamParts()),
        new("pipe", "Steel Pipe", "Construction", new(0.2f, 3f, 0.2f), 30f, "Metal",
            PipeParts(), Round: true),
        new("sawhorse", "Sawhorse", "Construction", new(1.0f, 0.8f, 0.5f), 8f, "Wood",
            SawhorseParts()),
        new("metal_crate", "Metal Crate", "Construction", new(1.0f, 1.0f, 1.0f), 60f, "Metal",
            [M(Meshes.RoundedBox(new(0.96f, 0.96f, 0.96f), 0.03f), 0, 0, 0, C(0.35f, 0.42f, 0.35f), true),
             M(PropMeshes.InsetPanel(0.9f, 0.9f, 0.04f, 0.09f), 0, 0, 0.47f, C(0.3f, 0.37f, 0.3f)), M(PropMeshes.InsetPanel(0.9f, 0.9f, 0.04f, 0.09f), 0, 0, -0.47f, C(0.3f, 0.37f, 0.3f), false, new(0, 180, 0)),
             M(PropMeshes.InsetPanel(0.9f, 0.9f, 0.04f, 0.09f), 0.47f, 0, 0, C(0.3f, 0.37f, 0.3f), false, new(0, 90, 0)), M(PropMeshes.InsetPanel(0.9f, 0.9f, 0.04f, 0.09f), -0.47f, 0, 0, C(0.3f, 0.37f, 0.3f), false, new(0, -90, 0)),
             M(PropMeshes.InsetPanel(0.9f, 0.9f, 0.04f, 0.09f), 0, 0.47f, 0, C(0.3f, 0.37f, 0.3f), false, new(-90, 0, 0)),
             M(Meshes.RoundedBox(new(0.14f, 0.14f, 0.14f), 0.03f), 0.45f, 0.45f, 0.45f, Steel), M(Meshes.RoundedBox(new(0.14f, 0.14f, 0.14f), 0.03f), -0.45f, 0.45f, 0.45f, Steel), M(Meshes.RoundedBox(new(0.14f, 0.14f, 0.14f), 0.03f), 0.45f, -0.45f, 0.45f, Steel), M(Meshes.RoundedBox(new(0.14f, 0.14f, 0.14f), 0.03f), -0.45f, -0.45f, 0.45f, Steel),
             M(Meshes.RoundedBox(new(0.14f, 0.14f, 0.14f), 0.03f), 0.45f, 0.45f, -0.45f, Steel), M(Meshes.RoundedBox(new(0.14f, 0.14f, 0.14f), 0.03f), -0.45f, 0.45f, -0.45f, Steel), M(Meshes.RoundedBox(new(0.14f, 0.14f, 0.14f), 0.03f), 0.45f, -0.45f, -0.45f, Steel), M(Meshes.RoundedBox(new(0.14f, 0.14f, 0.14f), 0.03f), -0.45f, -0.45f, -0.45f, Steel),
             M(PropMeshes.Handle(0.2f), 0.5f, 0.1f, 0, Dark, false, new(0, 90, 90)), M(PropMeshes.Handle(0.2f), -0.5f, 0.1f, 0, Dark, false, new(0, -90, 90))]),
        new("gas_can", "Gas Canister", "Construction", new(0.35f, 1.3f, 0.35f), 25f, "Metal",
            GasCanParts(), Round: true),
        new("fence", "Fence Panel", "Construction", new(2.0f, 1.2f, 0.08f), 18f, "Wood",
            [M(Meshes.Lathe("fpost", [(0.0001f, -0.6f), (0.04f, -0.6f), (0.04f, 0.52f), (0.048f, 0.54f), (0.048f, 0.56f), (0.03f, 0.6f), (0.0001f, 0.62f)], 4), -0.96f, 0, 0, DarkWood, false, new(0, 45, 0)), M(Meshes.Lathe("fpost", [(0.0001f, -0.6f), (0.04f, -0.6f), (0.04f, 0.52f), (0.048f, 0.54f), (0.048f, 0.56f), (0.03f, 0.6f), (0.0001f, 0.62f)], 4), 0.96f, 0, 0, DarkWood, false, new(0, 45, 0)),
             M(Meshes.RoundedBox(new(1.9f, 0.08f, 0.035f), 0.01f), 0, 0.3f, -0.005f, Wood, true), M(Meshes.RoundedBox(new(1.9f, 0.08f, 0.035f), 0.01f), 0, -0.35f, -0.005f, Wood, true),
             M(Meshes.Sweep([new Meshes.Sec(new Float3(-0.72f, -0.55f, 0.035f), 0.05f, 0.012f, 4f), new Meshes.Sec(new Float3(-0.72f, 0.42f, 0.035f), 0.05f, 0.012f, 4f), new Meshes.Sec(new Float3(-0.72f, 0.5f, 0.035f), 0.028f, 0.012f, 4f), new Meshes.Sec(new Float3(-0.72f, 0.56f, 0.035f), 0.004f, 0.01f, 4f)], Float3.UnitZ, 12, 0.2f), 0, 0, 0, Wood, true), M(Meshes.Sweep([new Meshes.Sec(new Float3(-0.48f, -0.55f, 0.035f), 0.05f, 0.012f, 4f), new Meshes.Sec(new Float3(-0.48f, 0.42f, 0.035f), 0.05f, 0.012f, 4f), new Meshes.Sec(new Float3(-0.48f, 0.5f, 0.035f), 0.028f, 0.012f, 4f), new Meshes.Sec(new Float3(-0.48f, 0.56f, 0.035f), 0.004f, 0.01f, 4f)], Float3.UnitZ, 12, 0.2f), 0, 0, 0, Wood, true), M(Meshes.Sweep([new Meshes.Sec(new Float3(-0.24f, -0.55f, 0.035f), 0.05f, 0.012f, 4f), new Meshes.Sec(new Float3(-0.24f, 0.42f, 0.035f), 0.05f, 0.012f, 4f), new Meshes.Sec(new Float3(-0.24f, 0.5f, 0.035f), 0.028f, 0.012f, 4f), new Meshes.Sec(new Float3(-0.24f, 0.56f, 0.035f), 0.004f, 0.01f, 4f)], Float3.UnitZ, 12, 0.2f), 0, 0, 0, Wood, true), M(Meshes.Sweep([new Meshes.Sec(new Float3(0f, -0.55f, 0.035f), 0.05f, 0.012f, 4f), new Meshes.Sec(new Float3(0f, 0.42f, 0.035f), 0.05f, 0.012f, 4f), new Meshes.Sec(new Float3(0f, 0.5f, 0.035f), 0.028f, 0.012f, 4f), new Meshes.Sec(new Float3(0f, 0.56f, 0.035f), 0.004f, 0.01f, 4f)], Float3.UnitZ, 12, 0.2f), 0, 0, 0, Wood, true), M(Meshes.Sweep([new Meshes.Sec(new Float3(0.24f, -0.55f, 0.035f), 0.05f, 0.012f, 4f), new Meshes.Sec(new Float3(0.24f, 0.42f, 0.035f), 0.05f, 0.012f, 4f), new Meshes.Sec(new Float3(0.24f, 0.5f, 0.035f), 0.028f, 0.012f, 4f), new Meshes.Sec(new Float3(0.24f, 0.56f, 0.035f), 0.004f, 0.01f, 4f)], Float3.UnitZ, 12, 0.2f), 0, 0, 0, Wood, true), M(Meshes.Sweep([new Meshes.Sec(new Float3(0.48f, -0.55f, 0.035f), 0.05f, 0.012f, 4f), new Meshes.Sec(new Float3(0.48f, 0.42f, 0.035f), 0.05f, 0.012f, 4f), new Meshes.Sec(new Float3(0.48f, 0.5f, 0.035f), 0.028f, 0.012f, 4f), new Meshes.Sec(new Float3(0.48f, 0.56f, 0.035f), 0.004f, 0.01f, 4f)], Float3.UnitZ, 12, 0.2f), 0, 0, 0, Wood, true), M(Meshes.Sweep([new Meshes.Sec(new Float3(0.72f, -0.55f, 0.035f), 0.05f, 0.012f, 4f), new Meshes.Sec(new Float3(0.72f, 0.42f, 0.035f), 0.05f, 0.012f, 4f), new Meshes.Sec(new Float3(0.72f, 0.5f, 0.035f), 0.028f, 0.012f, 4f), new Meshes.Sec(new Float3(0.72f, 0.56f, 0.035f), 0.004f, 0.01f, 4f)], Float3.UnitZ, 12, 0.2f), 0, 0, 0, Wood, true)]),
        new("window", "Window Frame", "Construction", new(1.2f, 1.2f, 0.1f), 15f, "Wood",
            WindowParts()),
        // ---------------- Signs & street
        new("stop_sign", "Stop Sign", "Signs", new(0.7f, 2.4f, 0.1f), 12f, "Metal",
            StopSignParts()),
        new("arrow_sign", "Arrow Sign", "Signs", new(0.9f, 2.2f, 0.4f), 7f, "Metal",
            ArrowSignParts()),
        new("warning_sign", "Warning Sign", "Signs", new(0.7f, 1.4f, 0.4f), 6f, "Metal",
            WarningSignParts()),
        new("billboard", "Billboard", "Signs", new(3.0f, 3.2f, 0.3f), 150f, "Metal",
            BillboardParts()),
        new("street_lamp", "Street Lamp", "Signs", new(0.4f, 4.0f, 0.4f), 60f, "Metal",
            StreetLampParts()),
        // ---------------- Wheels
        new("wheel_small", "Small Wheel", "Wheels", new(0.4f, 0.12f, 0.4f), 3f, "Rubber",
            [Ty(0.2f, 0.12f, 0, 0, 0, Side), M(PropMeshes.Rim(0.1f, 0.13f, 4), 0, 0, 0, Steel, true, Side)], Round: true),
        new("wheel_car", "Car Wheel", "Wheels", new(0.7f, 0.25f, 0.7f), 18f, "Rubber",
            [Ty(0.35f, 0.25f, 0, 0, 0, Side), M(PropMeshes.Rim(0.2f, 0.27f, 5), 0, 0, 0, Steel, true, Side), new('c', new(0.08f, 0.27f, 0.08f), new(0, 0, 0), Dark, false, Side)], Round: true),
        new("wheel_tractor", "Tractor Wheel", "Wheels", new(1.4f, 0.45f, 1.4f), 70f, "Rubber",
            [Ty(0.7f, 0.45f, 0, 0, 0, Side), M(PropMeshes.TractorRim(0.4f, 0.45f), 0, 0, 0, Yellow, true, Side), M(PropMeshes.TractorRimDark(0.4f, 0.45f), 0, 0, 0, Dark, false, Side),
             M(PropMeshes.TractorRim(0.4f, 0.45f), 0, 0, 0, Yellow, true, new(0f, 0f, -90f)), M(PropMeshes.TractorRimDark(0.4f, 0.45f), 0, 0, 0, Dark, false, new(0f, 0f, -90f))], Round: true),
        new("gear", "Gear", "Wheels", new(0.8f, 0.12f, 0.8f), 20f, "Metal",
            GearParts(), Round: true),
        // ---------------- Vehicle parts
        new("seat", "Vehicle Seat", "Vehicle parts", new(0.55f, 0.9f, 0.55f), 15f, "Rubber",
            SeatParts(), Seat: true),
    ];

    public static ModelDef? Find(string? entity) => entity is { } e && e.StartsWith("mdl_") ? All.FirstOrDefault(d => d.Entity == e) : null;

    public static void Register()
    {
        foreach (ModelDef d in All)
        {
            ModelDef def = d;
            SpawnCatalog.Register(new SpawnEntry("prop_" + def.Id, def.Name, MenuTab.Spawnlists, def.Category, Icon.Cube, null,
                (g, pos, yaw) => Spawn(g, def, pos, yaw).Go.Name, def.HalfHeight));
        }
    }

    public static Prop Spawn(SandboxGame g, ModelDef def, Float3 pos, float yaw, bool recordUndo = true)
    {
        Prop p = g.Props.Spawn(PropKind.Cube, pos - new Float3(0f, def.Ballast, 0f), yaw, recordUndo);
        Apply(g, p, def);
        return p;
    }

    /// <summary>Turns a spawned cube prop into the model (also used when loading saves).</summary>
    public static void Apply(SandboxGame g, Prop p, ModelDef def)
    {
        p.Entity = def.Entity;
        p.Go.Name = p.Go.Name.Replace("Cube", def.Name);
        if (p.Go.GetComponent<BoxCollider>() is { } box)
        {
            if (def.Round)
            {
                box.Enabled = false;
                p.Go.RemoveComponent(box);
                var cyl = p.Go.AddComponent<CylinderCollider>();
                // Configure while detached: changing an attached collider's shape throws inside the engine.
                cyl.Enabled = false;
                bool side = def.Collider.Y < def.Collider.X * 0.6f && def.Category == "Wheels";
                cyl.Radius = (side ? def.Collider.X : MathF.Max(def.Collider.X, def.Collider.Z)) / 2f;
                cyl.Height = def.Collider.Y;
                if (side) cyl.Rotation = new Float3(0f, 0f, 90f);
                cyl.Enabled = true;
            }
            else { box.Enabled = false; box.Size = def.Collider; box.Center = new Float3(0f, def.Ballast, 0f); box.Enabled = true; }
        }
        p.Body.Mass = def.Mass;
        p.SetSurface(SurfaceMaterial.Get(def.Surface));
        // Paint parts use the prop's own material: pick the first paint colour as the prop colour.
        if (def.Parts.FirstOrDefault(x => x.Paint) is { } paint) p.SetColor(paint.Color);
        if (p.Go.GetComponent<MeshRenderer>() is { } root) root.Enabled = false;
        int before = p.Go.Children.Count;
        BuildParts(p.Go, def, g.Shader, p.Material);
        // Ballast: the body origin (= centre of mass in Jitter) sits below the geometric centre, so bottom-heavy
        // props like traffic cones stand back up instead of toppling at a nudge. Geometry and collider ride up.
        if (def.Ballast != 0f)
            for (int i = before; i < p.Go.Children.Count; i++) p.Go.Children[i].Transform.LocalPosition += new Float3(0f, def.Ballast, 0f);
        if (def.Seat) p.Go.AddComponent<Seat>().Def = def;
    }

    public static void BuildParts(GameObject parent, ModelDef def, Shader shader, Material paint)
    {
        var mats = new Dictionary<Color, Material>();
        foreach (Part part in def.Parts)
        {
            var go = new GameObject("part");
            go.Transform.Parent = parent.Transform;
            go.Transform.LocalPosition = part.Offset;
            go.Transform.LocalEulerAngles = part.Rot;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            Float3 s = part.Size;
            float min = MathF.Min(s.X, MathF.Min(s.Y, s.Z));
            if (part.K != 1f) go.Transform.LocalScale = new Float3(part.K);
            r.Mesh = part.M ?? part.Shape switch
            {
                'c' => Meshes.RoundedCylinder(s.X / 2f, s.Y, min * 0.24f, 32),
                's' => Meshes.Sphere(s.X / 2f),
                't' => Meshes.Tyre(s.X / 2f, s.Y),
                'n' => Meshes.Lathe($"cone{s.X:0.###},{s.Y:0.###}", [(0f, -s.Y / 2f), (s.X / 2f, -s.Y / 2f), (s.X * 0.12f, s.Y / 2f), (0f, s.Y / 2f)], 28),
                _ => Meshes.RoundedBox(s, min * 0.3f, 4),
            };
            if (part.Paint) r.Material = paint;
            else
            {
                if (!mats.TryGetValue(part.Color, out Material? m))
                {
                    m = Mats.New(shader);
                    m.SetColor("_MainColor", part.Color);
                    if (part.Glow && !Application.IsHeadless)
                        try { m.SetTexture("_EmissionTex", ProcTex.Solid((byte)(part.Color.R * 255), (byte)(part.Color.G * 255), (byte)(part.Color.B * 255))); m.SetFloat("_EmissionIntensity", 1.5f); } catch { }
                    mats[part.Color] = m;
                }
                r.Material = m;
            }
        }
    }
}

/// <summary>A seat the player can sit in with E (weld it into a contraption to ride it). E again stands up.</summary>
public sealed class Seat : Component
{
    public ModelDef? Def;
    public Float3 SitOffset => new(0f, (Def?.Collider.Y ?? 0.9f) * 0.05f - 0.2f, 0.02f);
}
