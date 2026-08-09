namespace Ontology.GRM.Ontology;

/// <summary>Root metadata container for an application's graph mapping.</summary>
public sealed class OntologyModel
{
    private readonly IReadOnlyDictionary<Type, NodeMapping> _nodes;
    private readonly IReadOnlyDictionary<Type, EdgeMapping> _edges;

    internal OntologyModel(IEnumerable<NodeMapping> nodes, IEnumerable<EdgeMapping> edges)
    {
        _nodes = nodes.ToDictionary(node => node.ClrType);
        _edges = edges.ToDictionary(edge => edge.ClrType);
    }

    public IReadOnlyCollection<NodeMapping> Nodes => _nodes.Values.ToArray();
    public IReadOnlyCollection<EdgeMapping> Edges => _edges.Values.ToArray();

    public NodeMapping GetNode(Type type) => _nodes.TryGetValue(type, out var mapping)
        ? mapping
        : throw new InvalidOperationException($"No node mapping is configured for '{type.Name}'.");

    public EdgeMapping GetEdge(Type type) => _edges.TryGetValue(type, out var mapping)
        ? mapping
        : throw new InvalidOperationException($"No edge mapping is configured for '{type.Name}'.");
}
