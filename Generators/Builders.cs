using System.Text;
using Catalyst.SpecGraph.Nodes;
using Catalyst.SpecGraph.Properties;

namespace Catalyst.Generators.Builders;

public static class Builder
{
    public static readonly string Default = "default";
}

public interface IDefinitionBuilder
{
    string Name { get; }
    
    string GetBuiltFileName(BuildContext context, DefinitionNode definitionNode);
    void Build(BuildContext context, DefinitionNode definitionNode);
    
    BuiltPropertyValue GetCompiledPropertyValue(IPropertyType propertyType, IPropertyValue? propertyValue)
    {
        return propertyValue is null 
            ? GetCompiledDefaultValueForPropertyType(propertyType) 
            : GetCompiledDesiredPropertyValue(propertyValue);
    }
    
    BuiltPropertyValue GetCompiledDefaultValueForPropertyType(IPropertyType propertyType);
    BuiltPropertyValue GetCompiledDesiredPropertyValue(IPropertyValue propertyValue);
    string GetCompiledClassName(DefinitionNode definitionNode);
        

    IEnumerable<BuiltFunction> BuildSerialiseFunctions(BuildContext context, DefinitionNode definitionNode);
    IEnumerable<BuiltFunction> BuildDeserialiseFunctions(BuildContext context, DefinitionNode definitionNode);
}

public interface IDefinitionBuilder<T> : IDefinitionBuilder where T : Compiler
{
    T Compiler { get; init; }
}

public interface IEnumBuilder
{
    string Name { get; }
    string GetBuiltFileName(BuildContext context, EnumNode enumNode);
    void Build(BuildContext context, EnumNode enumNode);
    string GetCompiledEnumName(EnumNode enumNode);
}

public interface IEnumBuilder<T> : IEnumBuilder where T : Compiler
{
    T Compiler { get; init; }
}

public interface IClientServiceBuilder
{
    string Name { get; }
    string GetBuiltFileName(BuildContext context, ServiceNode serviceNode);
    void Build(BuildContext context, ServiceNode serviceNode);
    
    // Not sure if I like this, but it does prove useful for more complex or prototype setups.
    // But I think this indicates that the Build process is insufficient.
    void Compile(BuiltFile file, BuiltService service, StringBuilder fileStr) {}
}

public interface IClientServiceBuilder<T> : IClientServiceBuilder where T : Compiler
{
    T Compiler { get; init; }
}

public interface IServerServiceBuilder
{
    string Name { get; }
    string GetBuiltFileName(BuildContext context, ServiceNode serviceNode);
    void Build(BuildContext context, ServiceNode serviceNode);
    
    // Not sure if I like this, but it does prove useful for more complex or prototype setups.
    // But I think this indicates that the Build process is insufficient.
    void Compile(BuiltFile file, BuiltService service, StringBuilder fileStr) {}
}

public interface IServerServiceBuilder<T> : IServerServiceBuilder where T : Compiler
{
    T Compiler { get; init; }
}

/// <summary>
/// These extensions primarily exist because C# is annoying in that it doesn't let you call
/// functions from a parent interface.
/// </summary>
public static class BuilderExtensions
{
    public static BuiltPropertyValue GetCompiledPropertyValue(this IDefinitionBuilder builder, IPropertyType propertyType, IPropertyValue? propertyValue)
    {
        return propertyValue is null 
            ? builder.GetCompiledDefaultValueForPropertyType(propertyType) 
            : builder.GetCompiledDesiredPropertyValue(propertyValue);
    }
}