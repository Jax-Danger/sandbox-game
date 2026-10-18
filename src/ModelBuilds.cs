using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;
using static Sandbox.Meshes;

namespace Sandbox;

/// <summary>Hand-built detailed models (pallet, I-beam, window, sawhorse, signs, TV, bed, fridge) written as code
/// instead of long part lists: planks with gaps and nails, filleted steel, moulded sashes, lettered signs.</summary>
public static partial class ModelProps
{
    private static Float3 P3(float x, float y, float z) => new(x, y, z);
    private static Part RB(float x, float y, float z, float r, float ox, float oy, float oz, Color c, bool paint = false, Float3 rot = default)
        => M(RoundedBox(P3(x, y, z), r), ox, oy, oz, c, paint, rot);

    /// <summary>Mesh moved by an offset (for baking many small pieces into one draw).</summary>
    private static Mesh At(Mesh m, Float3 o)
    {
        var mm = new Mesh { IndexFormat = m.IndexFormat, Vertices = m.Vertices.Select(v => v + o).ToArray(), Normals = m.Normals, UV = m.UV, Indices = m.Indices };
        mm.RecalculateBounds(); return mm;
    }
    private static Mesh Bake(IEnumerable<(Mesh M, Float3 O)> ms) => Merge(ms.Select(x => At(x.M, x.O)).ToArray());

    /// <summary>Convex 2D polygon (counter-clockwise seen from +Z) extruded between z0 and z1.</summary>
    private static Mesh Slab(Float2[] p, float z0, float z1)
    {
        var faces = new List<Float3[]> { p.Select(q => P3(q.X, q.Y, z1)).ToArray(), p.Reverse().Select(q => P3(q.X, q.Y, z0)).ToArray() };
        for (int i = 0; i < p.Length; i++)
        {
            Float2 a = p[i], b = p[(i + 1) % p.Length];
            faces.Add([P3(a.X, a.Y, z0), P3(b.X, b.Y, z0), P3(b.X, b.Y, z1), P3(a.X, a.Y, z1)]);
        }
        return Faces(faces.ToArray());
    }
    private static Float2[] Rect(float x0, float y0, float x1, float y1) => [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)];
    private static Float2[] NGon(int n, float r, float rot0 = 0f) => Enumerable.Range(0, n).Select(i => { float a = rot0 + i * MathF.Tau / n; return new Float2(MathF.Cos(a) * r, MathF.Sin(a) * r); }).ToArray();
    /// <summary>Thin plate with rounded corners in the XY plane (half sizes w, h), thickness t, corner exponent n.</summary>
    private static Mesh Plate(float w, float h, float t, float n = 6f) => Sweep([new Sec(P3(0, 0, -t / 2f), w, h, n), new Sec(P3(0, 0, t / 2f), w, h, n)], Float3.UnitX, 40, 0.0f);

    /// <summary>Block letters from rectangles (unit cell 0.6 wide × 1 tall), scaled by h, baked as slabs z0..z1.</summary>
    private static Mesh Letters(string text, float h, float gap, float z0, float z1)
    {
        float s = h, w = 0.6f * s, t = 0.17f * s, x = -(text.Length * w + (text.Length - 1) * gap) / 2f, y0 = -s / 2f;
        var slabs = new List<Mesh>();
        // Mirrored in X: the engine's +X is screen-left when looking at a sign's +Z face.
        void R(float a, float b, float c, float d) => slabs.Add(Slab(Rect(-(x + c * w), y0 + b * s, -(x + a * w), y0 + d * s), z0, z1));
        float tw = t / w, th = t / s;
        foreach (char ch in text)
        {
            switch (ch)
            {
                case 'S': R(0, 1 - th, 1, 1); R(0, 0.5f - th / 2, 1, 0.5f + th / 2); R(0, 0, 1, th); R(0, 0.5f, tw, 1); R(1 - tw, 0, 1, 0.5f); break;
                case 'T': R(0, 1 - th, 1, 1); R(0.5f - tw / 2, 0, 0.5f + tw / 2, 1 - th); break;
                case 'O': R(0, 1 - th, 1, 1); R(0, 0, 1, th); R(0, th, tw, 1 - th); R(1 - tw, th, 1, 1 - th); break;
                case 'P': R(0, 0, tw, 1); R(tw, 1 - th, 1, 1); R(tw, 0.5f - th / 2, 1, 0.5f + th / 2); R(1 - tw, 0.5f, 1, 1 - th); break;
            }
            x += w + gap;
        }
        return Merge(slabs.ToArray());
    }

    // Properties, not fields: static initialisers in another partial file may run after ModelProps.All is built.
    private static Color Galv => C(0.62f, 0.64f, 0.66f);
    private static Color Glass => C(0.42f, 0.58f, 0.68f);
    private static Color Brass => C(0.78f, 0.62f, 0.3f);
    private static Color Linen => C(0.76f, 0.75f, 0.71f);

    private static Part[] PalletParts()
    {
        var l = new List<Part>();
        float[] tz = [-0.43f, -0.215f, 0f, 0.215f, 0.43f], tw = [0.14f, 0.1f, 0.14f, 0.1f, 0.14f];
        Color[] shades = [C(0.62f, 0.45f, 0.28f), C(0.56f, 0.4f, 0.24f), C(0.66f, 0.5f, 0.32f), C(0.58f, 0.43f, 0.27f), C(0.6f, 0.44f, 0.26f)];
        for (int i = 0; i < 5; i++) l.Add(RB(1.2f, 0.022f, tw[i], 0.005f, 0, 0.055f, tz[i], shades[i], i % 2 == 0, P3(0, (i - 2) * 0.35f, 0)));
        foreach (float x in new[] { -0.53f, 0f, 0.53f }) l.Add(RB(0.14f, 0.022f, 1.0f, 0.005f, x, 0.033f, 0, C(0.55f, 0.39f, 0.23f), true));
        var blocks = new List<(Mesh, Float3)>(); var nails = new List<(Mesh, Float3)>();
        Mesh blk = RoundedBox(P3(0.14f, 0.075f, 0.12f), 0.008f), nail = RoundedCylinder(0.0055f, 0.004f, 0.0015f, 10);
        foreach (float x in new[] { -0.53f, 0f, 0.53f })
            foreach (float z in new[] { -0.43f, 0f, 0.43f })
            {
                blocks.Add((blk, P3(x, -0.0155f, z)));
                foreach (float dx in new[] { -0.035f, 0.035f }) { nails.Add((nail, P3(x + dx, 0.0665f, z - 0.03f))); nails.Add((nail, P3(x + dx, 0.0665f, z + 0.03f))); }
            }
        l.Add(M(Bake(blocks), 0, 0, 0, C(0.48f, 0.33f, 0.19f)));
        l.Add(M(Bake(nails), 0, 0, 0, C(0.3f, 0.3f, 0.32f)));
        foreach (float z in new[] { -0.43f, 0f, 0.43f }) l.Add(RB(1.2f, 0.022f, 0.14f, 0.005f, 0, -0.064f, z, C(0.57f, 0.41f, 0.25f), true));
        // Burnt-in stamp on the middle block and a green paint dab (recycled pallet).
        l.Add(M(Slab(Rect(-0.045f, -0.022f, 0.045f, 0.022f), 0f, 0.002f), 0, -0.0155f, 0.4601f, C(0.18f, 0.12f, 0.07f)));
        l.Add(M(Slab(Rect(-0.02f, -0.02f, 0.02f, 0.02f), 0f, 0.002f), 0.53f, -0.0155f, 0.4601f, C(0.2f, 0.45f, 0.3f)));
        return l.ToArray();
    }

    private static Part[] IBeamParts()
    {
        Color rust = C(0.55f, 0.22f, 0.13f);
        var l = new List<Part>
        {
            RB(4.0f, 0.026f, 0.2f, 0.006f, 0, 0.137f, 0, rust, true), RB(4.0f, 0.026f, 0.2f, 0.006f, 0, -0.137f, 0, rust, true),
            RB(4.0f, 0.25f, 0.016f, 0.004f, 0, 0, 0, rust, true),
        };
        // Root fillets where web meets flanges (the thing that makes it read as rolled steel, not three planks).
        var fil = new List<(Mesh, Float3)>();
        Mesh rod = Sweep([new Sec(P3(-1.995f, 0, 0), 0.011f, 0.011f), new Sec(P3(1.995f, 0, 0), 0.011f, 0.011f)], Float3.UnitY, 10, 0f);
        foreach (float y in new[] { -0.118f, 0.118f }) foreach (float z in new[] { -0.009f, 0.009f }) fil.Add((rod, P3(0, y, z)));
        l.Add(M(Bake(fil), 0, 0, 0, rust, true));
        // Bolt holes at both ends of the web and flange, darker mill scale patches, a chalk/paint stencil mark.
        var holes = new List<(Mesh, Float3)>();
        Mesh hole = RoundedCylinder(0.011f, 0.019f, 0.002f, 14);
        foreach (float x in new[] { -1.88f, -1.78f, 1.78f, 1.88f }) foreach (float y in new[] { -0.05f, 0.05f }) holes.Add((hole, P3(x, y, 0)));
        l.Add(M(Bake(holes.Select(h => (Rot90X(h.Item1), h.Item2))), 0, 0, 0, C(0.05f, 0.04f, 0.04f)));
        l.Add(M(Slab(Rect(-0.12f, -0.012f, 0.12f, 0.012f), 0f, 0.0018f), 0.2f, 0.04f, 0.0081f, C(0.95f, 0.85f, 0.3f)));
        l.Add(M(Slab(Rect(-0.05f, -0.012f, 0.05f, 0.012f), 0f, 0.0018f), 0.2f, 0.0f, 0.0081f, C(0.95f, 0.85f, 0.3f)));
        return l.ToArray();
    }
    private static Mesh Rot90X(Mesh m)
    {
        var mm = new Mesh { IndexFormat = m.IndexFormat, Vertices = m.Vertices.Select(v => P3(v.X, -v.Z, v.Y)).ToArray(), Normals = m.Normals.Select(v => P3(v.X, -v.Z, v.Y)).ToArray(), UV = m.UV, Indices = m.Indices };
        mm.RecalculateBounds(); return mm;
    }

    private static Part[] WindowParts()
    {
        Color paint = C(0.92f, 0.92f, 0.9f), dark = C(0.2f, 0.2f, 0.22f);
        var l = new List<Part>
        {
            // Frame: head, jambs with a front stop moulding, and a projecting sloped sill.
            RB(1.2f, 0.08f, 0.1f, 0.012f, 0, 0.56f, 0, paint), RB(0.08f, 1.12f, 0.1f, 0.012f, -0.56f, 0.0f, 0, paint), RB(0.08f, 1.12f, 0.1f, 0.012f, 0.56f, 0.0f, 0, paint),
            RB(1.12f, 0.025f, 0.022f, 0.008f, 0, 0.51f, 0.045f, paint), RB(0.025f, 1.0f, 0.022f, 0.008f, -0.51f, 0.0f, 0.045f, paint), RB(0.025f, 1.0f, 0.022f, 0.008f, 0.51f, 0.0f, 0.045f, paint),
            RB(1.32f, 0.045f, 0.17f, 0.012f, 0, -0.565f, 0.025f, paint, true, P3(-6, 0, 0)), RB(1.2f, 0.03f, 0.03f, 0.008f, 0, -0.6f, 0.08f, paint),
        };
        // Two sliding sashes (upper behind, lower in front), each with stiles, rails, a glazing bar and glass with a sheen streak.
        foreach (var (cy, cz) in new[] { (0.255f, -0.02f), (-0.255f, 0.02f) })
        {
            l.Add(RB(1.02f, 0.05f, 0.035f, 0.008f, 0, cy + 0.235f, cz, paint)); l.Add(RB(1.02f, cy > 0 ? 0.035f : 0.06f, 0.035f, 0.008f, 0, cy - (cy > 0 ? 0.24f : 0.225f), cz, paint));
            l.Add(RB(0.05f, 0.51f, 0.035f, 0.008f, -0.485f, cy, cz, paint)); l.Add(RB(0.05f, 0.51f, 0.035f, 0.008f, 0.485f, cy, cz, paint));
            l.Add(RB(0.02f, 0.44f, 0.026f, 0.006f, 0, cy, cz, paint));
            l.Add(M(Slab(Rect(-0.46f, -0.215f, 0.46f, 0.215f), -0.003f, 0.003f), 0, cy, cz, Glass));
            foreach (float sx in new[] { -0.23f, 0.23f })
                l.Add(M(Slab([new(-0.12f, -0.2f), new(-0.06f, -0.2f), new(0.1f, 0.2f), new(0.04f, 0.2f)], 0.0031f, 0.0035f), sx, cy, cz, C(0.66f, 0.78f, 0.85f)));
        }
        // Sash lock on the meeting rails and two lift handles on the lower sash.
        l.Add(RB(0.07f, 0.014f, 0.03f, 0.005f, 0, 0.025f, 0.0f, Brass)); l.Add(M(RoundedCylinder(0.012f, 0.012f, 0.003f, 14), 0.025f, 0.035f, 0.0f, Brass));
        foreach (float x in new[] { -0.3f, 0.3f }) l.Add(M(PropMeshes.Handle(0.07f), x, -0.47f, 0.06f, Brass, false, P3(0, 0, 90)));
        return l.ToArray();
    }

    private static Part[] SawhorseParts()
    {
        Color timber = C(0.72f, 0.56f, 0.36f), orange = C(0.95f, 0.38f, 0.06f), white = C(0.95f, 0.95f, 0.93f), steel = C(0.3f, 0.32f, 0.35f);
        var l = new List<Part> { RB(1.0f, 0.09f, 0.09f, 0.008f, 0, 0.355f, 0, timber, true) };
        Mesh Leg(float sx, float sz) => Sweep([new Sec(P3(sx * 0.37f, 0.33f, sz * 0.035f), 0.045f, 0.02f, 8f), new Sec(P3(sx * 0.44f, -0.385f, sz * 0.22f), 0.045f, 0.02f, 8f)], Float3.UnitX, 12, 0.3f);
        foreach (float sx in new[] { -1f, 1f })
        {
            l.Add(M(Leg(sx, 1f), 0, 0, 0, timber, true)); l.Add(M(Leg(sx, -1f), 0, 0, 0, timber, true));
            // Folded steel bracket clamping the legs to the beam, rubber feet, and a spreader bar.
            l.Add(RB(0.11f, 0.13f, 0.13f, 0.01f, sx * 0.37f, 0.33f, 0, steel)); l.Add(M(RoundedCylinder(0.012f, 0.135f, 0.003f, 12), sx * 0.37f, 0.33f, 0, C(0.7f, 0.7f, 0.72f), false, P3(90, 0, 0)));
            l.Add(RB(0.1f, 0.03f, 0.06f, 0.008f, sx * 0.44f, -0.385f, 0.22f, Rubber)); l.Add(RB(0.1f, 0.03f, 0.06f, 0.008f, sx * 0.44f, -0.385f, -0.22f, Rubber));
            l.Add(RB(0.02f, 0.03f, 0.36f, 0.006f, sx * 0.425f, -0.2f, 0, steel));
        }
        // Barricade board with diagonal reflective stripes.
        l.Add(RB(0.92f, 0.16f, 0.022f, 0.006f, 0, 0.1f, 0.115f, orange));
        var str = new List<Mesh>();
        for (float x = -0.44f; x < 0.2f; x += 0.16f) str.Add(Slab([new(x, -0.075f), new(x + 0.07f, -0.075f), new(x + 0.07f + 0.15f, 0.075f), new(x + 0.15f, 0.075f)], 0f, 0.002f));
        l.Add(M(Merge(str.ToArray()), 0, 0.1f, 0.1262f, white));
        return l.ToArray();
    }

    private static Part[] StopSignParts()
    {
        Color red = C(0.75f, 0.06f, 0.07f);
        var holes = new List<(Mesh, Float3)>(); Mesh h = Slab(Rect(-0.006f, -0.006f, 0.006f, 0.006f), 0f, 0.001f);
        for (float y = -1.1f; y < 0.72f; y += 0.05f) holes.Add((h, P3(0, y, 0)));
        return
        [
            // Square perforated telespar post on a bolted base plate.
            RB(0.05f, 2.2f, 0.05f, 0.006f, 0, -0.1f, -0.035f, Galv), M(Bake(holes), 0, 0, -0.0095f, C(0.1f, 0.1f, 0.11f)),
            RB(0.26f, 0.02f, 0.26f, 0.004f, 0, -1.19f, -0.035f, Galv), M(Bake([(RoundedCylinder(0.014f, 0.02f, 0.004f, 6), P3(-0.1f, 0, -0.1f)), (RoundedCylinder(0.014f, 0.02f, 0.004f, 6), P3(0.1f, 0, -0.1f)), (RoundedCylinder(0.014f, 0.02f, 0.004f, 6), P3(-0.1f, 0, 0.1f)), (RoundedCylinder(0.014f, 0.02f, 0.004f, 6), P3(0.1f, 0, 0.1f))]), 0, -1.17f, -0.035f, C(0.45f, 0.46f, 0.48f)),
            // Aluminium blank, white reflective border, red field, STOP lettering, two bolts and a back clamp.
            M(Slab(NGon(8, 0.345f, MathF.PI / 8f), -0.004f, 0f), 0, 0.85f, 0, C(0.7f, 0.71f, 0.73f)),
            M(Slab(NGon(8, 0.34f, MathF.PI / 8f), 0f, 0.002f), 0, 0.85f, 0, C(0.96f, 0.96f, 0.95f)),
            M(Slab(NGon(8, 0.31f, MathF.PI / 8f), 0.002f, 0.003f), 0, 0.85f, 0, red, true),
            M(Letters("STOP", 0.15f, 0.025f, 0.003f, 0.0042f), 0, 0.85f, 0, C(0.97f, 0.97f, 0.96f)),
            M(RoundedCylinder(0.011f, 0.006f, 0.002f, 12), 0, 1.12f, 0.004f, Galv, false, P3(90, 0, 0)), M(RoundedCylinder(0.011f, 0.006f, 0.002f, 12), 0, 0.58f, 0.004f, Galv, false, P3(90, 0, 0)),
            RB(0.09f, 0.04f, 0.03f, 0.005f, 0, 1.12f, -0.02f, Galv), RB(0.09f, 0.04f, 0.03f, 0.005f, 0, 0.58f, -0.02f, Galv),
        ];
    }

    /// <summary>Double-sided leaf blade from <paramref name="b"/> along <paramref name="d"/>: pointed tip, slight
    /// V-fold along the midrib and a gentle droop; top and underside faces with their own normals.</summary>
    private static Mesh Leaf(Float3 b, Float3 d, Float3 side, float len, float wmax, int st = 8)
    {
        Float3 up = Float3.Normalize(Float3.Cross(side, d));
        if (up.Y < 0f) { up = -up; side = -side; }
        var rows = new List<Float3[]>();
        for (int i = 0; i <= st; i++)
        {
            float u = i / (float)st, w = wmax * MathF.Pow(MathF.Sin(MathF.PI * MathF.Pow(u, 0.8f)), 0.8f) + 0.0035f;
            Float3 c = b + d * (len * u) - up * (0.2f * len * u * u);
            rows.Add([c - side * w + up * (w * 0.25f), c, c + side * w + up * (w * 0.25f)]);
        }
        var faces = new List<Float3[]>();
        for (int i = 0; i < st; i++)
            for (int e = 0; e < 2; e++)
            {
                Float3 p0 = rows[i][e], p1 = rows[i][e + 1], p2 = rows[i + 1][e + 1], p3 = rows[i + 1][e];
                Float3[] q = i == st - 1 ? (e == 0 ? [p0, p1, rows[st][1]] : [p0, p1, rows[st][1]]) : [p0, p1, p2, p3];
                Float3 nn = Float3.Cross(p1 - p0, p3 - p0);
                if (Float3.Dot(Float3.Cross(q[1] - q[0], q[2] - q[0]), up) < 0f) q = q.Reverse().ToArray();
                faces.Add(q.Select(x => x + up * 0.0008f).ToArray());
                faces.Add(q.Reverse().Select(x => x - up * 0.0008f).ToArray());
            }
        return Faces(faces.ToArray());
    }

    /// <summary>Rubber-plant style houseplant: terracotta pot with rolled rim and saucer, bark mulch, three arching
    /// stems and ~30 individual leaves (tapered, midrib-folded blades) spiralling up them by the golden angle.</summary>
    private static Part[] PlantParts()
    {
        Color terra = C(0.66f, 0.36f, 0.22f), leafA = C(0.16f, 0.3f, 0.13f), leafB = C(0.22f, 0.37f, 0.16f), stemC = C(0.33f, 0.36f, 0.2f);
        var rnd = new Random(7);
        var leavesA = new List<Mesh>(); var leavesB = new List<Mesh>(); var stems = new List<Mesh>();
        Float3[] tops = [P3(0.02f, 0.47f, 0.01f), P3(0.13f, 0.33f, -0.07f), P3(-0.12f, 0.36f, 0.09f)];
        int n = 0;
        foreach (Float3 top in tops)
        {
            Float3 root = P3(top.X * 0.25f, -0.17f, top.Z * 0.25f);
            Float3 Mid(float t) => root + (top - root) * t + P3(top.X * 0.35f, 0f, top.Z * 0.35f) * MathF.Sin(t * MathF.PI);
            var sec = new List<Sec>();
            for (int k = 0; k <= 6; k++) { float t = k / 6f; sec.Add(new Sec(Mid(t), 0.009f - 0.004f * t, 0.009f - 0.004f * t)); }
            stems.Add(Sweep(sec, Float3.UnitX, 8, 0.6f));
            for (float t = 0.25f; t <= 1.001f; t += 0.05f, n++)
            {
                float ang = n * 2.39996f, L = 0.16f + 0.1f * (1f - MathF.Abs(t - 0.6f)) + (float)rnd.NextDouble() * 0.03f;
                Float3 b = Mid(MathF.Min(t, 1f));
                Float3 d = Float3.Normalize(P3(MathF.Cos(ang), 0.45f - t * 0.45f, MathF.Sin(ang)));
                Float3 side = Float3.Normalize(Float3.Cross(d, Float3.UnitY));
                (n % 3 == 0 ? leavesB : leavesA).Add(Leaf(b, d, side, L, 0.05f * (0.85f + 0.3f * L)));
            }
        }
        return
        [
            // Pot: tapered body, rolled rim, foot ring; saucer underneath; soil with bark chips.
            M(Lathe("pot2", [(0.0001f, -0.5f), (0.15f, -0.5f), (0.155f, -0.49f), (0.148f, -0.48f), (0.2f, -0.21f), (0.23f, -0.205f), (0.24f, -0.19f), (0.238f, -0.15f), (0.226f, -0.14f), (0.205f, -0.15f), (0.2f, -0.19f), (0.0001f, -0.19f)], 40), 0, 0, 0, terra, true),
            M(Lathe("saucer", [(0.0001f, -0.5f), (0.2f, -0.5f), (0.21f, -0.47f), (0.2f, -0.468f), (0.18f, -0.485f), (0.0001f, -0.485f)], 40), 0, -0.002f, 0, C(0.55f, 0.3f, 0.19f)),
            M(Lathe("soil2", [(0.0001f, -0.17f), (0.2f, -0.175f), (0.202f, -0.168f), (0.0001f, -0.162f)], 32), 0, 0, 0, C(0.22f, 0.15f, 0.09f)),
            M(Bake(Enumerable.Range(0, 14).Select(i => (RoundedBox(P3(0.026f, 0.006f, 0.018f), 0.003f), P3(MathF.Cos(i * 2.4f) * (0.05f + i * 0.0095f), -0.164f, MathF.Sin(i * 2.4f) * (0.05f + i * 0.0095f))))), 0, 0, 0, C(0.3f, 0.21f, 0.14f)),
            M(Merge(stems.ToArray()), 0, 0, 0, stemC),
            M(Merge(leavesA.ToArray()), 0, 0, 0, leafA),
            M(Merge(leavesB.ToArray()), 0, 0, 0, leafB),
        ];
    }

    private static Part[] ArrowSignParts()
    {
        Color blue = C(0.08f, 0.3f, 0.7f), white = C(0.96f, 0.96f, 0.95f);
        Float2[] shaft = Rect(-0.33f, -0.055f, 0.08f, 0.055f), head = [new(0.06f, -0.14f), new(0.34f, 0f), new(0.06f, 0.14f)];
        const float y = 0.82f; // panel height on the post
        return
        [
            M(Plate(0.44f, 0.2f, 0.01f, 7f), 0, y, -0.004f, C(0.7f, 0.71f, 0.73f)),
            M(Plate(0.435f, 0.195f, 0.004f, 7f), 0, y, 0.002f, white),
            M(Plate(0.415f, 0.175f, 0.003f, 7f), 0, y, 0.004f, blue, true),
            M(Merge(Slab(shaft, 0.0055f, 0.0068f), Slab(head, 0.0055f, 0.0068f)), 0, y, 0, white),
            // Rivets in the corners, a horizontal back rail, and two U-bolt clamps round the post.
            M(Bake(new[] { (-0.38f, 0.14f), (0.38f, 0.14f), (-0.38f, -0.14f), (0.38f, -0.14f) }.Select(p => (Rot90X(RoundedCylinder(0.009f, 0.004f, 0.0015f, 12)), P3(p.Item1, p.Item2, 0.007f)))), 0, y, 0, Galv),
            RB(0.7f, 0.05f, 0.02f, 0.006f, 0, y + 0.09f, -0.018f, Galv), RB(0.7f, 0.05f, 0.02f, 0.006f, 0, y - 0.09f, -0.018f, Galv),
            RB(0.075f, 0.03f, 0.075f, 0.01f, 0, y + 0.09f, -0.055f, Galv), RB(0.075f, 0.03f, 0.075f, 0.01f, 0, y - 0.09f, -0.055f, Galv),
            // Round galvanised post with a cap, a mid band, and the weighted rubber foot.
            Cy(0.026f, 2.02f, 0, -0.05f, -0.055f, Galv), M(RoundedCylinder(0.03f, 0.025f, 0.008f, 16), 0, 0.98f, -0.055f, Galv),
            RB(0.07f, 0.03f, 0.07f, 0.01f, 0, -0.2f, -0.055f, Galv),
            M(PropMeshes.SignFoot(), 0, -1.08f, -0.055f, Dark), RB(0.12f, 0.05f, 0.12f, 0.012f, 0, -1.06f, -0.055f, Dark),
        ];
    }

    private static Part[] WarningSignParts()
    {
        Color yel = C(0.98f, 0.76f, 0.08f), blk = C(0.06f, 0.06f, 0.07f);
        Float3 rot = P3(0, 0, 45);
        return
        [
            M(Plate(0.3f, 0.3f, 0.008f, 9f), 0, 0.35f, -0.004f, C(0.7f, 0.71f, 0.73f), false, rot),
            M(Plate(0.295f, 0.295f, 0.004f, 9f), 0, 0.35f, 0.001f, yel, true, rot),
            M(Plate(0.27f, 0.27f, 0.002f, 9f), 0, 0.35f, 0.0035f, blk, false, rot),
            M(Plate(0.25f, 0.25f, 0.002f, 9f), 0, 0.35f, 0.0045f, yel, true, rot),
            M(Slab([new(-0.022f, -0.02f), new(0.022f, -0.02f), new(0.03f, 0.17f), new(-0.03f, 0.17f)], 0.0055f, 0.0068f), 0, 0.33f, 0, blk),
            M(Rot90X(RoundedCylinder(0.026f, 0.0013f, 0.0004f, 20)), 0, 0.29f, 0.0061f, blk),
            // Round post with two band clamps, weighted foot.
            Cy(0.025f, 1.0f, 0, -0.2f, -0.03f, Galv), RB(0.07f, 0.03f, 0.06f, 0.008f, 0, 0.5f, -0.02f, Galv), RB(0.07f, 0.03f, 0.06f, 0.008f, 0, 0.2f, -0.02f, Galv),
            M(PropMeshes.SignFoot(), 0, -0.68f, -0.03f, Dark), RB(0.12f, 0.05f, 0.12f, 0.012f, 0, -0.66f, -0.03f, Dark),
        ];
    }

    private static Part[] TvParts()
    {
        Color body = C(0.06f, 0.06f, 0.07f), glass = C(0.02f, 0.025f, 0.03f);
        float sz = 0.0152f; // screen surface
        var pic = new List<Part>
        {
            // Picture (emissive): sky gradient, sun, two hills and a lake — reads as a TV that is on.
            M(Slab(Rect(-0.56f, 0.06f, 0.56f, 0.34f), sz, sz + 0.0005f), 0, 0.035f, 0, C(0.27f, 0.47f, 0.70f)) with { Glow = true },
            M(Slab(Rect(-0.56f, -0.1f, 0.56f, 0.06f), sz, sz + 0.0005f), 0, 0.035f, 0, C(0.47f, 0.58f, 0.72f)) with { Glow = true },
            M(Slab(NGon(24, 0.055f), sz + 0.0005f, sz + 0.001f), 0.28f, 0.255f, 0, C(0.78f, 0.70f, 0.43f)) with { Glow = true },
            M(Slab([new(-0.56f, -0.1f), new(0.05f, -0.1f), new(-0.2f, 0.09f), new(-0.4f, 0.12f), new(-0.56f, 0.05f)], sz + 0.0006f, sz + 0.0011f), 0, 0.035f, 0, C(0.16f, 0.39f, 0.20f)) with { Glow = true },
            M(Slab([new(-0.2f, -0.1f), new(0.56f, -0.1f), new(0.56f, 0.02f), new(0.3f, 0.07f), new(0.1f, 0.04f)], sz + 0.0007f, sz + 0.0012f), 0, 0.035f, 0, C(0.22f, 0.45f, 0.23f)) with { Glow = true },
            M(Slab(Rect(-0.56f, -0.3f, 0.56f, -0.1f), sz, sz + 0.0005f), 0, 0.035f, 0, C(0.16f, 0.33f, 0.55f)) with { Glow = true },
        };
        return
        [
            // Thin bezel, slimmer edge chamfer, deeper electronics hump behind, chin with logo and standby LED.
            RB(1.2f, 0.68f, 0.028f, 0.008f, 0, 0.035f, 0, body, true), RB(1.13f, 0.61f, 0.002f, 0.001f, 0, 0.035f, 0.0142f, glass),
            RB(0.86f, 0.44f, 0.05f, 0.02f, 0, 0.0f, -0.035f, body, true), RB(0.5f, 0.2f, 0.02f, 0.008f, 0, -0.02f, -0.065f, C(0.1f, 0.1f, 0.11f)),
            RB(0.06f, 0.012f, 0.002f, 0.001f, 0, -0.29f, 0.0145f, C(0.6f, 0.6f, 0.62f)), M(Rot90X(RoundedCylinder(0.003f, 0.002f, 0.0005f, 8)), 0.52f, -0.292f, 0.0145f, C(1f, 0.2f, 0.15f)) with { Glow = true },
            // Brushed pedestal: neck and an oval foot.
            M(Sweep([new Sec(P3(0, -0.36f, -0.03f), 0.04f, 0.012f, 4f), new Sec(P3(0, -0.24f, -0.035f), 0.035f, 0.01f, 4f)], Float3.UnitX, 14, 0.2f), 0, 0, 0, C(0.25f, 0.26f, 0.28f)),
            M(Sweep([new Sec(P3(0, -0.372f, -0.03f), 0.2f, 0.11f, 2.6f, Float3.UnitX), new Sec(P3(0, -0.36f, -0.03f), 0.195f, 0.105f, 2.6f, Float3.UnitX)], Float3.UnitX, 32, 0.4f), 0, 0, 0, C(0.25f, 0.26f, 0.28f)),
            .. pic,
        ];
    }

    private static Part[] BedParts()
    {
        Color duvet = C(0.5f, 0.14f, 0.13f), throwC = C(0.7f, 0.64f, 0.52f);
        var l = new List<Part>
        {
            // Frame: side and end rails, turned legs, headboard posts with finials and a raised panel, low footboard.
            RB(0.045f, 0.16f, 2.0f, 0.01f, -0.53f, -0.16f, 0, DarkWood, true), RB(0.045f, 0.16f, 2.0f, 0.01f, 0.53f, -0.16f, 0, DarkWood, true),
            RB(1.02f, 0.03f, 1.96f, 0.006f, 0, -0.13f, 0, C(0.5f, 0.4f, 0.3f)),
            M(PropMeshes.InsetPanel(1.0f, 0.42f, 0.035f, 0.05f), 0, 0.06f, -1.02f, DarkWood, true),
            M(PropMeshes.InsetPanel(1.0f, 0.2f, 0.035f, 0.04f), 0, -0.1f, 1.02f, DarkWood, true, P3(0, 180, 0)),
        };
        foreach (float x in new[] { -0.53f, 0.53f })
        {
            l.Add(M(Lathe("bedpost", [(0.0001f, -0.3f), (0.03f, -0.3f), (0.032f, -0.27f), (0.028f, -0.2f), (0.034f, 0.22f), (0.038f, 0.24f), (0.03f, 0.255f), (0.0001f, 0.26f)], 16), x, 0, -1.02f, DarkWood, true));
            l.Add(M(Sphere(0.032f), x, 0.27f, -1.02f, DarkWood));
            l.Add(M(Lathe("bedpostf", [(0.0001f, -0.3f), (0.03f, -0.3f), (0.032f, -0.27f), (0.028f, -0.2f), (0.034f, 0.02f), (0.038f, 0.04f), (0.03f, 0.055f), (0.0001f, 0.06f)], 16), x, 0, 1.02f, DarkWood, true));
            l.Add(M(Sphere(0.03f), x, 0.075f, 1.02f, DarkWood));
        }
        // Mattress with piping, pillows, duvet with a turned-down sheet, side drapes and a folded throw at the foot.
        l.Add(M(PropMeshes.Cushion(2.0f, 0.19f, 1.02f), 0, -0.02f, 0, Linen, false, P3(0, 90, 0)));
        l.Add(RB(1.04f, 0.012f, 2.0f, 0.005f, 0, -0.1f, 0, C(0.7f, 0.7f, 0.68f)));
        foreach (float x in new[] { -0.25f, 0.25f }) l.Add(M(PropMeshes.Cushion(0.46f, 0.13f, 0.32f), x, 0.135f, -0.78f, C(0.72f, 0.72f, 0.75f), false, P3(-12, x * 8f, 0)));
        l.Add(M(PropMeshes.Cushion(1.38f, 0.07f, 1.1f), 0, 0.105f, 0.33f, duvet, false, P3(0, 90, 0)));
        l.Add(M(PropMeshes.Cushion(0.24f, 0.075f, 1.12f), 0, 0.12f, -0.33f, Linen, false, P3(0, 90, 0)));
        foreach (float x in new[] { -0.555f, 0.555f }) l.Add(RB(0.025f, 0.2f, 1.36f, 0.01f, x, 0.0f, 0.33f, duvet, false, P3(0, 0, x > 0 ? -4 : 4)));
        l.Add(RB(1.12f, 0.12f, 0.025f, 0.01f, 0, 0.05f, 1.0f, duvet, false, P3(4, 0, 0)));
        l.Add(M(PropMeshes.Cushion(0.32f, 0.035f, 1.16f), 0, 0.15f, 0.78f, throwC, false, P3(0, 90, 0)));
        l.Add(M(PropMeshes.Cushion(0.045f, 0.05f, 1.175f), 0, 0.152f, 0.7f, C(0.42f, 0.12f, 0.1f), false, P3(0, 90, 0))); // stripes stand proud of the throw (were z-fighting into dashes)
        l.Add(M(PropMeshes.Cushion(0.045f, 0.05f, 1.175f), 0, 0.152f, 0.86f, C(0.42f, 0.12f, 0.1f), false, P3(0, 90, 0)));
        return l.ToArray();
    }

    private static Part[] FridgeParts()
    {
        Color body = C(0.82f, 0.82f, 0.8f), gasket = C(0.1f, 0.1f, 0.11f), ss = C(0.72f, 0.74f, 0.76f);
        var slats = new List<(Mesh, Float3)>(); Mesh slat = RoundedBox(P3(0.62f, 0.01f, 0.012f), 0.003f);
        for (int i = 0; i < 4; i++) slats.Add((slat, P3(0, -0.875f + i * 0.016f, 0.338f)));
        return
        [
            // Cabinet, black door gaskets in the gaps, freezer + fresh-food doors with rolled edges.
            RB(0.75f, 1.8f, 0.64f, 0.03f, 0, 0, -0.03f, body),
            RB(0.72f, 1.7f, 0.02f, 0.006f, 0, 0.03f, 0.3f, gasket),
            RB(0.74f, 0.55f, 0.055f, 0.022f, 0, 0.615f, 0.325f, body),
            RB(0.74f, 1.13f, 0.055f, 0.022f, 0, -0.24f, 0.325f, body),
            // Stainless bar handles on standoffs, badge, hinge caps and a slotted kick grille.
            M(PropMeshes.Handle(0.3f), -0.31f, 0.52f, 0.386f, ss, false, P3(0, 180, 0)), M(PropMeshes.Handle(0.55f), -0.31f, 0.07f, 0.386f, ss, false, P3(0, 180, 0)),
            RB(0.09f, 0.022f, 0.004f, 0.002f, 0.24f, 0.84f, 0.353f, ss),
            RB(0.03f, 0.02f, 0.06f, 0.006f, 0.35f, 0.9f, 0.32f, C(0.7f, 0.7f, 0.7f)), RB(0.03f, 0.02f, 0.06f, 0.006f, 0.35f, 0.325f, 0.33f, C(0.7f, 0.7f, 0.7f)), RB(0.03f, 0.02f, 0.06f, 0.006f, 0.35f, -0.82f, 0.32f, C(0.7f, 0.7f, 0.7f)),
            RB(0.7f, 0.08f, 0.02f, 0.006f, 0, -0.86f, 0.32f, gasket), M(Bake(slats), 0, 0, 0, C(0.25f, 0.25f, 0.27f)),
            // Tall vertical seam on the side so the cabinet doesn't read as a plain slab.
            RB(0.004f, 1.7f, 0.004f, 0.001f, 0.376f, 0, 0.2f, C(0.75f, 0.75f, 0.74f)), RB(0.004f, 1.7f, 0.004f, 0.001f, -0.376f, 0, 0.2f, C(0.75f, 0.75f, 0.74f)),
            // A shopping list and a child's drawing held on by magnets: lived-in, and breaks up the big white doors.
            RB(0.1f, 0.14f, 0.002f, 0.0008f, 0.04f, 0.6f, 0.354f, C(0.95f, 0.9f, 0.55f)), RB(0.07f, 0.004f, 0.001f, 0.0004f, 0.04f, 0.63f, 0.3555f, C(0.3f, 0.3f, 0.5f)),
            RB(0.06f, 0.004f, 0.001f, 0.0004f, 0.035f, 0.61f, 0.3555f, C(0.3f, 0.3f, 0.5f)), RB(0.065f, 0.004f, 0.001f, 0.0004f, 0.038f, 0.59f, 0.3555f, C(0.3f, 0.3f, 0.5f)),
            RB(0.16f, 0.12f, 0.002f, 0.0008f, 0.1f, 0.05f, 0.354f, C(0.97f, 0.97f, 0.95f)), RB(0.07f, 0.07f, 0.001f, 0.0004f, 0.07f, 0.04f, 0.3555f, C(0.95f, 0.75f, 0.15f)),
            RB(0.12f, 0.025f, 0.001f, 0.0004f, 0.1f, 0.0f, 0.3555f, C(0.3f, 0.65f, 0.3f)), RB(0.04f, 0.04f, 0.001f, 0.0004f, 0.14f, 0.06f, 0.3556f, C(0.85f, 0.2f, 0.15f)),
            M(Sphere(0.012f), 0.04f, 0.665f, 0.356f, C(0.85f, 0.15f, 0.12f)), M(Sphere(0.012f), 0.1f, 0.105f, 0.356f, C(0.15f, 0.35f, 0.85f)), M(Sphere(0.012f), 0.2f, 0.4f, 0.356f, C(0.95f, 0.75f, 0.1f)),
        ];
    }

    private static Color Stone => C(0.86f, 0.85f, 0.8f);

    private static Part[] CinderParts()
    {
        Color c = C(0.7f, 0.69f, 0.66f);
        // Hollow block: two face shells and three webs, so the two cores are really open.
        return
        [
            RB(0.4f, 0.2f, 0.034f, 0.006f, 0, 0, 0.083f, c, true), RB(0.4f, 0.2f, 0.034f, 0.006f, 0, 0, -0.083f, c, true),
            RB(0.034f, 0.2f, 0.14f, 0.006f, -0.183f, 0, 0, c, true), RB(0.03f, 0.2f, 0.14f, 0.006f, 0, 0, 0, c, true), RB(0.034f, 0.2f, 0.14f, 0.006f, 0.183f, 0, 0, c, true),
            RB(0.31f, 0.012f, 0.13f, 0.004f, 0, -0.094f, 0, C(0.5f, 0.49f, 0.47f)),
        ];
    }

    private static Part[] BarrierParts()
    {
        var l = new List<Part> { M(PropMeshes.Jersey(), 0, 0, 0, Stone, true) };
        // Fork-lift slots through the foot, reflectors on both slopes, steel connector loops at the ends, a stencil.
        foreach (float x in new[] { -0.5f, 0.5f }) l.Add(RB(0.28f, 0.09f, 0.62f, 0.01f, x, -0.355f, 0, C(0.12f, 0.12f, 0.12f)));
        foreach (float x in new[] { -0.65f, 0f, 0.65f }) foreach (float sd in new[] { -1f, 1f })
                l.Add(RB(0.09f, 0.09f, 0.012f, 0.003f, x, 0.2f, sd * 0.133f, C(0.98f, 0.78f, 0.1f), false, P3(-sd * 10f, 0, 0)));
        foreach (float x in new[] { -1f, 1f })
            l.Add(M(VehicleModels.BentPipe(0.016f, 0.04f, Float3.UnitY, P3(x * 0.98f, 0.18f, -0.06f), P3(x * 1.04f, 0.18f, -0.05f), P3(x * 1.04f, 0.18f, 0.05f), P3(x * 0.98f, 0.18f, 0.06f)), 0, 0, 0, C(0.45f, 0.35f, 0.25f)));
        l.Add(M(Glyphs.Text("CITY-07", 0.07f, 0f, 0.002f, mirror: true), 0.55f, -0.22f, 0.2f, C(0.2f, 0.2f, 0.2f), false, P3(-27, 0, 0)));
        return l.ToArray();
    }

    private static Part[] PipeParts()
    {
        Color mill = C(0.6f, 0.58f, 0.55f), dark = C(0.32f, 0.33f, 0.35f);
        var threads = new List<(Mesh, Float3)>();
        Mesh ring = Lathe("pthread", [(0.1f, -0.004f), (0.106f, 0f), (0.1f, 0.004f)], 24);
        for (int i = 0; i < 7; i++) { threads.Add((ring, P3(0, 1.3f - i * 0.012f, 0))); threads.Add((ring, P3(0, -1.3f + i * 0.012f, 0))); }
        return
        [
            Cy(0.1f, 2.86f, 0, 0, 0, mill, true), M(Bake(threads), 0, 0, 0, dark),
            // Coupling with hex flats on one end, plastic thread protector cap on the other, a weld seam and a stencil band.
            M(Meshes.Prism(6, 0.128f, 0.16f), 0, 1.42f, 0, dark), M(Lathe("pcoupring", [(0.122f, -0.08f), (0.13f, -0.075f), (0.13f, 0.075f), (0.122f, 0.08f)], 24), 0, 1.42f, 0, C(0.45f, 0.46f, 0.48f)),
            M(Lathe("pcap", [(0.0001f, 0.06f), (0.1f, 0.06f), (0.112f, 0.05f), (0.112f, -0.06f), (0.104f, -0.065f)], 24), 0, -1.43f, 0, C(0.85f, 0.65f, 0.1f)),
            RB(0.006f, 2.6f, 0.006f, 0.002f, 0, 0, 0.1f, C(0.5f, 0.48f, 0.45f)),
            M(Lathe("pband", [(0.1015f, -0.08f), (0.1015f, 0.08f)], 24), 0, 0.5f, 0, C(0.15f, 0.35f, 0.7f)),
        ];
    }

    private static Part[] SofaParts()
    {
        Color fab = C(0.36f, 0.42f, 0.5f), fab2 = C(0.4f, 0.46f, 0.54f), pip = C(0.28f, 0.33f, 0.4f), leg = C(0.25f, 0.16f, 0.1f), pil = C(0.85f, 0.65f, 0.25f);
        var l = new List<Part>
        {
            // Plinth frame and back frame, rolled arms with front panels, tapered wooden feet.
            RB(1.9f, 0.2f, 0.8f, 0.04f, 0, -0.22f, 0.02f, fab, true), RB(1.94f, 0.52f, 0.18f, 0.06f, 0, 0.12f, -0.34f, fab, true),
        };
        foreach (float x in new[] { -0.91f, 0.91f })
        {
            l.Add(RB(0.18f, 0.36f, 0.84f, 0.05f, x, -0.08f, 0.02f, fab, true));
            l.Add(M(Sweep([new Sec(P3(x, 0.1f, -0.38f), 0.11f, 0.08f, 2.2f, Float3.UnitX), new Sec(P3(x, 0.1f, 0.4f), 0.11f, 0.08f, 2.2f, Float3.UnitX)], Float3.UnitX, 20, 0.5f), 0, 0, 0, fab2));
            l.Add(M(Plate(0.1f, 0.13f, 0.02f, 3f), x, 0.02f, 0.44f, pip));
            foreach (float z in new[] { -0.33f, 0.37f }) l.Add(M(PropMeshes.Leg(0.025f, 0.1f), x * 0.97f, -0.375f, z, leg));
        }
        foreach (float x in new[] { -0.425f, 0.425f })
        {
            // Seat cushions with piping, back cushions leaning back.
            l.Add(M(PropMeshes.Cushion(0.84f, 0.17f, 0.64f), x, -0.03f, 0.08f, fab2, false));
            l.Add(M(Lathe("cpip", [(0.003f, -0.42f), (0.006f, -0.41f), (0.006f, 0.41f), (0.003f, 0.42f)], 8), x, -0.03f, 0.4f, pip, false, P3(0, 0, 90)));
            l.Add(M(PropMeshes.Cushion(0.84f, 0.44f, 0.2f), x, 0.23f, -0.2f, fab2, false, P3(-12, 0, 0)));
        }
        // Scatter pillows: a plump rounded-square sweep through their thickness (the old box cushion read as a hexagon).
        Mesh pillow = Sweep([new Sec(P3(0, 0, -0.065f), 0.12f, 0.12f, 2.4f), new Sec(P3(0, 0, -0.04f), 0.165f, 0.165f, 2.8f), new Sec(P3(0, 0, 0f), 0.175f, 0.175f, 3f),
            new Sec(P3(0, 0, 0.04f), 0.165f, 0.165f, 2.8f), new Sec(P3(0, 0, 0.065f), 0.12f, 0.12f, 2.4f)], Float3.UnitY, 28, 0.9f);
        l.Add(M(pillow, -0.66f, 0.17f, -0.02f, pil, false, P3(-15, 20, 35)));
        l.Add(M(pillow, 0.66f, 0.17f, -0.02f, pil, false, P3(-15, -20, -35)));
        return l.ToArray();
    }

    private static Part[] OfficeChairParts()
    {
        Color blk = C(0.1f, 0.1f, 0.11f), mesh = C(0.2f, 0.21f, 0.23f), chrome = C(0.75f, 0.76f, 0.78f);
        var l = new List<Part>
        {
            M(Sweep([new Sec(P3(0, -0.07f, -0.24f), 0.25f, 0.05f, 3f, Float3.UnitX), new Sec(P3(0, -0.06f, 0f), 0.26f, 0.06f, 3f, Float3.UnitX), new Sec(P3(0, -0.07f, 0.23f), 0.25f, 0.05f, 3f, Float3.UnitX)], Float3.UnitX, 24, 0.7f), 0, 0, 0, blk, true),
            RB(0.22f, 0.04f, 0.24f, 0.01f, 0, -0.14f, 0, C(0.18f, 0.18f, 0.2f)),
            Cy(0.024f, 0.26f, 0, -0.29f, 0, chrome), M(Lathe("ocover", [(0.035f, 0.12f), (0.04f, 0.04f), (0.045f, -0.04f), (0.05f, -0.12f)], 18), 0, -0.36f, 0, blk),
            // Contoured back frame with a mesh panel and lumbar pad, carried on a spine from the seat.
            M(Sweep([new Sec(P3(0, 0.04f, -0.27f), 0.22f, 0.03f, 4f, Float3.UnitX), new Sec(P3(0, 0.18f, -0.25f), 0.235f, 0.03f, 4f, Float3.UnitX), new Sec(P3(0, 0.36f, -0.28f), 0.23f, 0.03f, 4f, Float3.UnitX), new Sec(P3(0, 0.48f, -0.31f), 0.2f, 0.03f, 4f, Float3.UnitX)], Float3.UnitX, 24, 0.5f), 0, 0, 0, blk, true),
            M(Sweep([new Sec(P3(0, 0.07f, -0.25f), 0.19f, 0.012f, 4f, Float3.UnitX), new Sec(P3(0, 0.18f, -0.23f), 0.205f, 0.012f, 4f, Float3.UnitX), new Sec(P3(0, 0.36f, -0.26f), 0.2f, 0.012f, 4f, Float3.UnitX), new Sec(P3(0, 0.45f, -0.285f), 0.17f, 0.012f, 4f, Float3.UnitX)], Float3.UnitX, 24, 0.5f), 0, 0, 0, mesh),
            M(PropMeshes.Cushion(0.3f, 0.07f, 0.03f), 0, 0.15f, -0.225f, C(0.16f, 0.16f, 0.18f)),
            M(Sweep([new Sec(P3(0, -0.12f, -0.12f), 0.03f, 0.012f, 3f), new Sec(P3(0, -0.11f, -0.27f), 0.03f, 0.014f, 3f), new Sec(P3(0, 0.08f, -0.3f), 0.03f, 0.014f, 3f)], Float3.UnitX, 12, 0.5f), 0, 0, 0, C(0.18f, 0.18f, 0.2f)),
        };
        // Five-star base with twin-wheel casters.
        for (int i = 0; i < 5; i++)
        {
            float a = i * MathF.Tau / 5f + 0.3f, cx = MathF.Cos(a), cz = MathF.Sin(a);
            l.Add(M(Sweep([new Sec(P3(cx * 0.03f, -0.44f, cz * 0.03f), 0.03f, 0.025f, 3f), new Sec(P3(cx * 0.27f, -0.47f, cz * 0.27f), 0.02f, 0.018f, 3f)], Float3.UnitY, 12, 0.6f), 0, 0, 0, blk));
            l.Add(M(Rot90X(RoundedCylinder(0.026f, 0.034f, 0.008f, 14)), cx * 0.27f, -0.497f, cz * 0.27f, C(0.15f, 0.15f, 0.16f), false, P3(0, -a * 180f / MathF.PI, 0)));
        }
        // Armrests: post + pad.
        foreach (float x in new[] { -0.27f, 0.27f })
        {
            l.Add(M(Sweep([new Sec(P3(x * 0.8f, -0.1f, -0.02f), 0.02f, 0.03f, 3f), new Sec(P3(x, -0.08f, -0.02f), 0.02f, 0.03f, 3f), new Sec(P3(x, 0.12f, -0.03f), 0.02f, 0.03f, 3f)], Float3.UnitZ, 12, 0.5f), 0, 0, 0, blk));
            l.Add(M(PropMeshes.Cushion(0.07f, 0.035f, 0.24f), x, 0.14f, -0.02f, C(0.16f, 0.16f, 0.18f)));
        }
        return l.ToArray();
    }

    private static Part[] LampParts()
    {
        Color brass = C(0.72f, 0.56f, 0.3f), shade = C(0.93f, 0.88f, 0.76f);
        return
        [
            M(Lathe("lampbase2", [(0.0001f, -0.04f), (0.18f, -0.04f), (0.19f, -0.025f), (0.17f, 0f), (0.08f, 0.025f), (0.04f, 0.04f), (0.03f, 0.07f), (0.0001f, 0.07f)], 36), 0, -0.81f, 0, brass, true),
            Cy(0.014f, 1.3f, 0, -0.1f, 0, brass), M(Lathe("lampknuckle", [(0.0001f, -0.03f), (0.022f, -0.03f), (0.026f, 0f), (0.022f, 0.03f), (0.0001f, 0.03f)], 18), 0, 0.15f, 0, brass),
            M(Lathe("lampsock", [(0.0001f, -0.05f), (0.03f, -0.05f), (0.032f, 0.04f), (0.025f, 0.05f), (0.0001f, 0.05f)], 18), 0, 0.56f, 0, brass),
            // Drum shade: open-ended cylinder with rolled rims, bulb glowing inside.
            M(Lathe("lampdrum", [(0.19f, -0.13f), (0.195f, -0.125f), (0.165f, 0.13f), (0.16f, 0.135f), (0.155f, 0.13f), (0.185f, -0.125f)], 40), 0, 0.7f, 0, shade),
            M(Lathe("lamprimB", [(0.19f, -0.008f), (0.2f, 0f), (0.19f, 0.008f)], 40), 0, 0.575f, 0, brass), M(Lathe("lamprimT", [(0.16f, -0.007f), (0.17f, 0f), (0.16f, 0.007f)], 40), 0, 0.835f, 0, brass),
            M(Sphere(0.05f), 0, 0.66f, 0, C(1f, 0.95f, 0.8f)) with { Glow = true },
            M(Sweep([new Sec(P3(0.04f, 0.6f, 0f), 0.003f, 0.003f), new Sec(P3(0.05f, 0.5f, 0.01f), 0.003f, 0.003f)], Float3.UnitX, 6, 0.5f), 0, 0, 0, brass),
            M(Sphere(0.008f), 0.05f, 0.495f, 0.01f, brass),
        ];
    }

    private static Part[] BillboardParts()
    {
        Color frame = C(0.25f, 0.27f, 0.3f), sky = C(0.18f, 0.42f, 0.78f), white = C(0.97f, 0.97f, 0.96f);
        var l = new List<Part>
        {
            M(RoundedBox(P3(3.0f, 1.5f, 0.08f), 0.03f), 0, 0.75f, -0.02f, frame, true), M(PropMeshes.InsetPanel(2.96f, 1.46f, 0.06f, 0.06f), 0, 0.75f, 0.04f, C(0.85f, 0.85f, 0.83f)),
            M(RoundedBox(P3(2.82f, 1.32f, 0.01f), 0.004f), 0, 0.75f, 0.06f, sky),
            // Poster: horizon band, sun, big headline and a tagline.
            M(Slab(Rect(-1.41f, -0.66f, 1.41f, -0.25f), 0.0655f, 0.067f), 0, 0.75f, 0, C(0.2f, 0.55f, 0.3f)),
            M(Slab(NGon(32, 0.2f), 0.0655f, 0.067f), -1.08f, 1.12f, 0, C(1f, 0.82f, 0.3f)),
            // Sky gradient bands, rolling hills behind the meadow, puffy clouds.
            M(Slab(Rect(-1.41f, -0.25f, 1.41f, 0.12f), 0.0651f, 0.0653f), 0, 0.75f, 0, C(0.3f, 0.55f, 0.86f)),
            M(Slab(Rect(-1.41f, -0.25f, 1.41f, -0.08f), 0.0653f, 0.0655f), 0, 0.75f, 0, C(0.48f, 0.68f, 0.9f)),
            M(Slab([new(-1.41f, -0.3f), new(0.1f, -0.3f), new(-0.25f, -0.1f), new(-0.7f, 0.0f), new(-1.15f, -0.06f), new(-1.41f, -0.15f)], 0.0655f, 0.0659f), 0, 0.75f, 0, C(0.16f, 0.42f, 0.24f)),
            M(Slab([new(-0.3f, -0.3f), new(1.41f, -0.3f), new(1.41f, -0.08f), new(1.05f, 0.04f), new(0.55f, 0.02f), new(0.1f, -0.12f)], 0.0656f, 0.066f), 0, 0.75f, 0, C(0.24f, 0.5f, 0.28f)),
            M(Merge(Slab(NGon(20, 0.1f), 0.0656f, 0.0668f), At(Slab(NGon(20, 0.13f), 0.0656f, 0.0668f), P3(0.14f, 0.03f, 0f)), At(Slab(NGon(20, 0.09f), 0.0656f, 0.0668f), P3(0.29f, -0.01f, 0f)),
                At(Slab(Rect(-0.05f, -0.07f, 0.33f, -0.02f), 0.0656f, 0.0668f), P3(0f, 0f, 0f))), 0.9f, 1.32f, 0, C(0.95f, 0.96f, 0.98f)),
            M(Merge(Slab(NGon(20, 0.07f), 0.0656f, 0.0668f), At(Slab(NGon(20, 0.09f), 0.0656f, 0.0668f), P3(0.1f, 0.02f, 0f)), At(Slab(NGon(20, 0.06f), 0.0656f, 0.0668f), P3(0.2f, -0.01f, 0f))), -0.62f, 1.36f, 0, C(0.95f, 0.96f, 0.98f)),
            M(Glyphs.Text("SANDBOX", 0.36f, 0.067f, 0.075f, mirror: true), 0.12f, 0.95f, 0, white),
            M(Glyphs.Text("BUILD ANYTHING!", 0.13f, 0.067f, 0.073f, mirror: true), 0.12f, 0.62f, 0, C(1f, 0.85f, 0.25f)),
            // Posts with base plates, cross bracing, catwalk with railing, and two gooseneck flood lights.
            M(PropMeshes.Post(0.065f, 1.7f), -1.0f, -0.75f, -0.02f, Dark), M(PropMeshes.Post(0.065f, 1.7f), 1.0f, -0.75f, -0.02f, Dark),
            M(PropMeshes.Rod(P3(-1f, -1.4f, -0.08f), P3(1f, -0.2f, -0.08f), 0.02f), 0, 0, 0, Dark), M(PropMeshes.Rod(P3(1f, -1.4f, -0.08f), P3(-1f, -0.2f, -0.08f), 0.02f), 0, 0, 0, Dark),
            RB(3.0f, 0.03f, 0.22f, 0.006f, 0, -0.05f, 0.12f, C(0.4f, 0.42f, 0.44f)),
        };
        foreach (float x in new[] { -1.3f, -0.4f, 0.5f, 1.4f }) l.Add(RB(0.03f, 0.03f, 0.22f, 0.006f, x, -0.08f, 0.12f, Dark));
        foreach (float x in new[] { -0.9f, 0.9f })
        {
            l.Add(M(VehicleModels.BentPipe(0.022f, 0.12f, Float3.UnitX, P3(x, 1.5f, -0.04f), P3(x, 1.68f, 0.05f), P3(x, 1.66f, 0.35f)), 0, 0, 0, Dark));
            l.Add(M(VehicleModels.Lamp(0.08f), x, 1.62f, 0.4f, C(0.95f, 0.93f, 0.8f), false, P3(150, 0, 0)));
        }
        return l.ToArray();
    }

    private static Part[] StreetLampParts()
    {
        Color iron = C(0.16f, 0.18f, 0.19f), iron2 = C(0.22f, 0.24f, 0.25f), lens = C(1f, 0.93f, 0.72f);
        var bolts = new List<(Mesh, Float3)>();
        for (int i = 0; i < 4; i++) { float a = i * MathF.Tau / 4f + 0.785f; bolts.Add((Meshes.Prism(6, 0.018f, 0.03f), P3(MathF.Cos(a) * 0.15f, -1.93f, MathF.Sin(a) * 0.15f))); }
        return
        [
            // Square base plate with nut cluster, a fluted cast foot, tapering shaft with collars.
            RB(0.4f, 0.04f, 0.4f, 0.012f, 0, -1.98f, 0, C(0.42f, 0.43f, 0.44f)), M(Bake(bolts), 0, 0, 0, C(0.5f, 0.5f, 0.52f)),
            M(Lathe("slfoot", [(0.0001f, -1.96f), (0.16f, -1.96f), (0.165f, -1.92f), (0.14f, -1.9f), (0.14f, -1.6f), (0.12f, -1.55f), (0.1f, -1.45f), (0.085f, -1.38f), (0.0001f, -1.38f)], 8), 0, 0, 0, iron, true, P3(0, 22.5f, 0)),
            RB(0.07f, 0.16f, 0.012f, 0.005f, 0, -1.72f, 0.135f, iron2), M(Rot90X(RoundedCylinder(0.006f, 0.006f, 0.002f, 10)), 0, -1.66f, 0.142f, C(0.5f, 0.5f, 0.52f)),
            M(Lathe("slshaft", [(0.0001f, -1.4f), (0.075f, -1.4f), (0.07f, 0.4f), (0.055f, 1.75f), (0.0001f, 1.75f)], 28), 0, 0, 0, iron, true),
            M(Lathe("slcol1", [(0.07f, -0.03f), (0.09f, -0.02f), (0.09f, 0.02f), (0.07f, 0.03f)], 28), 0, -1.38f, 0, iron2),
            M(Lathe("slcol2", [(0.058f, -0.025f), (0.072f, -0.015f), (0.072f, 0.015f), (0.058f, 0.025f)], 28), 0, 1.5f, 0, iron2),
            M(Lathe("slcap", [(0.0001f, 0.08f), (0.03f, 0.075f), (0.06f, 0.03f), (0.065f, 0f), (0.055f, -0.02f)], 24), 0, 1.76f, 0, iron2),
            // Swept arm with a scroll brace, cobra head with lens glowing downward.
            M(Sweep([new Sec(P3(0, 1.58f, 0.03f), 0.038f, 0.038f), new Sec(P3(0, 1.74f, 0.1f), 0.034f, 0.034f), new Sec(P3(0, 1.84f, 0.32f), 0.03f, 0.03f), new Sec(P3(0, 1.86f, 0.6f), 0.028f, 0.028f), new Sec(P3(0, 1.84f, 0.74f), 0.028f, 0.028f)], Float3.UnitX, 14, 0.5f), 0, 0, 0, iron),
            M(Sweep([new Sec(P3(0, 1.3f, 0.05f), 0.014f, 0.014f), new Sec(P3(0, 1.48f, 0.2f), 0.013f, 0.013f), new Sec(P3(0, 1.68f, 0.3f), 0.012f, 0.012f), new Sec(P3(0, 1.83f, 0.36f), 0.012f, 0.012f)], Float3.UnitX, 10, 0.5f), 0, 0, 0, iron),
            M(Sweep([new Sec(P3(0, 1.86f, 0.68f), 0.06f, 0.04f, 2.4f), new Sec(P3(0, 1.88f, 0.8f), 0.15f, 0.07f, 2.6f), new Sec(P3(0, 1.88f, 1.0f), 0.17f, 0.075f, 2.6f), new Sec(P3(0, 1.86f, 1.16f), 0.12f, 0.05f, 2.4f), new Sec(P3(0, 1.84f, 1.2f), 0.04f, 0.02f, 2.4f)], Float3.UnitX, 28, 0.6f), 0, 0, -0.38f, C(0.55f, 0.57f, 0.58f), true),
            M(Sweep([new Sec(P3(0, 1.825f, 0.8f), 0.11f, 0.02f, 2.6f), new Sec(P3(0, 1.82f, 0.95f), 0.13f, 0.025f, 2.6f), new Sec(P3(0, 1.825f, 1.1f), 0.09f, 0.02f, 2.6f)], Float3.UnitX, 24, 0.6f), 0, 0, -0.38f, lens) with { Glow = true },
            M(Lathe("slphoto", [(0.0001f, 0.03f), (0.025f, 0.025f), (0.03f, 0f), (0.0001f, 0f)], 16), 0, 1.95f, 0.62f, C(0.2f, 0.2f, 0.22f)),
        ];
    }

    private static Part[] GasCanParts()
    {
        Color red = C(0.72f, 0.1f, 0.08f), brass = C(0.78f, 0.6f, 0.28f), steel = C(0.55f, 0.56f, 0.58f);
        return
        [
            // Seamless cylinder with domed shoulder, rolled foot ring, neck ring, label band and a guard collar round the valve.
            M(Lathe("gcbody", [(0.0001f, -0.6f), (0.14f, -0.6f), (0.16f, -0.57f), (0.165f, -0.5f), (0.165f, 0.3f), (0.158f, 0.38f), (0.135f, 0.45f), (0.095f, 0.5f), (0.05f, 0.52f), (0.0001f, 0.525f)], 40), 0, 0, 0, red, true),
            M(Lathe("gcfoot", [(0.15f, -0.65f), (0.168f, -0.645f), (0.17f, -0.58f), (0.162f, -0.55f), (0.15f, -0.56f)], 40), 0, 0, 0, C(0.18f, 0.18f, 0.2f)),
            M(Lathe("gcband", [(0.1665f, -0.12f), (0.1665f, 0.14f)], 40), 0, 0, 0, C(0.92f, 0.9f, 0.82f)),
            M(Lathe("gcband2", [(0.1668f, -0.02f), (0.1668f, 0.04f)], 40), 0, 0, 0, C(0.12f, 0.35f, 0.15f)),
            M(Lathe("gcneck", [(0.04f, 0.5f), (0.05f, 0.505f), (0.052f, 0.56f), (0.045f, 0.565f)], 24), 0, 0, 0, steel),
            M(Lathe("gcguard", [(0.1f, 0.5f), (0.108f, 0.5f), (0.11f, 0.68f), (0.116f, 0.69f), (0.116f, 0.7f), (0.1f, 0.7f), (0.1f, 0.5f)], 32), 0, -0.04f, 0, red, true),
            // Brass valve body with outlet stub, hand wheel with spokes.
            RB(0.05f, 0.08f, 0.05f, 0.012f, 0, 0.6f, 0, brass), Cy(0.016f, 0.07f, 0.045f, 0.59f, 0, brass, false, P3(0, 0, 90)),
            M(Meshes.Prism(6, 0.02f, 0.02f), 0.08f, 0.59f, 0, brass, false, P3(0, 0, 90)),
            Cy(0.008f, 0.04f, 0, 0.66f, 0, steel),
            M(Lathe("gcwheel", [(0.035f, -0.008f), (0.045f, -0.006f), (0.047f, 0f), (0.045f, 0.006f), (0.035f, 0.008f), (0.033f, 0f)], 24), 0, 0.685f, 0, C(0.15f, 0.15f, 0.16f)),
            RB(0.07f, 0.008f, 0.008f, 0.003f, 0, 0.685f, 0, C(0.15f, 0.15f, 0.16f)), RB(0.008f, 0.008f, 0.07f, 0.003f, 0, 0.685f, 0, C(0.15f, 0.15f, 0.16f)),
        ];
    }

    private static Part[] GearParts()
    {
        Color st = C(0.6f, 0.61f, 0.63f), dk = C(0.3f, 0.31f, 0.33f);
        var teeth = new List<(Mesh, Float3)>();
        int n = 16; float rr = 0.335f;
        Mesh tooth = Rot90X(Slab([new(-0.034f, 0f), new(0.034f, 0f), new(0.02f, 0.062f), new(-0.02f, 0.062f)], -0.05f, 0.05f));
        var l = new List<Part>
        {
            // Rim, thin web, raised hub with keyway, six lightening holes (dark discs) and bolt heads.
            M(Lathe("grim", [(0.27f, -0.05f), (0.34f, -0.05f), (0.345f, -0.045f), (0.345f, 0.045f), (0.34f, 0.05f), (0.27f, 0.05f), (0.26f, 0.04f), (0.26f, -0.04f)], 64), 0, 0, 0, st, true, Side),
            M(Lathe("gweb", [(0.1f, -0.018f), (0.265f, -0.018f), (0.265f, 0.018f), (0.1f, 0.018f)], 64), 0, 0, 0, st, true, Side),
            M(Lathe("ghub", [(0.04f, -0.07f), (0.1f, -0.07f), (0.11f, -0.06f), (0.11f, 0.06f), (0.1f, 0.07f), (0.04f, 0.07f), (0.04f, -0.07f)], 40), 0, 0, 0, st, true, Side),
            M(Lathe("gbore", [(0.04f, -0.071f), (0.04f, 0.071f)], 24), 0, 0, 0, dk, false, Side),
            RB(0.072f, 0.142f, 0.02f, 0.002f, 0, 0, 0.041f, dk, false, P3(0, 0, 90)),
        };
        for (int i = 0; i < n; i++)
        {
            float a = i * MathF.Tau / n;
            teeth.Add((Spin(tooth, a), P3(MathF.Cos(a) * rr, 0, MathF.Sin(a) * rr)));
        }
        l.Add(M(Bake(teeth), 0, 0, 0, st, true, Side));
        var holes = new List<(Mesh, Float3)>(); var bolts = new List<(Mesh, Float3)>();
        for (int i = 0; i < 6; i++)
        {
            float a = i * MathF.Tau / 6f, b = a + MathF.Tau / 12f;
            holes.Add((RoundedCylinder(0.045f, 0.038f, 0.004f, 24), P3(MathF.Cos(a) * 0.185f, 0, MathF.Sin(a) * 0.185f)));
            bolts.Add((Meshes.Prism(6, 0.014f, 0.15f), P3(MathF.Cos(b) * 0.075f, 0, MathF.Sin(b) * 0.075f)));
        }
        l.Add(M(Bake(holes), 0, 0, 0, C(0.06f, 0.06f, 0.07f), false, Side));
        l.Add(M(Bake(bolts), 0, 0, 0, dk, false, Side));
        return l.ToArray();
    }

    /// <summary>Rotate a mesh about Y so its local +Y... keeps +Y; local +Y-up tooth at +X is turned to angle a in XZ.</summary>
    private static Mesh Spin(Mesh m, float a)
    {
        // Tooth built pointing along +Z after Rot90X? Map its outward axis to (cos a, 0, sin a).
        float c = MathF.Cos(a), s = MathF.Sin(a);
        Float3 R(Float3 v) => P3(v.X * s + v.Z * c, v.Y, -v.X * c + v.Z * s);
        var mm = new Mesh { IndexFormat = m.IndexFormat, Vertices = m.Vertices.Select(R).ToArray(), Normals = m.Normals?.Select(R).ToArray(), UV = m.UV, Indices = m.Indices };
        mm.RecalculateBounds(); return mm;
    }

    private static Part[] SeatParts()
    {
        Color vinyl = C(0.12f, 0.12f, 0.13f), insert = C(0.2f, 0.2f, 0.22f), stitch = C(0.55f, 0.15f, 0.1f), steel = C(0.45f, 0.46f, 0.48f);
        var l = new List<Part>
        {
            // Bucket seat: fluted insert between raised side bolsters, rolled front edge, steel pan and slide rails.
            M(PropMeshes.Cushion(0.34f, 0.1f, 0.48f), 0, -0.3f, 0.02f, insert),
            Cu(0.5f, 0.06f, 0.52f, 0, -0.355f, 0.02f, vinyl, true),
            M(PropMeshes.Cushion(0.34f, 0.09f, 0.6f), 0, 0.1f, -0.235f, insert, false, P3(80, 0, 0)),
            Cu(0.5f, 0.06f, 0.64f, 0, 0.09f, -0.26f, vinyl, true, P3(80, 0, 0)),
            Cu(0.3f, 0.12f, 0.17f, 0, 0.39f, -0.27f, vinyl, true, P3(80, 0, 0)),
            RB(0.46f, 0.03f, 0.48f, 0.01f, 0, -0.4f, 0.02f, steel),
        };
        foreach (float x in new[] { -1f, 1f })
        {
            l.Add(M(Sweep([new Sec(P3(x * 0.215f, -0.3f, 0.27f), 0.045f, 0.05f), new Sec(P3(x * 0.225f, -0.28f, 0.05f), 0.05f, 0.06f), new Sec(P3(x * 0.225f, -0.27f, -0.18f), 0.05f, 0.06f), new Sec(P3(x * 0.22f, -0.15f, -0.27f), 0.05f, 0.05f), new Sec(P3(x * 0.225f, 0.25f, -0.31f), 0.05f, 0.045f), new Sec(P3(x * 0.205f, 0.36f, -0.31f), 0.035f, 0.035f)], Float3.UnitX, 16, 0.8f), 0, 0, 0, vinyl));
            foreach (float y in new[] { -0.3f, 0.12f })
                l.Add(M(Sweep([new Sec(P3(x * 0.17f, y, y < 0 ? 0.25f : -0.255f), 0.0025f, 0.0025f), new Sec(P3(x * 0.17f, y < 0 ? -0.255f : 0.32f, y < 0 ? -0.2f : -0.235f), 0.0025f, 0.0025f)], Float3.UnitX, 4, 0.5f), 0, 0, 0, stitch));
            l.Add(RB(0.03f, 0.025f, 0.56f, 0.006f, x * 0.17f, -0.43f, 0.02f, C(0.3f, 0.3f, 0.32f)));
            l.Add(Cy(0.008f, 0.08f, x * 0.08f, 0.33f, -0.29f, C(0.7f, 0.7f, 0.72f)));
        }
        l.Add(M(PropMeshes.Handle(0.12f), 0.2f, -0.41f, 0.25f, C(0.6f, 0.6f, 0.62f), false, P3(0, 0, 90)));
        return l.ToArray();
    }

    private static Part[] TrashCanParts()
    {
        Color g = C(0.2f, 0.45f, 0.25f), g2 = C(0.15f, 0.38f, 0.2f), steel = C(0.55f, 0.56f, 0.58f);
        var ribs = new List<(Mesh, Float3)>();
        Mesh hoop = Lathe("binhoop", [(0.0001f, -0.012f), (0.008f, -0.012f), (0.008f, 0.012f), (0.0001f, 0.012f)], 8);
        var l = new List<Part>
        {
            M(PropMeshes.TrashCan(), 0, 0f, 0, g, true), M(PropMeshes.BinLid(), 0, 0.38f, 0, g2),
            // Corrugation hoops, a foot ring, side drop handles on lugs, and a lid grab handle.
            M(Lathe("binring1", [(0.222f, -0.012f), (0.232f, 0f), (0.222f, 0.012f)], 40), 0, -0.22f, 0, g2),
            M(Lathe("binring2", [(0.236f, -0.012f), (0.246f, 0f), (0.236f, 0.012f)], 40), 0, 0.05f, 0, g2),
            M(Lathe("binfoot", [(0.19f, -0.02f), (0.21f, -0.02f), (0.214f, 0.01f), (0.2f, 0.015f)], 40), 0, -0.39f, 0, C(0.12f, 0.12f, 0.13f)),
            M(Sweep([new Sec(P3(-0.05f, 0.49f, 0), 0.008f, 0.008f), new Sec(P3(-0.04f, 0.53f, 0), 0.008f, 0.008f), new Sec(P3(0.04f, 0.53f, 0), 0.008f, 0.008f), new Sec(P3(0.05f, 0.49f, 0), 0.008f, 0.008f)], Float3.UnitZ, 8, 0.6f), 0, 0, 0, steel),
        };
        foreach (float x in new[] { -1f, 1f })
        {
            l.Add(RB(0.03f, 0.05f, 0.08f, 0.01f, x * 0.258f, 0.27f, 0, g2));
            l.Add(M(Sweep([new Sec(P3(x * 0.27f, 0.27f, -0.05f), 0.007f, 0.007f), new Sec(P3(x * 0.3f, 0.22f, -0.04f), 0.007f, 0.007f), new Sec(P3(x * 0.3f, 0.22f, 0.04f), 0.007f, 0.007f), new Sec(P3(x * 0.27f, 0.27f, 0.05f), 0.007f, 0.007f)], Float3.UnitX, 8, 0.6f), 0, 0, 0, steel));
        }
        return l.ToArray();
    }
}
