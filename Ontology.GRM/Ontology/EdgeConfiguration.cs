using System.Linq.Expressions;

namespace Ontology.GRM.Ontology;

public sealed class EdgeConfiguration<T> where T : class
{
    private readonly EdgeMappingBuilder _mapping;
    internal EdgeConfiguration(EdgeMappingBuilder mapping) => _mapping = mapping;

    public EdgeConfiguration<T> From<TNode>() where TNode : class { _mapping.FromType = typeof(TNode); return this; }
    public EdgeConfiguration<T> To<TNode>() where TNode : class { _mapping.ToType = typeof(TNode); return this; }
    public EdgeConfiguration<T> From<TNode>(Expression<Func<T, object?>> property) where TNode : class
    {
        _mapping.FromType = typeof(TNode);
        _mapping.FromKey = ExpressionExtensions.GetProperty(property);
        return this;
    }

    public EdgeConfiguration<T> To<TNode>(Expression<Func<T, object?>> property) where TNode : class
    {
        _mapping.ToType = typeof(TNode);
        _mapping.ToKey = ExpressionExtensions.GetProperty(property);
        return this;
    }

    public EdgeConfiguration<T> HasKey(Expression<Func<T, object?>> property)
    {
        if (_mapping.Key is not null)
            throw new InvalidOperationException($"Edge '{typeof(T).Name}' already has a key configured.");

        _mapping.Key = ExpressionExtensions.GetProperty(property);
        return this;
    }
    public EdgeConfiguration<T> WithProperty(Expression<Func<T, object?>> property)
    {
        var selectedProperty = ExpressionExtensions.GetProperty(property);
        if (_mapping.Properties.Contains(selectedProperty))
            throw new InvalidOperationException($"Property '{selectedProperty.Name}' is already configured for edge '{typeof(T).Name}'.");

        _mapping.Properties.Add(selectedProperty);
        return this;
    }
}

internal sealed class EdgeMappingBuilder(Type clrType, string relationshipType)
{
    public Type ClrType { get; } = clrType;
    public string RelationshipType { get; } = relationshipType;
    public Type? FromType { get; set; }
    public Type? ToType { get; set; }
    public System.Reflection.PropertyInfo? FromKey { get; set; }
    public System.Reflection.PropertyInfo? ToKey { get; set; }
    public System.Reflection.PropertyInfo? Key { get; set; }
    public List<System.Reflection.PropertyInfo> Properties { get; } = [];
    public EdgeMapping Build() => new(
        ClrType,
        RelationshipType,
        FromType,
        ToType,
        FromKey,
        ToKey,
        Key,
        Properties);
}
