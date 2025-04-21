using System.Text;
using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecGraph.Properties;

namespace Catalyst.SpecGraph;

public class CatalystGraphException : Exception
{
    
}

public class FileAlreadyAddedException : CatalystGraphException
{
    public required FileNode FileNode { get; init; }
    public override string Message => $"File Node '{FileNode.Name}' already exists in the SpecGraph";
}

public class GraphAlreadyBuiltException : CatalystGraphException
{
    public override string Message => "The Graph has already been built. No modifications can be made";
}

public class PropertyTypeAlreadyExistsException : CatalystGraphException
{
    public required IPropertyType ExistingPropertyType { get; init; }
    public required IPropertyType NewPropertyType { get; init; }
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
    public required IPropertyType ExistingPropertyType { get; init; }
    public required IPropertyType NewPropertyType { get; init; }
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
    public required string ExpectedProperty { get; init; }
    public required Node Node { get; init; }
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
    public required PropertyNode Node { get; init; }
    public required Type[] ExpectedPropertyTypes { get; init; }

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

public class PropertyValueAlreadyBuiltException : CatalystGraphException
{
    public required PropertyNode PropertyNode { get; init; }
    public override string Message => $"Property '{PropertyNode.FullName}' value has already been built as a '{PropertyNode.Value?.GetType().Name}";
}

public class InvalidPropertyValueFormatException : CatalystGraphException
{
    public required PropertyNode PropertyNode { get; init; }
    public required IPropertyType ExpectedPropertyType { get; init; }
    public required object? ReceivedValue { get; init; }
    public override string Message => $"Received an unexpected value format for Property '{PropertyNode.FullName}' of type '{ExpectedPropertyType.Name}: '{ReceivedValue}'";
}

public class SimilarServiceAlreadyExistsException : CatalystGraphException
{
    public required ServiceNode ExistingService { get; init; }
    public required ServiceNode NewService { get; init; }
    public override string Message  => 
        $"Similar Service '{ExistingService.Name}' already exists in the SpecGraph under '{ExistingService.FullName}'. Cannot add the new one found under '{NewService.FullName}'";
}

public class SimilarEndpointAlreadyExistsException : CatalystGraphException
{
    public required EndpointNode ExistingEndpoint { get; init; }
    public required EndpointNode NewEndpoint { get; init; }
    public override string Message  => 
        $"Similar Endpoint with Path '{ExistingEndpoint.Path}' already exists in the SpecGraph under '{ExistingEndpoint.FullName}'. Cannot add the new one found under '{NewEndpoint.FullName}'";
}