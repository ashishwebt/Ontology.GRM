namespace Ontology.GRM.Ontology;

public sealed class OntologyBuilder
{
    private readonly List<NodeMappingBuilder> _nodes = [];
    private readonly List<EdgeMappingBuilder> _edges = [];

    public NodeConfiguration<T> DefineNode<T>(string? label = null) where T : class
    {
        var resolvedLabel = label ?? typeof(T).Name;
        if (string.IsNullOrWhiteSpace(resolvedLabel))
            throw new ArgumentException("A node label cannot be empty.", nameof(label));

        var mapping = new NodeMappingBuilder(typeof(T), resolvedLabel);
        _nodes.Add(mapping);
        return new NodeConfiguration<T>(mapping);
    }

    public EdgeConfiguration<T> DefineEdge<T>(string? relationshipType = null) where T : class
    {
        var resolvedRelationshipType = relationshipType ?? typeof(T).Name.ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(resolvedRelationshipType))
            throw new ArgumentException("An edge relationship type cannot be empty.", nameof(relationshipType));

        var mapping = new EdgeMappingBuilder(typeof(T), resolvedRelationshipType);
        _edges.Add(mapping);
        return new EdgeConfiguration<T>(mapping);
    }

    public void ApplyConfiguration(IEntityConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.Configure(this);
    }

    public OntologyModel Build()
    {
        Validate();
        return new OntologyModel(_nodes.Select(node => node.Build()), _edges.Select(edge => edge.Build()));
    }

    private void Validate()
    {
        var duplicateTypes = _nodes.GroupBy(node => node.ClrType).FirstOrDefault(group => group.Count() > 1);
        if (duplicateTypes is not null)
            throw new InvalidOperationException($"Node type '{duplicateTypes.Key.Name}' is configured more than once.");

        var duplicateLabels = _nodes.GroupBy(node => node.Label, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
        if (duplicateLabels is not null)
            throw new InvalidOperationException($"Node label '{duplicateLabels.Key}' is configured more than once.");

        foreach (var node in _nodes)
        {
            if (node.Key is null)
                throw new InvalidOperationException($"Node '{node.ClrType.Name}' must configure a key using HasKey(...).");
        }

        var duplicateEdgeTypes = _edges.GroupBy(edge => edge.ClrType).FirstOrDefault(group => group.Count() > 1);
        if (duplicateEdgeTypes is not null)
            throw new InvalidOperationException($"Edge type '{duplicateEdgeTypes.Key.Name}' is configured more than once.");

        var duplicateRelationshipTypes = _edges
            .GroupBy(edge => edge.RelationshipType, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateRelationshipTypes is not null)
            throw new InvalidOperationException($"Relationship type '{duplicateRelationshipTypes.Key}' is configured more than once.");

        foreach (var edge in _edges)
        {
            if (edge.FromType is null || edge.ToType is null)
                throw new InvalidOperationException($"Edge '{edge.ClrType.Name}' must configure both From<T>() and To<T>().");
            if (!_nodes.Any(node => node.ClrType == edge.FromType) || !_nodes.Any(node => node.ClrType == edge.ToType))
                throw new InvalidOperationException($"Edge '{edge.ClrType.Name}' endpoints must be configured node types.");
            // Validate that the configured From/To properties on the edge either carry the
            // node key value (e.g., Guid) or a navigation reference to the node type itself.
            var fromNode = _nodes.First(n => n.ClrType == edge.FromType);
            var toNode = _nodes.First(n => n.ClrType == edge.ToType);

            var fromKeyProp = edge.FromKey;
            var toKeyProp = edge.ToKey;

            if (fromKeyProp is null)
                throw new InvalidOperationException($"Edge '{edge.ClrType.Name}' has no source key configured. Use From<TNode>(...) to map an endpoint property.");
            if (toKeyProp is null)
                throw new InvalidOperationException($"Edge '{edge.ClrType.Name}' has no target key configured. Use To<TNode>(...) to map an endpoint property.");

            // Determine allowed types: either the node key type, or the node CLR type (navigation property)
            var expectedFromKeyType = fromNode.Key is not null ? fromNode.Key.PropertyType : null;
            var expectedToKeyType = toNode.Key is not null ? toNode.Key.PropertyType : null;

            var fromPropType = fromKeyProp.PropertyType;
            var toPropType = toKeyProp.PropertyType;

            var fromOk = (expectedFromKeyType is not null && expectedFromKeyType == fromPropType) || fromPropType == fromNode.ClrType || fromPropType.IsAssignableTo(fromNode.ClrType);
            var toOk = (expectedToKeyType is not null && expectedToKeyType == toPropType) || toPropType == toNode.ClrType || toPropType.IsAssignableTo(toNode.ClrType);

            if (!fromOk || !toOk)
                throw new InvalidOperationException($"Edge '{edge.ClrType.Name}' endpoint property types must be either the node key type or the node type (navigation property).");
        }
    }
}
