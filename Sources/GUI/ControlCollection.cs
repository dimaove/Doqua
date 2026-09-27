using System.Collections.ObjectModel;

namespace Doqua.GUI;

/// <summary>Children of a container. Keeps <see cref="Control.Parent"/> in sync.</summary>
public sealed class ControlCollection : Collection<Control>
{
    private readonly Control _owner;

    internal ControlCollection(Control owner) => _owner = owner;

    protected override void InsertItem(int index, Control item)
    {
        Attach(item);
        base.InsertItem(index, item);
        item.ApplyAnchor();
        _owner.OnChildLayoutChanged();
        _owner.Invalidate();
    }

    protected override void SetItem(int index, Control item)
    {
        var old = this[index];
        if (ReferenceEquals(old, item))
            return;
        Attach(item);
        old.Parent = null;
        base.SetItem(index, item);
        item.ApplyAnchor();
        OnRemoved();
    }

    protected override void RemoveItem(int index)
    {
        this[index].Parent = null;
        base.RemoveItem(index);
        OnRemoved();
    }

    protected override void ClearItems()
    {
        foreach (var child in this)
            child.Parent = null;
        base.ClearItems();
        OnRemoved();
    }

    private void OnRemoved()
    {
        _owner.OnChildLayoutChanged();
        _owner.Invalidate();
        _owner.GetWindow()?.ValidateFocus(); // The focused control may have been removed.
    }

    private void Attach(Control item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Parent != null || item.Host != null)
            throw new InvalidOperationException("The control already has a parent.");
        for (var ancestor = _owner; ancestor != null; ancestor = ancestor.Parent)
        {
            if (ReferenceEquals(ancestor, item))
                throw new InvalidOperationException("A control cannot be added to itself or its descendant.");
        }
        item.Parent = _owner;
    }
}
