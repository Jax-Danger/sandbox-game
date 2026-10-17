using Prowl.Runtime;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// Built-in first-person view models made from smooth primitives: tapered forearms with sleeves and cuffs, gloved
/// hands (palm, thumb and three-segment fingers) posed to grip each weapon (trigger finger, support hand on the
/// foregrip or pump), and weapons with real sub-parts (slides, mags and pumps are bones so clips can move them).
/// A user model "vm_&lt;id&gt;" in the asset manifest replaces the primitives (see README "View models").
/// </summary>
public static class ViewModels
{
    private static readonly Color Sleeve = new(0.23f, 0.27f, 0.33f, 1f);
    private static readonly Color Cuff = new(0.15f, 0.17f, 0.21f, 1f);
    private static readonly Color Glove = new(0.11f, 0.11f, 0.12f, 1f);
    private static readonly Color GlovePad = new(0.19f, 0.19f, 0.20f, 1f);
    private static readonly Color Gun = new(0.13f, 0.135f, 0.15f, 1f);
    private static readonly Color Frame = new(0.22f, 0.22f, 0.24f, 1f);
    private static readonly Color Steel = new(0.42f, 0.43f, 0.46f, 1f);
    private static readonly Color Wood = new(0.46f, 0.28f, 0.15f, 1f);
    private static readonly Color Accent = new(0.30f, 0.64f, 1f, 1f);
    private static readonly Color Red = new(1f, 0.25f, 0.2f, 1f);
    private static readonly Color Olive = new(0.30f, 0.35f, 0.24f, 1f);

    private static (string, Float3, Float3) B(string bone, float px = 0, float py = 0, float pz = 0, float rx = 0, float ry = 0, float rz = 0)
        => (bone, new Float3(px, py, pz), new Float3(rx, ry, rz));

    private static Float3 V(float x, float y, float z) => new(x, y, z);
    private static Float3 N(Float3 v) => Float3.Length(v) > 1e-6f ? Float3.Normalize(v) : Float3.UnitZ;

    private static Quaternion Look(Float3 dir, Float3? up = null)
    {
        dir = N(dir);
        Float3 u = up ?? (MathF.Abs(dir.Y) < 0.95f ? Float3.UnitY : Float3.UnitX);
        return Quaternion.LookRotation(dir, u);
    }

    private static Quaternion Euler(float x, float y, float z) => Quaternion.FromEuler(new Float3(x, y, z) * (MathF.PI / 180f));

    /// <summary>Hand grip description in the hand bone's space (palm centre = bone origin).</summary>
    /// <param name="O">Palm normal, pointing at the held object.</param>
    /// <param name="F">Finger direction (knuckles → tips before curling).</param>
    /// <param name="Curl">0 = flat, 1 = wrapped round a grip.</param>
    /// <param name="Trigger">Index finger lies along the frame onto the trigger.</param>
    /// <param name="ThumbWrap">How far the thumb wraps over the far side (1 = round a grip, 0 = along the side).</param>
    /// <param name="Elbow">Where the forearm comes from (hand space).</param>
    private sealed record Grip(Float3 O, Float3 F, float Curl, bool Trigger, float ThumbWrap, Float3 Elbow, int Side = 1);

    private sealed class Rig(ViewModelDef d)
    {
        public void P(string bone, char shape, Float3 size, Float3 off, Color c, Finish f = Finish.Polymer, Quaternion? q = null, bool glow = false)
            => d.Parts.Add(new VmPart(bone, shape, size, off, c, default, glow, f, q));

        public void Box(string bone, Float3 size, Float3 off, Color c, Finish f = Finish.Metal, float rx = 0f, float ry = 0f, float rz = 0f, bool glow = false)
            => P(bone, 'b', size, off, c, f, Euler(rx, ry, rz), glow);

        /// <summary>Cylinder/tube along +Z (forward) unless rotated.</summary>
        public void Tube(string bone, float radius, float length, Float3 center, Color c, Finish f = Finish.Metal, float rx = 0f, float ry = 0f)
            => P(bone, 't', V(radius, radius, length), center, c, f, Euler(rx, ry, 0f) * Quaternion.Identity, false);

        public void Seg(string bone, Float3 a, Float3 b, float r0, float r1, Color c, Finish f)
        {
            Float3 dv = b - a;
            float len = Float3.Length(dv);
            if (len < 1e-4f) return;
            P(bone, 't', V(r0, r1, len), a, c, f, Look(dv));
        }

        /// <summary>Gloved hand with forearm, sleeve and cuff.</summary>
        public void Hand(string bone, Grip g)
        {
            Float3 f = N(g.F), o = N(g.O - f * Float3.Dot(g.O, f));
            Float3 a = N(Float3.Cross(f, o)) * g.Side; // index → pinky
            Quaternion palmQ = Look(f, -o);
            // Palm, heel and thenar pad.
            P(bone, 'b', V(0.074f, 0.030f, 0.080f), Float3.Zero, Glove, Finish.Leather, palmQ);
            P(bone, 'e', V(0.040f, 0.030f, 0.050f), -a * 0.022f - f * 0.012f + o * 0.006f, Glove, Finish.Leather, palmQ);
            P(bone, 'e', V(0.070f, 0.020f, 0.024f), -o * 0.012f + f * 0.032f, GlovePad, Finish.Rubber, palmQ); // knuckle guard
            // Fingers: index, middle, ring, pinky.
            float[] len = [1f, 1.08f, 1.0f, 0.82f];
            for (int k = 0; k < 4; k++)
            {
                Float3 p = f * 0.040f + a * (-0.027f + k * 0.018f) - o * 0.002f;
                float s = len[k], r = 0.0098f * (k == 3 ? 0.85f : 1f);
                float[] ang = k == 0 && g.Trigger ? [12f, 48f, 62f] : [32f * g.Curl, 92f * g.Curl, 148f * g.Curl];
                float[] seg = [0.025f * s, 0.017f * s, 0.014f * s];
                P(bone, 's', V(r * 2.15f, 0, 0), p, Glove, Finish.Leather);
                for (int j = 0; j < 3; j++)
                {
                    float th = ang[j] * MathF.PI / 180f;
                    Float3 dir = f * MathF.Cos(th) + o * MathF.Sin(th);
                    if (k == 0 && g.Trigger) dir = N(dir + a * -0.08f);
                    Float3 q = p + dir * seg[j];
                    float r0 = r * (1f - j * 0.09f), r1 = r * (0.93f - j * 0.1f);
                    Seg(bone, p - dir * r0 * 0.6f, q, r0, r1, Glove, Finish.Leather);
                    p = q - dir * r1 * 0.5f;
                }
            }
            // Thumb: two segments from the index side of the heel.
            {
                Float3 p = -a * 0.031f - f * 0.006f + o * 0.010f;
                Float3 d0 = N(-a * 0.45f + f * 0.55f + o * (0.25f + 0.45f * g.ThumbWrap));
                Float3 d1 = N(-a * 0.15f + f * 0.85f + o * (0.15f + 0.45f * g.ThumbWrap));
                Float3 m = p + d0 * 0.030f;
                Seg(bone, p, m, 0.0125f, 0.011f, Glove, Finish.Leather);
                Seg(bone, m - d1 * 0.005f, m + d1 * 0.024f, 0.0108f, 0.0092f, Glove, Finish.Leather);
            }
            // Wrist, cuff and sleeve back to the elbow.
            Float3 wrist = -f * 0.046f - o * 0.002f;
            Float3 e = g.Elbow;
            Float3 sleeveEnd = wrist + (e - wrist) * 0.16f, cuffEnd = wrist + (e - wrist) * 0.24f;
            Float3 ad = N(e - wrist);
            Seg(bone, wrist + f * 0.016f, sleeveEnd + ad * 0.004f, 0.023f, 0.027f, Glove, Finish.Leather);
            Seg(bone, sleeveEnd - ad * 0.002f, cuffEnd, 0.029f, 0.031f, Cuff, Finish.Fabric);
            Seg(bone, cuffEnd - ad * 0.004f, e, 0.032f, 0.041f, Sleeve, Finish.Fabric);
            Seg(bone, cuffEnd + ad * 0.004f, cuffEnd + ad * 0.012f, 0.0335f, 0.0335f, Cuff, Finish.Fabric); // sleeve hem
        }
    }

    // Standard grips (hand space).
    private static Grip PistolGrip => new(V(-1f, 0f, 0f), V(0f, 0.28f, 1f), 1f, true, 1f, V(0.07f, -0.11f, -0.36f));
    private static Grip FistGrip(Float3 o, Float3 f, Float3 elbow) => new(o, f, 1f, false, 0.9f, elbow);
    private static Grip SupportGrip => new(V(0f, 1f, 0.15f), V(1f, 0.15f, 0.1f), 0.85f, false, -0.1f, V(-0.24f, -0.12f, -0.27f), -1);
    private static Grip Relaxed(int side) => new(V(-0.75f * side, -0.65f, 0f), V(0.1f * side, 0.15f, 1f), 0.45f, false, 0.2f, V(0.09f * side, -0.08f, -0.34f), side);

    public static ViewModelDef Create(string id)
    {
        var d = new ViewModelDef { Id = id };
        var r = new Rig(d);
        // Rig: vm_root → arm_r → hand_r → weapon → muzzle/sight/mag/slide/pump (→ hand_l for two-handed weapons);
        // vm_root → arm_l → hand_l for a free left hand. Bone positions below the arm roots are in part units.
        void Bone(string b, string parent, Float3 pos, Float3 rot = default) => d.Bones.Add((b, parent, pos, rot));
        void W(char shape, Float3 size, Float3 off, Color c, Finish f = Finish.Metal, float rx = 0, float ry = 0, float rz = 0, bool glow = false, string bone = "weapon")
            => r.P(bone, shape, size, off, c, f, Euler(rx, ry, rz), glow);
        void Tube(float rad, float len, Float3 back, Color c, Finish f = Finish.Metal, string bone = "weapon", float r1 = -1f)
            => r.P(bone, 't', V(rad, r1 < 0 ? rad : r1, len), back, c, f, Quaternion.Identity);
        Float3 armR = V(0.14f, -0.13f, 0.28f), armL = V(-0.17f, -0.18f, 0.30f);
        string leftParent = "arm_l";
        Float3 leftPos = Float3.Zero;
        Grip left = Relaxed(-1), right = PistolGrip;
        bool freeLeftVisible = false;

        switch (id)
        {
            case "hands":
                right = Relaxed(1);
                armR = V(0.17f, -0.17f, 0.30f); armL = V(-0.17f, -0.17f, 0.30f);
                freeLeftVisible = true;
                Bone("arm_r", "vm_root", armR, V(8f, -8f, -10f));
                Bone("hand_r", "arm_r", Float3.Zero);
                Bone("weapon", "hand_r", Float3.Zero);
                Bone("muzzle", "weapon", V(0f, 0f, 0.1f));
                break;
            case "physgun":
                armR = V(0.15f, -0.16f, 0.32f);
                Bone("arm_r", "vm_root", armR, V(0f, -3f, 0f));
                Bone("hand_r", "arm_r", Float3.Zero);
                Bone("weapon", "hand_r", V(-0.034f, 0.010f, 0.02f));
                Bone("muzzle", "weapon", V(0f, 0.05f, 0.36f));
                leftParent = "weapon"; leftPos = V(-0.02f, -0.025f, 0.22f);
                left = SupportGrip with { O = V(0.15f, 1f, 0f) };
                // Rear housing, ribbed body, glowing spine, emitter ring, three claw prongs around a glowing core.
                W('b', V(0.07f, 0.08f, 0.09f), V(0f, 0.05f, 0.03f), Gun);
                W('b', V(0.072f, 0.022f, 0.07f), V(0f, 0.096f, 0.03f), Frame, Finish.Polymer);
                Tube(0.040f, 0.26f, V(0f, 0.05f, 0.04f), Frame, Finish.Polymer, r1: 0.034f);
                for (int i = 0; i < 4; i++) Tube(0.043f, 0.012f, V(0f, 0.05f, 0.08f + i * 0.04f), Gun);
                W('b', V(0.016f, 0.012f, 0.20f), V(0f, 0.093f, 0.15f), Accent, Finish.Polymer, glow: true);
                Tube(0.052f, 0.03f, V(0f, 0.05f, 0.29f), Steel);
                W('c', V(0.042f, 0.05f, 0f), V(0f, 0.05f, 0.33f), Accent, Finish.Polymer, 90f, glow: true);
                for (int i = 0; i < 3; i++)
                {
                    float an = i * 120f * MathF.PI / 180f;
                    Float3 bp = V(MathF.Sin(an) * 0.045f, 0.05f + MathF.Cos(an) * 0.045f, 0.30f);
                    r.Seg("weapon", bp, bp + V(MathF.Sin(an) * 0.012f, MathF.Cos(an) * 0.012f, 0.10f), 0.009f, 0.006f, Gun, Finish.Metal);
                }
                W('b', V(0.034f, 0.11f, 0.05f), V(0f, -0.02f, -0.005f), Frame, Finish.Rubber, 14f);
                W('b', V(0.03f, 0.06f, 0.05f), V(0f, -0.005f, 0.24f), Frame, Finish.Rubber, -8f);
                d.Weight = 1.5f; d.RecoilKick = 0.3f;
                break;
            case "toolgun":
                Bone("arm_r", "vm_root", armR, V(0f, -4f, 0f));
                Bone("hand_r", "arm_r", Float3.Zero);
                Bone("weapon", "hand_r", V(-0.034f, 0.017f, 0.014f));
                Bone("muzzle", "weapon", V(0f, 0.04f, 0.27f));
                W('b', V(0.06f, 0.075f, 0.22f), V(0f, 0.04f, 0.08f), new Color(0.86f, 0.86f, 0.89f, 1f), Finish.Polymer);
                W('b', V(0.062f, 0.02f, 0.20f), V(0f, 0.004f, 0.08f), Frame, Finish.Polymer);
                Tube(0.016f, 0.08f, V(0f, 0.045f, 0.19f), Gun, r1: 0.012f);
                Tube(0.022f, 0.012f, V(0f, 0.045f, 0.18f), Accent, Finish.Polymer);
                W('b', V(0.045f, 0.035f, 0.004f), V(0.0f, 0.06f, -0.032f), Accent, Finish.Polymer, -20f, glow: true); // rear screen
                W('b', V(0.05f, 0.012f, 0.05f), V(0f, 0.083f, 0.03f), Gun);
                W('b', V(0.032f, 0.105f, 0.05f), V(0f, -0.03f, 0.0f), Frame, Finish.Rubber, 14f);
                W('b', V(0.008f, 0.02f, 0.01f), V(0f, -0.005f, 0.04f), Gun);
                d.Weight = 0.8f;
                break;
            case "pistol":
                Bone("arm_r", "vm_root", armR, V(0f, -4f, 0f));
                Bone("hand_r", "arm_r", Float3.Zero);
                Bone("weapon", "hand_r", V(-0.033f, 0.015f, 0.012f));
                Bone("muzzle", "weapon", V(0f, 0.048f, 0.19f));
                Bone("sight", "weapon", V(0f, 0.078f, 0.0f));
                Bone("slide", "weapon", Float3.Zero);
                Bone("mag", "weapon", Float3.Zero);
                leftParent = "weapon"; leftPos = V(0.004f, -0.06f, 0.005f);
                left = new Grip(V(0.85f, 0.45f, 0.1f), V(0.25f, 0.2f, 1f), 0.95f, false, 0.2f, V(-0.13f, -0.12f, -0.30f), -1);
                W('b', V(0.034f, 0.034f, 0.20f), V(0f, 0.049f, 0.085f), Gun, Finish.Metal, bone: "slide");               // slide
                for (int i = 0; i < 5; i++) W('b', V(0.036f, 0.022f, 0.003f), V(0f, 0.052f, -0.002f + i * 0.007f), Frame, bone: "slide"); // serrations
                W('b', V(0.012f, 0.004f, 0.03f), V(0.016f, 0.058f, 0.085f), Frame, bone: "slide");                  // ejection port
                W('b', V(0.007f, 0.011f, 0.009f), V(0f, 0.071f, 0.175f), Gun, bone: "slide");                       // front sight
                W('s', V(0.004f, 0, 0), V(0f, 0.077f, 0.175f), Red, Finish.Polymer, glow: true, bone: "slide");
                W('b', V(0.026f, 0.010f, 0.010f), V(0f, 0.070f, 0.0f), Gun, bone: "slide");                          // rear sight
                W('s', V(0.004f, 0, 0), V(0.008f, 0.076f, 0.0f), Red, Finish.Polymer, glow: true, bone: "slide");
                W('s', V(0.004f, 0, 0), V(-0.008f, 0.076f, 0.0f), Red, Finish.Polymer, glow: true, bone: "slide");
                Tube(0.009f, 0.012f, V(0f, 0.048f, 0.183f), Steel);                                                 // barrel crown
                W('b', V(0.031f, 0.022f, 0.175f), V(0f, 0.023f, 0.08f), Frame, Finish.Polymer);                    // frame + rail
                W('b', V(0.032f, 0.105f, 0.052f), V(0f, -0.028f, -0.003f), Frame, Finish.Polymer, 13f);           // grip
                for (int i = 0; i < 4; i++) W('b', V(0.034f, 0.004f, 0.04f), V(0f, -0.005f - i * 0.018f, -0.006f + i * 0.0042f), Gun, Finish.Rubber, 13f); // texture
                W('b', V(0.03f, 0.012f, 0.05f), V(0f, -0.083f, -0.016f), Gun, Finish.Metal, 13f, bone: "mag");    // mag base
                W('b', V(0.024f, 0.006f, 0.05f), V(0f, 0.004f, 0.05f), Frame, Finish.Polymer);                     // trigger guard bottom
                W('b', V(0.024f, 0.022f, 0.006f), V(0f, 0.012f, 0.073f), Frame, Finish.Polymer);                   // guard front
                W('b', V(0.007f, 0.02f, 0.008f), V(0f, 0.014f, 0.042f), Steel, Finish.Metal, 15f);                 // trigger
                W('b', V(0.012f, 0.016f, 0.012f), V(0f, 0.062f, -0.018f), Gun, Finish.Metal, -20f, bone: "slide"); // hammer
                d.RecoilKick = 1.0f; d.Weight = 0.65f; d.CanAim = true; d.AimZoom = 1.12f;
                break;
            case "smg":
                Bone("arm_r", "vm_root", armR, V(0f, -4f, 0f));
                Bone("hand_r", "arm_r", Float3.Zero);
                Bone("weapon", "hand_r", V(-0.033f, 0.019f, 0.012f));
                Bone("muzzle", "weapon", V(0f, 0.046f, 0.37f));
                Bone("sight", "weapon", V(0f, 0.104f, 0.06f));
                Bone("mag", "weapon", Float3.Zero);
                Bone("charge", "weapon", Float3.Zero);
                leftParent = "weapon"; leftPos = V(-0.004f, -0.035f, 0.24f);
                left = SupportGrip with { O = V(0f, 1f, 0f), F = V(1f, 0.2f, 0f) };
                W('b', V(0.05f, 0.06f, 0.25f), V(0f, 0.038f, 0.10f), Gun);                                        // receiver
                W('b', V(0.052f, 0.012f, 0.25f), V(0f, 0.004f, 0.10f), Frame, Finish.Polymer);                     // lower
                W('b', V(0.046f, 0.05f, 0.14f), V(0f, 0.03f, 0.27f), Frame, Finish.Polymer);                        // handguard
                for (int i = 0; i < 4; i++) W('b', V(0.048f, 0.006f, 0.02f), V(0f, 0.04f, 0.22f + i * 0.03f), Gun); // vents
                Tube(0.012f, 0.09f, V(0f, 0.046f, 0.31f), Steel);                                                   // barrel
                Tube(0.017f, 0.04f, V(0f, 0.046f, 0.36f), Gun);                                                     // muzzle device
                W('b', V(0.028f, 0.14f, 0.045f), V(0f, -0.055f, 0.125f), Frame, Finish.Polymer, -10f, bone: "mag"); // magazine
                W('b', V(0.032f, 0.012f, 0.05f), V(0f, -0.125f, 0.11f), Gun, Finish.Metal, -10f, bone: "mag");
                W('b', V(0.034f, 0.09f, 0.048f), V(0f, -0.028f, -0.002f), Frame, Finish.Rubber, 13f);            // grip
                W('b', V(0.022f, 0.006f, 0.06f), V(0f, -0.004f, 0.045f), Frame, Finish.Polymer);                   // guard
                W('b', V(0.007f, 0.02f, 0.008f), V(0f, 0.004f, 0.04f), Steel, Finish.Metal, 15f);                 // trigger
                W('b', V(0.03f, 0.045f, 0.14f), V(0f, 0.03f, -0.10f), Frame, Finish.Polymer);                      // stock
                W('b', V(0.034f, 0.06f, 0.016f), V(0f, 0.02f, -0.17f), Gun, Finish.Rubber);                        // butt pad
                W('b', V(0.02f, 0.012f, 0.16f), V(0f, 0.074f, 0.10f), Gun);                                        // top rail
                W('b', V(0.03f, 0.03f, 0.045f), V(0f, 0.094f, 0.06f), Gun);                                         // red-dot housing
                W('b', V(0.024f, 0.022f, 0.002f), V(0f, 0.097f, 0.083f), new Color(0.35f, 0.55f, 0.75f, 1f), Finish.Metal); // lens
                W('s', V(0.004f, 0, 0), V(0f, 0.104f, 0.06f), Red, Finish.Polymer, glow: true);                     // dot
                W('b', V(0.012f, 0.01f, 0.02f), V(0.03f, 0.05f, 0.04f), Steel, bone: "charge");                    // charging handle
                d.RecoilKick = 0.45f; d.Weight = 1.0f; d.CanAim = true; d.AimZoom = 1.2f;
                break;
            case "shotgun":
                armR = V(0.15f, -0.15f, 0.29f);
                Bone("arm_r", "vm_root", armR, V(0f, -4f, 0f));
                Bone("hand_r", "arm_r", Float3.Zero);
                Bone("weapon", "hand_r", V(-0.033f, 0.020f, 0.01f));
                Bone("muzzle", "weapon", V(0f, 0.066f, 0.55f));
                Bone("sight", "weapon", V(0f, 0.094f, 0.30f));
                Bone("pump", "weapon", Float3.Zero);
                leftParent = "pump"; leftPos = V(-0.004f, 0.0f, 0.30f);
                left = SupportGrip with { O = V(0f, 1f, 0f), F = V(1f, 0.25f, 0f) };
                W('b', V(0.052f, 0.07f, 0.16f), V(0f, 0.045f, 0.06f), Gun);                                         // receiver
                W('b', V(0.02f, 0.012f, 0.05f), V(0.026f, 0.055f, 0.07f), Steel);                                  // ejection port
                Tube(0.016f, 0.42f, V(0f, 0.066f, 0.13f), Gun);                                                     // barrel
                Tube(0.013f, 0.33f, V(0f, 0.032f, 0.13f), Frame);                                                   // tube magazine
                Tube(0.015f, 0.012f, V(0f, 0.032f, 0.455f), Gun);                                                   // mag cap
                W('b', V(0.008f, 0.008f, 0.30f), V(0f, 0.084f, 0.30f), Gun);                                        // vent rib
                W('s', V(0.008f, 0, 0), V(0f, 0.092f, 0.53f), new Color(0.95f, 0.85f, 0.5f, 1f), Finish.Metal);   // bead
                r.P("pump", 't', V(0.024f, 0.024f, 0.14f), V(0f, 0.032f, 0.23f), Wood, Finish.Wood, Quaternion.Identity);
                for (int i = 0; i < 6; i++) r.P("pump", 't', V(0.026f, 0.026f, 0.006f), V(0f, 0.032f, 0.245f + i * 0.02f), new Color(0.36f, 0.21f, 0.11f, 1f), Finish.Wood, Quaternion.Identity);
                W('b', V(0.04f, 0.075f, 0.05f), V(0f, -0.02f, -0.01f), Wood, Finish.Wood, 18f);                   // wrist
                W('b', V(0.044f, 0.07f, 0.20f), V(0f, 0.0f, -0.12f), Wood, Finish.Wood, -5f);                     // stock
                W('b', V(0.046f, 0.09f, 0.018f), V(0f, -0.01f, -0.22f), Gun, Finish.Rubber, -5f);                 // butt pad
                W('b', V(0.022f, 0.006f, 0.06f), V(0f, 0.0f, 0.05f), Gun);
                W('b', V(0.007f, 0.02f, 0.008f), V(0f, 0.006f, 0.045f), Steel, Finish.Metal, 15f);
                d.RecoilKick = 2.2f; d.Weight = 1.35f; d.CanAim = true; d.AimZoom = 1.1f;
                break;
            case "rpg":
                armR = V(0.14f, -0.17f, 0.24f);
                Bone("arm_r", "vm_root", armR, V(0f, -3f, 0f));
                Bone("hand_r", "arm_r", Float3.Zero);
                Bone("weapon", "hand_r", V(-0.034f, 0.025f, 0.0f));
                Bone("muzzle", "weapon", V(0f, 0.09f, 0.56f));
                Bone("sight", "weapon", V(-0.13f, 0.15f, 0.12f));
                Bone("rocket", "weapon", Float3.Zero);
                leftParent = "weapon"; leftPos = V(-0.004f, -0.02f, 0.25f);
                left = SupportGrip with { O = V(0f, 1f, 0f), F = V(1f, 0.1f, 0.1f) };
                Tube(0.052f, 0.62f, V(0f, 0.09f, -0.08f), Olive, Finish.Polymer);                                   // launch tube
                Tube(0.064f, 0.05f, V(0f, 0.09f, -0.11f), Gun, Finish.Metal, r1: 0.054f);                          // rear flare
                Tube(0.058f, 0.04f, V(0f, 0.09f, 0.50f), Gun);                                                      // front ring
                for (int i = 0; i < 2; i++) Tube(0.056f, 0.02f, V(0f, 0.09f, -0.02f + i * 0.28f), Gun, Finish.Rubber);
                r.P("rocket", 't', V(0.040f, 0.012f, 0.14f), V(0f, 0.09f, 0.53f), Olive, Finish.Metal, Quaternion.Identity); // warhead
                r.P("rocket", 't', V(0.012f, 0.004f, 0.03f), V(0f, 0.09f, 0.66f), Steel, Finish.Metal, Quaternion.Identity);
                W('b', V(0.034f, 0.10f, 0.05f), V(0f, 0.0f, 0.0f), Gun, Finish.Rubber, 12f);                     // grip
                W('b', V(0.03f, 0.08f, 0.045f), V(0f, 0.02f, 0.25f), Gun, Finish.Rubber, -6f);                   // fore grip
                W('b', V(0.04f, 0.05f, 0.09f), V(-0.13f, 0.14f, 0.12f), Gun);                                       // sight box
                W('b', V(0.03f, 0.025f, 0.002f), V(-0.13f, 0.15f, 0.168f), new Color(0.35f, 0.55f, 0.75f, 1f), Finish.Metal);
                W('b', V(0.09f, 0.014f, 0.04f), V(-0.075f, 0.125f, 0.12f), Gun);
                d.RecoilKick = 3f; d.Weight = 1.9f; d.CanAim = true; d.AimZoom = 1.25f;
                d.SprintPose = new BonePose(V(0.02f, -0.07f, -0.05f), V(14f, -20f, 10f));
                break;
            case "grenade":
                Bone("arm_r", "vm_root", V(0.14f, -0.15f, 0.29f), V(0f, -8f, 0f));
                Bone("hand_r", "arm_r", Float3.Zero);
                Bone("weapon", "hand_r", V(-0.04f, 0.035f, 0.02f));
                Bone("muzzle", "weapon", V(0f, 0.04f, 0.05f));
                Bone("pin", "weapon", Float3.Zero);
                right = FistGrip(V(-0.8f, 0.6f, 0f), V(0f, 0.3f, 1f), V(0.07f, -0.10f, -0.36f)) with { Curl = 0.8f };
                freeLeftVisible = true;
                r.P("weapon", 'c', V(0.075f, 0.09f, 0f), V(0f, 0.035f, 0.0f), Olive, Finish.Metal, null);
                for (int i = 0; i < 3; i++) r.P("weapon", 'c', V(0.079f, 0.008f, 0f), V(0f, 0.01f + i * 0.025f, 0f), new Color(0.24f, 0.28f, 0.19f, 1f), Finish.Metal, null);
                r.P("weapon", 'c', V(0.03f, 0.03f, 0f), V(0f, 0.093f, 0f), Steel, Finish.Metal, null);
                W('b', V(0.014f, 0.006f, 0.09f), V(0.02f, 0.085f, -0.03f), Steel, Finish.Metal, -60f);           // spoon
                r.P("pin", 't', V(0.012f, 0.012f, 0.004f), V(-0.02f, 0.1f, 0f), Steel, Finish.Metal, Euler(0f, 90f, 0f)); // pin ring
                r.P("pin", 't', V(0.002f, 0.002f, 0.03f), V(-0.002f, 0.1f, 0f), Steel, Finish.Metal, Euler(0f, -90f, 0f));
                d.Weight = 0.5f;
                break;
            case "crowbar":
                Bone("arm_r", "vm_root", V(0.16f, -0.17f, 0.31f), V(0f, -6f, 0f));
                Bone("hand_r", "arm_r", Float3.Zero);
                Bone("weapon", "hand_r", V(-0.03f, 0f, 0.0f));
                Bone("muzzle", "weapon", V(0f, 0.3f, 0.2f));
                right = FistGrip(V(-1f, 0f, 0f), V(0f, -0.6f, 0.8f), V(0.08f, -0.08f, -0.36f));
                freeLeftVisible = true;
                {
                    Float3 s = N(V(-0.25f, 0.55f, 0.80f));
                    Color red = new(0.70f, 0.14f, 0.12f, 1f);
                    r.Seg("weapon", -s * 0.06f, s * 0.48f, 0.014f, 0.014f, red, Finish.Metal);                      // shaft
                    r.Seg("weapon", -s * 0.05f, s * 0.07f, 0.0165f, 0.0165f, new Color(0.08f, 0.08f, 0.09f, 1f), Finish.Rubber); // tape grip
                    Float3 top = s * 0.48f, back = N(V(0.1f, 0.2f, -1f));
                    r.Seg("weapon", top - s * 0.01f, top + s * 0.03f + back * 0.03f, 0.014f, 0.013f, red, Finish.Metal);
                    r.Seg("weapon", top + s * 0.02f + back * 0.025f, top + back * 0.08f - s * 0.015f, 0.013f, 0.007f, Steel, Finish.Metal); // hook
                    r.Seg("weapon", -s * 0.06f, -s * 0.10f + V(0f, 0f, 0.03f), 0.012f, 0.006f, Steel, Finish.Metal); // pry end
                }
                d.Weight = 1.1f;
                break;
            default:
                Bone("arm_r", "vm_root", armR);
                Bone("hand_r", "arm_r", Float3.Zero);
                Bone("weapon", "hand_r", V(-0.034f, 0.015f, 0.012f));
                Bone("muzzle", "weapon", V(0f, 0.04f, 0.26f));
                W('b', V(0.08f, 0.08f, 0.22f), V(0f, 0.04f, 0.08f), new Color(0.25f, 0.70f, 0.45f, 1f), Finish.Polymer);
                W('s', V(0.09f, 0, 0), V(0f, 0.05f, 0.24f), Accent, Finish.Polymer, glow: true);
                break;
        }

        // Left arm: either on the weapon (support hand) or a free arm root.
        if (leftParent == "arm_l")
        {
            Bone("arm_l", "vm_root", freeLeftVisible ? armL : V(-0.24f, -0.42f, 0.22f), V(8f, 8f, 10f));
            Bone("hand_l", "arm_l", Float3.Zero);
        }
        else
        {
            Bone("arm_l", "vm_root", V(-0.2f, -0.3f, 0.2f));
            Bone("hand_l", leftParent, leftPos);
        }
        r.Hand("hand_r", right);
        r.Hand("hand_l", left);

        BuildClips(d, id, leftParent != "arm_l");
        return d;
    }

    // ───────────────────────────── Clips ─────────────────────────────
    // Offsets from rest. Ease names the curve used to reach a key from the previous one: Out = decelerate into the
    // pose, In = accelerate (strikes), Back = overshoot and settle. Most moves have a small anticipation key first.


    private static void BuildClips(ViewModelDef d, string id, bool supportHand)
    {
        bool has(string b) => d.Bones.Any(x => x.Bone == b);
        var clips = d.Clips;
        // Breathing idle: slow figure-eight on the rig; the support hand micro-adjusts its grip.
        clips["idle"] = new ClipBuilder(4.0f, loop: true)
            .Key(0f, Ease.InOut, B("vm_root"))
            .Key(1.3f, Ease.InOut, B("vm_root", 0.0015f, 0.004f, 0f, -0.7f, 0.2f, 0.3f), B("hand_l", 0f, 0.002f))
            .Key(2.7f, Ease.InOut, B("vm_root", -0.0015f, 0.002f, 0f, -0.3f, -0.2f, -0.3f), B("hand_l", 0f, 0.001f, 0.002f))
            .Build();
        // Draw: up from below with a roll, slight overshoot, settle. Holster: a quick dip away.
        clips["draw"] = new ClipBuilder(0.55f)
            .Key(0f, Ease.Linear, B("vm_root", 0.02f, -0.24f, -0.06f, 38f, 6f, -14f), B("hand_r", 0f, 0f, 0f, 0f, 0f, -20f))
            .Key(0.36f, Ease.Out, B("vm_root", 0f, 0.006f, 0f, -2.5f, 0f, 1f), B("hand_r", 0f, 0f, 0f, 0f, 0f, 3f))
            .Key(0.55f, Ease.InOut, B("vm_root"), B("hand_r"))
            .Build();
        clips["holster"] = new ClipBuilder(0.2f)
            .Key(0f, Ease.Linear, B("vm_root"))
            .Key(0.04f, Ease.Out, B("vm_root", 0f, 0.006f, 0f, -3f))
            .Key(0.2f, Ease.In, B("vm_root", 0.02f, -0.26f, -0.05f, 36f, 4f, 12f))
            .Build();

        switch (id)
        {
            case "crowbar":
                // Forehand: wind up right, fast diagonal cut down-left, follow through, settle with a little overshoot.
                clips["fire"] = new ClipBuilder(0.48f)
                    .Key(0f, Ease.Linear, B("arm_r"))
                    .Key(0.09f, Ease.Out, B("arm_r", 0.04f, 0.07f, -0.05f, -42f, 28f, 18f))
                    .Key(0.18f, Ease.In, B("arm_r", -0.13f, -0.05f, 0.10f, 52f, -38f, -22f))
                    .Key(0.26f, Ease.Out, B("arm_r", -0.17f, -0.08f, 0.06f, 62f, -46f, -26f))
                    .Key(0.48f, Ease.Back, B("arm_r"))
                    .Build();
                // Backhand: wind up across the body, cut right.
                clips["fire2"] = new ClipBuilder(0.48f)
                    .Key(0f, Ease.Linear, B("arm_r"))
                    .Key(0.09f, Ease.Out, B("arm_r", -0.10f, 0.05f, -0.03f, -30f, -30f, -35f))
                    .Key(0.18f, Ease.In, B("arm_r", 0.06f, -0.04f, 0.10f, 40f, 30f, 25f))
                    .Key(0.26f, Ease.Out, B("arm_r", 0.08f, -0.07f, 0.05f, 50f, 36f, 30f))
                    .Key(0.48f, Ease.Back, B("arm_r"))
                    .Build();
                clips["inspect"] = new ClipBuilder(2.2f)
                    .Key(0f, Ease.Linear, B("weapon"), B("arm_r"))
                    .Key(0.3f, Ease.Out, B("arm_r", -0.04f, 0.03f, 0f, -10f, 20f, 0f))
                    .Key(1.1f, Ease.InOut, B("arm_r", -0.04f, 0.03f, 0f, -10f, 20f, 0f), B("weapon", 0f, 0f, 0f, 0f, 0f, 360f))
                    .Key(1.5f, Ease.InOut, B("arm_r", -0.02f, 0.04f, 0f, -20f, 10f, 10f), B("weapon", 0f, 0f, 0f, 0f, 0f, 360f))
                    .Key(2.2f, Ease.Back, B("arm_r"), B("weapon", 0f, 0f, 0f, 0f, 0f, 360f))
                    .Build();
                break;
            case "grenade":
                // Left hand pulls the pin (pin flies off), arm winds back, throws, a fresh grenade comes up.
                clips["fire"] = new ClipBuilder(0.85f)
                    .Key(0f, Ease.Linear, B("arm_r"), B("arm_l"), B("pin"))
                    .Key(0.12f, Ease.Out, B("arm_l", 0.24f, 0.08f, 0.0f, -10f, -20f, -20f), B("arm_r", -0.01f, 0.01f))
                    .Key(0.2f, Ease.InOut, B("arm_l", 0.25f, 0.08f, 0.0f, -10f, -20f, -20f), B("arm_r", -0.01f, 0.01f))
                    .Key(0.3f, Ease.In, B("arm_l", 0.12f, 0.06f, -0.02f, -5f, -10f, -10f), B("pin", -0.13f, 0.02f, -0.01f, 0f, 0f, 60f), B("arm_r", -0.01f, 0.01f))
                    .Key(0.42f, Ease.Out, B("arm_l"), B("pin", -0.5f, -0.3f, 0f), B("arm_r", 0.05f, 0.05f, -0.04f, -45f, 10f, 12f))
                    .Key(0.52f, Ease.In, B("arm_l"), B("pin", -0.5f, -0.3f, 0f), B("arm_r", -0.05f, -0.05f, 0.17f, 52f, -16f, -10f))
                    .Key(0.62f, Ease.Linear, B("arm_r", 0f, -0.30f, 0.05f, 40f), B("pin", -0.5f, -0.3f, 0f))
                    .Key(0.85f, Ease.Out, B("arm_r"), B("pin"))
                    .Build();
                clips["inspect"] = new ClipBuilder(1.8f)
                    .Key(0f, Ease.Linear, B("weapon"))
                    .Key(0.15f, Ease.Out, B("weapon", 0f, -0.01f, 0f))
                    .Key(0.55f, Ease.Out, B("weapon", 0f, 0.14f, 0.02f, 360f, 0f, 0f))
                    .Key(0.95f, Ease.In, B("weapon", 0f, 0f, 0f, 360f, 0f, 0f))
                    .Key(1.05f, Ease.Out, B("weapon", 0f, -0.012f, 0f, 360f, 0f, 0f))
                    .Key(1.8f, Ease.Back, B("weapon", 0f, 0f, 0f, 360f, 0f, 0f))
                    .Build();
                break;
            case "physgun":
                clips["fire"] = new ClipBuilder(0.2f)
                    .Key(0f, Ease.Linear, B("weapon"))
                    .Key(0.04f, Ease.Out, B("weapon", 0f, 0.006f, -0.015f, -4f))
                    .Key(0.2f, Ease.InOut, B("weapon"))
                    .Build();
                // Grab: a push forward and a twist as the beam locks on. Release: a recoil flick that settles.
                clips["grab"] = new ClipBuilder(0.4f)
                    .Key(0f, Ease.Linear, B("weapon"))
                    .Key(0.06f, Ease.Out, B("weapon", 0f, 0f, 0.02f, 2f, 0f, -10f))
                    .Key(0.18f, Ease.InOut, B("weapon", 0f, 0.004f, -0.018f, -3f, 0f, 6f))
                    .Key(0.4f, Ease.Back, B("weapon"))
                    .Build();
                clips["release"] = new ClipBuilder(0.45f)
                    .Key(0f, Ease.Linear, B("weapon"))
                    .Key(0.05f, Ease.Out, B("weapon", 0f, 0.015f, -0.03f, -12f, 0f, 8f))
                    .Key(0.2f, Ease.InOut, B("weapon", 0f, -0.004f, 0.006f, 3f, 0f, -3f))
                    .Key(0.45f, Ease.InOut, B("weapon"))
                    .Build();
                clips["inspect"] = new ClipBuilder(2.6f)
                    .Key(0f, Ease.Linear, B("hand_r"))
                    .Key(0.6f, Ease.InOut, B("hand_r", -0.03f, 0.04f, 0f, -10f, 30f, -50f))
                    .Key(1.4f, Ease.InOut, B("hand_r", -0.03f, 0.05f, 0f, -14f, 34f, -54f))
                    .Key(1.9f, Ease.InOut, B("hand_r", -0.01f, 0.02f, 0f, 8f, -12f, 14f))
                    .Key(2.6f, Ease.InOut, B("hand_r"))
                    .Build();
                break;
            case "toolgun":
                clips["fire"] = new ClipBuilder(0.18f)
                    .Key(0f, Ease.Linear, B("weapon"))
                    .Key(0.03f, Ease.Out, B("weapon", 0f, 0.006f, -0.015f, -5f))
                    .Key(0.18f, Ease.InOut, B("weapon"))
                    .Build();
                // Turn the rear screen to the face, tap it, turn back.
                clips["inspect"] = new ClipBuilder(2.4f)
                    .Key(0f, Ease.Linear, B("hand_r"))
                    .Key(0.5f, Ease.InOut, B("hand_r", -0.05f, 0.05f, -0.02f, 25f, -45f, 10f))
                    .Key(1.6f, Ease.InOut, B("hand_r", -0.05f, 0.055f, -0.02f, 28f, -48f, 12f))
                    .Key(2.4f, Ease.InOut, B("hand_r"))
                    .Build();
                break;
            case "hands":
                clips["inspect"] = new ClipBuilder(2.4f)
                    .Key(0f, Ease.Linear, B("arm_r"), B("arm_l"))
                    .Key(0.6f, Ease.InOut, B("arm_r", -0.04f, 0.05f, 0f, -20f, 0f, 70f), B("arm_l", 0.04f, 0.05f, 0f, -20f, 0f, -70f))
                    .Key(1.6f, Ease.InOut, B("arm_r", -0.04f, 0.055f, 0f, -22f, 0f, 75f), B("arm_l", 0.04f, 0.055f, 0f, -22f, 0f, -75f))
                    .Key(2.4f, Ease.InOut, B("arm_r"), B("arm_l"))
                    .Build();
                clips["fire"] = new ClipBuilder(0.3f)
                    .Key(0f, Ease.Linear, B("arm_r"))
                    .Key(0.08f, Ease.Out, B("arm_r", -0.03f, 0.03f, 0.08f, -10f))
                    .Key(0.3f, Ease.InOut, B("arm_r"))
                    .Build();
                break;
            default:
                // Gun shot: sharp kick (slide/bolt cycles), quick recovery. The recoil spring adds randomness on top.
                clips["fire"] = new ClipBuilder(id == "shotgun" ? 0.8f : 0.16f)
                    .Key(0f, Ease.Linear, B("weapon"), B("slide"), B("pump"))
                    .Key(0.025f, Ease.Out, B("weapon", 0f, 0.008f, -0.022f, -6f), B("slide", 0f, 0f, -0.03f))
                    .Key(0.08f, Ease.InOut, B("weapon", 0f, 0.002f, -0.006f, -1.5f), B("slide"))
                    .Key(0.16f, Ease.InOut, B("weapon"), B("slide"))
                    .Build();
                if (id == "shotgun")
                {
                    // Pump: anticipation, rack back, slam forward with a small settle.
                    var f = new ClipBuilder(0.8f)
                        .Key(0f, Ease.Linear, B("weapon"), B("pump"))
                        .Key(0.04f, Ease.Out, B("weapon", 0f, 0.014f, -0.035f, -10f))
                        .Key(0.24f, Ease.InOut, B("weapon", 0f, 0f, 0f, 0f, 0f, 3f), B("pump", 0f, 0f, 0.006f))
                        .Key(0.38f, Ease.In, B("weapon", 0f, 0f, 0f, 1f, 0f, 5f), B("pump", 0f, 0f, -0.08f))
                        .Key(0.5f, Ease.In, B("weapon", 0f, 0f, 0.004f, -1f, 0f, 3f), B("pump", 0f, 0f, 0.004f))
                        .Key(0.8f, Ease.InOut, B("weapon"), B("pump"));
                    clips["fire"] = f.Build();
                }
                break;
        }

        // Reloads.
        if (id == "shotgun")
        {
            // Shell by shell (interruptible): roll the port up → (shell: hand dips to the belt, pushes one in) × n → rack.
            Float3 port = V(0.0f, -0.01f, -0.15f);
            var roll = B("hand_r", 0f, 0.01f, 0f, -6f, 0f, 26f);
            clips["reload_start"] = new ClipBuilder(Firearm.ShellStart)
                .Key(0f, Ease.Linear, B("hand_r"), B("hand_l"))
                .Key(Firearm.ShellStart, Ease.InOut, roll, B("hand_l", port.X, port.Y - 0.06f, port.Z, 10f))
                .Build();
            clips["reload_start"].Hold = true;
            clips["reload_shell"] = new ClipBuilder(Firearm.ShellTime)
                .Key(0f, Ease.Linear, roll, B("hand_l", port.X, port.Y - 0.06f, port.Z, 10f))
                .Key(0.12f, Ease.Out, roll, B("hand_l", port.X - 0.02f, port.Y - 0.12f, port.Z - 0.02f, 25f))
                .Key(0.26f, Ease.InOut, roll, B("hand_l", port.X, port.Y - 0.01f, port.Z + 0.01f, -8f))
                .Key(0.32f, Ease.In, roll, B("hand_l", port.X, port.Y + 0.004f, port.Z + 0.03f, -12f))
                .Key(Firearm.ShellTime, Ease.Out, roll, B("hand_l", port.X, port.Y - 0.06f, port.Z, 10f))
                .Build();
            clips["reload_shell"].Hold = true;
            clips["reload_end"] = new ClipBuilder(Firearm.ShellEnd + 0.15f)
                .Key(0f, Ease.Linear, roll, B("hand_l", port.X, port.Y - 0.06f, port.Z, 10f), B("pump"))
                .Key(0.2f, Ease.InOut, B("hand_r"), B("hand_l"), B("pump"))
                .Key(0.34f, Ease.In, B("hand_r", 0f, 0f, 0f, 0f, 0f, 4f), B("hand_l"), B("pump", 0f, 0f, -0.08f))
                .Key(0.46f, Ease.In, B("hand_r"), B("hand_l"), B("pump", 0f, 0f, 0.004f))
                .Key(Firearm.ShellEnd + 0.15f, Ease.InOut, B("hand_r"), B("hand_l"), B("pump"))
                .Build();
            clips["reload"] = clips["reload_end"];
            clips["inspect"] = new ClipBuilder(2.6f)
                .Key(0f, Ease.Linear, B("hand_r"), B("pump"))
                .Key(0.6f, Ease.InOut, B("hand_r", -0.04f, 0.05f, 0f, -12f, 22f, -38f))
                .Key(1.2f, Ease.InOut, B("hand_r", -0.04f, 0.05f, 0f, -12f, 22f, -38f), B("pump", 0f, 0f, -0.035f))
                .Key(1.5f, Ease.InOut, B("hand_r", -0.04f, 0.05f, 0f, -12f, 22f, -38f), B("pump"))
                .Key(2.6f, Ease.InOut, B("hand_r"), B("pump"))
                .Build();
        }
        else if (has("mag") || id == "rpg")
        {
            // Mag change: tilt, support hand to the mag, pull it, fetch a fresh one, seat it with a bump;
            // pistol racks the slide, SMG pulls the charging handle; RPG slides a new warhead in from the front.
            string m = id == "rpg" ? "rocket" : "mag";
            Float3 toMag = id switch { "smg" => V(0f, -0.07f, -0.12f), "pistol" => V(0f, -0.02f, -0.01f), "rpg" => V(0f, 0.10f, 0.25f), _ => V(0f, -0.04f, -0.08f) };
            Float3 pull = id == "rpg" ? V(-0.05f, 0.06f, 0.20f) : V(-0.02f, -0.22f, -0.04f);
            float L = id == "pistol" ? 1.4f : id == "rpg" ? 2.0f : 1.5f;
            var tilt = B("hand_r", 0f, 0.006f, 0f, 6f, 0f, -24f);
            var c = new ClipBuilder(L)
                .Key(0f, Ease.Linear, B("hand_r"), B("hand_l"), B(m), B("weapon"), B("slide"), B("charge"))
                .Key(0.06f * L, Ease.Out, B("hand_r", 0f, -0.004f, 0f, -3f, 0f, 4f)) // anticipation
                .Key(0.20f * L, Ease.InOut, tilt, B("hand_l", toMag.X, toMag.Y, toMag.Z))
                .Key(0.30f * L, Ease.InOut, tilt, B("hand_l", toMag.X, toMag.Y - 0.01f, toMag.Z), B(m, 0f, -0.01f, 0f))
                .Key(0.44f * L, Ease.In, B("hand_r", 0f, 0.006f, 0f, 8f, 0f, -26f), B("hand_l", toMag.X + pull.X, toMag.Y + pull.Y, toMag.Z + pull.Z, 20f), B(m, pull.X, pull.Y, pull.Z, 20f))
                .Key(0.55f * L, Ease.InOut, B("hand_r", 0f, 0.006f, 0f, 8f, 0f, -26f), B("hand_l", toMag.X + pull.X, toMag.Y + pull.Y, toMag.Z + pull.Z, 20f), B(m, pull.X, pull.Y, pull.Z, 20f))
                .Key(0.70f * L, Ease.Out, tilt, B("hand_l", toMag.X, toMag.Y - 0.015f, toMag.Z), B(m, 0f, -0.015f, 0f))
                .Key(0.76f * L, Ease.In, B("hand_r", 0f, 0.014f, 0f, 4f, 0f, -22f), B("hand_l", toMag.X, toMag.Y + 0.004f, toMag.Z), B(m), B("weapon", 0f, 0.006f, 0f, -2f));
            if (id == "pistol")
                c.Key(0.85f * L, Ease.InOut, B("hand_r", 0f, 0.01f, 0f, 2f, 0f, -12f), B("hand_l", -0.02f, 0.11f, -0.05f), B("slide"))
                 .Key(0.90f * L, Ease.In, B("hand_r", 0f, 0.01f, 0f, 2f, 0f, -12f), B("hand_l", -0.02f, 0.11f, -0.085f), B("slide", 0f, 0f, -0.03f))
                 .Key(0.93f * L, Ease.Out, B("hand_r"), B("hand_l", -0.03f, 0.10f, -0.10f), B("slide"));
            if (id == "smg")
                c.Key(0.85f * L, Ease.InOut, B("hand_r", 0f, 0.01f, 0f, 0f, 0f, 10f), B("hand_l", 0.035f, 0.09f, -0.20f), B("charge"))
                 .Key(0.90f * L, Ease.In, B("hand_r", 0f, 0.01f, 0f, 0f, 0f, 10f), B("hand_l", 0.035f, 0.09f, -0.24f), B("charge", 0f, 0f, -0.04f))
                 .Key(0.93f * L, Ease.Out, B("hand_r"), B("hand_l", 0.03f, 0.08f, -0.22f), B("charge"));
            c.Key(L, Ease.InOut, B("hand_r"), B("hand_l"), B(m), B("weapon"), B("slide"), B("charge"));
            clips["reload"] = c.Build();
            clips["inspect"] = id switch
            {
                // Pistol: show the left side, then flip to the right with a press check.
                "pistol" => new ClipBuilder(2.6f)
                    .Key(0f, Ease.Linear, B("hand_r"), B("slide"), B("hand_l"))
                    .Key(0.5f, Ease.InOut, B("hand_r", -0.04f, 0.04f, 0f, -8f, 30f, -55f), B("hand_l", 0f, -0.1f, -0.05f))
                    .Key(1.2f, Ease.InOut, B("hand_r", -0.04f, 0.045f, 0f, -10f, 34f, -58f), B("hand_l", 0f, -0.1f, -0.05f))
                    .Key(1.6f, Ease.InOut, B("hand_r", -0.02f, 0.03f, 0f, 6f, -20f, 30f), B("hand_l", -0.02f, 0.11f, -0.06f))
                    .Key(1.85f, Ease.InOut, B("hand_r", -0.02f, 0.03f, 0f, 6f, -20f, 30f), B("hand_l", -0.02f, 0.11f, -0.075f), B("slide", 0f, 0f, -0.012f))
                    .Key(2.05f, Ease.InOut, B("hand_r", -0.02f, 0.03f, 0f, 6f, -20f, 30f), B("hand_l", -0.02f, 0.11f, -0.06f), B("slide"))
                    .Key(2.6f, Ease.InOut, B("hand_r"), B("slide"), B("hand_l"))
                    .Build(),
                // SMG: roll to look at the mag, tap it.
                "smg" => new ClipBuilder(2.6f)
                    .Key(0f, Ease.Linear, B("hand_r"), B("hand_l"))
                    .Key(0.6f, Ease.InOut, B("hand_r", -0.03f, 0.04f, 0f, -15f, 15f, 45f), B("hand_l", 0f, -0.07f, -0.12f))
                    .Key(1.3f, Ease.InOut, B("hand_r", -0.03f, 0.045f, 0f, -16f, 16f, 47f), B("hand_l", 0f, -0.07f, -0.12f))
                    .Key(1.42f, Ease.In, B("hand_r", -0.03f, 0.045f, 0f, -16f, 16f, 47f), B("hand_l", 0f, -0.06f, -0.12f))
                    .Key(1.55f, Ease.Out, B("hand_r", -0.03f, 0.05f, 0f, -16f, 16f, 47f), B("hand_l", 0f, -0.07f, -0.12f))
                    .Key(2.6f, Ease.InOut, B("hand_r"), B("hand_l"))
                    .Build(),
                _ => new ClipBuilder(2.4f)
                    .Key(0f, Ease.Linear, B("hand_r"))
                    .Key(0.6f, Ease.InOut, B("hand_r", -0.03f, 0.06f, -0.03f, -20f, 20f, -20f))
                    .Key(1.6f, Ease.InOut, B("hand_r", -0.03f, 0.065f, -0.03f, -22f, 22f, -22f))
                    .Key(2.4f, Ease.InOut, B("hand_r"))
                    .Build(),
            };
        }
        if (!clips.ContainsKey("inspect"))
            clips["inspect"] = new ClipBuilder(2.4f)
                .Key(0f, Ease.Linear, B("arm_r"))
                .Key(0.6f, Ease.InOut, B("arm_r", -0.07f, 0.04f, 0f, -8f, 32f, 34f))
                .Key(1.5f, Ease.InOut, B("arm_r", -0.07f, 0.05f, 0f, -12f, 38f, 38f))
                .Key(2.4f, Ease.InOut, B("arm_r"))
                .Build();
    }
}
