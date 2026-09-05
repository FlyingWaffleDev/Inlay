using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Inlay;

internal static class DragReorder
{
    public static bool IsInButton(object? source, string styleClass) =>
        source is Visual visual &&
        (visual as Button ?? visual.FindAncestorOfType<Button>()) is { } button &&
        button.Classes.Contains(styleClass);

    public static TContainer? FindContainer<TContainer>(object? source)
        where TContainer : Control =>
        source is Visual visual
            ? visual as TContainer ?? visual.FindAncestorOfType<TContainer>()
            : null;

    // A slot past the item being moved lands one index earlier once that item
    // leaves its current place.
    public static int SlotToIndex(int targetSlot, int sourceIndex, int count) =>
        Math.Clamp(targetSlot > sourceIndex ? targetSlot - 1 : targetSlot, 0, count - 1);

    public static int DropSlot(
        ItemsControl items,
        int count,
        Point position,
        Orientation orientation)
    {
        var pointer = orientation == Orientation.Horizontal ? position.X : position.Y;
        var lastRealizedIndex = -1;
        for (var index = 0; index < count; index++)
        {
            if (ContainerExtent(items, items, index, orientation) is not { } extent)
            {
                continue;
            }

            lastRealizedIndex = index;
            if (pointer < extent.Start + extent.Size / 2)
            {
                return index;
            }
        }

        return Math.Min(lastRealizedIndex + 1, count);
    }

    // Where a slot's drop indicator belongs along the axis, or null when neither
    // the slot nor the item ahead of it has a realized container.
    public static double? SlotOffset(
        ItemsControl items,
        int count,
        int slot,
        Orientation orientation,
        Visual? relativeTo = null)
    {
        var origin = relativeTo ?? items;
        if (slot < count && ContainerExtent(items, origin, slot, orientation) is { } extent)
        {
            return extent.Start;
        }

        if (slot > 0 && ContainerExtent(items, origin, slot - 1, orientation) is { } previous)
        {
            return previous.Start + previous.Size;
        }

        return null;
    }

    private static (double Start, double Size)? ContainerExtent(
        ItemsControl items,
        Visual relativeTo,
        int index,
        Orientation orientation)
    {
        if (items.ContainerFromIndex(index) is not Control container ||
            container.TranslatePoint(new Point(0, 0), relativeTo) is not { } origin)
        {
            return null;
        }

        return orientation == Orientation.Horizontal
            ? (origin.X, container.Bounds.Width)
            : (origin.Y, container.Bounds.Height);
    }
}

// Tracks the item a pointer press landed on until the pointer has travelled far
// enough to mean a drag rather than a click.
internal sealed class DragCandidate<TItem, TContainer>
    where TItem : class
    where TContainer : Control
{
    private const double DragThreshold = 6;

    private (TItem Item, TContainer Container, PointerPressedEventArgs Trigger, Point Origin)?
        _pending;

    public void Arm(TItem item, TContainer container, PointerPressedEventArgs trigger, Point origin) =>
        _pending = (item, container, trigger, origin);

    public void Clear() => _pending = null;

    public (TItem Item, TContainer Container, PointerPressedEventArgs Trigger)? TryStart(Point position)
    {
        if (_pending is not { } pending)
        {
            return null;
        }

        var delta = position - pending.Origin;
        if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
        {
            return null;
        }

        _pending = null;
        return (pending.Item, pending.Container, pending.Trigger);
    }
}
