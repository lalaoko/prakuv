using System.Drawing;
using System.ComponentModel;

namespace MiniCad.Models;

public class TextBlockShape : Shape
{
    [Category("Текст")]
    [Description("Текст внутри фигуры")]
    public string Text { get; set; } = "Текст";

    [Category("Текст")]
    public float FontSize { get; set; } = 12f;

    public override void Draw(Graphics g)
    {
        using var brush = new SolidBrush(FillColor);
        using var pen = new Pen(StrokeColor, StrokeWidth);
        using var font = new Font("Arial", FontSize);
        using var textBrush = new SolidBrush(Color.Black);

        g.FillRectangle(brush, Bounds);
        g.DrawRectangle(pen, Bounds);

        var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        g.DrawString(Text, font, textBrush, Bounds, format);

        DrawSelectionFrame(g);
    }

    public override bool ContainsPoint(Point p)
    {
        return Bounds.Contains(p);
    }
}