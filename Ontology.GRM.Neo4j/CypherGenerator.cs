using System.Globalization;
using Ontology.GRM.Ontology;
using Ontology.GRM.Tracking;

namespace Ontology.GRM.Providers;

/// <summary>Generates Cypher statements from tracked POCO changes and ontology metadata.</summary>
public sealed class CypherGenerator
{
    public string Build(IEnumerable<GraphChange> changes, OntologyModel ontology)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(ontology);

        return string.Join(";\n", changes.Select(change => BuildChange(change, ontology)));
    }

    private static string BuildChange(GraphChange change, OntologyModel ontology)
    {
        ArgumentNullException.ThrowIfNull(change);

        return TryGetNode(change.Entity, ontology, out var node)
            ? BuildNodeChange(change, node)
            : BuildEdgeChange(change, ontology.GetEdge(change.Entity.GetType()), ontology);
    }

    private static bool TryGetNode(object entity, OntologyModel ontology, out NodeMapping node)
    {
        try
        {
            node = ontology.GetNode(entity.GetType());
            return true;
        }
        catch (InvalidOperationException)
        {
            node = null!;
            return false;
        }
    }

    private static string BuildNodeChange(GraphChange change, NodeMapping node)
    {
        var key = node.Key ?? throw new InvalidOperationException($"Node '{node.ClrType.Name}' has no configured key.");
        var identity = PropertyAssignment(key, change.Entity);
        var pattern = $"(n:{Identifier(node.Label)} {{{identity}}})";

        return change.State switch
        {
            GraphChangeState.Added => $"MERGE {pattern}{SetProperties("n", node.Properties, change.Entity, key.Name)}",
            GraphChangeState.Modified => $"MATCH {pattern}{SetProperties("n", node.Properties, change.Entity, key.Name)}",
            GraphChangeState.Deleted => $"MATCH {pattern} DETACH DELETE n",
            _ => throw new ArgumentOutOfRangeException(nameof(change), change.State, "Unknown graph change state.")
        };
    }

    private static string BuildEdgeChange(GraphChange change, EdgeMapping edge, OntologyModel ontology)
    {
        var fromType = edge.FromType ?? throw new InvalidOperationException($"Edge '{edge.ClrType.Name}' has no source node type.");
        var toType = edge.ToType ?? throw new InvalidOperationException($"Edge '{edge.ClrType.Name}' has no target node type.");
        var fromKey = edge.FromKey ?? throw new InvalidOperationException($"Edge '{edge.ClrType.Name}' must map its source key with From<TNode>(...).");
        var toKey = edge.ToKey ?? throw new InvalidOperationException($"Edge '{edge.ClrType.Name}' must map its target key with To<TNode>(...).");
        var fromNode = ontology.GetNode(fromType);
        var toNode = ontology.GetNode(toType);
        var fromNodeKey = fromNode.Key ?? throw new InvalidOperationException($"Node '{fromType.Name}' has no configured key.");
        var toNodeKey = toNode.Key ?? throw new InvalidOperationException($"Node '{toType.Name}' has no configured key.");

        var fromPattern = $"(from:{Identifier(fromNode.Label)} {{{Identifier(fromNodeKey.Name)}: {Value(fromKey.Property.GetValue(change.Entity))}}})";
        var toPattern = $"(to:{Identifier(toNode.Label)} {{{Identifier(toNodeKey.Name)}: {Value(toKey.Property.GetValue(change.Entity))}}})";
        var relationshipIdentity = edge.Key is null ? string.Empty : $" {{{PropertyAssignment(edge.Key, change.Entity)}}}";
        var relationship = $"[r:{Identifier(edge.RelationshipType)}{relationshipIdentity}]";
        var matchEndpoints = $"MATCH {fromPattern} MATCH {toPattern}";
        var properties = SetProperties("r", edge.Properties, change.Entity, edge.Key?.Name);

        return change.State switch
        {
            GraphChangeState.Added => $"{matchEndpoints} MERGE (from)-{relationship}->(to){properties}",
            GraphChangeState.Modified => $"{matchEndpoints} MATCH (from)-{relationship}->(to){properties}",
            GraphChangeState.Deleted => $"{matchEndpoints} MATCH (from)-{relationship}->(to) DELETE r",
            _ => throw new ArgumentOutOfRangeException(nameof(change), change.State, "Unknown graph change state.")
        };
    }

    private static string SetProperties(string variable, IEnumerable<PropertyMapping> properties, object entity, string? excludedProperty)
    {
        var assignments = properties
            .Where(property => !string.Equals(property.Name, excludedProperty, StringComparison.Ordinal))
            .Select(property => $"{variable}.{Identifier(property.Name)} = {Value(property.Property.GetValue(entity))}")
            .ToArray();

        return assignments.Length == 0 ? string.Empty : $" SET {string.Join(", ", assignments)}";
    }

    private static string PropertyAssignment(PropertyMapping property, object entity) =>
        $"{Identifier(property.Name)}: {Value(property.Property.GetValue(entity))}";

    private static string Identifier(string value) => $"`{value.Replace("`", "``", StringComparison.Ordinal)}`";

    private static string Value(object? value) => value switch
    {
        null => "null",
        string text => $"'{text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal)}'",
        char character => $"'{character.ToString().Replace("'", "\\'", StringComparison.Ordinal)}'",
        bool boolean => boolean ? "true" : "false",
        Guid guid => $"'{guid:D}'",
        DateTime dateTime => $"datetime('{dateTime:O}')",
        DateTimeOffset dateTimeOffset => $"datetime('{dateTimeOffset:O}')",
        DateOnly date => $"date('{date:O}')",
        TimeOnly time => $"time('{time:O}')",
        Enum enumeration => $"'{enumeration}'",
        byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => Convert.ToString(value, CultureInfo.InvariantCulture)!,
        _ => throw new NotSupportedException($"Cypher generation does not support values of type '{value.GetType().Name}'.")
    };
}
