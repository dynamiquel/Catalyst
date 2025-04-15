using System.Text;
using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecGraph.Properties;

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
            if (ExistingPropertyType is ObjectType existingUserPropertyType)
                msg += $" and is declared in '{existingUserPropertyType.OwnedFile.FullName}'";
            
            if (NewPropertyType is ObjectType newUserPropertyType)
                msg += $". Cannot add the new one found in '{newUserPropertyType.OwnedFile.FullName}'";

            return msg;
        }
    }
}

public class SimilarPropertyTypeAlreadyExistsException : CatalystGraphException
{
    public required IPropertyType ExistingPropertyType { get; set; }
    public required IPropertyType NewPropertyType { get; set; }
    public override string Message
    {
        get
        {
            var msg = $"Similar Property Type '{ExistingPropertyType.Name}' already exists in the SpecGraph";
            if (ExistingPropertyType is ObjectType existingUserPropertyType)
                msg += $" and is declared in '{existingUserPropertyType.OwnedFile.FullName}'";
            
            if (NewPropertyType is ObjectType newUserPropertyType)
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
            var msg = $"Property Type '{ExpectedProperty}' could not be found in the SpecGraph for '{Node.FullName}'";
            return msg;
        }
    }
}

public class PropertyTypeMismatchException : CatalystGraphException
{
    public required PropertyNode Node { get; set; }
    public required Type[] ExpectedPropertyTypes { get; set; }

    public override string Message
    {
        get
        {
            StringBuilder sb = new();
            
            sb.Append($"Unexpected Property Type '{Node.BuiltType!.Name}' for '{Node.FullName}'. Expected: ");
            foreach (var expectedPropertyType in ExpectedPropertyTypes)
                sb.Append($"{expectedPropertyType.Name}, ");
            
            return sb.ToString();
        }
    }
}