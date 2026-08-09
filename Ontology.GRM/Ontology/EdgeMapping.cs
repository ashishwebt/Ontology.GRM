using System.Reflection;

namespace Ontology.GRM.Ontology;

public sealed class EdgeMapping
{
    private readonly IReadOnlyDictionary<string, PropertyMapping> _properties;

    internal EdgeMapping(
        Type clrType,
        string relationshipType,
        Type? fromType,
        Type? toType,
        PropertyInfo? fromKey,
        PropertyInfo? toKey,
        PropertyInfo? key,
        IEnumerable<PropertyInfo> properties)
    {
        ClrType = clrType;
        RelationshipType = relationshipType;
        FromType = fromType;
        ToType = toType;
        FromKey = fromKey is null ? null : new PropertyMapping(fromKey);
        ToKey = toKey is null ? null : new PropertyMapping(toKey);
        Key = key is null ? null : new PropertyMapping(key);
        _properties = properties.Distinct().ToDictionary(property => property.Name, property => new PropertyMapping(property));
    }

    public Type ClrType { get; }
    public string RelationshipType { get; }
    public Type? FromType { get; }
    public Type? ToType { get; }
    public PropertyMapping? FromKey { get; }
    public PropertyMapping? ToKey { get; }
    public PropertyMapping? Key { get; }
    public IReadOnlyCollection<PropertyMapping> Properties => _properties.Values.ToArray();
}
