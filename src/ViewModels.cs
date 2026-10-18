using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;
using static Sandbox.Meshes;

namespace Sandbox;

/// <summary>
/// Built-in first-person view models, made from swept and lathed meshes with smooth normals (no stacked boxes).
/// Hands are generated around the thing they hold: every finger is one smooth swept tube that wraps an elliptical
/// grip, the palm follows the grip surface, the thumb closes from the other side and a trigger finger can be routed
/// along the frame. Forearms are separate bones that "look at" a fixed elbow below the screen (see ViewModelDef.Ik),
/// so a hand that travels during a reload never drags the sleeve into the camera.
/// Every weapon is authored in metres with the right-hand grip centre at the origin (hand_r = weapon origin).
/// A user model "vm_&lt;id&gt;" in the asset manifest replaces all of this (README "View models").
/// </summary>
public static partial class ViewModels
{
    private static readonly Color SleeveC = new(0.20f, 0.25f, 0.32f, 1f);
    private static readonly Color HemC = new(0.15f, 0.18f, 0.23f, 1f);
    private static readonly Color GloveC = new(0.21f, 0.195f, 0.175f, 1f);
    private static readonly Color PadC = new(0.12f, 0.115f, 0.11f, 1f);
    private static readonly Color Black = new(0.075f, 0.078f, 0.085f, 1f);
    private static readonly Color Gunmetal = new(0.17f, 0.175f, 0.19f, 1f);
    private static readonly Color Polymer = new(0.11f, 0.115f, 0.12f, 1f);
    private static readonly Color Steel = new(0.55f, 0.56f, 0.58f, 1f);
    private static readonly Color Dark = new(0.035f, 0.035f, 0.04f, 1f);
    private static readonly Color WoodC = new(0.34f, 0.21f, 0.12f, 1f);
    private static readonly Color Olive = new(0.29f, 0.33f, 0.22f, 1f);
    private static readonly Color Accent = new(0.30f, 0.66f, 1f, 1f);
    private static readonly Color Tritium = new(0.45f, 1f, 0.45f, 1f);
    private static readonly Color RedPaint = new(0.62f, 0.10f, 0.08f, 1f);
    private static readonly Color White = new(0.86f, 0.87f, 0.89f, 1f);

    private static (string, Float3, Float3) B(string bone, float px = 0, float py = 0, float pz = 0, float rx = 0, float ry = 0, float rz = 0)
        => (bone, new Float3(px, py, pz), new Float3(rx, ry, rz));
    private static Float3 V(float x, float y, float z) => new(x, y, z);
    private static Float3 Nz(Float3 v) => Float3.Length(v) > 1e-6f ? Float3.Normalize(v) : Float3.UnitZ;
    private static Float3 Lp(Float3 a, Float3 b, float t) => a + (b - a) * t;
    private static float Rad(float deg) => deg * MathF.PI / 180f;

    /// <summary>Elliptical grip cross-section swept along axis A; angle 0 = D0, 90° = A × D0.</summary>
    private readonly record struct GripFrame(Float3 C, Float3 A, Float3 D0, float Ra, float Rb)
    {
        public Float3 E => Nz(Float3.Cross(Nz(A), Nz(D0)));
        public Float3 P(float deg, float h, float off) =>
            C + Nz(A) * h + Nz(D0) * ((Ra + off) * MathF.Cos(Rad(deg))) + E * ((Rb + off) * MathF.Sin(Rad(deg)));
    }

    /// <summary>
    /// How a hand holds a grip. Fingers start at the knuckle angle and wrap in <see cref="Dir"/> (±1);
    /// the palm runs the other way for <see cref="PalmArc"/> degrees, then on to the wrist.
    /// <see cref="H"/> = finger heights along the grip axis (index … pinky; NaN = index on the trigger path).
    /// </summary>
    private sealed record HandSpec(GripFrame F, float ThK, int Dir, float[] H, float PalmArc, Float3 Wrist,
        float ThumbTh0, float ThumbTh1, float ThumbH0, float ThumbH1, Float3[]? Trigger = null, float Len = 1f, float ThumbLen = 1f);

    private sealed class Kit(ViewModelDef d)
    {
        public void Add(string bone, Mesh m, Color c, Finish f, Float3 at = default, bool glow = false, char shape = 'm')
            => d.Parts.Add(new VmPart(bone, shape, V(1, 1, 1), at, c, default, glow, f, null, m));

        public void Sw(string bone, Color c, Finish f, Float3 side, float round, params Sec[] s) => Add(bone, Sweep(s, side, 20, round), c, f);

        /// <summary>Straight bar a→b, superellipse section (half sizes w × h). Side defaults to +X.</summary>
        public void Bar(string bone, Color c, Finish f, Float3 a, Float3 b, float w, float h, float n = 4.5f, float round = 0.45f, Float3? side = null, bool glow = false)
            => Add(bone, Sweep(new[] { new Sec(a, w, h, n), new Sec(Lp(a, b, 0.5f), w, h, n), new Sec(b, w, h, n) }, side ?? Float3.UnitX, 20, round), c, f, default, glow);

        /// <summary>Tube along a path with a radius per point (circular section).</summary>
        public void Pipe(string bone, Color c, Finish f, float[] r, params Float3[] p)
        {
            var s = new Sec[p.Length];
            for (int i = 0; i < p.Length; i++) s[i] = new Sec(p[i], r[Math.Min(i, r.Length - 1)], r[Math.Min(i, r.Length - 1)]);
            Add(bone, Sweep(s, Float3.UnitX, 14, 0.8f), c, f);
        }

        public void Lz(string bone, Color c, Finish f, Float3 at, params (float R, float Z)[] prof) => Add(bone, LatheZ(prof), c, f, at);

        public void Dot(string bone, Color c, Float3 at, float r) => d.Parts.Add(new VmPart(bone, 's', V(r * 2f, 0, 0), at, c, default, true, Finish.Gloss));

        public void Hand(string bone, HandSpec s)
        {
            GripFrame F = s.F;
            float[] len = [0.086f, 0.092f, 0.086f, 0.070f], rad = [0.0083f, 0.0086f, 0.0082f, 0.0073f];
            float rm = (F.Ra + F.Rb) / 2f;
            for (int k = 0; k < 4; k++)
            {
                float r = rad[k];
                if (float.IsNaN(s.H[k]))
                {
                    if (s.Trigger is { } tp) Finger(bone, tp, r);
                    continue;
                }
                float L = len[k] * s.Len, R = rm + r;
                var pts = new List<Float3>();
                for (int i = 0; i <= 14; i++)
                {
                    float sl = -0.008f + (L + 0.008f) * i / 14f;
                    float tt = Math.Clamp(sl / L, 0f, 1f);
                    // Open (relaxed) hands: fingers fan apart toward the tips and curl progressively more toward the pinky,
                    // so they read as four separate digits instead of a mitten.
                    bool open = F.Ra >= 0.045f;
                    float fan = open ? (s.H[k] - 0.0f) * 0.32f * tt : 0f;
                    float curl = open ? 1f + 0.09f * (s.Dir > 0 ? k : 3 - k) * 0.5f : 1f;
                    float th = s.ThK + s.Dir * sl / R * 180f / MathF.PI * curl;
                    pts.Add(F.P(th, s.H[k] + fan, r * (1.02f - 0.18f * tt)));
                }
                Finger(bone, pts.ToArray(), r, F.A);
            }
            // Palm: follows the grip surface back from the knuckles, then narrows into the wrist.
            {
                float h0 = s.H.Where(x => !float.IsNaN(x)).Min() - 0.012f, h1 = s.H.Where(x => !float.IsNaN(x)).Max() + (s.Trigger is null ? 0.012f : 0.03f);
                float hc = (h0 + h1) / 2f, hw = (h1 - h0) / 2f;
                var secs = new List<Sec>();
                for (int i = 0; i <= 6; i++)
                {
                    float th = s.ThK + s.Dir * 4f - s.Dir * s.PalmArc * i / 6f;
                    secs.Add(new Sec(F.P(th, hc, 0.0115f), hw, 0.0118f + 0.002f * i / 6f, 2.6f, F.A));
                }
                Float3 last = secs[^1].P;
                secs.Add(new Sec(Lp(last, s.Wrist, 0.5f), hw * 0.82f, 0.0155f, 2.4f, F.A));
                secs.Add(new Sec(s.Wrist, 0.026f, 0.019f, 2.2f, F.A));
                Add(bone, Sweep(secs, F.A, 22, 0.5f), GloveC, Finish.Leather);
                // Knuckle guard on the back of the hand.
                var g = new List<Sec>();
                for (int i = 0; i <= 3; i++)
                    g.Add(new Sec(F.P(s.ThK - s.Dir * (6f + 26f * i / 3f), hc + (s.Trigger is null ? 0f : -0.006f), 0.0245f), hw * 0.5f, 0.0036f, 2.6f, F.A));
                Add(bone, Sweep(g, F.A, 18, 0.7f), PadC, Finish.Rubber);
            }
            // Thumb: from the heel of the palm round the other side of the grip.
            {
                var pts = new List<Float3>();
                float palmEnd = s.ThK - s.Dir * s.PalmArc;
                pts.Add(F.P(palmEnd, s.ThumbH0 - 0.006f, 0.004f));
                for (int i = 0; i <= 10; i++)
                {
                    float t = i / 10f;
                    pts.Add(F.P(s.ThumbTh0 + (s.ThumbTh1 - s.ThumbTh0) * t * s.ThumbLen, s.ThumbH0 + (s.ThumbH1 - s.ThumbH0) * t, 0.0112f - 0.002f * t));
                }
                var secs = new Sec[pts.Count];
                for (int i = 0; i < pts.Count; i++)
                {
                    float t = (float)i / (pts.Count - 1);
                    float r = 0.0106f * (1.35f - 0.5f * t + 0.05f * MathF.Exp(-MathF.Pow((t - 0.55f) / 0.08f, 2f)));
                    secs[i] = new Sec(pts[i], r, r * 0.9f, 2.2f, F.A);
                }
                Add(bone, Sweep(secs, F.A, 16, 0.85f), GloveC, Finish.Leather);
            }
        }

        private void Finger(string bone, Float3[] pts, float r, Float3? side = null)
        {
            // Resample by arc length so the knuckle bumps land at the joints.
            float total = 0f;
            for (int i = 1; i < pts.Length; i++) total += Float3.Length(pts[i] - pts[i - 1]);
            var secs = new List<Sec>();
            float acc = 0f;
            for (int i = 0; i < pts.Length; i++)
            {
                if (i > 0) acc += Float3.Length(pts[i] - pts[i - 1]);
                float t = acc / MathF.Max(total, 1e-5f);
                float G(float c, float w) => MathF.Exp(-MathF.Pow((t - c) / w, 2f));
                // Knuckle bumps at the joints with soft creases either side, so each finger reads as segments, not a mitten.
                float bump = 0.13f * G(0.5f, 0.055f) + 0.1f * G(0.76f, 0.045f) - 0.07f * G(0.62f, 0.035f) - 0.06f * G(0.88f, 0.03f);
                float rr = r * 0.9f * (1.04f - 0.26f * t + bump);
                secs.Add(new Sec(pts[i], rr, rr * 0.9f, 2.2f));
            }
            Add(bone, Sweep(secs, side ?? Float3.UnitY, 14, 0.9f), GloveC, Finish.Leather);
            // Rubber knuckle pad over the first joint (tactical-glove detail).
            int j = Math.Clamp((int)(pts.Length * 0.5f), 1, pts.Length - 2);
            Float3 dir = Float3.Normalize(pts[j + 1] - pts[j - 1]);
            Add(bone, Sweep([new Sec(pts[j] - dir * 0.006f, r * 1.02f, r * 1.0f, 2.4f), new Sec(pts[j] + dir * 0.006f, r * 1.02f, r * 1.0f, 2.4f)], side ?? Float3.UnitY, 12, 0.8f), PadC, Finish.Rubber);
        }

        /// <summary>Forearm on its IK bone: glove gauntlet, strap, sleeve hem and a sleeve with soft folds (local +Z = toward the elbow).</summary>
        public void Forearm(string bone)
        {
            Sw(bone, GloveC, Finish.Leather, Float3.UnitX, 0f,
                new Sec(V(0, 0, -0.012f), 0.0265f, 0.0205f, 2.4f), new Sec(V(0, 0, 0.02f), 0.029f, 0.0235f, 2.3f), new Sec(V(0, 0, 0.058f), 0.0325f, 0.028f, 2.2f));
            Sw(bone, PadC, Finish.Rubber, Float3.UnitX, 0.5f, new Sec(V(0, 0, 0.036f), 0.0318f, 0.0268f, 2.3f), new Sec(V(0, 0, 0.05f), 0.0322f, 0.0272f, 2.3f));
            Sw(bone, HemC, Finish.Fabric, Float3.UnitX, 0.5f, new Sec(V(0, 0, 0.055f), 0.0372f, 0.0332f, 2.2f), new Sec(V(0, 0, 0.072f), 0.0378f, 0.0338f, 2.2f));
            var s = new List<Sec>();
            for (int i = 0; i <= 18; i++)
            {
                float z = 0.068f + 0.36f * i / 18f;
                float fold = 0.0022f * MathF.Sin(i * 1.9f) * MathF.Exp(-i / 7f);
                s.Add(new Sec(V(0.0006f * MathF.Sin(i * 2.3f), 0, z), 0.0365f + 0.012f * i / 18f + fold, 0.0325f + 0.010f * i / 18f - fold * 0.5f, 2.2f));
            }
            Add(bone, Sweep(s, Float3.UnitX, 24, 0f, true, true), SleeveC, Finish.Fabric);
        }

        /// <summary>Trigger guard + trigger (side view path in the YZ plane).</summary>
        public void Guard(string bone, Color c, float yTop, float zFront, float yBot, float zBack, float zTrig)
        {
            Pipe(bone, c, Finish.Polymer, [0.0036f],
                V(0, yTop, zFront), V(0, yTop - (yTop - yBot) * 0.55f, zFront + 0.002f), V(0, yBot + 0.004f, zFront - 0.006f),
                V(0, yBot, zFront - 0.02f), V(0, yBot + 0.001f, zBack + 0.012f), V(0, yBot + 0.008f, zBack));
            Sw(bone, Steel, Finish.Metal, Float3.UnitX, 0.8f,
                new Sec(V(0, yTop - 0.002f, zTrig), 0.0028f, 0.0022f, 3f), new Sec(V(0, yTop - 0.011f, zTrig + 0.004f), 0.0028f, 0.0022f, 3f),
                new Sec(V(0, yBot + 0.012f, zTrig + 0.001f), 0.0026f, 0.002f, 3f), new Sec(V(0, yBot + 0.008f, zTrig - 0.004f), 0.0024f, 0.0018f, 3f));
        }

        /// <summary>Pistol-style grip swept along axis A from h0 to h1, with a grippy side panel.</summary>
        public void GripBody(string bone, Float3 a, float h0, float h1, float w, float depth, Color c, Color panel)
        {
            a = Nz(a);
            Sw(bone, c, Finish.Polymer, Float3.UnitX, 0.4f,
                new Sec(a * h0, w * 1.03f, depth * 1.04f, 2.8f), new Sec(a * (h0 * 0.4f + h1 * 0.6f) * 0.5f, w, depth, 2.8f),
                new Sec(a * (h1 * 0.7f), w * 0.97f, depth * 0.96f, 2.8f), new Sec(a * h1, w * 0.92f, depth * 0.92f, 3.2f));
            Sw(bone, panel, Finish.Rubber, Float3.UnitX, 0.6f,
                new Sec(a * (h0 + 0.012f), w + 0.0007f, depth * 0.66f, 3.5f), new Sec(a * (h1 - 0.02f), w + 0.0007f, depth * 0.62f, 3.5f));
        }
    }

    public static ViewModelDef Create(string id)
    {
        var d = new ViewModelDef { Id = id };
        var k = new Kit(d);
        void Bone(string b, string parent, Float3 pos, Float3 rot = default) => d.Bones.Add((b, parent, pos, rot));
        Float3 pistolA = Nz(V(0f, 0.964f, 0.266f));
        // Standard right-hand pistol grip (grip ellipse 14.8 × 24.5 mm, trigger finger along the frame).
        HandSpec RightPistol(float w, float depth, float hMid, Float3[] trig) => new(new GripFrame(Float3.Zero, pistolA, Float3.UnitX, w, depth),
            -22f, -1, [float.NaN, hMid, hMid - 0.0185f, hMid - 0.036f], 88f, V(0.024f, -0.004f, -0.078f), 72f, 200f, 0.034f, 0.05f, trig);
        Float3[] Trig(float y, float z) => [V(0.0225f, y - 0.004f, z - 0.024f), V(0.0222f, y, z - 0.004f), V(0.016f, y + 0.003f, z + 0.009f), V(0.006f, y + 0.002f, z + 0.011f), V(-0.002f, y, z + 0.008f)];
        // Support hand under a horizontal tube/handguard centred at c (radius ra vertical, rb horizontal), parented so c is its origin.
        HandSpec Under(float ra, float rb, float len = 0.78f) => new(new GripFrame(Float3.Zero, Float3.UnitZ, -Float3.UnitY, ra, rb),
            38f, 1, [0.029f, 0.0105f, -0.008f, -0.026f], 82f, V(-0.032f, -0.03f, -0.085f), -58f, -100f, -0.012f, 0.03f, null, len);
        HandSpec Fist(Float3 a, Float3 d0, float r, Float3 wrist, float len = 0.95f) => new(new GripFrame(Float3.Zero, a, d0, r, r),
            -22f, -1, [0.029f, 0.0105f, -0.008f, -0.026f], 86f, wrist, 70f, 195f, 0.03f, 0.042f, null, len);
        HandSpec Relaxed(int side) => new(new GripFrame(V(0f, -0.045f, 0f), V(side, 0f, 0f), -Float3.UnitY, 0.05f, 0.05f),
            side > 0 ? 178f : 182f, side, [-0.0285f, -0.0095f, 0.0095f, 0.0275f], 42f, V(0.004f * side, 0.0f, -0.075f),
            side > 0 ? 150f : 210f, side > 0 ? 222f : 138f, -0.04f, -0.05f, null, 1f);

        Float3 armR = V(0.085f, -0.135f, 0.25f);
        Float3 elbowR = V(0.34f, -0.55f, 0.02f), elbowL = V(-0.36f, -0.52f, 0.06f);
        string leftParent = "arm_l"; Float3 leftPos = Float3.Zero;
        HandSpec? right = null, left = null;
        bool freeLeft = false;
        void Std(Float3 arm, float yaw = -2.5f, float pitch = 0f)
        {
            armR = arm;
            Bone("arm_r", "vm_root", arm, V(pitch, yaw, 0f));
            Bone("hand_r", "arm_r", Float3.Zero);
            Bone("weapon", "hand_r", Float3.Zero);
        }

        switch (id)
        {
            case "pistol":
            {
                Std(V(0.115f, -0.17f, 0.36f), -7f);
                Bone("muzzle", "weapon", V(0f, 0.074f, 0.158f));
                Bone("sight", "weapon", V(0f, 0.0985f, -0.03f));
                Bone("slide", "weapon", Float3.Zero);
                Bone("mag", "weapon", Float3.Zero);
                // Slide with front taper, cocking serrations, ejection port, sights with tritium dots.
                k.Sw("slide", Gunmetal, Finish.Metal, Float3.UnitX, 0.25f,
                    new Sec(V(0, 0.079f, -0.044f), 0.0128f, 0.0142f, 4.5f), new Sec(V(0, 0.079f, 0.05f), 0.0128f, 0.0145f, 4.5f),
                    new Sec(V(0, 0.0795f, 0.135f), 0.0126f, 0.0142f, 4.5f), new Sec(V(0, 0.0805f, 0.152f), 0.0112f, 0.0128f, 3.6f));
                for (int i = 0; i < 7; i++)
                    k.Bar("slide", Black, Finish.Metal, V(0, 0.068f, -0.039f + i * 0.0042f), V(0, 0.090f, -0.039f + i * 0.0042f), 0.01298f, 0.0009f, 4f, 0.3f);
                k.Bar("slide", Dark, Finish.Metal, V(0.0066f, 0.0905f, 0.035f), V(0.0066f, 0.0905f, 0.07f), 0.0052f, 0.0032f, 4f, 0.5f);
                k.Bar("slide", Black, Finish.Metal, V(0, 0.0955f, 0.138f), V(0, 0.0955f, 0.146f), 0.0018f, 0.0032f, 4f, 0.5f);
                k.Dot("slide", Tritium, V(0, 0.0975f, 0.1375f), 0.0013f);
                k.Bar("slide", Black, Finish.Metal, V(0, 0.0952f, -0.036f), V(0, 0.0952f, -0.026f), 0.0115f, 0.0034f, 4f, 0.5f);
                k.Dot("slide", Tritium, V(0.005f, 0.0972f, -0.0365f), 0.0012f);
                k.Dot("slide", Tritium, V(-0.005f, 0.0972f, -0.0365f), 0.0012f);
                k.Lz("weapon", Steel, Finish.Metal, V(0, 0.0745f, 0.149f), (0.0064f, 0f), (0.0064f, 0.0045f), (0.0044f, 0.005f), (0.0042f, 0.003f), (0f, 0.003f));
                // Frame, dust cover, accessory rail, beavertail, slide stop, hammer.
                k.Sw("weapon", Polymer, Finish.Polymer, Float3.UnitX, 0.4f,
                    new Sec(V(0, 0.058f, -0.036f), 0.0118f, 0.0075f, 4f), new Sec(V(0, 0.058f, 0.06f), 0.0121f, 0.0078f, 4f), new Sec(V(0, 0.0585f, 0.142f), 0.0116f, 0.0072f, 4f));
                for (int i = 0; i < 3; i++) k.Bar("weapon", Black, Finish.Polymer, V(0, 0.0505f, 0.098f + i * 0.012f), V(0, 0.0505f, 0.104f + i * 0.012f), 0.0105f, 0.0015f, 4f, 0.4f);
                k.Sw("weapon", Polymer, Finish.Polymer, Float3.UnitX, 0.6f,
                    new Sec(V(0, 0.058f, -0.024f), 0.0112f, 0.0065f, 3f), new Sec(V(0, 0.056f, -0.04f), 0.0105f, 0.005f, 3f), new Sec(V(0, 0.052f, -0.05f), 0.0085f, 0.0035f, 3f));
                k.Bar("weapon", Black, Finish.Metal, V(-0.0122f, 0.0625f, 0.022f), V(-0.0122f, 0.063f, 0.056f), 0.0016f, 0.0028f, 3f, 0.6f);
                k.Sw("weapon", Black, Finish.Metal, Float3.UnitX, 0.7f, new Sec(V(0, 0.072f, -0.043f), 0.004f, 0.003f, 3f), new Sec(V(0, 0.08f, -0.05f), 0.0038f, 0.0028f, 3f), new Sec(V(0, 0.083f, -0.054f), 0.0042f, 0.003f, 3f));
                k.GripBody("weapon", pistolA, -0.058f, 0.062f, 0.0148f, 0.0245f, Polymer, Black);
                k.Guard("weapon", Polymer, 0.0505f, 0.073f, 0.0185f, 0.02f, 0.041f);
                k.Sw("mag", Black, Finish.Metal, Float3.UnitX, 0.5f, new Sec(pistolA * -0.068f + V(0, 0, -0.002f), 0.0158f, 0.027f, 3.2f), new Sec(pistolA * -0.058f, 0.0155f, 0.0265f, 3.2f));
                right = RightPistol(0.0148f, 0.0245f, 0.012f, Trig(0.034f, 0.045f));
                leftParent = "weapon";
                left = new HandSpec(new GripFrame(Float3.Zero, pistolA, Float3.UnitX, 0.0148f + 0.0175f, 0.0245f + 0.0175f),
                    196f, 1, [0.008f, -0.0105f, -0.029f, -0.046f], 74f, V(-0.04f, -0.036f, -0.06f), 152f, 212f, 0.018f, 0.03f, null, 0.92f, 1f);
                d.RecoilKick = 1.0f; d.Weight = 0.65f; d.CanAim = true; d.AimZoom = 1.12f;
                break;
            }
            case "smg":
            {
                Float3 ga = Nz(V(0f, 0.97f, 0.24f));
                Std(V(0.115f, -0.165f, 0.35f), -6f);
                Bone("muzzle", "weapon", V(0f, 0.072f, 0.395f));
                Bone("sight", "weapon", V(0f, 0.1235f, 0.04f));
                Bone("mag", "weapon", Float3.Zero);
                Bone("charge", "weapon", Float3.Zero);
                // Upper receiver, lower, handguard with vents, barrel and flash hider.
                k.Sw("weapon", Gunmetal, Finish.Metal, Float3.UnitX, 0.3f,
                    new Sec(V(0, 0.074f, -0.075f), 0.0175f, 0.0205f, 5f), new Sec(V(0, 0.075f, 0.06f), 0.0185f, 0.021f, 5f), new Sec(V(0, 0.074f, 0.185f), 0.018f, 0.0205f, 5f));
                k.Sw("weapon", Polymer, Finish.Polymer, Float3.UnitX, 0.35f,
                    new Sec(V(0, 0.048f, -0.06f), 0.0168f, 0.0085f, 4.5f), new Sec(V(0, 0.047f, 0.08f), 0.0172f, 0.009f, 4.5f), new Sec(V(0, 0.048f, 0.17f), 0.0165f, 0.0085f, 4.5f));
                k.Sw("weapon", Polymer, Finish.Polymer, Float3.UnitX, 0.4f,
                    new Sec(V(0, 0.068f, 0.17f), 0.0192f, 0.0228f, 4f), new Sec(V(0, 0.067f, 0.24f), 0.0186f, 0.0222f, 4f), new Sec(V(0, 0.067f, 0.3f), 0.0172f, 0.0205f, 3.4f));
                for (int i = 0; i < 4; i++) k.Bar("weapon", Dark, Finish.Polymer, V(0, 0.074f, 0.205f + i * 0.022f), V(0, 0.074f, 0.217f + i * 0.022f), 0.0194f, 0.004f, 4f, 0.6f);
                k.Lz("weapon", Steel, Finish.Metal, V(0, 0.072f, 0.29f), (0.0078f, 0f), (0.0078f, 0.065f), (0f, 0.065f));
                k.Lz("weapon", Black, Finish.Metal, V(0, 0.072f, 0.345f), (0.0115f, 0f), (0.0118f, 0.04f), (0.0095f, 0.046f), (0.0055f, 0.046f), (0.005f, 0.035f), (0f, 0.035f));
                // Top rail, red dot sight.
                k.Bar("weapon", Black, Finish.Metal, V(0, 0.0985f, -0.06f), V(0, 0.0985f, 0.17f), 0.0088f, 0.0035f, 5f, 0.3f);
                for (int i = 0; i < 16; i++) k.Bar("weapon", Black, Finish.Metal, V(0, 0.1018f, -0.055f + i * 0.0145f), V(0, 0.1018f, -0.049f + i * 0.0145f), 0.0092f, 0.0014f, 5f, 0.3f);
                k.Bar("weapon", Black, Finish.Metal, V(0, 0.106f, 0.02f), V(0, 0.106f, 0.065f), 0.0085f, 0.0045f, 4f, 0.4f);
                k.Lz("weapon", Black, Finish.Metal, V(0, 0.1235f, 0.022f), (0.0158f, 0f), (0.0162f, 0.004f), (0.0162f, 0.044f), (0.0156f, 0.048f), (0.0128f, 0.048f), (0.0128f, 0.002f), (0f, 0.002f));
                k.Add("weapon", LatheZ([(0f, 0f), (0.0129f, 0f), (0.0129f, 0.001f), (0f, 0.001f)]), new Color(0.25f, 0.42f, 0.55f, 1f), Finish.Gloss, V(0, 0.1235f, 0.066f));
                k.Dot("weapon", new Color(1f, 0.18f, 0.12f, 1f), V(0, 0.1235f, 0.064f), 0.0012f);
                // Magazine (forward of the grip, slightly curved), magwell, grip, guard, stock.
                k.Sw("mag", Black, Finish.Metal, Float3.UnitX, 0.4f,
                    new Sec(V(0, 0.04f, 0.115f), 0.0118f, 0.019f, 4f), new Sec(V(0, -0.03f, 0.125f), 0.0118f, 0.0195f, 4f), new Sec(V(0, -0.09f, 0.143f), 0.0118f, 0.02f, 4f));
                k.Sw("mag", Polymer, Finish.Polymer, Float3.UnitX, 0.5f, new Sec(V(0, -0.092f, 0.144f), 0.0135f, 0.022f, 4f), new Sec(V(0, -0.1f, 0.146f), 0.0135f, 0.022f, 4f));
                k.Bar("weapon", Polymer, Finish.Polymer, V(0, 0.033f, 0.095f), V(0, 0.033f, 0.148f), 0.0158f, 0.011f, 4f, 0.4f);
                k.GripBody("weapon", ga, -0.056f, 0.052f, 0.0155f, 0.0235f, Polymer, Black);
                k.Guard("weapon", Polymer, 0.04f, 0.075f, 0.0105f, 0.017f, 0.038f);
                k.Sw("weapon", Polymer, Finish.Polymer, Float3.UnitX, 0.5f, new Sec(V(0, 0.07f, -0.07f), 0.0168f, 0.024f, 4f), new Sec(V(0, 0.069f, -0.084f), 0.0155f, 0.022f, 3.4f));
                // MP5-style cocking tube slot + folded handle on the upper-left, forward (keeps the reach away from the camera).
                k.Bar("weapon", Black, Finish.Metal, V(-0.0182f, 0.084f, 0.13f), V(-0.0182f, 0.084f, 0.19f), 0.0016f, 0.0028f, 4f, 0.5f);
                k.Sw("charge", Black, Finish.Metal, Float3.UnitZ, 0.6f, new Sec(V(-0.018f, 0.084f, 0.182f), 0.003f, 0.003f), new Sec(V(-0.032f, 0.086f, 0.18f), 0.0045f, 0.004f), new Sec(V(-0.04f, 0.087f, 0.172f), 0.005f, 0.0045f));
                k.Bar("weapon", Steel, Finish.Metal, V(-0.0175f, 0.055f, -0.02f), V(-0.0175f, 0.058f, -0.006f), 0.0015f, 0.0032f, 3f, 0.6f);
                right = RightPistol(0.0155f, 0.0235f, 0.004f, Trig(0.026f, 0.042f)) with { F = new GripFrame(Float3.Zero, ga, Float3.UnitX, 0.0155f, 0.0235f) };
                leftParent = "weapon"; leftPos = V(0f, 0.067f, 0.245f);
                left = Under(0.0225f, 0.0189f);
                d.RecoilKick = 0.45f; d.Weight = 1.0f; d.CanAim = true; d.AimZoom = 1.2f;
                break;
            }
            case "shotgun":
            {
                Float3 ga = Nz(V(0f, 0.77f, 0.64f));
                Std(V(0.115f, -0.165f, 0.37f), -6f);
                Bone("muzzle", "weapon", V(0f, 0.082f, 0.625f));
                Bone("sight", "weapon", V(0f, 0.1035f, 0.1f));
                Bone("pump", "weapon", Float3.Zero);
                // Receiver: softly rounded slab (was a near-box), tapering into the barrel and rounding off at the back.
                k.Sw("weapon", Gunmetal, Finish.Metal, Float3.UnitX, 0.85f,
                    new Sec(V(0, 0.069f, 0.008f), 0.015f, 0.02f, 3f), new Sec(V(0, 0.07f, 0.024f), 0.0185f, 0.0245f, 3.2f), new Sec(V(0, 0.072f, 0.12f), 0.019f, 0.026f, 3.2f),
                    new Sec(V(0, 0.073f, 0.196f), 0.0185f, 0.0255f, 3.2f), new Sec(V(0, 0.074f, 0.212f), 0.015f, 0.02f, 3f));
                k.Bar("weapon", Dark, Finish.Metal, V(0.014f, 0.08f, 0.09f), V(0.014f, 0.08f, 0.15f), 0.0055f, 0.009f, 4f, 0.5f);
                k.Bar("weapon", Black, Finish.Metal, V(0, 0.097f, 0.03f), V(0, 0.097f, 0.2f), 0.0075f, 0.0035f, 4f, 0.4f);
                k.Lz("weapon", Gunmetal, Finish.Metal, V(0, 0.082f, 0.2f), (0.0108f, 0f), (0.0105f, 0.42f), (0.0108f, 0.425f), (0.0085f, 0.428f), (0.0075f, 0.41f), (0f, 0.41f));
                k.Bar("weapon", Black, Finish.Metal, V(0, 0.0915f, 0.2f), V(0, 0.0915f, 0.6f), 0.0032f, 0.0018f, 4f, 0.5f);
                k.Dot("weapon", new Color(1f, 0.85f, 0.45f, 1f), V(0, 0.0955f, 0.598f), 0.0022f);
                k.Lz("weapon", Black, Finish.Metal, V(0, 0.05f, 0.2f), (0.0095f, 0f), (0.0095f, 0.34f), (0.0105f, 0.342f), (0.0105f, 0.36f), (0.006f, 0.366f), (0f, 0.366f));
                k.Sw("weapon", Black, Finish.Metal, Float3.UnitX, 0.4f, new Sec(V(0, 0.066f, 0.53f), 0.0125f, 0.028f, 3f), new Sec(V(0, 0.066f, 0.545f), 0.0125f, 0.028f, 3f));
                // Ribbed wooden pump.
                var prof = new List<(float, float)> { (0.0098f, 0f), (0.0195f, 0.004f) };
                for (int i = 0; i < 9; i++) { float z = 0.012f + i * 0.0125f; prof.Add((0.0222f, z)); prof.Add((0.0222f, z + 0.006f)); prof.Add((0.0204f, z + 0.0085f)); prof.Add((0.0204f, z + 0.011f)); }
                prof.Add((0.0215f, 0.128f)); prof.Add((0.0195f, 0.135f)); prof.Add((0.0098f, 0.137f));
                k.Lz("pump", WoodC, Finish.Wood, V(0, 0.05f, 0.24f), prof.ToArray());
                // Stock: one sweep from the receiver through the wrist (the grip) to the butt, with a recoil pad.
                k.Sw("weapon", WoodC, Finish.Wood, Float3.UnitX, 0f,
                    new Sec(V(0, 0.058f, 0.03f), 0.0175f, 0.016f, 3f), new Sec(V(0, 0.034f, 0.02f), 0.0165f, 0.02f, 2.8f),
                    new Sec(V(0, 0f, 0f), 0.0162f, 0.022f, 2.6f), new Sec(V(0, -0.03f, -0.02f), 0.0168f, 0.024f, 2.6f),
                    new Sec(V(0, -0.06f, -0.035f), 0.0175f, 0.026f, 2.8f)); // short "breacher" pistol-grip stock: nothing reaches the camera
                k.Sw("weapon", Black, Finish.Polymer, Float3.UnitX, 0.5f, new Sec(V(0, -0.062f, -0.036f), 0.0178f, 0.0265f, 2.8f), new Sec(V(0, -0.07f, -0.04f), 0.0172f, 0.025f, 2.8f));
                                k.Guard("weapon", Black, 0.047f, 0.078f, 0.024f, 0.022f, 0.052f);
                right = new HandSpec(new GripFrame(Float3.Zero, ga, Float3.UnitX, 0.0163f, 0.022f), -22f, -1,
                    [float.NaN, 0.011f, -0.0075f, -0.026f], 86f, V(0.022f, -0.035f, -0.072f), 72f, 196f, 0.03f, 0.044f, Trig(0.036f, 0.054f));
                leftParent = "pump"; leftPos = V(0f, 0.05f, 0.305f);
                left = Under(0.0222f, 0.0222f);
                d.RecoilKick = 2.2f; d.Weight = 1.35f; d.CanAim = true; d.AimZoom = 1.15f; d.AimShift = V(-0.05f, 0.04f, 0.04f); // shoulder raise: bead sights behind a long stock read badly when centred
                break;
            }
            case "rpg":
            {
                Std(V(0.19f, -0.285f, 0.4f), 2.5f, 5f);
                Bone("muzzle", "weapon", V(0f, 0.115f, 0.52f));
                Bone("sight", "weapon", V(-0.09f, 0.19f, -0.075f));
                Bone("rocket", "weapon", Float3.Zero);
                k.Lz("weapon", Olive, Finish.Paint, V(0, 0.115f, -0.36f),
                    (0.0385f, 0.36f), (0.0395f, 0.364f), (0.0372f, 0.374f), (0.0365f, 0.384f), (0.0365f, 0.4f), (0.0365f, 0.72f), (0.0395f, 0.73f), (0.0395f, 0.85f), (0.0345f, 0.86f), (0.031f, 0.86f), (0.031f, 0.84f), (0f, 0.84f));
                // Rear: rolled lip into a sooty recessed bore instead of a flat cap.
                k.Lz("weapon", Olive, Finish.Paint, V(0, 0.115f, 0.0f), (0.0292f, 0.03f), (0.03f, 0.008f), (0.034f, 0.0f), (0.0385f, 0.002f), (0.0385f, 0.015f));
                k.Lz("weapon", new Color(0.04f, 0.04f, 0.035f, 1f), Finish.Rubber, V(0, 0.115f, 0.0f), (0f, 0.06f), (0.0293f, 0.06f), (0.0293f, 0.028f));
                k.Lz("weapon", WoodC, Finish.Wood, V(0, 0.115f, 0.0f), (0.0368f, 0f), (0.0418f, 0.008f), (0.0425f, 0.2f), (0.0372f, 0.208f));
                for (int i = 0; i < 3; i++) k.Lz("weapon", Black, Finish.Metal, V(0, 0.115f, 0.03f + i * 0.08f), (0.0428f, 0f), (0.0432f, 0.003f), (0.0432f, 0.009f), (0.0428f, 0.012f));
                // Warhead: classic double cone with a nose fuse, on the rocket bone.
                k.Lz("rocket", Olive, Finish.Paint, V(0, 0.115f, 0.49f),
                    (0f, 0f), (0.022f, 0f), (0.03f, 0.03f), (0.046f, 0.08f), (0.05f, 0.13f), (0.047f, 0.17f), (0.032f, 0.24f), (0.014f, 0.3f), (0.008f, 0.32f), (0f, 0.325f));
                k.Lz("rocket", Steel, Finish.Metal, V(0, 0.115f, 0.81f), (0.008f, 0f), (0.0075f, 0.02f), (0.004f, 0.03f), (0f, 0.032f));
                // Pistol grip and receiver block, front grip.
                k.Bar("weapon", Gunmetal, Finish.Metal, V(0, 0.07f, -0.045f), V(0, 0.07f, 0.06f), 0.014f, 0.0115f, 4f, 0.4f);
                k.GripBody("weapon", pistolA, -0.058f, 0.06f, 0.0148f, 0.0245f, WoodC, new Color(0.33f, 0.19f, 0.1f, 1f));
                k.Guard("weapon", Black, 0.0585f, 0.075f, 0.025f, 0.02f, 0.043f);
                k.Bar("weapon", Gunmetal, Finish.Metal, V(0, 0.075f, 0.17f), V(0, 0.075f, 0.24f), 0.012f, 0.008f, 4f, 0.4f);
                k.GripBody("weapon", Nz(V(0f, 1f, 0.12f)), -0.05f, 0.06f, 0.0142f, 0.019f, WoodC, new Color(0.33f, 0.19f, 0.1f, 1f));
                // Side-mounted optic on the left so aiming keeps the tube out of the centre of the view.
                k.Sw("weapon", Black, Finish.Metal, Float3.UnitX, 0.5f, new Sec(V(-0.034f, 0.13f, -0.02f), 0.016f, 0.0075f, 4f), new Sec(V(-0.08f, 0.18f, -0.02f), 0.024f, 0.007f, 4f));
                k.Lz("weapon", new Color(0.16f, 0.18f, 0.13f, 1f), Finish.Paint, V(-0.09f, 0.19f, -0.065f),
                    (0.0128f, 0f), (0.0142f, 0.004f), (0.0145f, 0.03f), (0.0158f, 0.034f), (0.0162f, 0.07f), (0.0158f, 0.1f), (0.0148f, 0.105f), (0.0145f, 0.115f), (0.0122f, 0.116f));
                k.Lz("weapon", Black, Finish.Metal, V(-0.09f, 0.19f, -0.035f), (0.0161f, 0f), (0.0166f, 0.002f), (0.0166f, 0.008f), (0.0161f, 0.01f));
                k.Lz("weapon", Black, Finish.Metal, V(-0.09f, 0.204f, -0.03f), (0f, 0.03f), (0.004f, 0.03f), (0.0045f, 0.026f), (0.0045f, 0f));
                k.Lz("weapon", Black, Finish.Rubber, V(-0.09f, 0.19f, -0.09f), (0.0125f, 0.006f), (0.015f, 0f), (0.0165f, 0.004f), (0.0142f, 0.035f));
                k.Lz("weapon", Black, Finish.Metal, V(-0.09f, 0.19f, 0.05f), (0.0145f, 0f), (0.0155f, 0.006f), (0.0125f, 0.008f), (0.0122f, 0.0f));
                // Eyepiece glass (dark blue-green coated lens) and a chunkier eyecup so the optic reads as a scope end-on, not a ring.
                k.Lz("weapon", new Color(0.05f, 0.09f, 0.11f, 1f), Finish.Gloss, V(-0.09f, 0.19f, -0.062f), (0f, 0.003f), (0.006f, 0.0025f), (0.0128f, 0f));
                k.Lz("weapon", Black, Finish.Rubber, V(-0.09f, 0.19f, -0.1f), (0.0135f, 0.03f), (0.0168f, 0.026f), (0.019f, 0.012f), (0.0198f, 0.0f), (0.0172f, 0.0f), (0.0142f, 0.03f));
                // Reticle at the objective: thin cross with a chevron, plus a faint lens ring, so ADS looks through the optic.
                k.Bar("weapon", Black, Finish.Metal, V(-0.1025f, 0.19f, 0.052f), V(-0.0775f, 0.19f, 0.052f), 0.0006f, 0.0006f, 4f, 0f);
                k.Bar("weapon", Black, Finish.Metal, V(-0.09f, 0.1775f, 0.052f), V(-0.09f, 0.1885f, 0.052f), 0.0006f, 0.0006f, 4f, 0f);
                k.Bar("weapon", Black, Finish.Metal, V(-0.0935f, 0.1865f, 0.052f), V(-0.09f, 0.19f, 0.052f), 0.0005f, 0.0005f, 4f, 0f);
                k.Bar("weapon", Black, Finish.Metal, V(-0.0865f, 0.1865f, 0.052f), V(-0.09f, 0.19f, 0.052f), 0.0005f, 0.0005f, 4f, 0f);
                k.Lz("weapon", new Color(0.55f, 0.12f, 0.05f, 1f), Finish.Gloss, V(-0.09f, 0.19f, 0.051f), (0.0122f, 0f), (0.0118f, 0.0005f), (0.0118f, 0.002f));
                right = RightPistol(0.0148f, 0.0245f, 0.012f, Trig(0.034f, 0.047f));
                leftParent = "weapon"; leftPos = V(0f, 0.0f, 0.206f);
                left = new HandSpec(new GripFrame(Float3.Zero, Nz(V(0f, 1f, 0.12f)), Float3.UnitX, 0.0142f, 0.019f),
                    202f, 1, [0.03f, 0.0115f, -0.007f, -0.025f], 86f, V(-0.026f, -0.02f, -0.075f), 112f, -18f, 0.035f, 0.045f);
                d.RecoilKick = 3f; d.Weight = 1.9f; d.CanAim = true; d.AimZoom = 1.25f; d.AimShift = V(-0.045f, 0.045f, 0.01f);
                d.SprintPose = new BonePose(V(0.03f, -0.02f, -0.05f), V(8f, -16f, 8f));
                break;
            }
            case "physgun":
            {
                Std(V(0.125f, -0.19f, 0.34f), -4f);
                Bone("muzzle", "weapon", V(0f, 0.088f, 0.37f));
                k.Lz("weapon", new Color(0.32f, 0.34f, 0.37f, 1f), Finish.Metal, V(0, 0.088f, -0.05f),
                    (0f, 0f), (0.009f, 0.001f), (0.017f, 0.008f), (0.023f, 0.025f), (0.0262f, 0.06f), (0.0262f, 0.15f), (0.024f, 0.21f), (0.0215f, 0.24f), (0.0205f, 0.26f), (0f, 0.26f));
                for (int i = 0; i < 4; i++) k.Lz("weapon", Black, Finish.Metal, V(0, 0.088f, -0.02f + i * 0.04f), (0.0266f, 0f), (0.0284f, 0.003f), (0.0284f, 0.013f), (0.0266f, 0.016f));
                k.Lz("weapon", Steel, Finish.Metal, V(0, 0.088f, 0.2f), (0.023f, 0f), (0.029f, 0.008f), (0.0305f, 0.04f), (0.0275f, 0.05f), (0.017f, 0.052f), (0.0155f, 0.04f), (0f, 0.04f));
                k.Add("weapon", Sphere(0.0145f), Accent, Finish.Gloss, V(0, 0.088f, 0.25f), true, 'L');
                k.Sw("weapon", Accent, Finish.Gloss, Float3.UnitX, 0.8f, new Sec(V(0, 0.1145f, -0.04f), 0.004f, 0.0025f, 4f), new Sec(V(0, 0.1152f, 0.08f), 0.004f, 0.0025f, 4f), new Sec(V(0, 0.1125f, 0.17f), 0.005f, 0.003f, 4f));
                d.Parts[^1] = d.Parts[^1] with { Emissive = true };
                for (int i = 0; i < 3; i++)
                {
                    float a = Rad(i * 120f + 60f);
                    Float3 rd = V(MathF.Sin(a), MathF.Cos(a), 0f);
                    Float3 b0 = V(0, 0.088f, 0.225f) + rd * 0.0275f;
                    k.Pipe("weapon", Gunmetal, Finish.Metal, [0.0052f, 0.0048f, 0.0038f, 0.0028f], b0, b0 + rd * 0.01f + V(0, 0, 0.04f), b0 + rd * 0.013f + V(0, 0, 0.08f), b0 + rd * 0.006f + V(0, 0, 0.115f));
                }
                foreach (int sx in new[] { -1, 1 })
                    k.Pipe("weapon", Black, Finish.Rubber, [0.0036f], V(sx * 0.011f, 0.076f, -0.03f), V(sx * 0.0285f, 0.071f, 0.02f), V(sx * 0.0292f, 0.074f, 0.12f), V(sx * 0.024f, 0.078f, 0.2f));
                k.GripBody("weapon", pistolA, -0.058f, 0.062f, 0.016f, 0.025f, Polymer, Black);
                k.Guard("weapon", Polymer, 0.052f, 0.072f, 0.0185f, 0.02f, 0.041f);
                right = RightPistol(0.016f, 0.025f, 0.012f, Trig(0.034f, 0.045f));
                leftParent = "weapon"; leftPos = V(0f, 0.088f, 0.13f);
                left = Under(0.027f, 0.027f, 0.62f);
                d.Weight = 1.5f; d.RecoilKick = 0.3f;
                break;
            }
            case "toolgun":
            {
                Std(V(0.12f, -0.175f, 0.36f), -4f);
                Bone("muzzle", "weapon", V(0f, 0.082f, 0.215f));
                // Body: sculpted shell — fuller over the grip, humped top, tapering into the emitter collar.
                k.Sw("weapon", White, Finish.Gloss, Float3.UnitX, 0.45f,
                    new Sec(V(0, 0.079f, -0.078f), 0.021f, 0.024f, 3.6f), new Sec(V(0, 0.081f, -0.04f), 0.0245f, 0.0275f, 3.8f), new Sec(V(0, 0.083f, 0.03f), 0.0255f, 0.029f, 3.8f),
                    new Sec(V(0, 0.082f, 0.1f), 0.0235f, 0.026f, 3.6f), new Sec(V(0, 0.081f, 0.15f), 0.019f, 0.021f, 3.2f), new Sec(V(0, 0.082f, 0.162f), 0.0165f, 0.0175f, 3f));
                // Panel seam ring, side vent slots, top fin rail.
                k.Lz("weapon", Black, Finish.Polymer, V(0, 0.082f, 0.098f), (0.0262f, 0f), (0.0266f, 0.0015f), (0.0262f, 0.003f));
                foreach (float sx in new[] { -1f, 1f })
                    for (int vi = 0; vi < 3; vi++)
                        k.Bar("weapon", Black, Finish.Polymer, V(sx * 0.0255f, 0.088f - vi * 0.0075f, 0.035f), V(sx * 0.0255f, 0.088f - vi * 0.0075f, 0.075f), 0.0012f, 0.0018f, 4f, 0.6f);
                k.Bar("weapon", new Color(0.82f, 0.84f, 0.87f, 1f), Finish.Gloss, V(0, 0.112f, -0.02f), V(0, 0.111f, 0.09f), 0.004f, 0.0045f, 3f, 0.6f);
                // Emitter coils glowing in the accent colour.
                for (int ci = 0; ci < 3; ci++)
                    k.Lz("weapon", Accent, Finish.Gloss, V(0, 0.082f, 0.168f + ci * 0.012f), (0.0128f, 0f), (0.0138f, 0.002f), (0.0138f, 0.005f), (0.0128f, 0.007f));
                k.Sw("weapon", Black, Finish.Polymer, Float3.UnitX, 0.4f, new Sec(V(0, 0.058f, -0.065f), 0.0238f, 0.0065f, 5f), new Sec(V(0, 0.057f, 0.15f), 0.0224f, 0.0065f, 5f));
                k.Bar("weapon", Accent, Finish.Gloss, V(0.0002f, 0.093f, -0.06f), V(0.0002f, 0.093f, 0.14f), 0.0254f, 0.0012f, 5f, 0.3f);
                k.Lz("weapon", Black, Finish.Metal, V(0, 0.082f, 0.155f), (0.015f, 0f), (0.0125f, 0.012f), (0.0105f, 0.05f), (0.0125f, 0.054f), (0.0125f, 0.062f), (0.006f, 0.064f), (0f, 0.062f));
                k.Lz("weapon", Accent, Finish.Gloss, V(0, 0.082f, 0.21f), (0.0128f, 0f), (0.0128f, 0.004f), (0.011f, 0.005f));
                // Rear screen in a dark bezel, angled toward the eye.
                k.Bar("weapon", Black, Finish.Polymer, V(0, 0.103f, -0.075f), V(0, 0.08f, -0.08f), 0.0205f, 0.0035f, 5f, 0.4f);
                k.Bar("weapon", new Color(0.35f, 0.8f, 1f, 1f), Finish.Gloss, V(0, 0.101f, -0.079f), V(0, 0.083f, -0.083f), 0.017f, 0.0008f, 6f, 0.2f, null, true);
                k.GripBody("weapon", pistolA, -0.058f, 0.062f, 0.015f, 0.024f, Black, Polymer);
                k.Guard("weapon", Black, 0.052f, 0.072f, 0.0185f, 0.02f, 0.041f);
                right = RightPistol(0.015f, 0.024f, 0.012f, Trig(0.034f, 0.045f));
                freeLeft = false;
                d.Weight = 0.8f;
                break;
            }
            case "grenade":
            {
                Std(V(0.14f, -0.17f, 0.34f), -8f);
                Bone("muzzle", "weapon", V(0f, 0.04f, 0.05f));
                Bone("pin", "weapon", Float3.Zero);
                k.Add("weapon", Lathe("vm_frag", [(0f, -0.042f), (0.014f, -0.04f), (0.025f, -0.032f), (0.0305f, -0.017f), (0.032f, 0f), (0.031f, 0.017f), (0.026f, 0.031f), (0.018f, 0.04f), (0.011f, 0.044f)], 32), Olive, Finish.Paint);
                for (int i = 0; i < 3; i++) { float y = -0.018f + i * 0.018f, r = MathF.Sqrt(MathF.Max(0f, 0.032f * 0.032f - y * y * 0.9f)) + 0.0004f; k.Add("weapon", Lathe($"vm_fragring{i}", [(r, y - 0.0012f), (r + 0.0008f, y), (r, y + 0.0012f)], 32), new Color(0.21f, 0.24f, 0.16f, 1f), Finish.Paint); }
                k.Add("weapon", Lathe("vm_fuse", [(0.0105f, 0.04f), (0.0105f, 0.05f), (0.0085f, 0.06f), (0.006f, 0.063f), (0f, 0.063f)], 24), Steel, Finish.Metal);
                k.Sw("weapon", Steel, Finish.Metal, Float3.UnitZ, 0.6f,
                    new Sec(V(0.004f, 0.062f, 0), 0.0065f, 0.0012f, 4f), new Sec(V(0.02f, 0.058f, 0), 0.0065f, 0.0012f, 4f), new Sec(V(0.031f, 0.04f, 0), 0.0068f, 0.0012f, 4f),
                    new Sec(V(0.0345f, 0.012f, 0), 0.0068f, 0.0012f, 4f), new Sec(V(0.0335f, -0.018f, 0), 0.006f, 0.0012f, 4f));
                var ring = new List<Float3>();
                for (int i = 0; i <= 24; i++) { float a = i * MathF.Tau / 24f; ring.Add(V(-0.026f, 0.058f + MathF.Cos(a) * 0.011f, MathF.Sin(a) * 0.011f)); }
                k.Pipe("pin", Steel, Finish.Metal, [0.0013f], ring.ToArray());
                k.Pipe("pin", Steel, Finish.Metal, [0.0014f], V(-0.016f, 0.058f, 0f), V(0.012f, 0.058f, 0f));
                right = Fist(Float3.UnitY, Float3.UnitX, 0.032f, V(0.03f, -0.03f, -0.07f), 0.85f);
                freeLeft = true;
                d.Weight = 0.5f;
                break;
            }
            case "crowbar":
            {
                Float3 s = Nz(V(-0.25f, 0.55f, 0.8f));
                Float3 back = Nz(V(0f, 0.25f, -1f) - s * Float3.Dot(V(0f, 0.25f, -1f), s));
                Std(V(0.16f, -0.2f, 0.34f), -6f);
                Bone("muzzle", "weapon", s * 0.45f);
                var shaft = new List<Sec>();
                shaft.Add(new Sec(-s * 0.075f, 0.0112f, 0.0112f, 2.4f, back));
                shaft.Add(new Sec(s * 0.44f, 0.0112f, 0.0112f, 2.4f, back));
                shaft.Add(new Sec(s * 0.475f + back * 0.004f, 0.0112f, 0.011f, 2.4f, back));
                shaft.Add(new Sec(s * 0.497f + back * 0.024f, 0.0108f, 0.0098f, 2.6f, back));
                shaft.Add(new Sec(s * 0.494f + back * 0.052f, 0.0103f, 0.008f, 3f, back));
                shaft.Add(new Sec(s * 0.472f + back * 0.076f, 0.0098f, 0.0055f, 3.4f, back));
                shaft.Add(new Sec(s * 0.446f + back * 0.084f, 0.0085f, 0.0022f, 4f, back));
                k.Add("weapon", Sweep(shaft, back, 16, 0.4f), RedPaint, Finish.Paint);
                k.Sw("weapon", Steel, Finish.Metal, back, 0.3f,
                    new Sec(-s * 0.074f, 0.0112f, 0.0112f, 2.4f), new Sec(-s * 0.1f - back * 0.006f, 0.0115f, 0.007f, 3f), new Sec(-s * 0.125f - back * 0.018f, 0.012f, 0.0022f, 4f));
                var tape = new List<Sec>();
                for (int i = 0; i <= 24; i++) { float h = -0.07f + 0.15f * i / 24f, r = 0.0148f + 0.0006f * MathF.Sin(i * 2.4f); tape.Add(new Sec(s * h, r, r, 2f)); }
                k.Add("weapon", Sweep(tape, back, 18, 0.35f), new Color(0.07f, 0.07f, 0.08f, 1f), Finish.Rubber);
                Float3 d0 = Nz(Float3.UnitX - s * Float3.Dot(Float3.UnitX, s));
                right = Fist(s, d0, 0.0152f, V(0.03f, -0.045f, -0.06f));
                freeLeft = true;
                d.Weight = 1.1f;
                break;
            }
            default: // "hands" and anything unknown
            {
                armR = V(0.15f, -0.15f, 0.31f);
                Bone("arm_r", "vm_root", armR, V(-14f, -10f, -10f));
                Bone("hand_r", "arm_r", Float3.Zero);
                Bone("weapon", "hand_r", Float3.Zero);
                Bone("muzzle", "weapon", V(0f, 0f, 0.1f));
                right = Relaxed(1);
                freeLeft = true;
                break;
            }
        }

        // Left arm: support hand on the weapon, or a free arm root (shown for hands/grenade/crowbar, parked below otherwise).
        if (leftParent == "arm_l")
        {
            Bone("arm_l", "vm_root", freeLeft ? V(-armR.X - (id == "hands" ? 0.0f : 0.03f), armR.Y - 0.01f, armR.Z) : V(-0.24f, -0.42f, 0.22f), id == "hands" ? V(-14f, 10f, 10f) : V(0f, 4f, 0f));
            Bone("hand_l", "arm_l", Float3.Zero);
            left ??= Relaxed(-1);
        }
        else
        {
            Bone("arm_l", "vm_root", V(-0.2f, -0.3f, 0.2f));
            Bone("hand_l", leftParent, leftPos);
        }
        right ??= Relaxed(1);
        k.Hand("hand_r", right);
        k.Hand("hand_l", left!);
        Bone("wrist_r", "hand_r", right.Wrist);
        Bone("wrist_l", "hand_l", left!.Wrist);
        Bone("forearm_r", "vm_root", armR + right.Wrist);
        Bone("forearm_l", "vm_root", V(-0.2f, -0.3f, 0.2f));
        k.Forearm("forearm_r");
        k.Forearm("forearm_l");
        // Elbows sit down, out and back from each hand so forearms enter from the lower screen corners.
        Float3 handL = leftParent == "arm_l" ? d.Bones.First(b => b.Bone == "arm_l").Pos : armR + leftPos;
        elbowR = armR + right.Wrist + V(0.15f, -0.34f, -0.2f);
        elbowL = handL + left.Wrist + V(-0.15f, -0.34f, -0.2f);
        d.Ik.Add(("forearm_r", "wrist_r", elbowR, Nz(V(-0.35f, 1f, 0f))));
        d.Ik.Add(("forearm_l", "wrist_l", elbowL, Nz(V(0.35f, 1f, 0f))));

        BuildClips(d, id, leftParent != "arm_l");
        return d;
    }
}
