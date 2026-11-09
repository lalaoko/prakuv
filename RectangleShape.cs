using System.Drawing;

namespace MiniCad.Models;

public class RectangleShape : Shape
{
    public override void Draw(Graphics g)
    {
        using var brush = new SolidBrush(FillColor);
        using var pen = new Pen(StrokeColor, StrokeWidth);

        g.FillRectangle(brush, Bounds);
        g.DrawRectangle(pen, Bounds);
        DrawSelectionFrame(g);
    }

    public override bool ContainsPoint(Point p)
    {
        return Bounds.Contains(p);
    }
}