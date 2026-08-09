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
        }
    }
}
