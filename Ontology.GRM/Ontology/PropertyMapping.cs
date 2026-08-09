using System.Reflection;

namespace Ontology.GRM.Ontology;

public sealed class PropertyMapping
{
    internal PropertyMapping(PropertyInfo property) => Property = property;

    public PropertyInfo Property { get; }
    public string Name => Property.Name;
}
