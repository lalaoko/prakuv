
using System.Windows.Forms;

namespace MiniCad.Controls;

public class CanvasPanel : Panel
{
    public CanvasPanel()
    {
        DoubleBuffered = true;

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer,
            true
        );

        UpdateStyles();
    }
}
