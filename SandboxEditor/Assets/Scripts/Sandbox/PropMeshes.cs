using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;
using static Sandbox.Meshes;

namespace Sandbox;

/// <summary>Lathed/swept meshes for model props (Y up, centred on the part offset).</summary>
public static class PropMeshes
{
    private static Float3 V(float x, float y, float z) => new(x, y, z);
    private static (float, float)[] Rnd((float R, float Y)[] p) => p;

    /// <summary>Round tapered furniture leg of height h (top radius r).</summary>
    public static Mesh Leg(float r, float h) => Lathe($"leg{r:0.###},{h:0.###}",
        [(0.0001f, -h / 2f), (r * 0.62f, -h / 2f), (r * 0.7f, -h / 2f + 0.012f), (r * 0.85f, 0f), (r, h / 2f - 0.03f), (r * 1.08f, h / 2f - 0.01f), (r * 1.08f, h / 2f), (0.0001f, h / 2f)], 20);

    /// <summary>Puffy cushion: a soft superellipse slab along X.</summary>
    public static Mesh Cushion(float w, float h, float d) => Sweep([
        new Sec(V(-w / 2f, 0f, 0f), h * 0.42f, d * 0.46f, 2.6f), new Sec(V(-w / 2f + h * 0.4f, 0f, 0f), h * 0.5f, d * 0.5f, 3.2f),
        new Sec(V(0f, 0f, 0f), h * 0.56f, d * 0.5f, 3.4f), new Sec(V(w / 2f - h * 0.4f, 0f, 0f), h * 0.5f, d * 0.5f, 3.2f), new Sec(V(w / 2f, 0f, 0f), h * 0.42f, d * 0.46f, 2.6f)],
        Float3.UnitY, 24, 0.7f);

    public static Mesh LampBase() => Lathe("lampbase", [(0.0001f, -0.03f), (0.19f, -0.03f), (0.2f, -0.015f), (0.16f, 0.02f), (0.05f, 0.035f), (0.03f, 0.05f), (0.0001f, 0.05f)], 36);
    public static Mesh LampShade() => Lathe("lampshade", [(0.2f, -0.15f), (0.205f, -0.14f), (0.13f, 0.15f), (0.125f, 0.16f), (0.11f, 0.15f), (0.185f, -0.13f), (0.19f, -0.15f)], 36);

    public static Mesh TrashCan() => Lathe("bin", [(0.0001f, -0.4f), (0.2f, -0.4f), (0.215f, -0.385f), (0.245f, 0.3f), (0.26f, 0.32f), (0.262f, 0.36f), (0.235f, 0.37f), (0.235f, 0.38f)], 40);
    public static Mesh BinLid() => Lathe("binlid", [(0.27f, -0.02f), (0.275f, 0f), (0.24f, 0.04f), (0.12f, 0.07f), (0.05f, 0.075f), (0.045f, 0.1f), (0.0001f, 0.11f)], 40);

    public static Mesh Cone() => Lathe("tcone", [(0.0001f, -0.33f), (0.16f, -0.33f), (0.165f, -0.31f), (0.035f, 0.32f), (0.02f, 0.34f), (0.0001f, 0.345f)], 36);
    public static Mesh ConeBase() => Sweep([new Sec(V(0f, -0.335f, 0f), 0.2f, 0.2f, 5f, Float3.UnitX), new Sec(V(0f, -0.315f, 0f), 0.2f, 0.2f, 5f, Float3.UnitX)], Float3.UnitX, 32, 0.3f);
    public static Mesh ConeBand(float y, float r) => Lathe($"cband{y}{r}", [(r, y - 0.04f), (r - 0.014f, y + 0.04f)], 36);

    public static Mesh GasCan() => Lathe("gascan", [(0.0001f, -0.63f), (0.15f, -0.63f), (0.17f, -0.6f), (0.17f, 0.36f), (0.15f, 0.48f), (0.09f, 0.55f), (0.05f, 0.57f), (0.0001f, 0.57f)], 36);

    public static Mesh LampArm() => Sweep([new Sec(V(0f, 0f, 0f), 0.04f, 0.04f), new Sec(V(0f, 0.18f, 0.05f), 0.035f, 0.035f), new Sec(V(0f, 0.26f, 0.25f), 0.03f, 0.03f), new Sec(V(0f, 0.25f, 0.6f), 0.03f, 0.03f), new Sec(V(0f, 0.2f, 0.8f), 0.03f, 0.03f)], Float3.UnitX, 14, 0.5f);
    public static Mesh StreetHead() => Sweep([new Sec(V(0f, 0.05f, -0.22f), 0.08f, 0.04f, 2.4f), new Sec(V(0f, 0.04f, 0f), 0.16f, 0.06f, 2.4f), new Sec(V(0f, 0.02f, 0.2f), 0.12f, 0.04f, 2.4f)], Float3.UnitX, 24, 0.6f);
    public static Mesh Post(float r, float h) => Lathe($"post{r},{h}", [(0.0001f, -h / 2f), (r * 1.9f, -h / 2f), (r * 1.9f, -h / 2f + 0.12f), (r * 1.3f, -h / 2f + 0.25f), (r, -h / 2f + 0.5f), (r * 0.8f, h / 2f), (0.0001f, h / 2f)], 28);
    public static Mesh Handle(float len) => Sweep([new Sec(V(0f, -len / 2f, -0.03f), 0.013f, 0.013f), new Sec(V(0f, -len / 2f + 0.02f, 0f), 0.014f, 0.014f), new Sec(V(0f, len / 2f - 0.02f, 0f), 0.014f, 0.014f), new Sec(V(0f, len / 2f, -0.03f), 0.013f, 0.013f)], Float3.UnitX, 12, 0.5f);

    /// <summary>Round rod a→b (radius r), softly capped. Cached by endpoints.</summary>
    public static Mesh Rod(Float3 a, Float3 b, float r, float r1 = -1f)
    {
        float rb = r1 < 0f ? r : r1;
        Float3 d = b - a; Float3 side = MathF.Abs(d.Y) > MathF.Abs(d.X) + MathF.Abs(d.Z) ? Float3.UnitX : Float3.UnitY;
        return Sweep([new Sec(a, r, r, 2f), new Sec((a + b) * 0.5f, (r + rb) / 2f, (r + rb) / 2f, 2f), new Sec(b, rb, rb, 2f)], side, 14, 0.5f);
    }

    /// <summary>Panel with a raised border and recessed field (w × h, depth t), facing +Z. Reads as a moulded / pressed panel.</summary>
    public static Mesh InsetPanel(float w, float h, float t, float border)
    {
        var frame = new List<Mesh>
        {
            RoundedBox(V(w, border, t), MathF.Min(0.008f, t * 0.4f)), RoundedBox(V(w, border, t), MathF.Min(0.008f, t * 0.4f)),
            RoundedBox(V(border, h, t), MathF.Min(0.008f, t * 0.4f)), RoundedBox(V(border, h, t), MathF.Min(0.008f, t * 0.4f)),
        };
        Mesh Off(Mesh m, Float3 o) { var mm = new Mesh { IndexFormat = m.IndexFormat, Vertices = m.Vertices.Select(v => v + o).ToArray(), Normals = m.Normals, UV = m.UV, Indices = m.Indices }; mm.RecalculateBounds(); return mm; }
        return Merge(Off(frame[0], V(0, h / 2f - border / 2f, 0)), Off(frame[1], V(0, -h / 2f + border / 2f, 0)), Off(frame[2], V(-w / 2f + border / 2f, 0, 0)), Off(frame[3], V(w / 2f - border / 2f, 0, 0)),
            Off(RoundedBox(V(w - border * 1.6f, h - border * 1.6f, t * 0.45f), MathF.Min(0.006f, t * 0.2f)), V(0, 0, -t * 0.25f)));
    }

    /// <summary>Jersey barrier: a 2D profile (z, y) extruded along X with softened ends.</summary>
    public static Mesh Jersey()
    {
        (float z, float y)[] prof = [(0.3f, -0.4f), (0.3f, -0.32f), (0.2f, -0.18f), (0.1f, 0.38f), (0.08f, 0.4f)];
        var secs = new List<Sec>();
        // Superellipse slabs can't do a profile, so stack thin horizontal rounded slabs: widths follow the profile.
        var parts = new List<Sec>();
        for (int i = 0; i <= 16; i++)
        {
            float y = -0.4f + 0.8f * i / 16f, w = 0f;
            for (int k = 0; k < prof.Length - 1; k++) if (y >= prof[k].y && y <= prof[k + 1].y) { float t = (y - prof[k].y) / MathF.Max(1e-4f, prof[k + 1].y - prof[k].y); w = prof[k].z + (prof[k + 1].z - prof[k].z) * t; }
            parts.Add(new Sec(V(0f, y, 0f), 0.98f, w, 6f, Float3.UnitX));
        }
        return Sweep(parts, Float3.UnitX, 32, 0.25f);
    }

    /// <summary>Cast-iron bench side: back foot up the backrest, seat rail forward, curled armrest.</summary>
    public static Mesh BenchSide() => Sweep([
        new Sec(V(0f, -0.42f, -0.26f), 0.03f, 0.022f, 3f, Float3.UnitX), new Sec(V(0f, -0.05f, -0.24f), 0.028f, 0.02f, 3f, Float3.UnitX), new Sec(V(0f, 0.2f, -0.28f), 0.026f, 0.02f, 3f, Float3.UnitX),
        new Sec(V(0f, 0.4f, -0.3f), 0.024f, 0.018f, 3f, Float3.UnitX)], Float3.UnitX, 12, 0.6f);
    // Front leg curving under the seat to the back upright, plus a separate armrest that runs forward from the
    // backrest and scrolls down onto the seat front. (Was one sweep that zig-zagged back through the seat.)
    public static Mesh BenchLeg() => Merge(
        Sweep([new Sec(V(0f, -0.42f, 0.25f), 0.03f, 0.022f, 3f, Float3.UnitX), new Sec(V(0f, -0.03f, 0.22f), 0.026f, 0.02f, 3f, Float3.UnitX), new Sec(V(0f, -0.01f, 0.05f), 0.024f, 0.022f, 3f, Float3.UnitX),
            new Sec(V(0f, -0.01f, -0.24f), 0.024f, 0.022f, 3f, Float3.UnitX)], Float3.UnitX, 12, 0.6f),
        Sweep([new Sec(V(0f, 0.2f, -0.28f), 0.02f, 0.016f, 3f, Float3.UnitX), new Sec(V(0f, 0.23f, -0.05f), 0.022f, 0.018f, 3f, Float3.UnitX), new Sec(V(0f, 0.22f, 0.2f), 0.024f, 0.02f, 3f, Float3.UnitX),
            new Sec(V(0f, 0.17f, 0.26f), 0.022f, 0.018f, 3f, Float3.UnitX), new Sec(V(0f, 0.08f, 0.25f), 0.02f, 0.016f, 3f, Float3.UnitX), new Sec(V(0f, 0.035f, 0.22f), 0.02f, 0.016f, 3f, Float3.UnitX)], Float3.UnitX, 12, 0.6f));

    public static Mesh SignFoot() => Sweep([new Sec(V(0f, -0.02f, -0.2f), 0.28f, 0.02f, 4f), new Sec(V(0f, 0.01f, 0f), 0.3f, 0.03f, 4f), new Sec(V(0f, -0.02f, 0.2f), 0.28f, 0.02f, 4f)], Float3.UnitX, 20, 0.6f);

    /// <summary>Wheel rim along Y: dished lathe with a hub and n swept spokes (spokes along the disc).</summary>
    private static readonly Dictionary<string, Mesh> _rims = new();
    public static Mesh Rim(float r, float w, int spokes) => _rims.TryGetValue($"{r},{w},{spokes}", out Mesh? m) ? m : _rims[$"{r},{w},{spokes}"] = BuildRim(r, w, spokes);
    private static Mesh BuildRim(float r, float w, int spokes)
    {
        var parts = new List<Mesh> { Lathe($"rim{r},{w}", [(r * 0.8f, -w / 2f), (r, -w / 2f), (r * 1.02f, -w / 2f + 0.01f), (r * 1.02f, w / 2f - 0.01f), (r, w / 2f), (r * 0.82f, w / 2f), (r * 0.86f, 0f), (r * 0.8f, -w / 2f)], 36) };
        _ = parts;
        // A single swept star: hub → spoke tips around the circle, kept as one tube loop so it stays one mesh.
        var secs = new List<Sec>();
        for (int i = 0; i <= spokes * 2; i++)
        {
            float a = MathF.Tau * i / (spokes * 2), rr = i % 2 == 0 ? r * 0.18f : r * 0.86f;
            secs.Add(new Sec(V(MathF.Cos(a) * rr, w * 0.38f, MathF.Sin(a) * rr), r * 0.06f, w * 0.1f, 2.6f, Float3.UnitY));
        }
        return Merge(parts[0], Sweep(secs, Float3.UnitY, 10, 0f, true, true));
    }

    /// <summary>Agricultural steel wheel along Y: rolled rim barrel with bead flanges, a dished centre disc welded inside it,
    /// a raised hub boss and a pressed stiffening ring. Painted part only; <see cref="TractorRimDark"/> adds holes and nuts.</summary>
    public static Mesh TractorRim(float r, float w)
    {
        Mesh barrel = Lathe($"trim{r},{w}", [(r * 0.9f, -w / 2f), (r * 1.06f, -w / 2f), (r * 1.08f, -w / 2f + 0.02f), (r * 1.0f, -w / 2f + 0.05f), (r * 0.97f, -w * 0.2f),
            (r * 0.97f, w * 0.2f), (r * 1.0f, w / 2f - 0.05f), (r * 1.08f, w / 2f - 0.02f), (r * 1.06f, w / 2f), (r * 0.9f, w / 2f), (r * 0.92f, -w / 2f + 0.01f)], 48);
        float d = w * 0.38f; // disc face sits a little proud of centre, dished toward the hub
        Mesh disc = Lathe($"tdisc{r},{w}", [(0.0001f, d + 0.06f), (r * 0.17f, d + 0.06f), (r * 0.2f, d + 0.045f), (r * 0.26f, d + 0.03f), (r * 0.4f, d + 0.015f),
            (r * 0.44f, d + 0.03f), (r * 0.48f, d + 0.015f), (r * 0.78f, d - 0.02f), (r * 0.94f, d - 0.03f), (r * 0.94f, d - 0.05f), (r * 0.5f, d - 0.025f), (0.0001f, d), ], 48);
        return Merge(barrel, disc);
    }
    /// <summary>Dark details for <see cref="TractorRim"/>: eight hand holes in the disc, a ring of wheel nuts and a hub cap.</summary>
    public static Mesh TractorRimDark(float r, float w)
    {
        float d = w * 0.38f;
        var ms = new List<Mesh>();
        for (int i = 0; i < 8; i++)
        {
            float a = MathF.Tau * (i + 0.5f) / 8f, rr = r * 0.63f;
            float y = d + 0.015f - (rr - r * 0.48f) / (r * 0.3f) * 0.035f + 0.004f;
            ms.Add(VehicleModels.Off(RoundedCylinder(r * 0.12f, 0.006f, 0.002f, 20), MathF.Cos(a) * rr, y, MathF.Sin(a) * rr));
        }
        for (int i = 0; i < 10; i++)
        {
            float a = MathF.Tau * i / 10f, rr = r * 0.32f;
            ms.Add(VehicleModels.Off(RoundedCylinder(0.016f, 0.03f, 0.005f, 6), MathF.Cos(a) * rr, d + 0.035f, MathF.Sin(a) * rr));
        }
        ms.Add(VehicleModels.Off(Lathe($"tcap{r}", [(0.0001f, 0f), (r * 0.13f, 0f), (r * 0.13f, 0.02f), (r * 0.1f, 0.04f), (0.0001f, 0.046f)], 28), 0f, d + 0.06f, 0f));
        return Merge(ms.ToArray());
    }

    /// <summary>Gear teeth ring + web along Y.</summary>
    public static Mesh GearTeeth(int n, float r, float tooth, float w)
    {
        var secs = new List<Sec>();
        int m = n * 8;
        for (int i = 0; i <= m; i++)
        {
            float a = MathF.Tau * i / m, ph = (i % 8) / 8f;
            float rr = r + (ph is > 0.15f and < 0.6f ? tooth : 0f);
            secs.Add(new Sec(V(MathF.Cos(a) * rr, 0f, MathF.Sin(a) * rr), 0.025f, w / 2f, 4f, Float3.UnitY));
        }
        return Sweep(secs, Float3.UnitY, 8, 0f, true, true);
    }
}
