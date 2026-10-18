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
}
