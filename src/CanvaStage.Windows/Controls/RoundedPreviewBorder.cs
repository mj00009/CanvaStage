using System.Windows;
using System.Windows.Media;
using Border = System.Windows.Controls.Border;
using Size = System.Windows.Size;

namespace CanvaStage.Windows.Controls;

/// <summary>Clips only the operator preview, without modifying the shared output visual.</summary>
public sealed class RoundedPreviewBorder : Border
{
    protected override Size ArrangeOverride(Size finalSize)
    {
        Size arranged = base.ArrangeOverride(finalSize);
        if (Child is not null)
        {
            double radius = Math.Max(0, CornerRadius.TopLeft - BorderThickness.Left);
            RectangleGeometry clip = new(new Rect(Child.RenderSize), radius, radius);
            clip.Freeze();
            Child.Clip = clip;
        }
        return arranged;
    }
}
