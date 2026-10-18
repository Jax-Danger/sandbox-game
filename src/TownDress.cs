using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

/// <summary>Building facades: real window assemblies (casing, sashes, glazing bars, sheen, sill, lintel, shutters)
/// on every wall, lap siding and corner boards on houses, pilasters/dentil cornices on commercial blocks, lettered signs.
/// Everything is baked per colour into one mesh per building (local space, rotated with the building).</summary>
public static partial class MapBuilder
{
    private sealed class Bucket : Dictionary<Color, List<Mesh>>
    {
        public void Add(Color c, Mesh m) { if (!TryGetValue(c, out var l)) this[c] = l = []; l.Add(m); }
    }

    private static Float3 L3(float x, float y, float z) => new(x, y, z);

    /// <summary>Rotate a mesh about Y by a (degrees, same convention as the building's local→world) and move it.</summary>
    private static Mesh Xf(Mesh m, float a, Float3 o)
    {
        float r = a * MathF.PI / 180f, c = MathF.Cos(r), s = MathF.Sin(r);
        Float3 R(Float3 v) => new(v.X * c + v.Z * s, v.Y, -v.X * s + v.Z * c);
        var mm = new Mesh { IndexFormat = m.IndexFormat, Vertices = m.Vertices.Select(v => R(v) + o).ToArray(), Normals = m.Normals.Select(R).ToArray(), UV = m.UV, Indices = m.Indices };
        mm.RecalculateBounds(); return mm;
    }
    private static Mesh Bx(float x, float y, float z, float ox, float oy, float oz, float r = 0.01f) => Xf(Meshes.RoundedBox(L3(x, y, z), r), 0f, L3(ox, oy, oz));

    /// <summary>One window, built facing -Z with the wall face at z = 0, then turned by a and moved to o.</summary>
    private static void Window(Bucket b, float a, Float3 o, float w, float h, bool house, Color frame, Color trim, Color glass, Color shutter)
    {
        var parts = new List<(Color, Mesh)>();
        float hw = w / 2f, hh = h / 2f;
        // Dark reveal behind the glass fakes the wall depth of a real opening.
        parts.Add((new Color(0.12f, 0.13f, 0.15f, 1f), Bx(w + 0.04f, h + 0.04f, 0.02f, 0, 0, -0.01f)));
        parts.Add((glass, Bx(w - 0.08f, h - 0.08f, 0.01f, 0, 0, -0.035f, 0.002f)));
        // Sheen: two diagonal pale streaks per pane (reads as glass, not blue paint).
        foreach (float sx in new[] { hw * 0.35f })
            parts.Add((new Color(glass.R + 0.1f, glass.G + 0.1f, glass.B + 0.08f, 1f), Meshes.Faces(new Float3[] { L3(sx - 0.2f, -hh * 0.7f, -0.041f), L3(sx - 0.2f + 0.1f, -hh * 0.7f, -0.041f), L3(sx + 0.15f, hh * 0.7f, -0.041f), L3(sx + 0.05f, hh * 0.7f, -0.041f) }.Reverse().ToArray())));
        // Casing (proud of the wall), sash frame, mullion and transom.
        parts.Add((frame, Bx(w + 0.16f, 0.09f, 0.07f, 0, hh + 0.02f, -0.045f))); parts.Add((frame, Bx(0.09f, h + 0.13f, 0.07f, -hw - 0.035f, 0, -0.045f))); parts.Add((frame, Bx(0.09f, h + 0.13f, 0.07f, hw + 0.035f, 0, -0.045f)));
        parts.Add((frame, Bx(w, 0.05f, 0.05f, 0, -hh + 0.025f, -0.05f)));
        parts.Add((frame, Bx(0.05f, h, 0.05f, 0, 0, -0.05f)));
        parts.Add((frame, Bx(w, 0.045f, 0.05f, 0, house ? 0f : hh * 0.35f, -0.05f)));
        if (house) foreach (float gx in new[] { -hw / 2f, hw / 2f }) parts.Add((frame, Bx(0.025f, h - 0.06f, 0.03f, gx, 0, -0.05f, 0.005f)));
        // Sloped sill with a drip lip, and a lintel (with a keystone on houses).
        parts.Add((trim, Xf(Meshes.RoundedBox(L3(w + 0.3f, 0.07f, 0.2f), 0.015f), 0f, L3(0, -hh - 0.06f, -0.09f))));
        parts.Add((trim, Bx(w + 0.26f, 0.03f, 0.03f, 0, -hh - 0.11f, -0.18f)));
        parts.Add((trim, Bx(w + 0.36f, 0.16f, 0.09f, 0, hh + 0.15f, -0.05f, 0.015f)));
        if (house)
        {
            parts.Add((trim, Meshes.Faces(new Float3[] { L3(-0.09f, hh + 0.06f, -0.11f), L3(0.09f, hh + 0.06f, -0.11f), L3(0.12f, hh + 0.28f, -0.11f), L3(-0.12f, hh + 0.28f, -0.11f) }.Reverse().ToArray())));
            // Louvred shutters either side.
            foreach (float sd in new[] { -1f, 1f })
            {
                float cx = sd * (hw + 0.1f + w * 0.22f), sw = w * 0.42f;
                parts.Add((shutter, Bx(sw, h + 0.1f, 0.035f, cx, 0, -0.03f)));
                for (int i = 0; i < 9; i++) parts.Add((new Color(shutter.R * 0.75f, shutter.G * 0.75f, shutter.B * 0.75f, 1f), Xf(Meshes.RoundedBox(L3(sw - 0.08f, 0.035f, 0.02f), 0.006f), 0f, L3(cx, -hh + 0.12f + i * (h - 0.2f) / 8.5f, -0.055f))));
            }
        }
        foreach (var (c, m) in parts) b.Add(c, Xf(m, a, o));
    }

    private static void DressBuilding(Scene scene, Shader shader, string tag, string title, Float3 c, Float3 s, float yaw, Color wall, Color trim, Color plate, float dw, float dh)
    {
        var b = new Bucket();
        bool house = title.StartsWith("House");
        float hw = s.X / 2f, hd = s.Z / 2f, h = s.Y, face = 0.15f;
        Color frame = new(0.95f, 0.95f, 0.93f, 1f), glass = new(0.33f, 0.47f, 0.6f, 1f), stone = new(0.78f, 0.76f, 0.7f, 1f);
        Color shutter = title switch { "House 1" => new(0.16f, 0.32f, 0.22f, 1f), "House 2" => new(0.2f, 0.25f, 0.42f, 1f), _ => new(0.45f, 0.2f, 0.18f, 1f) };
        Color lintel = house ? frame : stone;
        int floors = h > 6f ? 2 : 1;
        float ww = house ? 1.3f : 1.7f, wh = house ? 1.25f : 1.5f;
        IEnumerable<float> Span(float half, bool door) { int n = Math.Max(1, (int)((half * 2f - 2f) / 3.4f)); for (int i = 0; i < n; i++) { float x = -half + 1.6f + (half * 2f - 3.2f) * (n == 1 ? 0.5f : i / (float)(n - 1)); if (!door || MathF.Abs(x) > dw / 2f + ww / 2f + (house ? 0.9f : 0.4f)) yield return x; } }
        for (int f = 0; f < floors; f++)
        {
            float wy = 1.65f + f * 3f;
            foreach (float x in Span(hw, true)) if (title != "Garage" || x < 0f || f > 0) Window(b, 0f, L3(x, wy, -hd - face), ww, wh, house, frame, lintel, glass, shutter);
            foreach (float x in Span(hw, false)) Window(b, 180f, L3(x, wy, hd + face), ww, wh, house, frame, lintel, glass, shutter);
            foreach (float z in Span(hd, false)) { Window(b, -90f, L3(hw + face, wy, z), ww, wh, house, frame, lintel, glass, shutter); Window(b, 90f, L3(-hw - face, wy, z), ww, wh, house, frame, lintel, glass, shutter); }
        }
        if (house)
        {
            // Lap siding: overlapping boards on all four faces (front boards stop at the doorway), white corner boards, stone plinth.
            Color siding = new(wall.R * 0.96f, wall.G * 0.96f, wall.B * 0.96f, 1f);
            for (float y = 0.55f; y < h - 0.05f; y += 0.2f)
            {
                Mesh Board(float len) => Xf(Meshes.RoundedBox(L3(len, 0.19f, 0.025f), 0.006f), 0f, Float3.Zero);
                void Run(float a, Float3 o, float len) => b.Add(siding, Xf(Xf(Board(len), 0f, L3(0, 0, 0)), a, o));
                if (y < dh + 0.1f) { float side = hw - dw / 2f - 0.12f; Run(0f, L3(-hw + side / 2f, y, -hd - face - 0.012f), side); Run(0f, L3(hw - side / 2f, y, -hd - face - 0.012f), side); }
                else Run(0f, L3(0, y, -hd - face - 0.012f), s.X);
                Run(180f, L3(0, y, hd + face + 0.012f), s.X);
                Run(-90f, L3(hw + face + 0.012f, y, 0), s.Z); Run(90f, L3(-hw - face - 0.012f, y, 0), s.Z);
            }
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f })
                b.Add(frame, Bx(0.16f, h - 0.4f, 0.16f, x * (hw + face), 0.4f + (h - 0.4f) / 2f, z * (hd + face)));
            b.Add(stone, Bx(s.X + 0.36f, 0.45f, 0.08f, 0, 0.225f, -hd - face - 0.03f)); b.Add(stone, Bx(s.X + 0.36f, 0.45f, 0.08f, 0, 0.225f, hd + face + 0.03f));
            b.Add(stone, Bx(0.08f, 0.45f, s.Z + 0.36f, hw + face + 0.03f, 0.225f, 0)); b.Add(stone, Bx(0.08f, 0.45f, s.Z + 0.36f, -hw - face - 0.03f, 0.225f, 0));
            // House number plaque by the door.
            string num = title[^1..];
            b.Add(new Color(0.1f, 0.1f, 0.12f, 1f), Bx(0.3f, 0.36f, 0.03f, dw / 2f + 0.45f, 1.75f, -hd - face - 0.03f));
            b.Add(new Color(0.9f, 0.82f, 0.5f, 1f), Glyphs.Text(num, 0.22f, -hd - face - 0.06f, -hd - face - 0.045f, mirror: false) is { } nm ? Xf(nm, 0f, L3(dw / 2f + 0.45f, 1.75f, 0)) : null!);
        }
        else
        {
            // Pilasters with bases and capitals, a plinth, and a dentil course under the cornice.
            int np = Math.Max(2, (int)(s.X / 4f) + 1);
            for (int i = 0; i < np; i++)
            {
                float x = -hw + 0.25f + (s.X - 0.5f) * i / (np - 1f);
                if (MathF.Abs(x) < dw / 2f + 0.5f) continue;
                b.Add(trim, Bx(0.42f, h - 0.6f, 0.12f, x, 0.45f + (h - 0.6f) / 2f, -hd - face - 0.05f));
                b.Add(stone, Bx(0.54f, 0.3f, 0.18f, x, 0.3f, -hd - face - 0.07f)); b.Add(stone, Bx(0.54f, 0.16f, 0.18f, x, h - 0.28f, -hd - face - 0.07f));
            }
            b.Add(stone, Bx(s.X + 0.1f, 0.45f, 0.1f, 0, 0.225f, -hd - face - 0.04f));
            if (title == "Garage")
            {
                // Service bay: a ribbed roller shutter in a steel frame with a hood box, guide rails, a handle and bollards.
                float gx = hw * 0.5f + 0.35f, gw = 3.2f, gh = 3.0f, z0 = -hd - face;
                Color steelC = new(0.46f, 0.48f, 0.5f, 1f), slat = new(0.5f, 0.54f, 0.57f, 1f), slatLow = new(0.42f, 0.44f, 0.45f, 1f), dark = new(0.2f, 0.21f, 0.23f, 1f), yellow = new(0.9f, 0.72f, 0.1f, 1f);
                b.Add(dark, Bx(gw, gh, 0.05f, gx, gh / 2f, z0 - 0.01f));
                for (float y = 0.1f; y < gh - 0.05f; y += 0.16f) b.Add(y < 0.5f ? slatLow : slat, Bx(gw - 0.04f, 0.13f, 0.04f, gx, y + 0.065f, z0 - 0.05f, 0.02f));
                foreach (float sx in new[] { -1f, 1f }) b.Add(steelC, Bx(0.14f, gh + 0.1f, 0.14f, gx + sx * (gw / 2f + 0.07f), (gh + 0.1f) / 2f, z0 - 0.06f));
                b.Add(steelC, Bx(gw + 0.5f, 0.45f, 0.4f, gx, gh + 0.25f, z0 - 0.18f, 0.03f));
                b.Add(dark, Bx(0.3f, 0.04f, 0.05f, gx, 0.35f, z0 - 0.1f));
                b.Add(new Color(0.42f, 0.42f, 0.41f, 1f), Bx(gw + 1.4f, 0.05f, 4.5f, gx, 0.025f, z0 - 2.25f)); // concrete apron
                foreach (float sx in new[] { -1f, 1f })
                {
                    b.Add(yellow, Xf(Meshes.Lathe("bollard", [(0.0001f, 0f), (0.1f, 0f), (0.1f, 0.85f), (0.07f, 0.95f), (0.0001f, 0.97f)], 20), 0f, L3(gx + sx * (gw / 2f + 0.45f), 0f, z0 - 0.5f)));
                    b.Add(dark, Xf(Meshes.Lathe("bolband", [(0.102f, 0f), (0.102f, 0.1f)], 20), 0f, L3(gx + sx * (gw / 2f + 0.45f), 0.6f, z0 - 0.5f)));
                }
            }
            // Sides and back: plinth, corner quoin pilasters, a cornice band under the parapet and a belt course between floors.
            b.Add(stone, Bx(s.X + 0.1f, 0.45f, 0.1f, 0, 0.225f, hd + face + 0.04f));
            b.Add(stone, Bx(0.1f, 0.45f, s.Z + 0.1f, hw + face + 0.04f, 0.225f, 0)); b.Add(stone, Bx(0.1f, 0.45f, s.Z + 0.1f, -hw - face - 0.04f, 0.225f, 0));
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f })
            {
                b.Add(trim, Bx(0.36f, h - 0.6f, 0.36f, x * (hw + face - 0.1f), 0.45f + (h - 0.6f) / 2f, z * (hd + face - 0.1f)));
                for (float qy = 0.75f; qy < h - 0.6f; qy += 0.9f) b.Add(stone, Bx(0.42f, 0.3f, 0.42f, x * (hw + face - 0.1f), qy, z * (hd + face - 0.1f), 0.02f));
            }
            foreach (float yy in floors > 1 ? new[] { h - 0.22f, 3.05f } : new[] { h - 0.22f })
            {
                b.Add(stone, Bx(s.X + 0.5f, 0.14f, 0.14f, 0, yy, hd + face + 0.05f)); b.Add(stone, Bx(s.X + 0.5f, 0.14f, 0.14f, 0, yy, -hd - face - 0.05f));
                b.Add(stone, Bx(0.14f, 0.14f, s.Z + 0.5f, hw + face + 0.05f, yy, 0)); b.Add(stone, Bx(0.14f, 0.14f, s.Z + 0.5f, -hw - face - 0.05f, yy, 0));
            }
            // Lettered sign board over the entrance (sits just above the awning; the dentils stop either side of it).
            string word = title switch { "Police Station" => "POLICE", "General Store" => "GENERAL STORE", "Apartment" => "APARTMENTS", "Garage" => "GARAGE", _ => title.ToUpperInvariant() };
            float sw = MathF.Min(s.X - 1f, word.Length * 0.36f + 0.8f), sy = MathF.Min(dh + 1.35f, h - 0.62f);
            for (float x = -hw + 0.1f; x <= hw - 0.1f; x += 0.3f) if (MathF.Abs(x) > sw / 2f + 0.15f) b.Add(trim, Bx(0.13f, 0.12f, 0.1f, x, h - 0.32f, -hd - face - 0.08f, 0.01f));
            b.Add(plate, Bx(sw, 0.5f, 0.08f, 0, sy, -hd - face - 0.05f));
            b.Add(frame, Bx(sw + 0.08f, 0.05f, 0.1f, 0, sy + 0.27f, -hd - face - 0.05f)); b.Add(frame, Bx(sw + 0.08f, 0.05f, 0.1f, 0, sy - 0.27f, -hd - face - 0.05f));
            b.Add(frame, Xf(Glyphs.Text(word, 0.3f, -hd - face - 0.12f, -hd - face - 0.09f, mirror: false), 0f, L3(0, sy, 0)));
            if (title == "Police Station")
            {
                // Flagpole on the front lawn: lathed pole on a concrete footing, gold finial, rippled blue-and-white flag.
                float px = -hw + 2.2f, pz = -hd - face - 3.2f;
                b.Add(stone, Xf(Meshes.Lathe("pfoot", [(0.0001f, 0f), (0.45f, 0f), (0.45f, 0.12f), (0.38f, 0.18f), (0.0001f, 0.18f)], 20), 0f, L3(px, 0f, pz)));
                b.Add(new Color(0.85f, 0.86f, 0.88f, 1f), Xf(Meshes.Lathe("ppole", [(0.0001f, 0.15f), (0.07f, 0.15f), (0.06f, 3f), (0.04f, 7.6f), (0.0001f, 7.62f)], 14), 0f, L3(px, 0f, pz)));
                b.Add(new Color(0.9f, 0.72f, 0.25f, 1f), Xf(Meshes.Sphere(0.09f), 0f, L3(px, 7.68f, pz)));
                Float3 F(float u, float v) => L3(px + 0.06f + u * 1.5f, 7.45f - v * 0.95f, pz + 0.08f * MathF.Sin(u * 7f) * u);
                var bands = new[] { (0f, 0.36f, new Color(0.16f, 0.27f, 0.6f, 1f)), (0.36f, 0.5f, new Color(0.95f, 0.95f, 0.95f, 1f)), (0.5f, 1f, new Color(0.16f, 0.27f, 0.6f, 1f)) };
                foreach (var (v0, v1, bc) in bands)
                {
                    var quads = new List<Float3[]>();
                    for (int i = 0; i < 8; i++)
                    {
                        float u0 = i / 8f, u1 = (i + 1) / 8f;
                        quads.Add([F(u0, v0), F(u1, v0), F(u1, v1), F(u0, v1)]);
                        quads.Add([F(u1, v0), F(u0, v0), F(u0, v1), F(u1, v1)]); // back face
                    }
                    b.Add(bc, Meshes.Faces(quads.ToArray()));
                }
            }
        }
        Float3 eul = new(0f, yaw, 0f);
        int k = 0;
        foreach (var (col, list) in b) Shape(scene, shader, $"~{tag} facade {k++}", Meshes.Merge(list.Where(m => m is not null).ToArray()), c, col, euler: eul);
    }
}
