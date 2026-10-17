using System.Windows;
using System.Windows.Controls;

namespace RBX15.App.Controls;

/// <summary>
/// One row of the asset grid: three square cells with a 4 DIP gap, flush at both ends (grid-template-columns:
/// repeat(3, 1fr); gap 4px, editor.html:305; aspect-ratio 1, editor.js:2470). The cell side comes from the width the
/// row is measured with, so a row knows its height in its first measure: the virtualizing list sizes and realizes
/// rows from real heights, never from zero-height rows waiting for an ActualWidth binding.
/// </summary>
public sealed class AssetRowPanel : Panel
{
    public const int Columns = 3;

    public const double Gap = 4;

    /// <summary>The side of a cell in a row <paramref name="width"/> DIPs wide.</summary>
    public static double CellSide(double width) => double.IsFinite(width) ? Math.Max(0, (width - Gap * (Columns - 1)) / Columns) : 0;

    protected override Size MeasureOverride(Size availableSize)
    {
        double side = CellSide(availableSize.Width);
        foreach (UIElement child in InternalChildren) child.Measure(new Size(side, side));
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : 0, InternalChildren.Count > 0 ? side : 0);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double side = CellSide(finalSize.Width);
        for (int i = 0; i < InternalChildren.Count; i++)
            InternalChildren[i].Arrange(new Rect(i % Columns * (side + Gap), i / Columns * (side + Gap), side, side));
        return finalSize;
    }
}
