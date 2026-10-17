using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

/// <summary>One visual part of a model prop. Shape: b = bevelled box, c = rounded cylinder, s = sphere, t = tyre, n = cone-ish (tapered cylinder).
/// Paint parts share the prop's material, so the colour tool and the highlight affect them.</summary>
public sealed record Part(char Shape, Float3 Size, Float3 Offset, Color Color, bool Paint = false, Float3 Rot = default);

/// <summary>A multi-part placeholder model with one box (or cylinder) collider. Real assets can replace it via the manifest id.</summary>
public sealed record ModelDef(string Id, string Name, string Category, Float3 Collider, float Mass, string Surface, Part[] Parts,
    bool Seat = false, bool Round = false, Float3 Center = default)
{
    public string Entity => "mdl_" + Id;
    public float HalfHeight => Collider.Y / 2f;
}

public static class ModelProps
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

    private static Part[] Legs(float w, float d, float h, float y, float t, Color c) =>
        [B(t, h, t, -w / 2 + t / 2, y, -d / 2 + t / 2, c), B(t, h, t, w / 2 - t / 2, y, -d / 2 + t / 2, c), B(t, h, t, -w / 2 + t / 2, y, d / 2 - t / 2, c), B(t, h, t, w / 2 - t / 2, y, d / 2 - t / 2, c)];

    public static readonly ModelDef[] All =
    [
        // ---------------- Furniture
        new("chair", "Wooden Chair", "Furniture", new(0.5f, 0.95f, 0.5f), 6f, "Wood",
            [B(0.5f, 0.06f, 0.5f, 0, -0.03f, 0, Wood, true), B(0.5f, 0.5f, 0.05f, 0, 0.25f, -0.225f, Wood, true), .. Legs(0.5f, 0.5f, 0.45f, -0.285f, 0.05f, DarkWood)], Seat: true),
        new("stool", "Stool", "Furniture", new(0.38f, 0.62f, 0.38f), 3f, "Wood",
            [Cy(0.19f, 0.06f, 0, 0.28f, 0, Wood, true), .. Legs(0.3f, 0.3f, 0.56f, -0.03f, 0.04f, DarkWood)], Seat: true, Round: false),
        new("office_chair", "Office Chair", "Furniture", new(0.6f, 1.05f, 0.6f), 12f, "Plastic",
            [B(0.52f, 0.1f, 0.5f, 0, -0.02f, 0, Dark, true), B(0.5f, 0.55f, 0.08f, 0, 0.3f, -0.24f, Dark, true), Cy(0.03f, 0.35f, 0, -0.25f, 0, Steel),
             B(0.6f, 0.04f, 0.06f, 0, -0.45f, 0, Steel), B(0.06f, 0.04f, 0.6f, 0, -0.45f, 0, Steel), Sp(0.04f, 0.28f, -0.49f, 0, Rubber), Sp(0.04f, -0.28f, -0.49f, 0, Rubber), Sp(0.04f, 0, -0.49f, 0.28f, Rubber), Sp(0.04f, 0, -0.49f, -0.28f, Rubber)], Seat: true),
        new("table", "Table", "Furniture", new(1.6f, 0.78f, 0.9f), 25f, "Wood",
            [B(1.6f, 0.06f, 0.9f, 0, 0.36f, 0, Wood, true), .. Legs(1.5f, 0.8f, 0.72f, -0.03f, 0.07f, DarkWood)]),
        new("coffee_table", "Coffee Table", "Furniture", new(1.1f, 0.42f, 0.6f), 12f, "Wood",
            [B(1.1f, 0.06f, 0.6f, 0, 0.18f, 0, DarkWood, true), B(1.0f, 0.03f, 0.5f, 0, -0.1f, 0, DarkWood), .. Legs(1.0f, 0.5f, 0.36f, -0.03f, 0.05f, Dark)]),
        new("desk", "Desk", "Furniture", new(1.4f, 0.76f, 0.7f), 30f, "Wood",
            [B(1.4f, 0.05f, 0.7f, 0, 0.355f, 0, White, true), B(0.45f, 0.7f, 0.66f, 0.45f, -0.03f, 0, White, true), B(0.4f, 0.16f, 0.02f, 0.45f, 0.18f, 0.335f, Steel), B(0.4f, 0.16f, 0.02f, 0.45f, -0.02f, 0.335f, Steel),
             B(0.05f, 0.7f, 0.66f, -0.67f, -0.03f, 0, White, true)]),
        new("sofa", "Sofa", "Furniture", new(2.0f, 0.85f, 0.9f), 45f, "Rubber",
            [B(2.0f, 0.4f, 0.9f, 0, -0.2f, 0, Fabric, true), B(2.0f, 0.5f, 0.22f, 0, 0.18f, -0.34f, Fabric, true), B(0.22f, 0.3f, 0.9f, -0.89f, 0.1f, 0, Fabric, true), B(0.22f, 0.3f, 0.9f, 0.89f, 0.1f, 0, Fabric, true),
             B(0.76f, 0.12f, 0.62f, -0.39f, 0.05f, 0.1f, C(0.28f, 0.38f, 0.6f)), B(0.76f, 0.12f, 0.62f, 0.39f, 0.05f, 0.1f, C(0.28f, 0.38f, 0.6f))], Seat: true),
        new("bed", "Bed", "Furniture", new(1.1f, 0.6f, 2.1f), 50f, "Rubber",
            [B(1.1f, 0.25f, 2.1f, 0, -0.17f, 0, DarkWood, true), B(1.0f, 0.2f, 2.0f, 0, 0.05f, 0, White), B(1.1f, 0.6f, 0.08f, 0, 0.0f, -1.01f, DarkWood, true), B(0.6f, 0.12f, 0.35f, 0, 0.2f, -0.75f, C(0.92f, 0.92f, 0.95f)), B(1.02f, 0.05f, 1.2f, 0, 0.17f, 0.35f, Red)]),
        new("bookshelf", "Bookshelf", "Furniture", new(1.0f, 1.9f, 0.35f), 35f, "Wood",
            [B(1.0f, 1.9f, 0.04f, 0, 0, -0.155f, DarkWood, true), B(0.04f, 1.9f, 0.35f, -0.48f, 0, 0, Wood, true), B(0.04f, 1.9f, 0.35f, 0.48f, 0, 0, Wood, true),
             B(0.96f, 0.04f, 0.33f, 0, -0.93f, 0, Wood, true), B(0.96f, 0.04f, 0.33f, 0, -0.3f, 0, Wood, true), B(0.96f, 0.04f, 0.33f, 0, 0.33f, 0, Wood, true), B(0.96f, 0.04f, 0.33f, 0, 0.93f, 0, Wood, true),
             B(0.5f, 0.26f, 0.22f, -0.2f, -0.15f, 0.02f, Red), B(0.3f, 0.24f, 0.22f, 0.25f, -0.16f, 0.02f, Blue), B(0.7f, 0.25f, 0.22f, 0.05f, 0.47f, 0.02f, Green)]),
        new("cabinet", "Filing Cabinet", "Furniture", new(0.5f, 1.3f, 0.6f), 40f, "Metal",
            [B(0.5f, 1.3f, 0.6f, 0, 0, 0, C(0.45f, 0.5f, 0.45f), true), B(0.42f, 0.36f, 0.02f, 0, 0.42f, 0.305f, C(0.5f, 0.55f, 0.5f)), B(0.42f, 0.36f, 0.02f, 0, 0.0f, 0.305f, C(0.5f, 0.55f, 0.5f)), B(0.42f, 0.36f, 0.02f, 0, -0.42f, 0.305f, C(0.5f, 0.55f, 0.5f)),
             B(0.14f, 0.025f, 0.03f, 0, 0.5f, 0.32f, Steel), B(0.14f, 0.025f, 0.03f, 0, 0.08f, 0.32f, Steel), B(0.14f, 0.025f, 0.03f, 0, -0.34f, 0.32f, Steel)]),
        new("fridge", "Fridge", "Furniture", new(0.75f, 1.8f, 0.7f), 70f, "Metal",
            [B(0.75f, 1.8f, 0.7f, 0, 0, 0, White, true), B(0.73f, 0.01f, 0.02f, 0, 0.35f, 0.355f, Dark), B(0.04f, 0.5f, 0.05f, 0.3f, 0.7f, 0.37f, Steel), B(0.04f, 0.7f, 0.05f, 0.3f, -0.2f, 0.37f, Steel)]),
        new("tv", "TV", "Furniture", new(1.2f, 0.75f, 0.25f), 15f, "Plastic",
            [B(1.2f, 0.68f, 0.06f, 0, 0.035f, 0, Dark, true), B(1.12f, 0.6f, 0.01f, 0, 0.04f, 0.031f, C(0.05f, 0.08f, 0.12f)), B(0.3f, 0.03f, 0.25f, 0, -0.36f, 0, Dark), B(0.06f, 0.1f, 0.04f, 0, -0.31f, 0, Dark)]),
        new("lamp", "Floor Lamp", "Furniture", new(0.4f, 1.7f, 0.4f), 6f, "Metal",
            [Cy(0.2f, 0.04f, 0, -0.83f, 0, Dark, true), Cy(0.02f, 1.4f, 0, -0.1f, 0, Steel), new('n', new(0.4f, 0.32f, 0.4f), new(0, 0.69f, 0), C(0.95f, 0.9f, 0.75f), true)], Round: true),
        new("bench", "Park Bench", "Furniture", new(1.8f, 0.85f, 0.6f), 30f, "Wood",
            [B(1.8f, 0.05f, 0.14f, 0, 0.02f, 0.2f, Wood, true), B(1.8f, 0.05f, 0.14f, 0, 0.02f, 0.03f, Wood, true), B(1.8f, 0.05f, 0.14f, 0, 0.02f, -0.14f, Wood, true),
             B(1.8f, 0.12f, 0.04f, 0, 0.3f, -0.26f, Wood, true), B(1.8f, 0.12f, 0.04f, 0, 0.18f, -0.26f, Wood, true),
             B(0.06f, 0.85f, 0.55f, -0.8f, 0, 0, Dark), B(0.06f, 0.85f, 0.55f, 0.8f, 0, 0, Dark)], Seat: true),
        new("trash_can", "Trash Can", "Furniture", new(0.5f, 0.8f, 0.5f), 8f, "Metal",
            [Cy(0.25f, 0.76f, 0, -0.02f, 0, Green, true), Cy(0.27f, 0.05f, 0, 0.38f, 0, C(0.15f, 0.42f, 0.2f)), Cy(0.05f, 0.04f, 0, 0.42f, 0, Steel)], Round: true),
        new("plant", "Potted Plant", "Furniture", new(0.5f, 1.0f, 0.5f), 10f, "Concrete",
            [Cy(0.22f, 0.36f, 0, -0.32f, 0, C(0.62f, 0.32f, 0.2f), true), Cy(0.03f, 0.3f, 0, -0.0f, 0, DarkWood), Sp(0.26f, 0, 0.25f, 0, Green), Sp(0.18f, 0.12f, 0.36f, 0.05f, C(0.25f, 0.62f, 0.3f)), Sp(0.16f, -0.12f, 0.3f, -0.06f, C(0.18f, 0.5f, 0.22f))], Round: true),
        // ---------------- Construction
        new("cinder", "Cinder Block", "Construction", new(0.4f, 0.2f, 0.2f), 14f, "Concrete",
            [B(0.4f, 0.2f, 0.2f, 0, 0, 0, Concrete, true), B(0.14f, 0.205f, 0.12f, -0.1f, 0, 0, C(0.3f, 0.3f, 0.3f)), B(0.14f, 0.205f, 0.12f, 0.1f, 0, 0, C(0.3f, 0.3f, 0.3f))]),
        new("barrier", "Concrete Barrier", "Construction", new(2.0f, 0.8f, 0.6f), 400f, "Concrete",
            [B(2.0f, 0.25f, 0.6f, 0, -0.275f, 0, Concrete, true), B(2.0f, 0.3f, 0.4f, 0, -0.0f, 0, Concrete, true), B(2.0f, 0.25f, 0.18f, 0, 0.275f, 0, Concrete, true), B(1.98f, 0.06f, 0.01f, 0, 0.1f, 0.2f, Yellow)]),
        new("cone", "Traffic Cone", "Construction", new(0.36f, 0.7f, 0.36f), 2f, "Rubber",
            [B(0.4f, 0.04f, 0.4f, 0, -0.33f, 0, Orange, true), new('n', new(0.3f, 0.66f, 0.3f), new(0, 0.02f, 0), Orange, true), Cy(0.1f, 0.08f, 0, 0.05f, 0, White)], Round: false),
        new("pallet", "Wooden Pallet", "Construction", new(1.2f, 0.15f, 1.0f), 15f, "Wood",
            [B(1.2f, 0.025f, 0.14f, 0, 0.06f, -0.43f, Wood, true), B(1.2f, 0.025f, 0.14f, 0, 0.06f, -0.14f, Wood, true), B(1.2f, 0.025f, 0.14f, 0, 0.06f, 0.14f, Wood, true), B(1.2f, 0.025f, 0.14f, 0, 0.06f, 0.43f, Wood, true),
             B(0.1f, 0.1f, 1.0f, -0.55f, -0.025f, 0, DarkWood), B(0.1f, 0.1f, 1.0f, 0, -0.025f, 0, DarkWood), B(0.1f, 0.1f, 1.0f, 0.55f, -0.025f, 0, DarkWood)]),
        new("ibeam", "I-Beam", "Construction", new(4.0f, 0.3f, 0.2f), 120f, "Metal",
            [B(4.0f, 0.03f, 0.2f, 0, 0.135f, 0, C(0.62f, 0.25f, 0.15f), true), B(4.0f, 0.03f, 0.2f, 0, -0.135f, 0, C(0.62f, 0.25f, 0.15f), true), B(4.0f, 0.25f, 0.03f, 0, 0, 0, C(0.62f, 0.25f, 0.15f), true)]),
        new("pipe", "Steel Pipe", "Construction", new(0.2f, 3f, 0.2f), 30f, "Metal",
            [Cy(0.1f, 3f, 0, 0, 0, Steel, true), Cy(0.12f, 0.1f, 0, 1.45f, 0, C(0.4f, 0.42f, 0.45f)), Cy(0.12f, 0.1f, 0, -1.45f, 0, C(0.4f, 0.42f, 0.45f))], Round: true),
        new("sawhorse", "Sawhorse", "Construction", new(1.0f, 0.8f, 0.5f), 8f, "Wood",
            [B(1.0f, 0.1f, 0.1f, 0, 0.35f, 0, Wood, true), B(0.06f, 0.8f, 0.06f, -0.4f, 0, 0.12f, Yellow, false, new(-17, 0, 0)), B(0.06f, 0.8f, 0.06f, -0.4f, 0, -0.12f, Yellow, false, new(17, 0, 0)),
             B(0.06f, 0.8f, 0.06f, 0.4f, 0, 0.12f, Yellow, false, new(-17, 0, 0)), B(0.06f, 0.8f, 0.06f, 0.4f, 0, -0.12f, Yellow, false, new(17, 0, 0)), B(0.8f, 0.12f, 0.02f, 0, 0.1f, 0.2f, Orange)]),
        new("metal_crate", "Metal Crate", "Construction", new(1.0f, 1.0f, 1.0f), 60f, "Metal",
            [B(1.0f, 1.0f, 1.0f, 0, 0, 0, C(0.35f, 0.42f, 0.35f), true), B(1.02f, 0.08f, 1.02f, 0, 0.42f, 0, Steel), B(1.02f, 0.08f, 1.02f, 0, -0.42f, 0, Steel), B(0.08f, 1.02f, 1.02f, 0.42f, 0, 0, Steel), B(0.08f, 1.02f, 1.02f, -0.42f, 0, 0, Steel)]),
        new("gas_can", "Gas Canister", "Construction", new(0.35f, 1.3f, 0.35f), 25f, "Metal",
            [Cy(0.17f, 1.1f, 0, -0.08f, 0, Red, true), Sp(0.17f, 0, 0.45f, 0, Red, true), Cy(0.05f, 0.15f, 0, 0.6f, 0, Steel)], Round: true),
        new("fence", "Fence Panel", "Construction", new(2.0f, 1.2f, 0.08f), 18f, "Wood",
            [B(0.08f, 1.2f, 0.08f, -0.96f, 0, 0, DarkWood), B(0.08f, 1.2f, 0.08f, 0.96f, 0, 0, DarkWood), B(2.0f, 0.08f, 0.04f, 0, 0.35f, 0, Wood, true), B(2.0f, 0.08f, 0.04f, 0, -0.35f, 0, Wood, true),
             B(0.12f, 1.1f, 0.03f, -0.6f, 0, 0.03f, Wood, true), B(0.12f, 1.1f, 0.03f, -0.2f, 0, 0.03f, Wood, true), B(0.12f, 1.1f, 0.03f, 0.2f, 0, 0.03f, Wood, true), B(0.12f, 1.1f, 0.03f, 0.6f, 0, 0.03f, Wood, true)]),
        new("window", "Window Frame", "Construction", new(1.2f, 1.2f, 0.1f), 15f, "Wood",
            [B(1.2f, 0.1f, 0.1f, 0, 0.55f, 0, White, true), B(1.2f, 0.1f, 0.1f, 0, -0.55f, 0, White, true), B(0.1f, 1.2f, 0.1f, 0.55f, 0, 0, White, true), B(0.1f, 1.2f, 0.1f, -0.55f, 0, 0, White, true),
             B(1.0f, 0.04f, 0.06f, 0, 0, 0, White, true), B(1.0f, 1.0f, 0.01f, 0, 0, 0, C(0.6f, 0.75f, 0.85f))]),
        // ---------------- Signs & street
        new("stop_sign", "Stop Sign", "Signs", new(0.7f, 2.4f, 0.1f), 12f, "Metal",
            [Cy(0.035f, 2.0f, 0, -0.2f, 0, Steel), new('c', new(0.66f, 0.03f, 0.66f), new(0, 0.85f, 0.03f), Red, true, new(90, 22.5f, 0)), new('c', new(0.56f, 0.032f, 0.56f), new(0, 0.85f, 0.03f), White, false, new(90, 22.5f, 0)),
             new('c', new(0.52f, 0.034f, 0.52f), new(0, 0.85f, 0.03f), Red, true, new(90, 22.5f, 0)), B(0.8f, 0.04f, 0.04f, 0, -1.18f, 0, Steel)]),
        new("arrow_sign", "Arrow Sign", "Signs", new(0.9f, 0.5f, 0.06f), 4f, "Metal",
            [B(0.6f, 0.22f, 0.04f, -0.12f, 0, 0, Blue, true), B(0.3f, 0.3f, 0.04f, 0.22f, 0, 0, Blue, true, new(0, 0, 45)), B(0.5f, 0.06f, 0.045f, -0.08f, 0, 0, White), B(0.16f, 0.16f, 0.045f, 0.2f, 0, 0, White, false, new(0, 0, 45))]),
        new("warning_sign", "Warning Sign", "Signs", new(0.7f, 1.4f, 0.4f), 6f, "Metal",
            [B(0.6f, 0.6f, 0.03f, 0, 0.35f, 0, Yellow, true, new(0, 0, 45)), B(0.46f, 0.46f, 0.035f, 0, 0.35f, 0, Dark, false, new(0, 0, 45)), B(0.42f, 0.42f, 0.04f, 0, 0.35f, 0, Yellow, true, new(0, 0, 45)),
             B(0.05f, 0.18f, 0.045f, 0, 0.38f, 0, Dark), B(0.05f, 0.05f, 0.045f, 0, 0.24f, 0, Dark), Cy(0.03f, 0.9f, 0, -0.25f, 0, Steel), B(0.6f, 0.05f, 0.4f, 0, -0.68f, 0, Dark)]),
        new("billboard", "Billboard", "Signs", new(3.0f, 3.2f, 0.3f), 150f, "Metal",
            [B(3.0f, 1.5f, 0.1f, 0, 0.75f, 0, White, true), B(2.8f, 1.3f, 0.02f, 0, 0.75f, 0.06f, Blue), B(1.6f, 0.3f, 0.025f, -0.4f, 0.95f, 0.07f, Yellow), B(0.9f, 0.18f, 0.025f, -0.75f, 0.55f, 0.07f, White),
             B(0.15f, 1.7f, 0.15f, -1.0f, -0.75f, 0, Dark), B(0.15f, 1.7f, 0.15f, 1.0f, -0.75f, 0, Dark)]),
        new("street_lamp", "Street Lamp", "Signs", new(0.4f, 4.0f, 0.4f), 60f, "Metal",
            [Cy(0.2f, 0.3f, 0, -1.85f, 0, Dark, true), Cy(0.07f, 3.6f, 0, 0.05f, 0, Dark, true), B(0.12f, 0.08f, 0.9f, 0, 1.85f, 0.4f, Dark), B(0.3f, 0.1f, 0.4f, 0, 1.8f, 0.8f, Dark), B(0.26f, 0.02f, 0.34f, 0, 1.74f, 0.8f, C(1f, 0.92f, 0.7f))], Round: true),
        // ---------------- Wheels
        new("wheel_small", "Small Wheel", "Wheels", new(0.4f, 0.12f, 0.4f), 3f, "Rubber",
            [Ty(0.2f, 0.12f, 0, 0, 0, Side), new('c', new(0.18f, 0.13f, 0.18f), new(0, 0, 0), Steel, true, Side)], Round: true),
        new("wheel_car", "Car Wheel", "Wheels", new(0.7f, 0.25f, 0.7f), 18f, "Rubber",
            [Ty(0.35f, 0.25f, 0, 0, 0, Side), new('c', new(0.38f, 0.26f, 0.38f), new(0, 0, 0), Steel, true, Side), new('c', new(0.1f, 0.28f, 0.1f), new(0, 0, 0), Dark, false, Side)], Round: true),
        new("wheel_tractor", "Tractor Wheel", "Wheels", new(1.4f, 0.45f, 1.4f), 70f, "Rubber",
            [Ty(0.7f, 0.45f, 0, 0, 0, Side), new('c', new(0.8f, 0.46f, 0.8f), new(0, 0, 0), Yellow, true, Side), new('c', new(0.2f, 0.5f, 0.2f), new(0, 0, 0), Dark, false, Side)], Round: true),
        new("gear", "Gear", "Wheels", new(0.8f, 0.12f, 0.8f), 20f, "Metal",
            [new('c', new(0.7f, 0.12f, 0.7f), new(0, 0, 0), Steel, true, Side), new('c', new(0.14f, 0.14f, 0.14f), new(0, 0, 0), Dark, false, Side),
             .. Enumerable.Range(0, 10).Select(i => new Part('b', new(0.12f, 0.12f, 0.12f), new(0, MathF.Cos(i * MathF.Tau / 10) * 0.38f, MathF.Sin(i * MathF.Tau / 10) * 0.38f), Steel, true, new(-i * 36f, 0, 0)))], Round: true),
        // ---------------- Vehicle parts
        new("seat", "Vehicle Seat", "Vehicle parts", new(0.55f, 0.9f, 0.55f), 15f, "Rubber",
            [B(0.55f, 0.14f, 0.55f, 0, -0.3f, 0.02f, Dark, true), B(0.55f, 0.65f, 0.14f, 0, 0.1f, -0.22f, Dark, true, new(-10, 0, 0)), B(0.36f, 0.2f, 0.1f, 0, 0.34f, -0.27f, Dark, true, new(-10, 0, 0)),
             B(0.5f, 0.08f, 0.5f, 0, -0.41f, 0.02f, Steel)], Seat: true),
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
        Prop p = g.Props.Spawn(PropKind.Cube, pos, yaw, recordUndo);
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
            else { box.Enabled = false; box.Size = def.Collider; box.Enabled = true; }
        }
        p.Body.Mass = def.Mass;
        p.SetSurface(SurfaceMaterial.Get(def.Surface));
        // Paint parts use the prop's own material: pick the first paint colour as the prop colour.
        if (def.Parts.FirstOrDefault(x => x.Paint) is { } paint) p.SetColor(paint.Color);
        if (p.Go.GetComponent<MeshRenderer>() is { } root) root.Enabled = false;
        BuildParts(p.Go, def, g.Shader, p.Material);
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
            r.Mesh = part.Shape switch
            {
                'c' => Meshes.RoundedCylinder(s.X / 2f, s.Y, min * 0.15f, 28),
                's' => Meshes.Sphere(s.X / 2f),
                't' => Meshes.Tyre(s.X / 2f, s.Y),
                'n' => Meshes.Lathe($"cone{s.X:0.###},{s.Y:0.###}", [(0f, -s.Y / 2f), (s.X / 2f, -s.Y / 2f), (s.X * 0.12f, s.Y / 2f), (0f, s.Y / 2f)], 28),
                _ => Meshes.RoundedBox(s, min * 0.18f, 2),
            };
            if (part.Paint) r.Material = paint;
            else
            {
                if (!mats.TryGetValue(part.Color, out Material? m))
                {
                    m = Mats.New(shader);
                    m.SetColor("_MainColor", part.Color);
                    mats[part.Color] = m;
                }
                r.Material = m;
            }
        }
    }
}

/// <summary>A seat the player can sit in with E (weld it into a contraption to ride it). E again stands up.</summary>
public sealed class Seat : MonoBehaviour
{
    public ModelDef? Def;
    public Float3 SitOffset => new(0f, (Def?.Collider.Y ?? 0.9f) * 0.05f - 0.2f, 0.02f);
}
