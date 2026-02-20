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

public class DataTypeAlreadyExistsException : CatalystGraphException
{
    public required IDataType ExistingDataType { get; init; }
    public required IDataType NewDataType { get; init; }
    public override string Message
    {
        get
        {
            var msg = $"Data Type '{ExistingDataType.Name}' already exists in the SpecGraph";
            if (ExistingDataType is IUserDataType existingUserDataType)
                msg += $" and is declared in '{existingUserDataType.OwnedFile.FullName}'";
            
            if (NewDataType is IUserDataType newUserDataType)
                msg += $". Cannot add the new one found in '{newUserDataType.OwnedFile.FullName}'";

            return msg;
        }
    }
}

public class SimilarDataTypeAlreadyExistsException : CatalystGraphException
{
    public required IDataType ExistingDataType { get; init; }
    public required IDataType NewDataType { get; init; }
    public override string Message
    {
        get
        {
            var msg = $"Similar Data Type '{ExistingDataType.Name}' already exists in the SpecGraph";
            if (ExistingDataType is IUserDataType existingUserDataType)
                msg += $" and is declared in '{existingUserDataType.OwnedFile.FullName}'";
            
            if (NewDataType is IUserDataType newUserDataType)
                msg += $". Cannot add the new one found in '{newUserDataType.OwnedFile.FullName}'";

            return msg;
        }
    }
}

public class DataTypeNotFoundException : CatalystGraphException
{
    public required string ExpectedDataType { get; init; }
    public required Node Node { get; init; }
    public override string Message
    {
        get
        {
            var msg = $"Data Type '{ExpectedDataType}' could not be found in the SpecGraph for '{Node.FullName}'";
            return msg;
        }
    }
}

public class DataMemberTypeMismatchException : CatalystGraphException
{
    public required DataMemberNode DataMemberNode { get; init; }
    public required Type[] ExpectedDataTypes { get; init; }

    public override string Message
    {
        get
        {
            StringBuilder sb = new();
            
            sb.Append($"Unexpected Data Type '{DataMemberNode.BuiltType?.Name}' for '{DataMemberNode.FullName}'. Expected: ");
            foreach (var expectedDataType in ExpectedDataTypes)
                sb.Append($"{expectedDataType.Name}, ");
            
            return sb.ToString();
        }
    }
}

public class DataMemberValueAlreadyBuiltException : CatalystGraphException
{
    public required DataMemberNode DataMemberNode { get; init; }
    public override string Message => $"DataMember '{DataMemberNode.FullName}' value has already been built as a '{DataMemberNode.Value?.GetType().Name ?? "null"}";
}

public class InvalidDataMemberValueFormatException : CatalystGraphException
{
    public required DataMemberNode DataMemberNode { get; init; }
    public required IDataType ExpectedDataType { get; init; }
    public required object? ReceivedValue { get; init; }
    public override string Message => $"Received an unexpected value format for DataMember '{DataMemberNode.FullName}' of type '{ExpectedDataType.Name}: '{ReceivedValue}'";
}

public class InvalidEnumValueException : CatalystGraphException
{
    public required DataMemberNode DataMemberNode { get; init; }
    public required IDataType ExpectedDataType { get; init; }
    public required object? ReceivedValue { get; init; }
    public override string Message => $"Received an unexpected enum value for DataMember '{DataMemberNode.FullName}' of type '{ExpectedDataType.Name}: '{ReceivedValue}'";
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