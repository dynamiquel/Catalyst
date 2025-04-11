using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecGraph.PropertyTypes;

namespace Catalyst.SpecGraph;

public class CatalystGraphException : Exception
{
    
}

public class FileAlreadyAddedException : CatalystGraphException
{
    public required FileNode FileNode { get; set; }
    public override string Message => $"File Node '{FileNode.Name}' already exists in the SpecGraph";
}


public class PropertyTypeAlreadyExistsException : CatalystGraphException
{
    public required IPropertyType ExistingPropertyType { get; set; }
    public required IPropertyType NewPropertyType { get; set; }
    public override string Message
    {
        get
        {
            var msg = $"Property Type '{ExistingPropertyType.Name}' already exists in the SpecGraph";
            if (ExistingPropertyType is UserType existingUserPropertyType)
                msg += $" and is declared in '{existingUserPropertyType.OwnedFile.FullName}'";
            
            if (NewPropertyType is UserType newUserPropertyType)
                msg += $". Cannot add the new one found in '{newUserPropertyType.OwnedFile.FullName}'";

            return msg;
        }
    }
}

public class PropertyTypeNotFoundException : CatalystGraphException
{
    public required string ExpectedProperty { get; set; }
    public required Node Node { get; set; }
    public override string Message
    {
        get
        {
            var msg = $"Property Type '{ExpectedProperty}' could not be found in the SpecGraph";
            msg += $" for '{Node.FullName}'";

            return msg;
        }
    }
}

