using Prowl.Quill;
using Prowl.Vector;

namespace Sandbox;

public enum Icon
{
    Cube, Sphere, Plank, Barrel, Crate, Ramp, Grid, Bolt, Wrench, Sliders, Search, Gear, Play, Power, Back,
    Magnet, Link, Balloon, Trash, Undo, Pause, Keyboard, Folder, Check, Pistol, Car, Save, Load, Eye,
}

/// <summary>Stroke-based vector icons drawn with Quill on a 24x24 grid (1.75px strokes, round caps).</summary>
public static class Icons
{
    public static void Draw(Canvas c, Icon icon, float x, float y, float size, Color32 color)
    {
        float s = size / 24f;
        float X(float v) => x + v * s;
        float Y(float v) => y + v * s;
        c.SaveState();
        c.SetStrokeColor(color);
        c.SetStrokeWidth(MathF.Max(1.2f, 1.75f * s));
        c.SetStrokeCap(EndCapStyle.Round);
        c.SetStrokeJoint(JointStyle.Round);

        void Line(params float[] p)
        {
            c.BeginPath();
            c.MoveTo(X(p[0]), Y(p[1]));
            for (int i = 2; i < p.Length; i += 2) c.LineTo(X(p[i]), Y(p[i + 1]));
            c.Stroke();
        }
        void Closed(params float[] p)
        {
            c.BeginPath();
            c.MoveTo(X(p[0]), Y(p[1]));
            for (int i = 2; i < p.Length; i += 2) c.LineTo(X(p[i]), Y(p[i + 1]));
            c.ClosePath();
            c.Stroke();
        }
        void Circle(float cx, float cy, float r)
        {
            c.BeginPath();
            c.Circle(X(cx), Y(cy), r * s, 32);
            c.Stroke();
        }
        void Dot(float cx, float cy, float r) => c.CircleFilled(X(cx), Y(cy), r * s, color, 16);
        void Box(float bx, float by, float bw, float bh, float r)
        {
            c.BeginPath();
            c.RoundedRect(X(bx), Y(by), bw * s, bh * s, r * s);
            c.Stroke();
        }

        switch (icon)
        {
            case Icon.Cube:
            case Icon.Crate:
                Closed(12, 3, 20, 7.5f, 20, 16.5f, 12, 21, 4, 16.5f, 4, 7.5f);
                Line(4, 7.5f, 12, 12, 20, 7.5f);
                Line(12, 12, 12, 21);
                if (icon == Icon.Crate) Line(8, 9.8f, 8, 18.8f);
                break;
            case Icon.Sphere:
                Circle(12, 12, 8.5f);
                c.BeginPath(); c.Ellipse(X(12), Y(12), 8.5f * s, 3.2f * s, 32); c.Stroke();
                break;
            case Icon.Plank:
                Closed(3, 14, 17, 7, 21, 9, 7, 16);
                Line(3, 14, 3, 16, 7, 18, 21, 11, 21, 9);
                Line(7, 16, 7, 18);
                break;
            case Icon.Barrel:
                c.BeginPath(); c.Ellipse(X(12), Y(5.5f), 6.5f * s, 2.5f * s, 32); c.Stroke();
                Line(5.5f, 5.5f, 5.5f, 18.5f);
                Line(18.5f, 5.5f, 18.5f, 18.5f);
                c.BeginPath(); c.Arc(X(12), Y(18.5f), 6.5f * s, 0f, MathF.PI, false); c.Stroke();
                Line(5.5f, 12, 18.5f, 12);
                break;
            case Icon.Ramp:
                Closed(3, 19, 21, 19, 21, 7);
                Line(9, 19, 9, 15);
                break;
            case Icon.Grid:
                Box(3.5f, 3.5f, 7, 7, 1.5f); Box(13.5f, 3.5f, 7, 7, 1.5f);
                Box(3.5f, 13.5f, 7, 7, 1.5f); Box(13.5f, 13.5f, 7, 7, 1.5f);
                break;
            case Icon.Bolt:
                Closed(13, 2.5f, 5, 13.5f, 11.5f, 13.5f, 10.5f, 21.5f, 19, 10, 12.5f, 10);
                break;
            case Icon.Wrench:
                Line(14.5f, 9.5f, 4.5f, 19.5f);
                c.BeginPath(); c.Arc(X(16.5f), Y(7.5f), 4.5f * s, MathF.PI * 0.75f, MathF.PI * 2.25f, false); c.Stroke();
                break;
            case Icon.Sliders:
                Line(4, 7, 20, 7); Line(4, 17, 20, 17);
                Dot(9, 7, 2.6f); Dot(15, 17, 2.6f);
                break;
            case Icon.Search:
                Circle(10.5f, 10.5f, 6.5f);
                Line(15.5f, 15.5f, 20.5f, 20.5f);
                break;
            case Icon.Gear:
                Circle(12, 12, 3.2f);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * MathF.PI / 4f;
                    Line(12 + MathF.Cos(a) * 6.2f, 12 + MathF.Sin(a) * 6.2f, 12 + MathF.Cos(a) * 9f, 12 + MathF.Sin(a) * 9f);
                }
                Circle(12, 12, 6.2f);
                break;
            case Icon.Play:
                Closed(7, 4.5f, 19, 12, 7, 19.5f);
                break;
            case Icon.Power:
                c.BeginPath(); c.Arc(X(12), Y(13), 7.5f * s, -MathF.PI * 0.32f, MathF.PI * 1.32f, false); c.Stroke();
                Line(12, 3, 12, 11);
                break;
            case Icon.Back:
                Line(15, 5, 8, 12, 15, 19);
                break;
            case Icon.Magnet:
                c.BeginPath(); c.Arc(X(12), Y(11), 6.5f * s, 0f, MathF.PI, false); c.Stroke();
                Line(5.5f, 11, 5.5f, 4); Line(18.5f, 11, 18.5f, 4);
                Line(4, 6.5f, 8, 6.5f); Line(16, 6.5f, 20, 6.5f);
                break;
            case Icon.Link:
                Box(2.5f, 8.5f, 11, 7, 3.5f); Box(10.5f, 8.5f, 11, 7, 3.5f);
                break;
            case Icon.Balloon:
                c.BeginPath(); c.Ellipse(X(12), Y(9), 6 * s, 7 * s, 32); c.Stroke();
                Line(12, 16, 11, 18.5f, 13, 20, 12, 22);
                break;
            case Icon.Trash:
                Line(4, 6.5f, 20, 6.5f);
                Line(9, 6.5f, 9.5f, 3.5f, 14.5f, 3.5f, 15, 6.5f);
                Closed(6, 6.5f, 7, 20.5f, 17, 20.5f, 18, 6.5f);
                Line(10, 10, 10, 17); Line(14, 10, 14, 17);
                break;
            case Icon.Undo:
                Line(8, 5, 4, 9, 8, 13);
                c.BeginPath(); c.MoveTo(X(4), Y(9)); c.LineTo(X(14), Y(9));
                c.Arc(X(14), Y(14), 5 * s, -MathF.PI / 2f, MathF.PI / 2f, false); c.LineTo(X(9), Y(19)); c.Stroke();
                break;
            case Icon.Pause:
                Line(9, 5, 9, 19); Line(15, 5, 15, 19);
                break;
            case Icon.Keyboard:
                Box(2.5f, 6, 19, 12, 2.5f);
                for (int i = 0; i < 5; i++) Dot(6 + i * 3, 10, 0.9f);
                Line(8, 14.5f, 16, 14.5f);
                break;
            case Icon.Folder:
                Closed(3, 6, 9.5f, 6, 11.5f, 8.5f, 21, 8.5f, 21, 19, 3, 19);
                break;
            case Icon.Check:
                Line(5, 12.5f, 10, 17.5f, 19, 7);
                break;
            case Icon.Pistol:
                Closed(3, 7, 21, 7, 21, 11, 11, 11, 10, 19, 6, 19, 7, 11, 3, 11);
                break;
            case Icon.Car:
                Closed(3, 16, 3, 12, 6, 7.5f, 16, 7.5f, 19.5f, 12, 21, 12.5f, 21, 16);
                Circle(7.5f, 16.5f, 2.2f); Circle(16.5f, 16.5f, 2.2f);
                break;
            case Icon.Save:
                Closed(4, 4, 17, 4, 20, 7, 20, 20, 4, 20);
                Box(8, 13, 8, 7, 1);
                Line(8, 4, 8, 8, 15, 8, 15, 4);
                break;
            case Icon.Load:
                Line(12, 3.5f, 12, 15); Line(7, 10, 12, 15, 17, 10);
                Line(4, 17, 4, 20, 20, 20, 20, 17);
                break;
            case Icon.Eye:
                c.BeginPath(); c.Ellipse(X(12), Y(12), 9 * s, 5.5f * s, 32); c.Stroke();
                Circle(12, 12, 2.5f);
                break;
        }
        c.RestoreState();
    }

    public static Icon For(PropKind kind) => kind switch
    {
        PropKind.Cube => Icon.Cube,
        PropKind.Sphere => Icon.Sphere,
        PropKind.Plank => Icon.Plank,
        PropKind.Barrel => Icon.Barrel,
        PropKind.BigCrate => Icon.Crate,
        PropKind.Ramp => Icon.Ramp,
        _ => Icon.Cube,
    };

    public static Icon For(ToolMode mode) => mode switch
    {
        ToolMode.Weld => Icon.Link,
        ToolMode.Balloon => Icon.Balloon,
        ToolMode.Remover => Icon.Trash,
        _ => Icon.Wrench,
    };
}
