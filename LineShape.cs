using System;
using System.Drawing;

namespace MiniCad.Models;

public class LineShape : Shape
{
    public override void Draw(Graphics g)
    {
        using var pen = new Pen(StrokeColor, StrokeWidth);

        g.DrawLine(
            pen,
            X,
            Y,
            X + Width,
            Y + Height
        );

        DrawSelectionFrame(g);
    }

    public override bool ContainsPoint(Point p)
    {
        double x1 = X;
        double y1 = Y;
        double x2 = X + Width;
        double y2 = Y + Height;

        double dx = x2 - x1;
        double dy = y2 - y1;

        double lengthSquared = dx * dx + dy * dy;

        if (lengthSquared == 0)
            return false;

        double t = ((p.X - x1) * dx + (p.Y - y1) * dy)
                   / lengthSquared;

        t = Math.Max(0, Math.Min(1, t));

        double nearestX = x1 + t * dx;
        double nearestY = y1 + t * dy;

        double distanceX = p.X - nearestX;
        double distanceY = p.Y - nearestY;

        return distanceX * distanceX + distanceY * distanceY <= 100;
    }
}