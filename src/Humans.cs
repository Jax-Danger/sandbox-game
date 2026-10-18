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
    public enum Tint { Main, Skin, Shoe, Hair, Eye, Belt, Cuff, Pants }
    public sealed record Detail(Mesh Mesh, Tint Tint, Float3 Offset = default);

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
            _ when name.StartsWith("Lower arm") => LowerArm(side),
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
        parts.Add(new(LatheY("collar", [(0.064f, 0.205f), (0.068f, 0.215f), (0.062f, 0.24f), (0.056f, 0.245f)]), Tint.Cuff));
        parts.Add(new(Sweep([new Sec(V(0f, -0.24f, 0.106f), 0.012f, 0.003f, 4f), new Sec(V(0f, -0.02f, 0.124f), 0.012f, 0.003f, 4f), new Sec(V(0f, 0.1f, 0.13f), 0.012f, 0.003f, 4f), new Sec(V(0f, 0.2f, 0.104f), 0.012f, 0.003f, 4f)], Float3.UnitX, 8, 0.4f), Tint.Cuff));
        parts.Add(new(Sweep([new Sec(V(-0.09f, 0.06f, 0.123f), 0.035f, 0.003f, 4f), new Sec(V(-0.09f, 0.11f, 0.12f), 0.035f, 0.003f, 4f)], Float3.UnitX, 8, 0.4f), Tint.Cuff));
        foreach (float sd in new[] { -1f, 1f })
            parts.Add(new(Sweep([new Sec(V(sd * 0.06f, 0.212f, -0.004f), 0.06f, 0.028f), new Sec(V(sd * 0.14f, 0.192f, -0.002f), 0.07f, 0.04f),
                new Sec(V(sd * 0.21f, 0.17f, 0f), 0.056f, 0.047f), new Sec(V(sd * 0.25f, 0.15f, 0f), 0.05f, 0.047f), new Sec(V(sd * 0.272f, 0.125f, 0f), 0.04f, 0.04f), new Sec(V(sd * 0.28f, 0.1f, 0f), 0.026f, 0.026f)],
                Float3.UnitZ, 20, 0.85f), Tint.Main));
        return (body, parts.ToArray());
    }

    private static Sec[] Ring(float y, float rx, float rz, float z)
    {
        var l = new List<Sec>();
        for (int i = 0; i <= 24; i++) { float a = i * MathF.Tau / 24f; l.Add(new Sec(V(MathF.Cos(a) * rx, y, z + MathF.Sin(a) * rz), 0.012f, 0.008f, 2f, Float3.UnitY)); }
        return l.ToArray();
    }

    private static Mesh LatheY(string key, (float R, float Y)[] p) => Lathe("hum_" + key, p, 24);

    private static (Mesh, Detail[]) Head()
    {
        // Egg-shaped skull with a jaw, nose, ears, eyes and a hair cap.
        Mesh skull = Sweep([
            new Sec(V(0f, -0.125f, 0.012f), 0.045f, 0.045f), new Sec(V(0f, -0.11f, 0.018f), 0.072f, 0.075f), new Sec(V(0f, -0.07f, 0.01f), 0.098f, 0.105f),
            new Sec(V(0f, -0.01f, 0f), 0.112f, 0.122f), new Sec(V(0f, 0.05f, -0.006f), 0.114f, 0.126f), new Sec(V(0f, 0.1f, -0.01f), 0.096f, 0.11f),
            new Sec(V(0f, 0.128f, -0.012f), 0.05f, 0.06f)], Float3.UnitX, 26, 0.3f);
        Mesh nose = Sweep([new Sec(V(0f, 0.012f, 0.112f), 0.012f, 0.01f), new Sec(V(0f, -0.02f, 0.13f), 0.016f, 0.014f), new Sec(V(0f, -0.035f, 0.126f), 0.017f, 0.012f)], Float3.UnitX, 12, 0.9f);
        var ears = new List<Detail>();
        foreach (float sx in new[] { -1f, 1f })
            ears.Add(new(Sweep([new Sec(V(sx * 0.11f, 0.02f, -0.01f), 0.012f, 0.025f, 2f, Float3.UnitZ), new Sec(V(sx * 0.122f, 0.0f, -0.012f), 0.012f, 0.028f, 2f, Float3.UnitZ), new Sec(V(sx * 0.118f, -0.028f, -0.008f), 0.008f, 0.016f, 2f, Float3.UnitZ)], Float3.UnitZ, 12, 0.9f), Tint.Skin));
        Mesh eyeL = Sphere(0.0115f), hair = Sweep([
            new Sec(V(0f, 0.0f, -0.035f), 0.12f, 0.122f), new Sec(V(0f, 0.06f, -0.01f), 0.123f, 0.14f), new Sec(V(0f, 0.105f, -0.01f), 0.106f, 0.124f),
            new Sec(V(0f, 0.14f, -0.014f), 0.054f, 0.07f)], Float3.UnitX, 26, 0.3f, flatStart: true);
        var d = new List<Detail> { new(nose, Tint.Skin), new(Sweep([new Sec(V(-0.026f, -0.062f, 0.105f), 0.004f, 0.003f), new Sec(V(0f, -0.066f, 0.112f), 0.004f, 0.003f), new Sec(V(0.026f, -0.062f, 0.105f), 0.004f, 0.003f)], Float3.UnitY, 8, 0.9f), Tint.Eye), new(hair, Tint.Hair), new(eyeL, Tint.Eye, V(-0.04f, 0.018f, 0.104f)), new(eyeL, Tint.Eye, V(0.04f, 0.018f, 0.104f)) };
        foreach (float sx in new[] { -1f, 1f })
            d.Add(new(Sweep([new Sec(V(sx * 0.018f, 0.044f, 0.112f), 0.006f, 0.004f), new Sec(V(sx * 0.042f, 0.05f, 0.108f), 0.007f, 0.0045f), new Sec(V(sx * 0.064f, 0.044f, 0.098f), 0.005f, 0.0035f)], Float3.UnitY, 8, 0.8f), Tint.Hair));
        d.Add(new(Sphere(0.004f), Tint.Skin, V(-0.04f, 0.018f, 0.1155f))); // (eye glints stay dark; keeps cache keys distinct)
        d.AddRange(ears);
        return (skull, d.ToArray());
    }

    private static (Mesh, Detail[]) UpperArm(float side)
    {
        Mesh arm = Sw(2.2f, Float3.UnitX, 0.95f, (0.12f, 0.052f, 0.052f, 0f), (0.08f, 0.057f, 0.056f, 0f), (0.0f, 0.054f, 0.052f, 0f), (-0.11f, 0.049f, 0.049f, 0.004f), (-0.165f, 0.047f, 0.047f, 0.004f));
        Mesh delt = Sweep([new Sec(V(-side * 0.02f, 0.13f, 0f), 0.03f, 0.036f), new Sec(V(-side * 0.006f, 0.105f, 0f), 0.054f, 0.054f), new Sec(V(0f, 0.05f, 0f), 0.057f, 0.055f)], Float3.UnitZ, 20, 0.8f);
        Mesh hem = Sw(2.2f, Float3.UnitX, 0.3f, (-0.02f, 0.06f, 0.058f, 0f), (-0.045f, 0.058f, 0.056f, 0f));
        // Sleeve end rolls softly over the elbow (no hard rim cutting into the forearm when the arm bends).
        Mesh roll = Sw(2.2f, Float3.UnitX, 0.95f, (-0.13f, 0.049f, 0.049f, 0.004f), (-0.16f, 0.052f, 0.052f, 0.004f), (-0.185f, 0.05f, 0.05f, 0.004f), (-0.2f, 0.045f, 0.045f, 0.004f), (-0.21f, 0.04f, 0.04f, 0.003f));
        return (arm, [new(delt, Tint.Main), new(roll, Tint.Cuff)]);
    }

    private static (Mesh, Detail[]) LowerArm(float side)
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
                float a = i / 6f * 1.25f, L = fl[f];
                fs.Add(new Sec(V(fx[f] * (1f + 0.15f * i / 6f), -0.205f - MathF.Sin(a) * L, 0.004f + (1f - MathF.Cos(a)) * L), 0.0085f - i * 0.0004f, 0.0085f - i * 0.0004f, 2.2f));
            }
            fms.Add(Sweep(fs, Float3.UnitX, 10, 0.9f));
        }
        Mesh fing = Merge(fms.ToArray());
        Mesh thumb = Sweep([new Sec(V(-side * 0.032f, -0.15f, 0.012f), 0.012f, 0.011f), new Sec(V(-side * 0.046f, -0.18f, 0.03f), 0.011f, 0.01f), new Sec(V(-side * 0.04f, -0.205f, 0.045f), 0.0095f, 0.009f)], Float3.UnitX, 12, 0.9f);
        return (fore, [new(palm, Tint.Skin), new(fing, Tint.Skin), new(thumb, Tint.Skin)]);
    }

    private static (Mesh, Detail[]) UpperLeg()
        => (Sw(2.3f, Float3.UnitX, 0.6f, (0.29f, 0.07f, 0.07f, 0f), (0.25f, 0.084f, 0.082f, 0f), (0.12f, 0.081f, 0.08f, 0.004f), (-0.05f, 0.068f, 0.068f, 0.004f), (-0.2f, 0.058f, 0.058f, 0f), (-0.23f, 0.056f, 0.056f, 0f)), []);

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
        return (calf, [new(cuff, Tint.Pants), new(shoe, Tint.Shoe), new(sole, Tint.Belt)]);
    }
}
