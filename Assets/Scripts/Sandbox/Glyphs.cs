using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

/// <summary>5×7 block-letter font baked into slab meshes (signs, plates, building names). Each row's filled runs
/// become one bevelled-free slab, so a word is a handful of quads. Text lies in the XY plane between z0 and z1.</summary>
public static partial class Glyphs
{
    private static readonly Dictionary<char, string[]> F = new()
    {
        ['A'] = [".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"], ['B'] = ["####.", "#...#", "#...#", "####.", "#...#", "#...#", "####."],
        ['C'] = [".####", "#....", "#....", "#....", "#....", "#....", ".####"], ['D'] = ["####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####."],
        ['E'] = ["#####", "#....", "#....", "####.", "#....", "#....", "#####"], ['F'] = ["#####", "#....", "#....", "####.", "#....", "#....", "#...."],
        ['G'] = [".####", "#....", "#....", "#.###", "#...#", "#...#", ".###."], ['H'] = ["#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
        ['I'] = ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####"], ['K'] = ["#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#"],
        ['L'] = ["#....", "#....", "#....", "#....", "#....", "#....", "#####"], ['M'] = ["#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#"],
        ['N'] = ["#...#", "##..#", "#.#.#", "#.#.#", "#..##", "#...#", "#...#"], ['O'] = [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
        ['P'] = ["####.", "#...#", "#...#", "####.", "#....", "#....", "#...."], ['R'] = ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
        ['S'] = [".####", "#....", "#....", ".###.", "....#", "....#", "####."], ['T'] = ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
        ['U'] = ["#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."], ['V'] = ["#...#", "#...#", "#...#", "#...#", ".#.#.", ".#.#.", "..#.."],
        ['W'] = ["#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#"], ['X'] = ["#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#"], ['Y'] = ["#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.."],
        ['0'] = [".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###."], ['1'] = ["..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###."],
        ['2'] = [".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####"], ['3'] = ["####.", "....#", "....#", ".###.", "....#", "....#", "####."],
        ['!'] = ["..#..", "..#..", "..#..", "..#..", "..#..", ".....", "..#.."], ['-'] = [".....", ".....", ".....", "#####", ".....", ".....", "....."], ['$'] = ["..#..", ".####", "#.#..", ".###.", "..#.#", "####.", "..#.."],
    };

    private static Dictionary<char, (float Adv, float[] V, int[] Loops, int[] T)>? _outline;

    /// <summary>Text mesh centred on the origin, cap height h, in real Inter Bold outlines (smooth curves, counters),
    /// extruded between z0 and z1. mirror = flip X (for faces seen from the other side).</summary>
    public static Mesh Text(string text, float h, float z0, float z1, bool mirror = false)
    {
        _outline ??= Outline.ToDictionary(o => o.C, o => (o.Adv, o.V, o.Loops, o.T));
        const float track = 0.04f;
        float W(char c) => _outline.TryGetValue(char.ToUpperInvariant(c), out var g) ? g.Adv + track : 0.32f;
        float total = text.Sum(W) - track, x = -total / 2f * h;
        var faces = new List<Float3[]>();
        float sx = mirror ? -1f : 1f;
        // Caps carry an explicit ±Z normal: earcut emits sliver triangles whose computed normal would be NaN (renders black).
        var cv = new List<Float3>(); var cn = new List<Float3>(); var cuv = new List<Float2>(); var ci = new List<uint>();
        void Cap(Float3 a, Float3 b, Float3 c, Float3 n)
        {
            uint i0 = (uint)cv.Count;
            foreach (Float3 q in (Float3[])[a, b, c]) { cv.Add(q); cn.Add(n); cuv.Add(new Float2(q.X, q.Y)); }
            ci.Add(i0); ci.Add(i0 + 1); ci.Add(i0 + 2);
        }
        Float3 P(float gx, float gy, float z) => new(sx * (x + gx * h), gy * h - h / 2f, z);
        foreach (char ch in text)
        {
            if (_outline.TryGetValue(char.ToUpperInvariant(ch), out var g))
            {
                float[] v = g.V;
                for (int t = 0; t + 2 < g.T.Length; t += 3)
                {
                    int a = g.T[t], b = g.T[t + 1], c = g.T[t + 2];
                    Float3 fa = P(v[a * 2], v[a * 2 + 1], z1), fb = P(v[b * 2], v[b * 2 + 1], z1), fc = P(v[c * 2], v[c * 2 + 1], z1);
                    // earcut may emit either winding: orient the cap to face +Z (front) / -Z (back)
                    bool ccw = (fb.X - fa.X) * (fc.Y - fa.Y) - (fb.Y - fa.Y) * (fc.X - fa.X) > 0f;
                    if (!ccw) (fb, fc) = (fc, fb);
                    Cap(fa, fb, fc, new Float3(0f, 0f, 1f));
                    Cap(new Float3(fa.X, fa.Y, z0), new Float3(fc.X, fc.Y, z0), new Float3(fb.X, fb.Y, z0), new Float3(0f, 0f, -1f));
                }
                for (int l = 0; l + 1 < g.Loops.Length; l += 2)
                {
                    int s0 = g.Loops[l], n = g.Loops[l + 1];
                    for (int k = 0; k < n; k++)
                    {
                        int ia = s0 + k, ib = s0 + (k + 1) % n;
                        if (mirror) (ia, ib) = (ib, ia);
                        Float3 a0 = P(v[ia * 2], v[ia * 2 + 1], z0), b0 = P(v[ib * 2], v[ib * 2 + 1], z0);
                        faces.Add([a0, b0, new Float3(b0.X, b0.Y, z1), new Float3(a0.X, a0.Y, z1)]);
                    }
                }
            }
            x += W(ch) * h;
        }
        // Walls: skip zero-length edges (duplicate outline points) for the same reason.
        faces.RemoveAll(f => Float3.Length(f[1] - f[0]) < 1e-5f);
        return Meshes.Merge(Meshes.Build(cv, cn, cuv, ci), Meshes.Faces(faces.ToArray()));
    }

    /// <summary>The old 5×7 block font (kept for pixel-style displays).</summary>
    public static Mesh Pixel(string text, float h, float z0, float z1, bool mirror = false)
    {
        float px = h / 7f, adv = 6f * px, x0 = -(text.Length * adv - px) / 2f;
        var faces = new List<Float3[]>();
        for (int c = 0; c < text.Length; c++)
        {
            if (!F.TryGetValue(char.ToUpperInvariant(text[c]), out var g)) continue;
            for (int row = 0; row < 7; row++)
            {
                int col = 0;
                while (col < 5)
                {
                    if (g[row][col] != '#') { col++; continue; }
                    int e = col; while (e < 5 && g[row][e] == '#') e++;
                    float xa = x0 + c * adv + col * px, xb = x0 + c * adv + e * px, ya = h / 2f - (row + 1) * px, yb = ya + px;
                    if (mirror) (xa, xb) = (-xb, -xa);
                    Box(faces, xa, ya, xb, yb, z0, z1);
                    col = e;
                }
            }
        }
        return Meshes.Faces(faces.ToArray());
    }

    private static void Box(List<Float3[]> f, float x0, float y0, float x1, float y1, float z0, float z1)
    {
        Float3 P(float x, float y, float z) => new(x, y, z);
        f.Add([P(x0, y0, z1), P(x1, y0, z1), P(x1, y1, z1), P(x0, y1, z1)]);
        f.Add([P(x1, y0, z0), P(x0, y0, z0), P(x0, y1, z0), P(x1, y1, z0)]);
        f.Add([P(x0, y1, z0), P(x0, y1, z1), P(x1, y1, z1), P(x1, y1, z0)]);
        f.Add([P(x0, y0, z1), P(x0, y0, z0), P(x1, y0, z0), P(x1, y0, z1)]);
        f.Add([P(x1, y0, z1), P(x1, y0, z0), P(x1, y1, z0), P(x1, y1, z1)]);
        f.Add([P(x0, y0, z0), P(x0, y0, z1), P(x0, y1, z1), P(x0, y1, z0)]);
    }
}
