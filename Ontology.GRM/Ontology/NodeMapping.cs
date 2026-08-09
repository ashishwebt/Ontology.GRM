using System.Reflection;

namespace Ontology.GRM.Ontology;

public sealed class NodeMapping
{
    private readonly IReadOnlyDictionary<string, PropertyMapping> _properties;

    internal NodeMapping(Type clrType, string label, PropertyInfo? key, IEnumerable<PropertyInfo> properties)
    {
        ClrType = clrType;
        Label = label;
        Key = key is null ? null : new PropertyMapping(key);
        _properties = properties.Distinct().ToDictionary(property => property.Name, property => new PropertyMapping(property));
    }

    public Type ClrType { get; }
    public string Label { get; }
    public PropertyMapping? Key { get; }
    public IReadOnlyCollection<PropertyMapping> Properties => _properties.Values.ToArray();
}
