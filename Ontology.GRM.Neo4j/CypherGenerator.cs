using System.Globalization;
using System.Linq;
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

        var changeList = changes.ToList();

        // Build a single-session statement for added nodes and their edges so
        // newly created nodes can be referenced when creating relationships.
        var sessionStatement = TryBuildSessionStatement(changeList, ontology, out var remainingStatements);
        if (sessionStatement is not null)
            return string.Join(";\n", new[] { sessionStatement }.Concat(remainingStatements));

        return string.Join(";\n", changeList.Select(change => BuildChange(change, ontology)));
    }

    private static string? TryBuildSessionStatement(IReadOnlyList<GraphChange> changes, OntologyModel ontology, out List<string> remaining)
    {
        remaining = new List<string>();

        var nodeTemps = new Dictionary<object, string>(new ReferenceEqualityComparer());
        int tempCounter = 0;

        // Collect added node changes and map instances to temp ids
        var addedNodes = new List<(GraphChange change, NodeMapping mapping)>();
        var addedEdges = new List<(GraphChange change, EdgeMapping mapping)>();

        foreach (var change in changes)
        {
            if (TryGetNode(change.Entity, ontology, out var node))
            {
                if (change.State == GraphChangeState.Added)
                {
                    var tempId = $"t{++tempCounter}";
                    nodeTemps[change.Entity] = tempId;
                    addedNodes.Add((change, node));
                    continue;
                }
            }

            // not an added node; if it's an added edge reference and both endpoints are added nodes, include it
            if (change.State == GraphChangeState.Added)
            {
                try
                {
                    var edge = ontology.GetEdge(change.Entity.GetType());
                    addedEdges.Add((change, edge));
                    continue;
                }
                catch (InvalidOperationException)
                {
                    // fall through
                }
            }

            // otherwise generate standalone statement later
            remaining.Add(BuildChange(change, ontology));
        }

        if (addedNodes.Count == 0 && addedEdges.Count == 0)
            return null;

        // Group added nodes by label
        var nodesByLabel = addedNodes.GroupBy(x => x.mapping.Label, x => x.change.Entity).ToList();

        var clauses = new List<string>();

        // For each label, create UNWIND create clause
        foreach (var group in nodesByLabel)
        {
            var label = group.Key;
            var rows = group.Select(entity =>
            {
                var temp = nodeTemps[entity];
                var mapping = ontology.GetNode(entity.GetType());
                var propsList = new List<string>();
                // include key property value when creating the node so relationships can match by key
                if (mapping.Key is not null)
                    propsList.Add(PropertyAssignment(mapping.Key, entity));
                propsList.AddRange(mapping.Properties.Select(p => PropertyAssignment(p, entity)));
                var props = propsList.ToArray();
                var propsMap = props.Length == 0 ? "{}" : $"{{{string.Join(", ", props)}}}";
                return $"{{temp: {Value(temp)}, props: {propsMap}}}";
            }).ToArray();

            var list = string.Join(", ", rows);
            var clause = $"UNWIND [{list}] AS row CREATE (n:{Identifier(label)}) SET n += row.props SET n.{Identifier("__session_temp_id")} = row.temp";
            clauses.Add(clause);
        }

        // Create relationships using temp ids when endpoints refer to newly added node instances
        foreach (var (change, edge) in addedEdges)
        {
            var fromType = edge.FromType ?? throw new InvalidOperationException($"Edge '{edge.ClrType.Name}' has no source node type.");
            var toType = edge.ToType ?? throw new InvalidOperationException($"Edge '{edge.ClrType.Name}' has no target node type.");
            var fromKey = edge.FromKey ?? throw new InvalidOperationException($"Edge '{edge.ClrType.Name}' must map its source key with From<TNode>(...).");
            var toKey = edge.ToKey ?? throw new InvalidOperationException($"Edge '{edge.ClrType.Name}' must map its target key with To<TNode>(...).");
            var fromNode = ontology.GetNode(fromType);
            var toNode = ontology.GetNode(toType);
            var fromNodeKey = fromNode.Key ?? throw new InvalidOperationException($"Node '{fromType.Name}' has no configured key.");
            var toNodeKey = toNode.Key ?? throw new InvalidOperationException($"Node '{toType.Name}' has no configured key.");

            object? rawFrom = fromKey.Property.GetValue(change.Entity);
            object? rawTo = toKey.Property.GetValue(change.Entity);

            object? actualFrom = ResolveEndpointKeyValueForSession(rawFrom, nodeTemps, ontology, fromNode);
            object? actualTo = ResolveEndpointKeyValueForSession(rawTo, nodeTemps, ontology, toNode);

            string fromMatch = actualFrom is TempRef tf
                ? $"(from:{Identifier(fromNode.Label)} {{{Identifier("__session_temp_id")} : {Value(tf.Id)}}})"
                : $"(from:{Identifier(fromNode.Label)} {{{Identifier(fromNodeKey.Name)}: {Value(actualFrom)}}})";

            string toMatch = actualTo is TempRef tt
                ? $"(to:{Identifier(toNode.Label)} {{{Identifier("__session_temp_id")} : {Value(tt.Id)}}})"
                : $"(to:{Identifier(toNode.Label)} {{{Identifier(toNodeKey.Name)}: {Value(actualTo)}}})";

            var relationshipIdentity = edge.Key is null ? string.Empty : $" {{{PropertyAssignment(edge.Key, change.Entity)}}}";
            var relationship = $"[r:{Identifier(edge.RelationshipType)}{relationshipIdentity}]";
            var properties = SetProperties("r", edge.Properties, change.Entity, edge.Key?.Name);

            var clause = $"MATCH {fromMatch} MATCH {toMatch} MERGE (from)-{relationship}->(to){properties}";
            clauses.Add(clause);
        }

        var createClauses = clauses.Where(c => c.StartsWith("UNWIND", StringComparison.Ordinal)).ToList();
        var matchClauses = clauses.Where(c => c.StartsWith("MATCH", StringComparison.Ordinal)).ToList();

        string statement;
        if (createClauses.Count > 0)
        {
            // Chain create clauses and then use WITH to separate from MATCH clauses in the same statement.
            var createSection = string.Join("\n", createClauses);
            var matchSection = matchClauses.Count > 0 ? string.Join("\n", matchClauses) : string.Empty;
            statement = createSection + (string.IsNullOrEmpty(matchSection) ? string.Empty : "\nWITH 1 AS __session_marker\n" + matchSection);
        }
        else
        {
            statement = string.Join("\n", matchClauses);
        }

        return statement;
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

        object? rawFrom = fromKey.Property.GetValue(change.Entity);
        object? rawTo = toKey.Property.GetValue(change.Entity);

        object? actualFromValue = ResolveEndpointKeyValue(rawFrom, ontology, fromNode);
        object? actualToValue = ResolveEndpointKeyValue(rawTo, ontology, toNode);

        var fromPattern = $"(from:{Identifier(fromNode.Label)} {{{Identifier(fromNodeKey.Name)}: {Value(actualFromValue)}}})";
        var toPattern = $"(to:{Identifier(toNode.Label)} {{{Identifier(toNodeKey.Name)}: {Value(actualToValue)}}})";
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

    private static object? ResolveEndpointKeyValue(object? rawValue, OntologyModel ontology, NodeMapping expectedNode)
    {
        if (rawValue is null)
            return null;

        var type = rawValue.GetType();

        if (IsSimpleValue(rawValue))
            return rawValue;

        // rawValue appears to be a referenced node instance. Try to get its key property value.
        try
        {
            var referencedNode = ontology.GetNode(type);
            var key = referencedNode.Key ?? throw new InvalidOperationException($"Referenced node '{type.Name}' has no configured key.");
            return key.Property.GetValue(rawValue);
        }
        catch (InvalidOperationException)
        {
            // Not a known node type; fall back to original raw value.
            return rawValue;
        }
    }

    private static object? ResolveEndpointKeyValueForSession(object? rawValue, IDictionary<object, string> nodeTemps, OntologyModel ontology, NodeMapping expectedNode)
    {
        if (rawValue is null)
            return null;

        if (nodeTemps.TryGetValue(rawValue, out var tempId))
            return new TempRef(tempId);

        return ResolveEndpointKeyValue(rawValue, ontology, expectedNode);
    }

    private sealed record TempRef(string Id);

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }

    private static bool IsSimpleValue(object? value)
    {
        if (value is null)
            return true;

        var t = value.GetType();
        if (t.IsPrimitive || t.IsEnum)
            return true;
        if (value is string || value is Guid || value is DateTime || value is DateTimeOffset || value is DateOnly || value is TimeOnly || value is decimal)
            return true;
        return false;
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
