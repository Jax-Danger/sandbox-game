using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// Procedural "placeholder art" meshes that look finished: rounded/chamfered boxes with smooth normals,
/// lathed shapes (barrels, tyres, rounded cylinders) and capsules. Cached by parameters.
/// Real models from the asset manifest still replace these.
/// </summary>
public static class Meshes
{
    private static readonly Dictionary<string, Mesh> _cache = [];

    /// <summary>Box with rounded edges and corners (radius r) and smooth normals. UVs are planar per face, in metres / 1 m.</summary>
    public static Mesh RoundedBox(Float3 size, float r, int bands = 3, bool worldUv = false)
    {
        Float3 h = size / 2f;
        r = Math.Clamp(r, 0f, MathF.Min(h.X, MathF.Min(h.Y, h.Z)) * 0.95f);
        string key = $"rb{size.X:0.###},{size.Y:0.###},{size.Z:0.###},{r:0.###},{bands}{(worldUv ? "w" : "")}";
        if (_cache.TryGetValue(key, out Mesh? cached)) return cached;
        if (r < 0.001f) return _cache[key] = Mesh.CreateCube(size);

        float[] Coords(float half)
        {
            var c = new List<float> { -half };
            for (int k = 1; k <= bands; k++) c.Add(-half + r * k / bands);
            if (half - r > -half + r + 1e-4f) c.Add(half - r); else c.RemoveAt(c.Count - 1);
            for (int k = bands - 1; k >= 0; k--) c.Add(half - r * k / bands);
            return c.Distinct().ToArray();
        }
        float[] cx = Coords(h.X), cy = Coords(h.Y), cz = Coords(h.Z);
        Float3 inner = new(h.X - r, h.Y - r, h.Z - r);

        var verts = new List<Float3>(); var norms = new List<Float3>(); var uvs = new List<Float2>(); var idx = new List<uint>();
        void Face(int axis, float sign)
        {
            int ua = (axis + 1) % 3, va = (axis + 2) % 3;
            float[] cu = ua == 0 ? cx : ua == 1 ? cy : cz, cv = va == 0 ? cx : va == 1 ? cy : cz;
            float fixedV = sign * (axis == 0 ? h.X : axis == 1 ? h.Y : h.Z);
            uint start = (uint)verts.Count;
            Float3 faceN = axis == 0 ? new Float3(sign, 0, 0) : axis == 1 ? new Float3(0, sign, 0) : new Float3(0, 0, sign);
            for (int j = 0; j < cv.Length; j++)
                for (int i = 0; i < cu.Length; i++)
                {
                    float[] p = new float[3];
                    p[axis] = fixedV; p[ua] = cu[i]; p[va] = cv[j];
                    Float3 pt = new(p[0], p[1], p[2]);
                    Float3 cl = new(Math.Clamp(pt.X, -inner.X, inner.X), Math.Clamp(pt.Y, -inner.Y, inner.Y), Math.Clamp(pt.Z, -inner.Z, inner.Z));
                    Float3 d = pt - cl;
                    float len = Float3.Length(d);
                    Float3 n = len > 1e-6f ? d / len : faceN;
                    verts.Add(cl + n * r);
                    norms.Add(n);
                    // worldUv: U always runs horizontally and V up the wall (sides) / along Z (top), so anisotropic textures
                    // (render streaks, courses) keep one orientation on every face of a non-square building.
                    if (worldUv) uvs.Add(axis == 1 ? new Float2(pt.X, pt.Z) : axis == 0 ? new Float2(pt.Z * sign, pt.Y) : new Float2(-pt.X * sign, pt.Y));
                    else uvs.Add(new Float2(cu[i] + 0.5f, cv[j] + 0.5f));
                }
            int w = cu.Length;
            for (int j = 0; j < cv.Length - 1; j++)
                for (int i = 0; i < w - 1; i++)
                {
                    uint a = start + (uint)(j * w + i), b = a + 1, c = a + (uint)w, e = c + 1;
                    Tri(a, b, e); Tri(a, e, c);
                }
            void Tri(uint a, uint b, uint c)
            {
                Float3 cr = Float3.Cross(verts[(int)b] - verts[(int)a], verts[(int)c] - verts[(int)a]);
                if (Float3.Dot(cr, faceN) >= 0f) { idx.Add(a); idx.Add(b); idx.Add(c); } else { idx.Add(a); idx.Add(c); idx.Add(b); }
            }
        }
        for (int axis = 0; axis < 3; axis++) { Face(axis, 1f); Face(axis, -1f); }
        return _cache[key] = Build(verts, norms, uvs, idx);
    }

    /// <summary>Revolves a (radius, height) profile around +Y. Normals come from the profile, so curves are smooth.</summary>
    public static Mesh Lathe(string key, (float R, float Y)[] profile, int slices = 32, bool alongZ = false)
    {
        key = "lathe:" + key + slices + (alongZ ? "z" : "");
        if (_cache.TryGetValue(key, out Mesh? cached)) return cached;
        var verts = new List<Float3>(); var norms = new List<Float3>(); var uvs = new List<Float2>(); var idx = new List<uint>();
        int pn = profile.Length;
        // Profile normals (2D), averaged at interior points for smoothness.
        var pnorm = new Float2[pn];
        for (int i = 0; i < pn; i++)
        {
            Float2 t = i == 0 ? Dir(0, 1) : i == pn - 1 ? Dir(pn - 2, pn - 1) : Float2.Normalize(Dir(i - 1, i) + Dir(i, i + 1));
            pnorm[i] = new Float2(t.Y, -t.X); // rotate tangent → outward normal (profile goes bottom→top)
            // Points on the axis are poles: the normal must be the axis itself, otherwise every slice gets a different tilt and
            // the pole shades as a star of dark specks (button domes, lens caps, nose cones).
            if (profile[i].R < 0.002f) pnorm[i] = new Float2(0f, pnorm[i].Y >= 0f ? 1f : -1f);
        }
        Float2 Dir(int a, int b) { Float2 d = new(profile[b].R - profile[a].R, profile[b].Y - profile[a].Y); float l = Float2.Length(d); return l > 1e-6f ? d / l : new Float2(0, 1); }
        float vlen = 0f; var vacc = new float[pn];
        for (int i = 1; i < pn; i++) { vlen += Float2.Length(new Float2(profile[i].R - profile[i - 1].R, profile[i].Y - profile[i - 1].Y)); vacc[i] = vlen; }
        for (int s = 0; s <= slices; s++)
        {
            float a = s * MathF.Tau / slices, ca = MathF.Cos(a), sa = MathF.Sin(a);
            for (int i = 0; i < pn; i++)
            {
                verts.Add(alongZ ? new Float3(profile[i].R * ca, profile[i].R * sa, profile[i].Y) : new Float3(profile[i].R * ca, profile[i].Y, profile[i].R * sa));
                norms.Add(Float3.Normalize(alongZ ? new Float3(pnorm[i].X * ca, pnorm[i].X * sa, pnorm[i].Y) : new Float3(pnorm[i].X * ca, pnorm[i].Y, pnorm[i].X * sa)));
                uvs.Add(new Float2((float)s / slices * 3f, vlen > 0 ? vacc[i] / vlen : 0));
            }
        }
        for (int s = 0; s < slices; s++)
            for (int i = 0; i < pn - 1; i++)
            {
                uint a = (uint)(s * pn + i), b = a + 1, c = (uint)((s + 1) * pn + i), d = c + 1;
                // Winding: outward normal from (a,c,b) when profile goes bottom→top and slices go +angle.
                Tri(a, b, d); Tri(a, d, c);
            }
        void Tri(uint a, uint b, uint c)
        {
            Float3 cr = Float3.Cross(verts[(int)b] - verts[(int)a], verts[(int)c] - verts[(int)a]);
            Float3 n = norms[(int)a] + norms[(int)b] + norms[(int)c];
            if (Float3.Dot(cr, n) >= 0f) { idx.Add(a); idx.Add(b); idx.Add(c); } else { idx.Add(a); idx.Add(c); idx.Add(b); }
        }
        return _cache[key] = Build(verts, norms, uvs, idx);
    }

    /// <summary>
    /// Smooth tapered capsule along +Z from z=0 (radius r0) to z=length (radius r1) with domed ends: forearms,
    /// finger segments, grips. Domes stay inside [0, length] so chained segments meet cleanly.
    /// </summary>
    public static Mesh Taper(float r0, float r1, float length, int slices = 20)
    {
        r0 = MathF.Max(r0, 0.0005f); r1 = MathF.Max(r1, 0.0005f); length = MathF.Max(length, 0.001f);
        float d0 = MathF.Min(r0, length * 0.45f), d1 = MathF.Min(r1, length * 0.45f);
        var prof = new List<(float, float)>();
        for (int k = 0; k <= 5; k++) { float a = MathF.PI / 2f * k / 5f; prof.Add((MathF.Sin(a) * r0, d0 - MathF.Cos(a) * d0)); }
        for (int k = 0; k <= 5; k++) { float a = MathF.PI / 2f * k / 5f; prof.Add((MathF.Cos(a) * r1, length - d1 + MathF.Sin(a) * d1)); }
        return Lathe($"tp{r0:0.####},{r1:0.####},{length:0.####}", prof.ToArray(), slices, alongZ: true);
    }

    /// <summary>Flat-shaded regular prism along Y (e.g. octagonal sign plate), first corner rotated by half a side.</summary>
    public static Mesh Prism(int sides, float r, float h)
    {
        string key = $"prism{sides},{r:0.###},{h:0.###}";
        if (_cache.TryGetValue(key, out Mesh? c)) return c;
        var v = new List<Float3>(); var n = new List<Float3>(); var uv = new List<Float2>(); var idx = new List<uint>();
        Float3 P(int i, float y) { float a = (i + 0.5f) * MathF.Tau / sides; return new Float3(MathF.Cos(a) * r, y, MathF.Sin(a) * r); }
        void Quad(Float3 a, Float3 b, Float3 cc, Float3 d, Float3 nn)
        {
            uint o = (uint)v.Count; v.AddRange([a, b, cc, d]); for (int k = 0; k < 4; k++) { n.Add(nn); uv.Add(new Float2(k & 1, k >> 1)); }
            Float3 cr = Float3.Cross(b - a, cc - a); bool f = Float3.Dot(cr, nn) >= 0f;
            idx.AddRange(f ? [o, o + 1, o + 2, o, o + 2, o + 3] : [o, o + 2, o + 1, o, o + 3, o + 2]);
        }
        float hy = h / 2f;
        for (int i = 0; i < sides; i++)
        {
            Float3 a = P(i, -hy), b = P(i + 1, -hy), mid = (a + b) / 2f;
            Quad(a, b, P(i + 1, hy), P(i, hy), Float3.Normalize(new Float3(mid.X, 0f, mid.Z)));
            Quad(new Float3(0f, hy, 0f), new Float3(0f, hy, 0f), P(i + 1, hy), P(i, hy), Float3.UnitY);
            Quad(new Float3(0f, -hy, 0f), new Float3(0f, -hy, 0f), P(i, -hy), P(i + 1, -hy), -Float3.UnitY);
        }
        return _cache[key] = Build(v, n, uv, idx);
    }

    /// <summary>Cylinder along Y with rounded rims (bevel b) and closed caps; smooth.</summary>
    public static Mesh RoundedCylinder(float radius, float height, float bevel, int slices = 32)
    {
        float h = height / 2f; bevel = Math.Clamp(bevel, 0f, MathF.Min(radius, h) * 0.9f);
        var prof = new List<(float, float)> { (0f, -h) };
        for (int k = 0; k <= 4; k++) { float a = MathF.PI / 2f * k / 4f; prof.Add((radius - bevel + MathF.Sin(a) * bevel, -h + bevel - MathF.Cos(a) * bevel)); }
        for (int k = 0; k <= 4; k++) { float a = MathF.PI / 2f * k / 4f; prof.Add((radius - bevel + MathF.Cos(a) * bevel, h - bevel + MathF.Sin(a) * bevel)); }
        prof.Add((0f, h));
        return Lathe($"rc{radius:0.###},{height:0.###},{bevel:0.###}", prof.ToArray(), slices);
    }

    /// <summary>Barrel: bulged staves with two rings and rounded rims.</summary>

    /// <summary>Wooden crate: inset panel core, proud edge battens on all 12 edges and a diagonal brace on every face.</summary>
    public static Mesh Crate(float size)
    {
        string key = $"crate{size:0.###}";
        if (_cache.TryGetValue(key, out Mesh? hit)) return hit;
        float h = size / 2f, w = size * 0.13f, p = w * 0.45f, e = h - w / 2f;
        var parts = new List<Mesh> { RoundedBox(new Float3(size - 2f * p, size - 2f * p, size - 2f * p), size * 0.01f) };
        Mesh Off(Mesh m, Float3 o)
        {
            var mm = new Mesh { IndexFormat = m.IndexFormat, Vertices = m.Vertices.Select(v => v + o).ToArray(), Normals = m.Normals, UV = m.UV, Indices = m.Indices };
            mm.RecalculateBounds(); return mm;
        }
        foreach (float a in new[] { -e, e })
            foreach (float b in new[] { -e, e })
            {
                parts.Add(Off(RoundedBox(new Float3(size, w, w), w * 0.12f), new Float3(0f, a, b)));
                parts.Add(Off(RoundedBox(new Float3(w, size - 2f * w + 0.002f, w), w * 0.12f), new Float3(a, 0f, b)));
                parts.Add(Off(RoundedBox(new Float3(w, w, size - 2f * w + 0.002f), w * 0.12f), new Float3(a, b, 0f)));
            }
        float d = h - p + w * 0.22f, q = h - w * 0.9f, bw = w * 0.42f, bt = w * 0.24f;
        Mesh Brace(Float3 a, Float3 b, Float3 side) => Sweep([new Sec(a, bw, bt, 6f), new Sec(b, bw, bt, 6f)], side, 12, 0.15f);
        foreach (float sg in new[] { -1f, 1f })
        {
            parts.Add(Brace(new Float3(-q, -q * sg, sg * d), new Float3(q, q * sg, sg * d), Float3.UnitZ));
            parts.Add(Brace(new Float3(sg * d, -q, q * sg), new Float3(sg * d, q, -q * sg), Float3.UnitX));
            parts.Add(Brace(new Float3(-q, sg * d, q * sg), new Float3(q, sg * d, -q * sg), Float3.UnitY));
        }
        return _cache[key] = Merge(parts.ToArray());
    }

    public static Mesh Barrel(float radius, float height)
    {
        // Steel drum: recessed heads inside rolled chimes, straight walls with two swaged rolling hoops and shallow ribs.
        float h = height / 2f, r = radius * 0.95f;
        var prof = new List<(float, float)> { (0.0001f, -h + 0.03f), (r * 0.9f, -h + 0.03f), (r * 0.93f, -h + 0.01f), (r * 0.97f, -h), (r + 0.012f, -h + 0.012f), (r + 0.01f, -h + 0.03f), (r, -h + 0.045f) };
        foreach (float hy in new[] { -0.33f, -0.1f, 0.1f, 0.33f })
        {
            bool hoop = MathF.Abs(hy) > 0.2f; float y = hy * height, d = hoop ? 0.022f : 0.007f, w = hoop ? 0.035f : 0.02f;
            prof.Add((r, y - w)); prof.Add((r + d * 0.7f, y - w * 0.4f)); prof.Add((r + d, y)); prof.Add((r + d * 0.7f, y + w * 0.4f)); prof.Add((r, y + w));
        }
        prof.Add((r, h - 0.045f)); prof.Add((r + 0.01f, h - 0.03f)); prof.Add((r + 0.012f, h - 0.012f)); prof.Add((r * 0.97f, h)); prof.Add((r * 0.93f, h - 0.01f)); prof.Add((r * 0.9f, h - 0.03f)); prof.Add((0.0001f, h - 0.03f));
        return Lathe($"drum{radius:0.###},{height:0.###}", prof.ToArray(), 40);
    }

    /// <summary>Tyre (axis along Y): rounded tread, sidewalls and a recessed hub.</summary>
    public static Mesh Tyre(float radius, float width)
    {
        float w = width / 2f, hub = radius * 0.55f;
        var prof = new List<(float, float)> { (0f, -w * 0.5f), (hub, -w * 0.5f), (hub * 1.05f, -w * 0.85f) };
        for (int k = 0; k <= 8; k++) { float a = -MathF.PI / 2f + MathF.PI * k / 8f; prof.Add((radius - 0.35f * w + MathF.Cos(a) * 0.35f * w, MathF.Sin(a) * w)); }
        prof.Add((hub * 1.05f, w * 0.85f)); prof.Add((hub, w * 0.5f)); prof.Add((0f, w * 0.5f));
        Mesh body = Lathe($"tyre{radius:0.###},{width:0.###}", prof.ToArray(), 48);
        string key = $"tread{radius:0.###},{width:0.###}";
        if (_cache.TryGetValue(key, out Mesh? hit)) return hit;
        // Tread: car tyres get two staggered rows of blocks plus a centre rib; big (tractor) tyres get chevron lugs.
        var parts = new List<Mesh> { body };
        bool lug = radius > 0.6f;
        int n = lug ? 22 : Math.Clamp((int)(radius * 80f), 18, 44);
        float t = lug ? radius * 0.09f : MathF.Max(0.012f, radius * 0.045f), rr = radius - t * 0.35f;
        for (int i = 0; i < n; i++)
        {
            float a0 = i * MathF.Tau / n;
            if (lug)
            {
                foreach (float sd in new[] { -1f, 1f })
                {
                    float a = a0 + (sd > 0 ? MathF.PI / n : 0f);
                    parts.Add(Block(RoundedBox(new Float3(t, w * 0.8f, radius * 0.11f), t * 0.25f, 1), a, rr, sd * w * 0.42f, sd * 32f));
                }
            }
            else
            {
                float len = w * 0.36f, tw = MathF.Tau * radius / n * 0.6f;
                parts.Add(Block(RoundedBox(new Float3(t, len, tw), t * 0.3f, 1), a0, rr, -w * 0.42f, 0f));
                parts.Add(Block(RoundedBox(new Float3(t, len, tw), t * 0.3f, 1), a0 + MathF.PI / n, rr, w * 0.42f, 0f));
                parts.Add(Block(RoundedBox(new Float3(t * 0.8f, w * 0.18f, tw * 0.7f), t * 0.25f, 1), a0 + MathF.PI / (2 * n), rr - t * 0.1f, 0f, 0f));
            }
        }
        return _cache[key] = Merge(parts.ToArray());
    }

    /// <summary>Tread block: tilted by twist (degrees, chevron) in the axial/tangential plane, pushed out to radius r at angle a, axial offset y.</summary>
    private static Mesh Block(Mesh m, float a, float r, float y, float twist)
    {
        float tw = twist * MathF.PI / 180f, ct = MathF.Cos(tw), st = MathF.Sin(tw), c = MathF.Cos(a), s = MathF.Sin(a);
        Float3 T(Float3 v)
        {
            Float3 q = new(v.X, v.Y * ct - v.Z * st, v.Y * st + v.Z * ct); // twist about the radial axis
            // radial X → (cos a, 0, sin a); tangential Z → (-sin a, 0, cos a)
            return new Float3(q.X * c - q.Z * s, q.Y, q.X * s + q.Z * c);
        }
        var mm = new Mesh { IndexFormat = IndexFormat.UInt32, Vertices = m.Vertices.Select(v => T(v) + new Float3(c * r, y, s * r)).ToArray(), Normals = m.Normals.Select(T).ToArray(), UV = m.UV, Indices = m.Indices };
        mm.RecalculateBounds(); return mm;
    }

    public static Mesh Capsule(float radius, float height)
    {
        string key = $"cap{radius:0.###},{height:0.###}";
        if (_cache.TryGetValue(key, out Mesh? m)) return m;
        float h = MathF.Max(0f, height / 2f - radius);
        var prof = new List<(float, float)>();
        for (int k = 0; k <= 8; k++) { float a = -MathF.PI / 2f + MathF.PI / 2f * k / 8f; prof.Add((MathF.Cos(a) * radius, -h + MathF.Sin(a) * radius)); }
        for (int k = 0; k <= 8; k++) { float a = MathF.PI / 2f * k / 8f; prof.Add((MathF.Cos(a) * radius, h + MathF.Sin(a) * radius)); }
        return _cache[key] = Lathe(key, prof.ToArray(), 24);
    }

    public static Mesh Sphere(float radius) => _cache.TryGetValue($"sph{radius:0.###}", out Mesh? m) ? m : _cache[$"sph{radius:0.###}"] = Mesh.CreateSphere(radius, 24, 32);


    /// <summary>One cross-section of a <see cref="Sweep"/>: centre, half-width (along the side axis), half-height,
    /// superellipse exponent (2 = ellipse, 4+ = rounded rectangle) and an optional per-section side axis.</summary>
    public readonly record struct Sec(Float3 P, float W, float H, float N = 2f, Float3? Side = null);

    /// <summary>
    /// Sweeps superellipse cross-sections along a path (smooth normals, capped ends). The section's width axis is
    /// <paramref name="side"/> projected perpendicular to the path, its height axis is tangent × side. This is the
    /// workhorse for organic and machined shapes: fingers, palms, forearms, slides, stocks, grips.
    /// <paramref name="round"/> adds rounded ends of that radius (fraction of the smaller half-size, 0..1).
    /// </summary>
    public static Mesh Sweep(IList<Sec> input, Float3 side, int slices = 18, float round = 0.6f, bool flatStart = false, bool flatEnd = false)
    {
        var secs = new List<Sec>(input);
        Float3 Tan(List<Sec> l, int i) => SafeN(l[Math.Min(i + 1, l.Count - 1)].P - l[Math.Max(i - 1, 0)].P);
        if (round > 0f)
        {
            Sec Shrink(Sec s, Float3 t, float r, float k) // k in 0..1 along the quarter circle
            {
                float a = k * MathF.PI / 2f, cut = r * (1f - MathF.Cos(a));
                return s with { P = s.P + t * (r * MathF.Sin(a)), W = MathF.Max(0.0004f, s.W - cut), H = MathF.Max(0.0004f, s.H - cut) };
            }
            int n0 = secs.Count;
            if (!flatEnd)
            {
                Sec e = secs[n0 - 1]; Float3 t = Tan(secs, n0 - 1); float r = MathF.Min(e.W, e.H) * round;
                if (n0 > 1) r = MathF.Min(r, 0.45f * Float3.Length(e.P - secs[n0 - 2].P)); // never fold back past the previous section
                secs[n0 - 1] = Shrink(e, t, r, 0f) with { P = e.P - t * r };
                for (int k = 1; k <= 4; k++) secs.Add(Shrink(e with { P = e.P - t * r }, t, r, k / 4f));
            }
            if (!flatStart)
            {
                Sec b = secs[0]; Float3 t = -Tan(secs, 0); float r = MathF.Min(b.W, b.H) * round;
                if (secs.Count > 1) r = MathF.Min(r, 0.45f * Float3.Length(secs[1].P - b.P));
                secs[0] = b with { P = b.P - t * r };
                for (int k = 1; k <= 4; k++) secs.Insert(0, Shrink(b with { P = b.P - t * r }, t, r, k / 4f));
            }
        }
        int m = secs.Count;
        var v = new List<Float3>(); var nrm = new List<Float3>(); var uv = new List<Float2>(); var idx = new List<uint>();
        var cen = new List<Float3>();
        float acc = 0f;
        for (int i = 0; i < m; i++)
        {
            Sec s = secs[i];
            Float3 t = Tan(secs, i);
            Float3 sd = s.Side ?? side;
            Float3 x = sd - t * Float3.Dot(sd, t);
            x = Float3.Length(x) < 1e-5f ? SafeN(Float3.Cross(t, MathF.Abs(t.Y) < 0.9f ? Float3.UnitY : Float3.UnitX)) : SafeN(x);
            Float3 y = SafeN(Float3.Cross(t, x));
            if (i > 0) acc += Float3.Length(s.P - secs[i - 1].P);
            float e = 2f / MathF.Max(0.5f, s.N);
            for (int k = 0; k <= slices; k++)
            {
                float a = k * MathF.Tau / slices, c = MathF.Cos(a), sn = MathF.Sin(a);
                float cx = MathF.Sign(c) * MathF.Pow(MathF.Abs(c), e), cy = MathF.Sign(sn) * MathF.Pow(MathF.Abs(sn), e);
                v.Add(s.P + x * (s.W * cx) + y * (s.H * cy));
                nrm.Add(Float3.Zero); uv.Add(new Float2((float)k / slices, acc * 8f));
                cen.Add(s.P);
            }
        }
        int w = slices + 1;
        void Tri(uint a, uint b, uint c, Float3 outward)
        {
            Float3 cr = Float3.Cross(v[(int)b] - v[(int)a], v[(int)c] - v[(int)a]);
            if (Float3.Dot(cr, outward) < 0f) (b, c) = (c, b);
            idx.Add(a); idx.Add(b); idx.Add(c);
            Float3 fn = Float3.Cross(v[(int)b] - v[(int)a], v[(int)c] - v[(int)a]);
            nrm[(int)a] += fn; nrm[(int)b] += fn; nrm[(int)c] += fn;
        }
        for (int i = 0; i < m - 1; i++)
            for (int k = 0; k < slices; k++)
            {
                uint a = (uint)(i * w + k), b = a + 1, c = (uint)((i + 1) * w + k), d = c + 1;
                Float3 o = v[(int)a] + v[(int)d] - cen[(int)a] - cen[(int)d];
                Tri(a, b, d, o); Tri(a, d, c, o);
            }
        // Merge seam normals (k = 0 and k = slices share a position).
        for (int i = 0; i < m; i++) { int a = i * w, b = a + slices; Float3 sum = nrm[a] + nrm[b]; nrm[a] = sum; nrm[b] = sum; }
        int body = v.Count;
        void Cap(int ring, Float3 outward)
        {
            uint c0 = (uint)v.Count; v.Add(secs[ring].P); nrm.Add(outward); uv.Add(new Float2(0.5f, 0.5f));
            uint st = (uint)v.Count;
            for (int k = 0; k <= slices; k++) { v.Add(v[ring * w + k]); nrm.Add(outward); uv.Add(uv[ring * w + k]); }
            for (int k = 0; k < slices; k++)
            {
                uint a = st + (uint)k, b = a + 1;
                Float3 cr = Float3.Cross(v[(int)a] - v[(int)c0], v[(int)b] - v[(int)c0]);
                if (Float3.Dot(cr, outward) >= 0f) { idx.Add(c0); idx.Add(a); idx.Add(b); } else { idx.Add(c0); idx.Add(b); idx.Add(a); }
            }
        }
        for (int i = 0; i < body; i++) nrm[i] = SafeN(nrm[i]);
        Cap(0, -Tan(secs, 0));
        Cap(m - 1, Tan(secs, m - 1));
        return Build(v, nrm, uv, idx);
    }

    private static Float3 SafeN(Float3 a) { float l = Float3.Length(a); return l > 1e-7f ? a / l : Float3.UnitZ; }

    /// <summary>Lathe along +Z from (radius, z) pairs with caps closed where the radius is 0 (not cached).</summary>
    public static Mesh LatheZ((float R, float Z)[] profile, int slices = 28) => Lathe($"lz{string.Join(',', profile.Select(p => $"{p.R:0.####}/{p.Z:0.####}"))}", profile, slices, alongZ: true);

    /// <summary>Bevelled box with a raised picture-frame border on chosen faces (dev-cube / framed panel look).
    /// faces: bit 0..5 = +X,-X,+Y,-Y,+Z,-Z.</summary>
    public static Mesh FramedBox(Float3 size, float border, float lip, int faces = 63)
    {
        string key = $"framed{size.X:0.###}/{size.Y:0.###}/{size.Z:0.###}/{border:0.###}/{lip:0.####}/{faces}";
        if (_cache.TryGetValue(key, out Mesh? c)) return c;
        var parts = new List<Mesh> { RoundedBox(size, MathF.Min(0.05f, MathF.Min(size.X, MathF.Min(size.Y, size.Z)) * 0.3f)) };
        Float3 h = size / 2f;
        Mesh Bar(Float3 sz, Float3 at)
        {
            Mesh m = RoundedBox(sz, MathF.Min(lip * 0.45f, MathF.Min(sz.X, MathF.Min(sz.Y, sz.Z)) * 0.45f), 2);
            return new Mesh { IndexFormat = m.IndexFormat, Vertices = m.Vertices!.Select(v => v + at).ToArray(), Normals = m.Normals, UV = m.UV, Indices = m.Indices };
        }
        for (int f = 0; f < 6; f++)
        {
            if ((faces & (1 << f)) == 0) continue;
            int ax = f / 2; float sg = f % 2 == 0 ? 1f : -1f;
            // u, v = the two in-face axes
            int ua = (ax + 1) % 3, va = (ax + 2) % 3;
            float[] hh = [h.X, h.Y, h.Z];
            float hu = hh[ua] - 0.02f, hv = hh[va] - 0.02f, off = hh[ax] + lip / 2f - 0.002f;
            Float3 Mk(float a, float u, float v) { float[] r = new float[3]; r[ax] = a; r[ua] = u; r[va] = v; return new Float3(r[0], r[1], r[2]); }
            parts.Add(Bar(Mk(lip, hu * 2f, border), Mk(sg * off, 0f, hv - border / 2f)));
            parts.Add(Bar(Mk(lip, hu * 2f, border), Mk(sg * off, 0f, -hv + border / 2f)));
            parts.Add(Bar(Mk(lip, border, hv * 2f - border * 2f), Mk(sg * off, hu - border / 2f, 0f)));
            parts.Add(Bar(Mk(lip, border, hv * 2f - border * 2f), Mk(sg * off, -hu + border / 2f, 0f)));
        }
        return _cache[key] = Merge(parts.ToArray());
    }

    /// <summary>Concatenates meshes into one (same material).</summary>
    public static Mesh Merge(params Mesh[] ms)
    {
        var v = new List<Float3>(); var n = new List<Float3>(); var uv = new List<Float2>(); var idx = new List<uint>();
        foreach (Mesh m in ms)
        {
            uint o = (uint)v.Count;
            v.AddRange(m.Vertices ?? []); n.AddRange(m.Normals ?? []);
            uv.AddRange(m.UV is { } u && u.Length == (m.Vertices?.Length ?? 0) ? u : new Float2[m.Vertices?.Length ?? 0]);
            foreach (uint i in m.Indices ?? []) idx.Add(i + o);
        }
        return Build(v, n, uv, idx);
    }

    /// <summary>Lumpy foliage blob: lat-long sphere displaced by layered sine noise (leaf clumps), smooth normals from the
    /// displaced surface. Cached by (r, seed).</summary>
    public static Mesh Blob(float r, int seed, float lump = 0.18f)
    {
        string key = $"blob{r:0.###},{seed},{lump:0.###}";
        if (_cache.TryGetValue(key, out Mesh? hit)) return hit;
        const int la = 18, lo = 28;
        float ph = seed * 1.7f;
        Float3 P(float th, float fi)
        {
            Float3 d = new(MathF.Sin(th) * MathF.Cos(fi), MathF.Cos(th), MathF.Sin(th) * MathF.Sin(fi));
            float n = MathF.Sin(d.X * 5.1f + ph) * MathF.Sin(d.Y * 4.3f + ph * 0.7f) * MathF.Sin(d.Z * 4.7f + ph * 1.3f)
                    + 0.5f * MathF.Sin(d.X * 9.3f - ph) * MathF.Sin(d.Z * 8.1f + ph) * MathF.Cos(d.Y * 7.7f);
            float flat = d.Y < 0f ? 1f + d.Y * 0.35f : 1f; // flatter underside
            return d * r * (1f + lump * n) * flat;
        }
        var v = new List<Float3>(); var n = new List<Float3>(); var uv = new List<Float2>(); var idx = new List<uint>();
        for (int i = 0; i <= la; i++)
            for (int j = 0; j <= lo; j++)
            {
                float th = MathF.PI * i / la, fi = MathF.Tau * j / lo, e = 0.01f;
                Float3 p = P(th, fi), a = P(MathF.Min(MathF.PI, th + e), fi) - P(MathF.Max(0f, th - e), fi), b = P(th, fi + e) - P(th, fi - e);
                Float3 nn = SafeN(Float3.Cross(b, a));
                if (i == 0 || i == la || Float3.Dot(nn, p) < 0f) nn = Float3.Dot(nn, p) < 0f ? -nn : nn;
                if (i == 0 || i == la) nn = SafeN(p);
                v.Add(p); n.Add(nn); uv.Add(new Float2((float)j / lo * 3f, (float)i / la * 2f));
            }
        for (int i = 0; i < la; i++)
            for (int j = 0; j < lo; j++)
            {
                uint a = (uint)(i * (lo + 1) + j), b = a + 1, c = a + (uint)(lo + 1), d = c + 1;
                idx.Add(a); idx.Add(b); idx.Add(c); idx.Add(b); idx.Add(d); idx.Add(c);
            }
        // Wind the triangles outward.
        for (int t = 0; t < idx.Count; t += 3)
        {
            Float3 p0 = v[(int)idx[t]], p1 = v[(int)idx[t + 1]], p2 = v[(int)idx[t + 2]];
            if (Float3.Dot(Float3.Cross(p1 - p0, p2 - p0), p0 + p1 + p2) < 0f) (idx[t + 1], idx[t + 2]) = (idx[t + 2], idx[t + 1]);
        }
        return _cache[key] = Build(v, n, uv, idx);
    }

    /// <summary>Ramp prop (2 x 0.2 x 3.5): steel deck between raised side rails, with anti-slip cleats across the top.</summary>
    public static Mesh RampDeck()
    {
        if (_cache.TryGetValue("rampdeck", out Mesh? hit)) return hit;
        static Mesh At(Mesh m, float x, float y, float z)
        {
            var mm = new Mesh { IndexFormat = m.IndexFormat, Vertices = m.Vertices.Select(v => v + new Float3(x, y, z)).ToArray(), Normals = m.Normals, UV = m.UV, Indices = m.Indices };
            mm.RecalculateBounds(); return mm;
        }
        var parts = new List<Mesh> { RoundedBox(new Float3(1.84f, 0.12f, 3.46f), 0.02f) };
        foreach (float sx in new[] { -0.94f, 0.94f }) parts.Add(At(RoundedBox(new Float3(0.12f, 0.2f, 3.5f), 0.03f), sx, 0f, 0f));
        for (float z = -1.55f; z <= 1.56f; z += 0.25f) parts.Add(At(RoundedBox(new Float3(1.8f, 0.035f, 0.06f), 0.012f), 0f, 0.07f, z));
        foreach (float sz in new[] { -1.72f, 1.72f }) parts.Add(At(RoundedBox(new Float3(1.84f, 0.16f, 0.06f), 0.02f), 0f, -0.01f, sz));
        return _cache["rampdeck"] = Merge(parts.ToArray());
    }

    /// <summary>Chiselled boulder: a sphere cut by random planes, flat shaded so facets read crisply.</summary>
    /// <summary>Moss/lichen cap for <see cref="Rock"/>: the rock's upward-facing facets (plus a ragged noisy edge) lifted 1 cm.</summary>
    public static Mesh RockMoss(float r, int seed)
    {
        string key = $"rockmoss{r:0.###},{seed}";
        if (_cache.TryGetValue(key, out Mesh? hit)) return hit;
        Mesh rock = Rock(r, seed);
        var v = new List<Float3>(); var n = new List<Float3>(); var uv = new List<Float2>(); var idx = new List<uint>();
        uint[] ri = rock.Indices; Float3[] rv = rock.Vertices, rn = rock.Normals;
        for (int t = 0; t + 2 < ri.Length; t += 3)
        {
            Float3 c = (rv[ri[t]] + rv[ri[t + 1]] + rv[ri[t + 2]]) / 3f, cn = SafeN(rn[ri[t]] + rn[ri[t + 1]] + rn[ri[t + 2]]);
            float wob = MathF.Sin(c.X * 7.3f + seed) * MathF.Cos(c.Z * 6.1f - seed) * 0.25f;
            if (cn.Y + wob < 0.78f) continue;
            Float3 a = rv[ri[t]], b = rv[ri[t + 1]], cc = rv[ri[t + 2]];
            Float3 fn = SafeN(Float3.Cross(b - a, cc - a));
            bool flip = Float3.Dot(fn, c) < 0f; if (flip) fn = -fn; // face outward, whatever the source winding
            foreach (Float3 p in flip ? new[] { a, cc, b } : new[] { a, b, cc }) { v.Add(p + fn * 0.012f); n.Add(fn); uv.Add(new Float2(p.X + p.Y * 0.5f, p.Z - p.Y * 0.3f)); idx.Add((uint)v.Count - 1); }
        }
        return _cache[key] = Build(v, n, uv, idx);
    }

    public static Mesh Rock(float r, int seed)
    {
        string key = $"rock{r:0.###},{seed}";
        if (_cache.TryGetValue(key, out Mesh? hit)) return hit;
        var rng = new Random(seed * 7919 + 13);
        int k = 11;
        var pn = new Float3[k]; var ph = new float[k];
        for (int i = 0; i < k; i++)
        {
            Float3 d = new((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1);
            if (i == 0) d = new Float3(0, -1, 0);
            pn[i] = SafeN(d); ph[i] = i == 0 ? 0.45f : 0.55f + 0.17f * (float)rng.NextDouble();
        }
        Float3 P(float th, float fi)
        {
            Float3 d = new(MathF.Sin(th) * MathF.Cos(fi), MathF.Cos(th), MathF.Sin(th) * MathF.Sin(fi));
            float t = 1f;
            for (int i = 0; i < k; i++) { float c = Float3.Dot(d, pn[i]); if (c > 0.01f) t = MathF.Min(t, ph[i] / c); }
            return d * r * t * 1.45f;
        }
        const int la = 14, lo = 22;
        var v = new List<Float3>(); var n = new List<Float3>(); var uv = new List<Float2>(); var idx = new List<uint>();
        void Tri(Float3 a, Float3 b, Float3 c)
        {
            Float3 nn = Float3.Cross(b - a, c - a);
            if (Float3.Length(nn) < 1e-7f) return;
            if (Float3.Dot(nn, a + b + c) < 0f) { (b, c) = (c, b); nn = -nn; }
            nn = SafeN(nn);
            foreach (var q in new[] { a, b, c }) { idx.Add((uint)v.Count); v.Add(q); n.Add(nn); uv.Add(new Float2(q.X + q.Z, q.Y)); }
        }
        for (int i = 0; i < la; i++)
            for (int j = 0; j < lo; j++)
            {
                float t0 = MathF.PI * i / la, t1 = MathF.PI * (i + 1) / la, f0 = MathF.Tau * j / lo, f1 = MathF.Tau * (j + 1) / lo;
                Float3 a = P(t0, f0), b = P(t0, f1), c = P(t1, f0), d = P(t1, f1);
                Tri(a, b, c); Tri(b, d, c);
            }
        return _cache[key] = Build(v, n, uv, idx);
    }

    /// <summary>Flat-shaded mesh from convex polygons (each wound counter-clockwise seen from outside). UVs are planar in metres.</summary>
    public static Mesh Faces(params Float3[][] polys)
    {
        var v = new List<Float3>(); var n = new List<Float3>(); var uv = new List<Float2>(); var idx = new List<uint>();
        foreach (Float3[] f in polys)
        {
            Float3 nn = SafeN(Float3.Cross(f[1] - f[0], f[2] - f[0]));
            bool yUp = MathF.Abs(nn.Y) > 0.7f, xSide = MathF.Abs(nn.X) > MathF.Abs(nn.Z);
            uint b = (uint)v.Count;
            foreach (Float3 p in f) { v.Add(p); n.Add(nn); uv.Add(yUp ? new Float2(p.X, p.Z) : xSide ? new Float2(p.Z, p.Y) : new Float2(p.X, p.Y)); }
            for (int k = 1; k + 1 < f.Length; k++) { idx.Add(b); idx.Add(b + (uint)k); idx.Add(b + (uint)k + 1); }
        }
        return Build(v, n, uv, idx);
    }

    /// <summary>Curved label patch on a Y-axis cylinder of radius <paramref name="r"/>, centred on +Z, spanning
    /// ±<paramref name="halfAngle"/> radians and y0..y1. Smooth radial normals, so decals hug drums and pipes.</summary>
    public static Mesh CylPatch(float r, float halfAngle, float y0, float y1, int segs = 12)
    {
        var v = new List<Float3>(); var n = new List<Float3>(); var uv = new List<Float2>(); var idx = new List<uint>();
        for (int i = 0; i <= segs; i++)
        {
            float a = -halfAngle + 2f * halfAngle * i / segs; Float3 d = new(MathF.Sin(a), 0f, MathF.Cos(a));
            v.Add(d * r + new Float3(0f, y0, 0f)); n.Add(d); uv.Add(new Float2((float)i / segs, 0f));
            v.Add(d * r + new Float3(0f, y1, 0f)); n.Add(d); uv.Add(new Float2((float)i / segs, 1f));
        }
        for (int i = 0; i < segs; i++) { uint b = (uint)(i * 2); idx.AddRange([b, b + 2, b + 1, b + 1, b + 2, b + 3]); }
        return Build(v, n, uv, idx);
    }

    /// <summary>Organic closed surface: a lat/long sphere grid (shared seam and poles, smooth normals) whose every
    /// point is placed by <paramref name="shape"/>(unit direction). Used for heads and other sculpted forms.</summary>
    public static Mesh Sculpt(Func<Float3, Float3> shape, int rings = 28, int segs = 40)
    {
        var v = new List<Float3>(); var uv = new List<Float2>(); var idx = new List<uint>();
        v.Add(shape(new Float3(0f, -1f, 0f))); uv.Add(new Float2(0.5f, 0f));
        for (int r = 1; r < rings; r++)
        {
            float th = MathF.PI * r / rings - MathF.PI / 2f;
            for (int sgi = 0; sgi < segs; sgi++)
            {
                float ph = MathF.Tau * sgi / segs;
                v.Add(shape(new Float3(MathF.Cos(th) * MathF.Sin(ph), MathF.Sin(th), MathF.Cos(th) * MathF.Cos(ph))));
                uv.Add(new Float2((float)sgi / segs, (float)r / rings));
            }
        }
        v.Add(shape(new Float3(0f, 1f, 0f))); uv.Add(new Float2(0.5f, 1f));
        uint top = (uint)(v.Count - 1);
        uint Ix(int r, int sgi) => (uint)(1 + (r - 1) * segs + (sgi % segs));
        for (int sgi = 0; sgi < segs; sgi++) { idx.Add(0); idx.Add(Ix(1, sgi + 1)); idx.Add(Ix(1, sgi)); }
        for (int r = 1; r < rings - 1; r++)
            for (int sgi = 0; sgi < segs; sgi++)
            {
                uint a = Ix(r, sgi), b = Ix(r, sgi + 1), c = Ix(r + 1, sgi), d = Ix(r + 1, sgi + 1);
                idx.Add(a); idx.Add(b); idx.Add(d); idx.Add(a); idx.Add(d); idx.Add(c);
            }
        for (int sgi = 0; sgi < segs; sgi++) { idx.Add(top); idx.Add(Ix(rings - 1, sgi)); idx.Add(Ix(rings - 1, sgi + 1)); }
        var mesh = new Mesh { IndexFormat = IndexFormat.UInt16, Vertices = v.ToArray(), UV = uv.ToArray(), Indices = idx.ToArray() };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
        return mesh;
    }

    /// <summary>Gaussian bump weight around direction c (unit d), sharpness k.</summary>
    public static float Bump(Float3 d, Float3 c, float k) => MathF.Exp(-(1f - Float3.Dot(d, Float3.Normalize(c))) * k);

    internal static Mesh Build(List<Float3> v, List<Float3> n, List<Float2> uv, List<uint> idx)
    {
        var mesh = new Mesh { IndexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16, Vertices = v.ToArray(), Normals = n.ToArray(), UV = uv.ToArray(), Indices = idx.ToArray() };
        mesh.RecalculateBounds();
        if (mesh.UV is { Length: > 0 }) mesh.RecalculateTangents();
        return mesh;
    }
}
