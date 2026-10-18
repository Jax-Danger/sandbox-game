using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;
using static Sandbox.Meshes;

namespace Sandbox;

/// <summary>
/// Sculpted humanoid meshes for ragdolls and NPCs: each limb is a smooth swept/lathed shape (deltoids, calves,
/// a tapered waist and chest), plus detail parts that ride on the limb (hands, shoes, neck, face, hair, belt).
/// Colliders stay the simple boxes/sphere in <see cref="Ragdoll"/>; this is visual only. Meshes are cached per limb.
/// </summary>
public static class Humans
{
    public enum Tint { Main, Skin, Shoe, Hair, Eye, Belt, Cuff, Pants, Sclera, Lip }
    public sealed record Detail(Mesh Mesh, Tint Tint, Float3 Offset = default)
    {
        // Pole/seam vertices of sculpted ears/hair came out with zero normals: NaN pixels that blacked out the HDR frame.
        public Mesh Mesh { get; init; } = Meshes.Sanitize(Mesh);
    }

    private static readonly Dictionary<string, (Mesh Main, Detail[] Details)> _cache = [];
    private static Float3 V(float x, float y, float z) => new(x, y, z);

    /// <summary>Main mesh (drawn with the limb's own tinted material) and detail parts for a limb name from Ragdoll.Defs.</summary>
    public static (Mesh Main, Detail[] Details) Limb(string name)
    {
        string key = name.EndsWith(" L") || name.EndsWith(" R") ? name[..^2] + (name.EndsWith(" L") ? "L" : "R") : name;
        if (_cache.TryGetValue(key, out var hit)) return hit;
        float side = name.EndsWith(" L") ? -1f : 1f;
        (Mesh, Detail[]) r = name switch
        {
            "Pelvis" => Pelvis(),
            "Torso" => Torso(),
            "Head" => Head(),
            _ when name.StartsWith("Upper arm") => UpperArm(side),
            _ when name.StartsWith("Lower arm") => LowerArm(side, name.Contains("grip")),
            _ when name.StartsWith("Upper leg") => UpperLeg(),
            _ => LowerLeg(),
        };
        return _cache[key] = r;
    }

    private static readonly Dictionary<string, Detail[]> _merged = [];
    /// <summary>Same details as <see cref="Limb"/>, but every part sharing a tint merged into one mesh (offsets baked):
    /// one draw call per colour instead of one per eye, glint, button and cuff. Ten NPCs went from 400 to ~200 draws.</summary>
    public static Detail[] MergedDetails(string name)
    {
        if (_merged.TryGetValue(name, out var hit)) return hit;
        var outp = new List<Detail>();
        foreach (var grp in Limb(name).Details.GroupBy(d => d.Tint))
        {
            if (grp.Count() == 1) { outp.Add(grp.First()); continue; }
            var v = new List<Float3>(); var nr = new List<Float3>(); var uv = new List<Float2>(); var idx = new List<uint>();
            foreach (Detail d in grp)
            {
                uint b = (uint)v.Count; Mesh m = d.Mesh;
                Float3[] mv = m.Vertices, mn = m.Normals; Float2[] mu = m.UV;
                for (int i = 0; i < mv.Length; i++) { v.Add(mv[i] + d.Offset); nr.Add(i < mn.Length ? mn[i] : Float3.UnitY); uv.Add(i < mu.Length ? mu[i] : Float2.Zero); }
                foreach (uint ii in m.Indices) idx.Add(ii + b);
            }
            var mesh = new Mesh { IndexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16, Vertices = v.ToArray(), Normals = nr.ToArray(), UV = uv.ToArray(), Indices = idx.ToArray() };
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            outp.Add(new Detail(mesh, grp.Key));
        }
        return _merged[name] = outp.ToArray();
    }

    private static Mesh Sw(float n, Float3 side, float round, params (float y, float w, float d, float z)[] s)
        => Sweep(s.Select(x => new Sec(V(0f, x.y, x.z), x.w, x.d, n)).ToArray(), side, 22, round);

    private static (Mesh, Detail[]) Pelvis()
    {
        Mesh hips = Sw(2.6f, Float3.UnitX, 0.5f, (0.10f, 0.158f, 0.098f, 0f), (0.02f, 0.172f, 0.104f, 0f), (-0.06f, 0.176f, 0.106f, -0.004f), (-0.11f, 0.16f, 0.096f, -0.006f));
        Mesh belt = Sw(2.6f, Float3.UnitX, 0.3f, (0.105f, 0.162f, 0.101f, 0f), (0.07f, 0.166f, 0.103f, 0f));
        Mesh buckle = Sweep([new Sec(V(0f, 0.088f, 0.1f), 0.022f, 0.015f, 4f), new Sec(V(0f, 0.088f, 0.108f), 0.022f, 0.015f, 4f)], Float3.UnitX, 12, 0.4f);
        return (hips, [new(belt, Tint.Belt), new(buckle, Tint.Cuff)]);
    }

    private static (Mesh, Detail[]) Torso()
    {
        // Waist → chest → shoulders, slightly forward-leaning chest, flatter back.
        Mesh body = Sw(2.4f, Float3.UnitX, 0.75f,
            (-0.26f, 0.158f, 0.098f, 0f), (-0.15f, 0.163f, 0.104f, 0.004f), (-0.02f, 0.18f, 0.114f, 0.008f), (0.10f, 0.2f, 0.12f, 0.006f),
            (0.17f, 0.214f, 0.11f, 0f), (0.215f, 0.198f, 0.092f, -0.004f), (0.235f, 0.16f, 0.07f, -0.006f));
        Mesh neck = LatheY("neck", [(0.052f, 0.22f), (0.050f, 0.26f), (0.047f, 0.31f)]);
        // Shoulder yokes: trapezius slopes from the neck out over each shoulder joint so arms hang from the body
        // instead of floating beside a flat-topped box.
        var parts = new List<Detail> { new(neck, Tint.Skin) };
        // Shirt collar ring, button placket and a chest pocket.
        // Collar: a stand band hugging the neck plus two pointed fold-down flaps lying on the chest.
        parts.Add(new(LatheY("collar2", [(0.06f, 0.2f), (0.07f, 0.206f), (0.066f, 0.235f), (0.058f, 0.252f), (0.053f, 0.254f)]), Tint.Cuff));
        foreach (float sd in new[] { -1f, 1f })
            parts.Add(new(Sweep([new Sec(V(sd * 0.03f, 0.238f, 0.06f), 0.016f, 0.005f, 4f), new Sec(V(sd * 0.045f, 0.218f, 0.09f), 0.021f, 0.005f, 4f),
                new Sec(V(sd * 0.058f, 0.198f, 0.106f), 0.012f, 0.004f, 4f), new Sec(V(sd * 0.065f, 0.188f, 0.11f), 0.003f, 0.003f, 4f)], Float3.UnitX, 8, 0.5f), Tint.Main));
        parts.Add(new(Sweep([new Sec(V(0f, -0.24f, 0.106f), 0.012f, 0.003f, 4f), new Sec(V(0f, -0.02f, 0.124f), 0.012f, 0.003f, 4f), new Sec(V(0f, 0.1f, 0.13f), 0.012f, 0.003f, 4f), new Sec(V(0f, 0.2f, 0.104f), 0.012f, 0.003f, 4f)], Float3.UnitX, 8, 0.4f), Tint.Cuff));
        parts.Add(new(Sweep([new Sec(V(-0.09f, 0.06f, 0.123f), 0.035f, 0.003f, 4f), new Sec(V(-0.09f, 0.11f, 0.12f), 0.035f, 0.003f, 4f)], Float3.UnitX, 8, 0.4f), Tint.Cuff));
        // Shirt buttons down the placket (following its curve), a pocket flap with its own button.
        {
            var btn = new List<Mesh>();
            (float y, float z)[] plk = [(-0.24f, 0.106f), (-0.02f, 0.124f), (0.1f, 0.13f), (0.2f, 0.104f)];
            float Z(float y) { for (int i = 0; i < plk.Length - 1; i++) if (y <= plk[i + 1].y) { float t = (y - plk[i].y) / (plk[i + 1].y - plk[i].y); return plk[i].z + (plk[i + 1].z - plk[i].z) * t; } return plk[^1].z; }
            Mesh disc = LatheZ([(0.0001f, 0f), (0.0055f, 0f), (0.006f, 0.0016f), (0.0045f, 0.0028f), (0.0001f, 0.003f)], 10);
            foreach (float by in new[] { 0.15f, 0.07f, -0.01f, -0.09f, -0.17f }) btn.Add(Moved(disc, V(0f, by, Z(by) + 0.0025f)));
            btn.Add(Moved(disc, V(-0.09f, 0.1f, 0.1255f)));
            parts.Add(new(Merge(btn.ToArray()), Tint.Sclera));
            parts.Add(new(Sweep([new Sec(V(-0.09f, 0.104f, 0.1235f), 0.037f, 0.0035f, 4f), new Sec(V(-0.09f, 0.12f, 0.121f), 0.037f, 0.0035f, 4f)], Float3.UnitX, 8, 0.4f), Tint.Cuff));
        }
        foreach (float sd in new[] { -1f, 1f })
            parts.Add(new(Sweep([new Sec(V(sd * 0.06f, 0.212f, -0.004f), 0.06f, 0.028f), new Sec(V(sd * 0.14f, 0.192f, -0.002f), 0.07f, 0.04f),
                new Sec(V(sd * 0.21f, 0.17f, 0f), 0.056f, 0.047f), new Sec(V(sd * 0.245f, 0.15f, 0f), 0.05f, 0.047f), new Sec(V(sd * 0.258f, 0.132f, 0f), 0.036f, 0.036f), new Sec(V(sd * 0.262f, 0.12f, 0f), 0.02f, 0.02f)],
                Float3.UnitZ, 20, 0.85f), Tint.Main));
        return (body, parts.ToArray());
    }

    private static Sec[] Ring(float y, float rx, float rz, float z)
    {
        var l = new List<Sec>();
        for (int i = 0; i <= 24; i++) { float a = i * MathF.Tau / 24f; l.Add(new Sec(V(MathF.Cos(a) * rx, y, z + MathF.Sin(a) * rz), 0.012f, 0.008f, 2f, Float3.UnitY)); }
        return l.ToArray();
    }

    private static Mesh Moved(Mesh m, Float3 o)
    {
        var mm = new Mesh { IndexFormat = m.IndexFormat, Vertices = m.Vertices.Select(v => v + o).ToArray(), Normals = m.Normals, UV = m.UV, Indices = m.Indices };
        mm.RecalculateBounds(); return mm;
    }

    private static Mesh LatheY(string key, (float R, float Y)[] p) => Lathe("hum_" + key, p, 24);

    private static (Mesh, Detail[]) Head()
    {
        // Egg-shaped skull with a jaw, nose, ears, eyes and a hair cap.
        Mesh skull = Sculpt(d =>
        {
            float x = d.X, y = d.Y, z = d.Z;
            // Jaw: the lower face narrows toward a rounded chin; cheeks stay full just under the eyes.
            float low = Math.Clamp(-y, 0f, 1f);
            float wx = 0.109f * (1f - 0.36f * MathF.Pow(low, 1.4f));
            float wz = (z > 0f ? 0.123f : 0.125f) * (1f - 0.2f * MathF.Pow(low, 1.6f));
            float hy = y > 0f ? 0.13f : 0.128f;
            Float3 p = new(x * wx, y * hy, z * wz);
            // Back of the skull sits higher and rounder; the face plane is flatter than a ball.
            Float3 n = d;
            float push = 0.004f * Bump(d, new(0.55f, -0.05f, 0.8f), 18f) + 0.004f * Bump(d, new(-0.55f, -0.05f, 0.8f), 18f)   // cheekbones
                       - 0.005f * Bump(d, new(0.36f, 0.15f, 0.92f), 120f) - 0.005f * Bump(d, new(-0.36f, 0.15f, 0.92f), 120f)  // eye sockets
                       + 0.005f * Bump(d, new(0.3f, 0.32f, 0.9f), 70f) + 0.005f * Bump(d, new(-0.3f, 0.32f, 0.9f), 70f)       // brow ridge
                       + 0.008f * Bump(d, new(0f, -0.8f, 0.6f), 40f)                                                          // chin
                       + 0.004f * Bump(d, new(0f, -0.45f, 0.9f), 60f)                                                         // upper lip / muzzle
                       - 0.006f * Bump(d, new(1f, 0.3f, 0.1f), 18f) - 0.006f * Bump(d, new(-1f, 0.3f, 0.1f), 18f);            // temples
            p += n * push;
            return new Float3(p.X, p.Y - 0.002f, p.Z + 0.002f);
        }, 30, 44);
        Mesh nose = Sweep([new Sec(V(0f, 0.014f, 0.112f), 0.008f, 0.008f), new Sec(V(0f, -0.016f, 0.127f), 0.011f, 0.012f), new Sec(V(0f, -0.032f, 0.128f), 0.014f, 0.012f), new Sec(V(0f, -0.039f, 0.121f), 0.012f, 0.008f)], Float3.UnitX, 12, 0.9f);
        var ears = new List<Detail>();
        foreach (float sx in new[] { -1f, 1f })
        {
            // Ear: a thin tilted shell with a rolled rim (helix), a hollow bowl (concha) and a fleshy lobe.
            float side = sx;
            Mesh ear = Sculpt(d =>
            {
                float y = d.Y, zz = d.Z, out_ = d.X * side;
                float w = 0.025f * (1f - 0.25f * MathF.Max(0f, -y)), h = 0.036f;
                float t = out_ > 0f ? 0.0075f * (1f - 0.75f * Bump(d, new(side, -0.05f, 0.1f), 6f)) : 0.006f; // bowl dished on the outer face
                Float3 p = new(d.X * t, y * h, zz * w);
                if (y < -0.55f) p.Z += (y + 0.55f) * 0.006f; // lobe tucks forward
                return p;
            }, 14, 20);
            Mesh tilt = Meshes.Merge(ear);
            // Flared: the back edge stands ~25 degrees off the skull (ears read from the front instead of hiding in the hair line),
            // set lower so the top of the helix sits at brow height and the lobe near the nose base.
            var vs = tilt.Vertices.Select(v => { float flare = MathF.Max(0f, -v.Z) * 0.5f; return new Float3(v.X + side * (flare + 0.002f), v.Y, v.Z) + V(sx * 0.113f, -0.006f, -0.01f); }).ToArray();
            tilt.Vertices = vs; tilt.RecalculateNormals(); tilt.RecalculateBounds();
            ears.Add(new(tilt, Tint.Skin));
        }
        Mesh hair = Sweep([
            new Sec(V(0f, 0.0f, -0.035f), 0.12f, 0.122f), new Sec(V(0f, 0.06f, -0.017f), 0.123f, 0.138f), new Sec(V(0f, 0.105f, -0.014f), 0.108f, 0.126f),
            new Sec(V(0f, 0.14f, -0.018f), 0.056f, 0.074f)], Float3.UnitX, 26, 0.3f, flatStart: true);
        var d = new List<Detail> { new(nose, Tint.Skin), new(hair, Tint.Hair) };
        // Mouth: a dark parting line between a thin upper lip and a fuller lower lip, with a soft chin pad below.
        d.Add(new(Sweep([new Sec(V(-0.024f, -0.061f, 0.106f), 0.003f, 0.002f), new Sec(V(0f, -0.063f, 0.113f), 0.003f, 0.002f), new Sec(V(0.024f, -0.061f, 0.106f), 0.003f, 0.002f)], Float3.UnitY, 8, 0.9f), Tint.Eye));
        d.Add(new(Sweep([new Sec(V(-0.022f, -0.059f, 0.104f), 0.003f, 0.004f), new Sec(V(-0.008f, -0.055f, 0.112f), 0.005f, 0.005f), new Sec(V(0f, -0.056f, 0.1135f), 0.0045f, 0.005f), new Sec(V(0.008f, -0.055f, 0.112f), 0.005f, 0.005f), new Sec(V(0.022f, -0.059f, 0.104f), 0.003f, 0.004f)], Float3.UnitY, 10, 0.9f), Tint.Lip));
        d.Add(new(Sweep([new Sec(V(-0.02f, -0.066f, 0.104f), 0.004f, 0.004f), new Sec(V(0f, -0.069f, 0.112f), 0.0065f, 0.0065f), new Sec(V(0.02f, -0.066f, 0.104f), 0.004f, 0.004f)], Float3.UnitY, 10, 0.9f), Tint.Lip));
        foreach (float sx in new[] { -1f, 1f })
        {
            // Eyes: white eyeball set into the socket, dark iris, upper lid crease and a heavier brow.
            d.Add(new(Sphere(0.0155f), Tint.Sclera, V(sx * 0.04f, 0.018f, 0.1015f)));
            d.Add(new(Sphere(0.0086f), Tint.Eye, V(sx * 0.0395f, 0.0175f, 0.1125f)));
            d.Add(new(Sweep([new Sec(V(sx * 0.024f, 0.022f, 0.111f), 0.003f, 0.003f), new Sec(V(sx * 0.04f, 0.0305f, 0.1165f), 0.0045f, 0.004f), new Sec(V(sx * 0.056f, 0.022f, 0.107f), 0.003f, 0.003f)], Float3.UnitY, 8, 0.9f), Tint.Skin));
            d.Add(new(Sweep([new Sec(V(sx * 0.026f, 0.008f, 0.11f), 0.002f, 0.002f), new Sec(V(sx * 0.04f, 0.0045f, 0.1145f), 0.0028f, 0.0028f), new Sec(V(sx * 0.054f, 0.009f, 0.106f), 0.002f, 0.002f)], Float3.UnitY, 8, 0.9f), Tint.Skin));
            d.Add(new(Sweep([new Sec(V(sx * 0.018f, 0.044f, 0.114f), 0.007f, 0.005f), new Sec(V(sx * 0.042f, 0.05f, 0.11f), 0.0085f, 0.0055f), new Sec(V(sx * 0.066f, 0.043f, 0.1f), 0.005f, 0.0035f)], Float3.UnitY, 8, 0.8f), Tint.Hair));
            // Sideburn.
            d.Add(new(Sweep([new Sec(V(sx * 0.108f, 0.035f, 0.02f), 0.006f, 0.014f, 2f, Float3.UnitZ), new Sec(V(sx * 0.11f, -0.005f, 0.022f), 0.005f, 0.011f, 2f, Float3.UnitZ)], Float3.UnitZ, 8, 0.8f), Tint.Hair));
        }
        // Side-parted fringe sweeping across the forehead.
        d.Add(new(Sweep([new Sec(V(-0.08f, 0.1f, 0.07f), 0.012f, 0.012f), new Sec(V(-0.035f, 0.104f, 0.094f), 0.02f, 0.014f), new Sec(V(0.02f, 0.094f, 0.1f), 0.02f, 0.012f), new Sec(V(0.068f, 0.08f, 0.088f), 0.012f, 0.008f), new Sec(V(0.09f, 0.07f, 0.07f), 0.004f, 0.004f)], Float3.UnitY, 12, 0.8f), Tint.Hair));
        d.Add(new(Sphere(0.0025f), Tint.Sclera, V(-0.036f, 0.021f, 0.1198f))); // catch-lights
        d.Add(new(Sphere(0.0025f), Tint.Sclera, V(0.043f, 0.021f, 0.1198f)));
        d.AddRange(ears);
        return (skull, d.ToArray());
    }

    private static (Mesh, Detail[]) UpperArm(float side)
    {
        Mesh arm = Sw(2.2f, Float3.UnitX, 0.95f, (0.12f, 0.052f, 0.052f, 0f), (0.08f, 0.057f, 0.056f, 0f), (0.0f, 0.054f, 0.052f, 0f), (-0.11f, 0.049f, 0.049f, 0.004f), (-0.165f, 0.047f, 0.047f, 0.004f));
        Mesh hem = Sw(2.2f, Float3.UnitX, 0.3f, (-0.02f, 0.06f, 0.058f, 0f), (-0.045f, 0.058f, 0.056f, 0f));
        // Sleeve end rolls softly over the elbow (no hard rim cutting into the forearm when the arm bends).
        Mesh roll = Sw(2.2f, Float3.UnitX, 0.95f, (-0.13f, 0.049f, 0.049f, 0.004f), (-0.16f, 0.052f, 0.052f, 0.004f), (-0.185f, 0.05f, 0.05f, 0.004f), (-0.2f, 0.045f, 0.045f, 0.004f), (-0.21f, 0.04f, 0.04f, 0.003f));
        return (arm, [new(roll, Tint.Cuff)]);
    }

    private static (Mesh, Detail[]) LowerArm(float side, bool grip = false)
    {
        Mesh fore = Sw(2.2f, Float3.UnitX, 0.95f, (0.2f, 0.042f, 0.042f, 0f), (0.16f, 0.046f, 0.044f, 0f), (0.08f, 0.048f, 0.046f, 0.003f), (-0.06f, 0.038f, 0.034f, 0f), (-0.14f, 0.032f, 0.026f, 0f));
        // Relaxed hand: palm, grouped fingers curling in, thumb.
        Mesh palm = Sweep([new Sec(V(0f, -0.13f, 0f), 0.03f, 0.018f, 2.6f, Float3.UnitZ), new Sec(V(0f, -0.17f, 0.003f), 0.042f, 0.017f, 2.8f, Float3.UnitZ), new Sec(V(0f, -0.21f, 0.004f), 0.044f, 0.015f, 3f, Float3.UnitZ)], Float3.UnitZ, 16, 0.7f);
        // Four separate curled fingers (was one mitten slab), slightly fanned, middle longest.
        var fms = new List<Mesh>();
        float[] fx = [-0.03f, -0.01f, 0.01f, 0.029f], fl = [0.045f, 0.052f, 0.049f, 0.04f];
        for (int f = 0; f < 4; f++)
        {
            var fs = new List<Sec>();
            for (int i = 0; i <= 6; i++)
            {
                // Relaxed: a loose 1.25 rad curl. Grip (driving): fingers wrap a ~2 cm bar (steering-wheel rim).
                float L = fl[f], R = grip ? 0.019f : L, a = i / 6f * (grip ? L / R * 0.95f : 1.25f);
                fs.Add(new Sec(V(fx[f] * (1f + (grip ? 0.03f : 0.15f) * i / 6f), -0.205f - MathF.Sin(a) * R, 0.004f + (1f - MathF.Cos(a)) * R), 0.0085f - i * 0.0004f, 0.0085f - i * 0.0004f, 2.2f));
            }
            fms.Add(Sweep(fs, Float3.UnitX, 10, 0.9f));
        }
        Mesh fing = Merge(fms.ToArray());
        Mesh thumb = grip
            ? Sweep([new Sec(V(-side * 0.032f, -0.15f, 0.012f), 0.012f, 0.011f), new Sec(V(-side * 0.04f, -0.185f, 0.034f), 0.011f, 0.01f), new Sec(V(-side * 0.022f, -0.215f, 0.046f), 0.0095f, 0.009f), new Sec(V(-side * 0.004f, -0.228f, 0.04f), 0.0088f, 0.0085f)], Float3.UnitX, 12, 0.9f)
            : Sweep([new Sec(V(-side * 0.032f, -0.15f, 0.012f), 0.012f, 0.011f), new Sec(V(-side * 0.046f, -0.18f, 0.03f), 0.011f, 0.01f), new Sec(V(-side * 0.04f, -0.205f, 0.045f), 0.0095f, 0.009f)], Float3.UnitX, 12, 0.9f);
        return (fore, [new(palm, Tint.Skin), new(fing, Tint.Skin), new(thumb, Tint.Skin)]);
    }

    private static (Mesh, Detail[]) UpperLeg()
    {
        Mesh thigh = Sw(2.3f, Float3.UnitX, 0.6f, (0.29f, 0.07f, 0.07f, 0f), (0.25f, 0.084f, 0.082f, 0f), (0.12f, 0.081f, 0.08f, 0.004f), (-0.05f, 0.068f, 0.068f, 0.004f), (-0.2f, 0.058f, 0.058f, 0f), (-0.23f, 0.056f, 0.056f, 0f));
        // Trouser seams down both sides and a pressed front crease, so the legs aren't plain tubes.
        var d = new List<Detail>();
        foreach (float sx in new[] { -1f, 1f })
            d.Add(new(Sweep([new Sec(V(sx * 0.083f, 0.25f, 0f), 0.0025f, 0.006f), new Sec(V(sx * 0.081f, 0.12f, 0.002f), 0.0025f, 0.006f), new Sec(V(sx * 0.068f, -0.05f, 0.004f), 0.0025f, 0.005f), new Sec(V(sx * 0.057f, -0.22f, 0f), 0.0025f, 0.005f)], Float3.UnitZ, 6, 0.6f), Tint.Pants));
        d.Add(new(Sweep([new Sec(V(0f, 0.2f, 0.08f), 0.004f, 0.0025f), new Sec(V(0f, 0.12f, 0.083f), 0.004f, 0.0025f), new Sec(V(0f, -0.05f, 0.071f), 0.004f, 0.0025f), new Sec(V(0f, -0.22f, 0.057f), 0.004f, 0.0025f)], Float3.UnitX, 6, 0.6f), Tint.Pants));
        return (thigh, d.ToArray());
    }

    private static Mesh? _thighFp;
    /// <summary>First-person thigh: only the knee half, so looking down shows trousers running into the knee instead of
    /// the top of the shin (the full thigh sits right under the camera and fills the view).</summary>
    public static Mesh ThighFp => _thighFp ??= Sw(2.3f, Float3.UnitX, 0.9f, (0.0f, 0.064f, 0.064f, 0.004f), (-0.05f, 0.066f, 0.066f, 0.004f), (-0.2f, 0.058f, 0.058f, 0f), (-0.23f, 0.056f, 0.056f, 0f));

    private static (Mesh, Detail[]) LowerLeg()
    {
        Mesh calf = Sw(2.2f, Float3.UnitX, 0.9f, (0.26f, 0.054f, 0.054f, 0f), (0.14f, 0.06f, 0.064f, -0.008f), (0.0f, 0.052f, 0.052f, -0.004f), (-0.16f, 0.04f, 0.04f, 0f), (-0.2f, 0.04f, 0.04f, 0f));
        Mesh cuff = Sw(2.2f, Float3.UnitX, 0.3f, (-0.155f, 0.046f, 0.046f, 0f), (-0.19f, 0.046f, 0.046f, 0f));
        // Shoe swept heel → toe with a sole.
        Mesh shoe = Sweep([new Sec(V(0f, -0.2f, -0.06f), 0.042f, 0.035f, 3f), new Sec(V(0f, -0.205f, -0.02f), 0.046f, 0.038f, 3f), new Sec(V(0f, -0.212f, 0.05f), 0.05f, 0.03f, 3f),
            new Sec(V(0f, -0.218f, 0.11f), 0.046f, 0.024f, 3f), new Sec(V(0f, -0.22f, 0.135f), 0.036f, 0.02f, 3f)], Float3.UnitX, 18, 0.7f);
        Mesh sole = Sweep([new Sec(V(0f, -0.238f, -0.07f), 0.044f, 0.008f, 4f), new Sec(V(0f, -0.24f, 0.05f), 0.052f, 0.008f, 4f), new Sec(V(0f, -0.239f, 0.14f), 0.04f, 0.008f, 4f)], Float3.UnitX, 18, 0.7f);
        // Lace-up details: tongue, laces across the instep, heel counter and a toe-cap seam.
        Mesh tongue = Sweep([new Sec(V(0f, -0.172f, 0.0f), 0.02f, 0.006f, 3f), new Sec(V(0f, -0.19f, 0.045f), 0.022f, 0.006f, 3f), new Sec(V(0f, -0.2f, 0.07f), 0.018f, 0.005f, 3f)], Float3.UnitX, 10, 0.6f);
        var laces = new List<Mesh>();
        for (int i = 0; i < 4; i++) { float z = 0.012f + i * 0.017f, y = -0.18f - i * 0.0055f; laces.Add(Sweep([new Sec(V(-0.022f, y - 0.002f, z), 0.0025f, 0.0025f), new Sec(V(0f, y + 0.003f, z + 0.002f), 0.0025f, 0.0025f), new Sec(V(0.022f, y - 0.002f, z), 0.0025f, 0.0025f)], Float3.UnitY, 6, 0.8f)); }
        Mesh heel = Sweep([new Sec(V(-0.04f, -0.215f, -0.045f), 0.006f, 0.014f, 2f, Float3.UnitY), new Sec(V(0f, -0.215f, -0.068f), 0.006f, 0.016f, 2f, Float3.UnitY), new Sec(V(0.04f, -0.215f, -0.045f), 0.006f, 0.014f, 2f, Float3.UnitY)], Float3.UnitY, 8, 0.6f);
        Mesh toe = Sweep([new Sec(V(-0.046f, -0.222f, 0.085f), 0.003f, 0.003f), new Sec(V(0f, -0.2f, 0.1f), 0.003f, 0.003f), new Sec(V(0.046f, -0.222f, 0.085f), 0.003f, 0.003f)], Float3.UnitY, 6, 0.8f);
        return (calf, [new(cuff, Tint.Pants), new(shoe, Tint.Shoe), new(sole, Tint.Belt), new(tongue, Tint.Shoe), new(Merge(laces.ToArray()), Tint.Belt), new(heel, Tint.Belt), new(toe, Tint.Belt)]);
    }
}
