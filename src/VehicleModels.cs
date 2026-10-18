using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;
using static Sandbox.Meshes;

namespace Sandbox;

/// <summary>Smooth swept/lathed vehicle bodies and parts (vehicle-local space, +Z forward). Colliders stay boxes.</summary>
public static class VehicleModels
{
    private static readonly Dictionary<string, Mesh> _cache = new();
    private static Float3 V(float x, float y, float z) => new(x, y, z);
    private static Mesh C(string k, Func<Mesh> f) => _cache.TryGetValue(k, out Mesh? m) ? m : _cache[k] = f();

    /// <summary>Round tube through the points.</summary>
    public static Mesh Pipe(float r, Float3 side, params Float3[] pts)
        => Sweep(pts.Select(p => new Sec(p, r, r)).ToArray(), side, 12, 0.5f);

    /// <summary>Tube along a path with rounded bends: inserts arc points at each interior corner.</summary>
    public static Mesh BentPipe(float r, float bend, Float3 side, params Float3[] pts)
    {
        var o = new List<Float3> { pts[0] };
        for (int i = 1; i < pts.Length - 1; i++)
        {
            Float3 a = pts[i - 1], b = pts[i], c = pts[i + 1];
            Float3 p0 = b + Float3.Normalize(a - b) * bend, p1 = b + Float3.Normalize(c - b) * bend;
            for (int k = 0; k <= 4; k++) { float t = k / 4f; Float3 q0 = p0 + (b - p0) * t, q1 = b + (p1 - b) * t; o.Add(q0 + (q1 - q0) * t); }
        }
        o.Add(pts[^1]);
        return Pipe(r, side, o.ToArray());
    }

    /// <summary>Body tub: (z, halfWidth, halfHeight, yCentre, squareness).</summary>
    public static Mesh Tub(params (float z, float w, float h, float y, float n)[] s)
        => Sweep(s.Select(q => new Sec(V(0f, q.y, q.z), q.w, q.h, q.n)).ToArray(), Float3.UnitX, 32, 0.35f);

    public static Mesh BuggyBody() => C("buggy", () => Tub(
        (-1.38f, 0.66f, 0.15f, 0.02f, 4f), (-1.1f, 0.74f, 0.17f, 0f, 4.5f), (0.4f, 0.74f, 0.17f, 0f, 4.5f), (0.95f, 0.6f, 0.15f, 0.02f, 4f),
        (1.3f, 0.42f, 0.11f, 0.05f, 3.5f), (1.42f, 0.3f, 0.07f, 0.07f, 3f)));

    /// <summary>Buggy nose cowl: rises from the cockpit to a rounded hump over the front axle, then drops to the bumper.</summary>
    public static Mesh BuggyNose() => C("bnose", () => Tub(
        (0.25f, 0.62f, 0.05f, 0.2f, 3.5f), (0.45f, 0.66f, 0.12f, 0.24f, 3.6f), (0.8f, 0.64f, 0.15f, 0.25f, 3.4f), (1.1f, 0.56f, 0.13f, 0.22f, 3.2f),
        (1.32f, 0.42f, 0.09f, 0.16f, 3f), (1.44f, 0.28f, 0.05f, 0.11f, 2.6f)));

    public static Mesh BuggyDash() => C("bdash", () => Sweep([
        new Sec(V(0f, 0.38f, 0.22f), 0.5f, 0.05f, 3f), new Sec(V(0f, 0.43f, 0.32f), 0.52f, 0.06f, 3f), new Sec(V(0f, 0.4f, 0.4f), 0.5f, 0.04f, 3f)], Float3.UnitX, 20, 0.6f));

    /// <summary>Flat-four style engine: rounded block with finned cylinder heads either side and a fan shroud.</summary>
    public static Mesh BuggyEngine() => C("bengine", () =>
    {
        var parts = new List<Mesh> { RoundedBox(V(0.5f, 0.3f, 0.36f), 0.07f) };
        foreach (float sx in new[] { -1f, 1f })
            for (int i = 0; i < 4; i++)
                parts.Add(Sweep([new Sec(V(sx * 0.25f, -0.02f, -0.12f + i * 0.08f), 0.004f, 0.11f, 4f, Float3.UnitZ), new Sec(V(sx * 0.33f, -0.02f, -0.12f + i * 0.08f), 0.004f, 0.1f, 4f, Float3.UnitZ)], Float3.UnitZ, 10, 0.3f));
        parts.Add(Sweep([new Sec(V(0f, 0.15f, -0.1f), 0.18f, 0.03f, 3f), new Sec(V(0f, 0.17f, 0.1f), 0.18f, 0.03f, 3f)], Float3.UnitX, 16, 0.6f));
        return Merge(parts.ToArray());
    });

    /// <summary>Rear fender hugging the tyre at its real ride height (the wheel settles ~0.22 below its design point,
    /// so the old arch floated like a spoiler): a flared arch with a rolled lip, plus a side skirt joining it to the tub.</summary>
    public static Mesh BuggyArch(float x, float z, float r) => C($"barch2{x}{z}{r}", () =>
    {
        float cy = -0.37f, R = r + 0.065f, sx = MathF.Sign(x);
        var arch = new List<Sec>(); var lip = new List<Sec>();
        for (int i = 0; i <= 16; i++)
        {
            float a = MathF.PI * (0.1f + 0.68f * i / 16f), sy = MathF.Sin(a), cz = MathF.Cos(a);
            arch.Add(new Sec(V(x, cy + sy * R, z - cz * R), 0.16f, 0.018f, 5f, Float3.UnitX));
            lip.Add(new Sec(V(x + sx * 0.16f, cy + sy * (R - 0.01f), z - cz * (R - 0.01f)), 0.022f, 0.024f, 2f, Float3.UnitX));
        }
        // Skirt: vertical panel on the inner side from the arch up to the deck, so the fender grows out of the body.
        Mesh skirt = Sweep([new Sec(V(x - sx * 0.12f, -0.02f, z - 0.42f), 0.012f, 0.1f, 4f, Float3.UnitX), new Sec(V(x - sx * 0.12f, 0.05f, z), 0.012f, 0.1f, 4f, Float3.UnitX), new Sec(V(x - sx * 0.12f, 0.02f, z + 0.4f), 0.012f, 0.09f, 4f, Float3.UnitX)], Float3.UnitX, 10, 0.4f);
        return Merge(Sweep(arch, Float3.UnitX, 16, 0.5f), Sweep(lip, Float3.UnitX, 12, 0.6f), skirt);
    });

    public static Mesh JeepBody() => C("jeep", () => Tub(
        (-1.68f, 0.86f, 0.25f, 0.02f, 5f), (-1.5f, 0.9f, 0.27f, 0f, 5.5f), (0.45f, 0.9f, 0.27f, 0f, 5.5f), (0.6f, 0.88f, 0.3f, 0.04f, 5.5f),
        (1.5f, 0.86f, 0.3f, 0.06f, 5f), (1.68f, 0.82f, 0.26f, 0.06f, 4.5f)));

    public static Mesh JeepHood() => C("hood", () => Tub(
        (0.5f, 0.8f, 0.1f, 0.42f, 4f), (0.6f, 0.82f, 0.14f, 0.4f, 4.5f), (1.5f, 0.8f, 0.13f, 0.38f, 4.5f), (1.7f, 0.74f, 0.1f, 0.36f, 4f)));

    public static Mesh Fender(float x, float z, float r) => C($"fend{x}{z}{r}", () =>
    {
        var s = new List<Sec>();
        for (int i = 0; i <= 12; i++)
        {
            float a = MathF.PI * (0.08f + 0.84f * i / 12f);
            s.Add(new Sec(V(x, -0.2f + MathF.Sin(a) * (r + 0.1f), z - MathF.Cos(a) * (r + 0.1f)), 0.22f, 0.035f, 4f, Float3.UnitX));
        }
        return Sweep(s, Float3.UnitX, 16, 0.5f);
    });

    /// <summary>Bucket seat: cushion + back as one swept L.</summary>
    public static Mesh Seat(float w) => C($"seat{w}", () => Sweep([
        new Sec(V(0f, 0.0f, 0.22f), w, 0.06f, 3f), new Sec(V(0f, 0.0f, -0.12f), w, 0.07f, 3f), new Sec(V(0f, 0.08f, -0.24f), w, 0.07f, 3f),
        new Sec(V(0f, 0.38f, -0.3f), w * 0.95f, 0.06f, 3f), new Sec(V(0f, 0.55f, -0.32f), w * 0.85f, 0.05f, 3f)], Float3.UnitX, 20, 0.6f));

    public static Mesh Lamp(float r) => C($"lamp{r}", () => LatheZ([(0.0001f, -0.04f), (r * 0.85f, -0.04f), (r, 0f), (r, 0.03f), (r * 0.8f, 0.045f), (0.0001f, 0.05f)]));

    public static Mesh Grille() => C("grille", () =>
    {
        var parts = new List<Mesh>();
        return Sweep([new Sec(V(0f, 0.3f, 1.69f), 0.58f, 0.2f, 6f), new Sec(V(0f, 0.3f, 1.73f), 0.56f, 0.19f, 6f)], Float3.UnitX, 24, 0.4f);
    });

    public static Mesh Windshield() => C("wind", () => Sweep([new Sec(V(0f, -0.3f, 0f), 0.78f, 0.02f, 6f), new Sec(V(0f, 0.3f, 0f), 0.78f, 0.02f, 6f)], Float3.UnitX, 20, 0.4f));

    public static Mesh Hull() => C("hull", () => Sweep([
        new Sec(V(0f, 0.02f, -1.8f), 0.9f, 0.16f, 4f), new Sec(V(0f, 0f, -1.4f), 0.95f, 0.17f, 4f), new Sec(V(0f, 0f, 1.1f), 0.95f, 0.17f, 3.6f),
        new Sec(V(0f, 0.08f, 1.7f), 0.85f, 0.14f, 3f), new Sec(V(0f, 0.2f, 2.1f), 0.62f, 0.09f, 2.6f), new Sec(V(0f, 0.27f, 2.26f), 0.36f, 0.05f, 2.4f)],
        Float3.UnitX, 32, 0.4f));

    public static Mesh Blade() => C("blade", () => Sweep([
        new Sec(V(0f, 0.05f, 0f), 0.05f, 0.014f, 2f), new Sec(V(0f, 0.35f, 0f), 0.085f, 0.012f, 2f), new Sec(V(0f, 0.68f, 0f), 0.06f, 0.008f, 2f)], Float3.UnitX, 14, 0.8f));

    public static Mesh Ring(float r, float t) => C($"ring{r}{t}", () =>
    {
        var s = new List<Sec>();
        for (int i = 0; i <= 40; i++) { float a = MathF.Tau * i / 40f; s.Add(new Sec(V(MathF.Cos(a) * r, MathF.Sin(a) * r, 0f), t, t * 1.6f, 2.5f, Float3.UnitZ)); }
        return Sweep(s, Float3.UnitZ, 12, 0f, true, true);
    });

    public static Mesh Hub(float r) => C($"hub{r}", () => LatheZ([(0.0001f, 0.06f), (r * 0.5f, 0.055f), (r * 0.62f, 0.03f), (r * 0.62f, -0.03f), (0.0001f, -0.03f)]));

    /// <summary>Aluminium diamond treadplate: a thin sheet with staggered raised lozenges (alternating ±45° pairs).</summary>
    public static Mesh TreadPlate(float w, float l, float y) => C($"tread{w}{l}{y}", () =>
    {
        var parts = new List<Mesh> { Off(RoundedBox(V(w, 0.012f, l), 0.004f), 0f, y, 0f) };
        float step = 0.085f;
        int nx = (int)(w / step) - 1, nz = (int)(l / step) - 1;
        for (int i = 0; i < nx; i++)
            for (int k = 0; k < nz; k++)
            {
                float x = -w / 2f + step * (i + 1), z = -l / 2f + step * (k + 1);
                float a = ((i + k) % 2 == 0 ? 0.785f : -0.785f), c = MathF.Cos(a), sn = MathF.Sin(a);
                Mesh m = Mesh.CreateCube(V(0.008f, 0.004f, 0.034f));
                parts.Add(new Mesh { IndexFormat = m.IndexFormat, Vertices = m.Vertices!.Select(v => V(v.X * c + v.Z * sn + x, v.Y + y + 0.008f, -v.X * sn + v.Z * c + z)).ToArray(),
                    Normals = m.Normals!.Select(v => V(v.X * c + v.Z * sn, v.Y, -v.X * sn + v.Z * c)).ToArray(), UV = m.UV, Indices = m.Indices });
            }
        return Merge(parts.ToArray());
    });

    private static Mesh Off(Mesh m, float x, float y, float z)
        => new() { IndexFormat = m.IndexFormat, Vertices = m.Vertices!.Select(v => V(v.X + x, v.Y + y, v.Z + z)).ToArray(), Normals = m.Normals, UV = m.UV, Indices = m.Indices };

    /// <summary>Mooring cleat: a low horn bar on two feet.</summary>
    public static Mesh Cleat() => C("cleat", () => Merge(
        Sweep([new Sec(V(-0.08f, 0.035f, 0f), 0.006f, 0.008f), new Sec(V(-0.05f, 0.045f, 0f), 0.011f, 0.012f), new Sec(V(0.05f, 0.045f, 0f), 0.011f, 0.012f), new Sec(V(0.08f, 0.035f, 0f), 0.006f, 0.008f)], Float3.UnitZ, 10, 0.8f),
        Off(RoundedBox(V(0.03f, 0.04f, 0.022f), 0.006f), -0.03f, 0.02f, 0f), Off(RoundedBox(V(0.03f, 0.04f, 0.022f), 0.006f), 0.03f, 0.02f, 0f)));
}
