using System.Drawing;
using System.Drawing.Drawing2D;

namespace MiniCad.Models;

public class RhombusShape : Shape
{
    private Point[] GetVertices()
    {
        return new Point[]
        {
            new Point(X + Width / 2, Y),
            new Point(X + Width, Y + Height / 2),
            new Point(X + Width / 2, Y + Height),
            new Point(X, Y + Height / 2)
        };
    }

    public override void Draw(Graphics g)
    {
        using var brush = new SolidBrush(FillColor);
        using var pen = new Pen(StrokeColor, StrokeWidth);

        Point[] points = GetVertices();

        g.FillPolygon(brush, points);
        g.DrawPolygon(pen, points);

        DrawSelectionFrame(g);
    }

    public override bool ContainsPoint(Point p)
    {
        using var path = new GraphicsPath();
        path.AddPolygon(GetVertices());

        return path.IsVisible(p);
    }
}