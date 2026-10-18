using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

/// <summary>5×7 block-letter font baked into slab meshes (signs, plates, building names). Each row's filled runs
/// become one bevelled-free slab, so a word is a handful of quads. Text lies in the XY plane between z0 and z1.</summary>
public static class Glyphs
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
        ['W'] = ["#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#"], ['Y'] = ["#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.."],
        ['0'] = [".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###."], ['1'] = ["..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###."],
        ['2'] = [".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####"], ['3'] = ["####.", "....#", "....#", ".###.", "....#", "....#", "####."],
        ['$'] = ["..#..", ".####", "#.#..", ".###.", "..#.#", "####.", "..#.."],
    };

    /// <summary>Text mesh centred on the origin, cap height h. mirror = flip X (for faces seen from the other side).</summary>
    public static Mesh Text(string text, float h, float z0, float z1, bool mirror = false)
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
