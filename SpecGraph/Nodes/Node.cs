using System.Text;

namespace Catalyst.SpecGraph.Nodes;

public abstract class Node
{
    public required Node? Parent { get; init; }
    public required string Name { get; init; }

    public string FullName
    {
        get
        {
            if (Parent is not null)
                return Parent.FullName + ":" + Name;
            
            return Name;
        }
    }
}