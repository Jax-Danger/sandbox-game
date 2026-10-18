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
    public static Mesh RoundedBox(Float3 size, float r, int bands = 3)
    {
        Float3 h = size / 2f;
        r = Math.Clamp(r, 0f, MathF.Min(h.X, MathF.Min(h.Y, h.Z)) * 0.95f);
        string key = $"rb{size.X:0.###},{size.Y:0.###},{size.Z:0.###},{r:0.###},{bands}";
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
                    uvs.Add(new Float2(cu[i] + 0.5f, cv[j] + 0.5f));
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
    public static Mesh Barrel(float radius, float height)
    {
        float h = height / 2f;
        var prof = new List<(float, float)> { (0f, -h), (radius * 0.86f, -h), (radius * 0.9f, -h + 0.02f) };
        for (int k = 0; k <= 12; k++)
        {
            float t = k / 12f, y = -h + 0.04f + t * (height - 0.08f);
            float bulge = 1f + 0.06f * MathF.Sin(t * MathF.PI);
            float ring = (MathF.Abs(t - 0.25f) < 0.035f || MathF.Abs(t - 0.75f) < 0.035f) ? 1.025f : 1f;
            prof.Add((radius * 0.93f * bulge * ring, y));
        }
        prof.Add((radius * 0.9f, h - 0.02f)); prof.Add((radius * 0.86f, h)); prof.Add((0f, h - 0.015f));
        return Lathe($"barrel{radius:0.###},{height:0.###}", prof.ToArray(), 36);
    }

    /// <summary>Tyre (axis along Y): rounded tread, sidewalls and a recessed hub.</summary>
    public static Mesh Tyre(float radius, float width)
    {
        float w = width / 2f, hub = radius * 0.55f;
        var prof = new List<(float, float)> { (0f, -w * 0.5f), (hub, -w * 0.5f), (hub * 1.05f, -w * 0.85f) };
        for (int k = 0; k <= 8; k++) { float a = -MathF.PI / 2f + MathF.PI * k / 8f; prof.Add((radius - 0.35f * w + MathF.Cos(a) * 0.35f * w, MathF.Sin(a) * w)); }
        prof.Add((hub * 1.05f, w * 0.85f)); prof.Add((hub, w * 0.5f)); prof.Add((0f, w * 0.5f));
        return Lathe($"tyre{radius:0.###},{width:0.###}", prof.ToArray(), 40);
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

    private static Mesh Build(List<Float3> v, List<Float3> n, List<Float2> uv, List<uint> idx)
    {
        var mesh = new Mesh { IndexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16, Vertices = v.ToArray(), Normals = n.ToArray(), UV = uv.ToArray(), Indices = idx.ToArray() };
        mesh.RecalculateBounds();
        if (mesh.UV is { Length: > 0 }) mesh.RecalculateTangents();
        return mesh;
    }
}
