using System.Linq.Expressions;

namespace Ontology.GRM.Ontology;

public sealed class NodeConfiguration<T> where T : class
{
    private readonly NodeMappingBuilder _mapping;
    internal NodeConfiguration(NodeMappingBuilder mapping) => _mapping = mapping;

    public NodeConfiguration<T> HasKey(Expression<Func<T, object?>> property)
    {
        if (_mapping.Key is not null)
            throw new InvalidOperationException($"Node '{typeof(T).Name}' already has a key configured.");

        _mapping.Key = ExpressionExtensions.GetProperty(property);
        return this;
    }

    public NodeConfiguration<T> HasProperty(Expression<Func<T, object?>> property)
    {
        var selectedProperty = ExpressionExtensions.GetProperty(property);
        if (_mapping.Properties.Contains(selectedProperty))
            throw new InvalidOperationException($"Property '{selectedProperty.Name}' is already configured for node '{typeof(T).Name}'.");

        _mapping.Properties.Add(selectedProperty);
        return this;
    }
}

internal sealed class NodeMappingBuilder(Type clrType, string label)
{
    public Type ClrType { get; } = clrType;
    public string Label { get; } = label;
    public System.Reflection.PropertyInfo? Key { get; set; }
    public List<System.Reflection.PropertyInfo> Properties { get; } = [];
    public NodeMapping Build() => new(ClrType, Label, Key, Properties);
}
