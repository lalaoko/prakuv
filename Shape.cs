using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.Json.Serialization;

namespace MiniCad.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(RectangleShape), "rect")]
[JsonDerivedType(typeof(EllipseShape), "ellipse")]
[JsonDerivedType(typeof(RhombusShape), "rhombus")]
[JsonDerivedType(typeof(TextBlockShape), "text")]
[JsonDerivedType(typeof(LineShape), "line")]
public abstract class Shape
{
    private int _width = 80;
    private int _height = 50;

    [Category("Позиция")]
    [Description("Координата X")]
    public int X { get; set; }

    [Category("Позиция")]
    [Description("Координата Y")]
    public int Y { get; set; }

    [Category("Размеры")]
    public int Width
    {
        get => _width;
        set => _width = Math.Max(10, value);
    }

    [Category("Размеры")]
    public int Height
    {
        get => _height;
        set => _height = Math.Max(10, value);
    }

    [JsonIgnore]
    public Color StrokeColor { get; set; } = Color.Black;

    [JsonIgnore]
    public Color FillColor { get; set; } = Color.LightSkyBlue;

    public int StrokeColorArgb
    {
        get => StrokeColor.ToArgb();
        set => StrokeColor = Color.FromArgb(value);
    }

    public int FillColorArgb
    {
        get => FillColor.ToArgb();
        set => FillColor = Color.FromArgb(value);
    }

    [Category("Внешний вид")]
    public float StrokeWidth { get; set; } = 2f;

    [Browsable(false)]
    public bool IsSelected { get; set; }

    [Browsable(false)]
    public Rectangle Bounds => new Rectangle(X, Y, Width, Height);

    public abstract void Draw(Graphics g);

    public abstract bool ContainsPoint(Point p);

    public virtual void Move(int deltaX, int deltaY)
    {
        X += deltaX;
        Y += deltaY;
    }

    protected void DrawSelectionFrame(Graphics g)
    {
        if (!IsSelected)
            return;

        using var pen = new Pen(Color.RoyalBlue, 1.5f)
        {
            DashStyle = DashStyle.Dash
        };

        g.DrawRectangle(
            pen,
            X - 3,
            Y - 3,
            Width + 6,
            Height + 6
        );
    }
}