namespace Catalyst.SpecGraph.Nodes;

public abstract class Node
{
    public required WeakReference<Node>? Parent { get; init; }
    public required string Name { get; init; }

    public string FullName
    {
        get
        {
            if (Parent is not null && Parent.TryGetTarget(out Node? parent))
                return parent.FullName + ":" + Name;
            
            return Name;
        }
    }

    public T GetParentChecked<T>() where T : Node
    {
        if (Parent is null)
            throw new ArgumentNullException();

        Parent.TryGetTarget(out Node? parent);

        T? parentCasted = parent as T;
        if (parentCasted is null)
            throw new InvalidOperationException();
        
        return parentCasted;
    }
}