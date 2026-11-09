using System;
using System.Drawing;

namespace MiniCad.Models;

public class EllipseShape : Shape
{
    public override void Draw(Graphics g)
    {
        using var brush = new SolidBrush(FillColor);
        using var pen = new Pen(StrokeColor, StrokeWidth);

        g.FillEllipse(brush, Bounds);
        g.DrawEllipse(pen, Bounds);

        DrawSelectionFrame(g);
    }

    public override bool ContainsPoint(Point p)
    {
        double rx = Width / 2.0;
        double ry = Height / 2.0;

        double cx = X + rx;
        double cy = Y + ry;

        double dx = p.X - cx;
        double dy = p.Y - cy;

        return (dx * dx) / (rx * rx)
             + (dy * dy) / (ry * ry) <= 1.0;
    }
}