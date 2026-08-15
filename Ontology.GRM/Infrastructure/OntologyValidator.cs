using Ontology.GRM.Ontology;

namespace Ontology.GRM.Infrastructure;

/// <summary>Performs runtime validation of immutable graph mapping metadata.</summary>
public sealed class OntologyValidator
{
    public void Validate(OntologyModel ontology)
    {
        ArgumentNullException.ThrowIfNull(ontology);

        EnsureUnique(ontology.Nodes, node => node.Label, "node label");
        EnsureUnique(ontology.Edges, edge => edge.RelationshipType, "relationship type");

        foreach (var node in ontology.Nodes)
        {
            if (node.Key is null)
                throw new InvalidOperationException($"Invalid ontology: node '{node.ClrType.Name}' has no key. Configure one with HasKey(...).");

            ValidateProperty(node.ClrType, node.Key.Name, "key");
            foreach (var property in node.Properties)
                ValidateProperty(node.ClrType, property.Name, "property");
        }

        foreach (var edge in ontology.Edges)
        {
            if (edge.FromType is null || edge.ToType is null)
                throw new InvalidOperationException($"Invalid ontology: edge '{edge.ClrType.Name}' must define both endpoints with From<T>() and To<T>().");

            EnsureNodeExists(ontology, edge.FromType, edge.ClrType, "source");
            EnsureNodeExists(ontology, edge.ToType, edge.ClrType, "target");
            if (edge.FromKey is null)
                throw new InvalidOperationException($"Invalid ontology: edge '{edge.ClrType.Name}' has no source key. Configure From<TNode>(...).");
            if (edge.ToKey is null)
                throw new InvalidOperationException($"Invalid ontology: edge '{edge.ClrType.Name}' has no target key. Configure To<TNode>(...).");
            ValidateOptionalProperty(edge.ClrType, edge.FromKey?.Name, "source key");
            ValidateOptionalProperty(edge.ClrType, edge.ToKey?.Name, "target key");
            ValidateEndpointKeyType(ontology, edge, edge.FromType, edge.FromKey!, "source");
            ValidateEndpointKeyType(ontology, edge, edge.ToType, edge.ToKey!, "target");
            ValidateOptionalProperty(edge.ClrType, edge.Key?.Name, "key");
            foreach (var property in edge.Properties)
                ValidateProperty(edge.ClrType, property.Name, "property");
        }
    }

    private static void EnsureUnique<T>(IEnumerable<T> mappings, Func<T, string> name, string kind)
    {
        var duplicate = mappings.GroupBy(name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"Invalid ontology: {kind} '{duplicate.Key}' is configured more than once.");
    }

    private static void EnsureNodeExists(OntologyModel ontology, Type endpointType, Type edgeType, string endpointName)
    {
        if (!ontology.Nodes.Any(node => node.ClrType == endpointType))
            throw new InvalidOperationException($"Invalid ontology: {endpointName} endpoint '{endpointType.Name}' on edge '{edgeType.Name}' is not a configured node type.");
    }

    private static void ValidateOptionalProperty(Type type, string? propertyName, string role)
    {
        if (propertyName is not null)
            ValidateProperty(type, propertyName, role);
    }

    private static void ValidateEndpointKeyType(
        OntologyModel ontology,
        EdgeMapping edge,
        Type endpointType,
        PropertyMapping endpointKey,
        string endpointName)
    {
        var nodeKey = ontology.GetNode(endpointType).Key!;
        var endpointPropType = endpointKey.Property.PropertyType;
        var nodeKeyType = nodeKey.Property.PropertyType;

        // Accept either the raw node key type (e.g. Guid) or a navigation property of the node CLR type.
        if (endpointPropType != nodeKeyType && endpointPropType != endpointType && !endpointPropType.IsAssignableTo(endpointType))
        {
            throw new InvalidOperationException(
                $"Invalid ontology: {endpointName} key '{endpointKey.Name}' on edge '{edge.ClrType.Name}' has type " +
                $"'{endpointKey.Property.PropertyType.Name}', but node '{endpointType.Name}' uses key " +
                $"'{nodeKey.Name}' of type '{nodeKey.Property.PropertyType.Name}'. Endpoint key must be either the node key type or the node CLR type for navigation.");
        }
    }

    private static void ValidateProperty(Type type, string propertyName, string role)
    {
        if (type.GetProperty(propertyName) is null)
            throw new InvalidOperationException($"Invalid ontology: {role} '{propertyName}' is not a public property of '{type.Name}'.");
    }
}
