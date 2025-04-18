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
}