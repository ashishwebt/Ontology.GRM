using System.Globalization;
using System.Linq.Expressions;
using System.Linq;
using Ontology.GRM.Ontology;

namespace Ontology.GRM.Providers;

internal sealed class CypherQueryTranslator
{
    private readonly OntologyModel _ontology;
    private readonly Type _rootType;
    private readonly bool _isEdge;
    private readonly List<string> _filters = [];
    private readonly HashSet<string> _mappedProperties;
    private LambdaExpression? _projection;

    public CypherQueryTranslator(OntologyModel ontology, Type rootType, bool isEdge)
    {
        _ontology = ontology;
        _rootType = rootType;
        _isEdge = isEdge;
        var properties = isEdge
            ? EdgeProperties(ontology.GetEdge(rootType))
            : NodeProperties(ontology.GetNode(rootType));
        _mappedProperties = properties.Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
    }

    public string Translate(Expression expression)
    {
        VisitQuery(expression);
        var mapping = _isEdge ? _ontology.GetEdge(_rootType) : null;
        var match = _isEdge
            ? $"MATCH (from)-[r:`{Escape(mapping!.RelationshipType)}`]->(to)"
            : $"MATCH (n:`{Escape(_ontology.GetNode(_rootType).Label)}`)";
        var variable = _isEdge ? "r" : "n";
        var where = _filters.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", _filters);
        return match + where + " RETURN " + BuildProjection(variable);
    }

    private void VisitQuery(Expression expression)
    {
        if (expression is MethodCallExpression call && call.Method.DeclaringType == typeof(Queryable))
        {
            VisitQuery(call.Arguments[0]);
            var lambda = Unquote(call.Arguments[1]);
            switch (call.Method.Name)
            {
                case nameof(Queryable.Where):
                    _filters.Add(TranslatePredicate(lambda.Body, lambda.Parameters[0], _isEdge ? "r" : "n"));
                    return;
                case nameof(Queryable.Select):
                    if (_projection is not null)
                        throw new NotSupportedException("Only one Select projection is supported.");
                    _projection = lambda;
                    return;
                default:
                    throw new NotSupportedException($"LINQ operator '{call.Method.Name}' is not supported by the Cypher translator.");
            }
        }
    }

    private static LambdaExpression Unquote(Expression expression) => expression is UnaryExpression
        {
            NodeType: ExpressionType.Quote,
            Operand: LambdaExpression lambda
        }
        ? lambda
        : expression as LambdaExpression ?? throw new NotSupportedException("The LINQ operator requires a lambda expression.");

    private string BuildProjection(string variable)
    {
        if (_projection is null)
            return variable;
        return _projection.Body switch
        {
            MemberExpression member => Property(variable, member, _projection.Parameters[0]),
            NewExpression @new => string.Join(", ", @new.Arguments.Select((argument, index) =>
                $"{Property(variable, RequireMember(argument), _projection.Parameters[0])} AS `{Escape(@new.Members?[index].Name ?? RequireMember(argument).Member.Name)}`")),
            _ => throw new NotSupportedException("Select must project mapped properties or an anonymous object of mapped properties.")
        };
    }

    private string TranslatePredicate(Expression expression, ParameterExpression parameter, string variable) => expression switch
    {
        BinaryExpression { NodeType: ExpressionType.AndAlso } binary => $"({TranslatePredicate(binary.Left, parameter, variable)} AND {TranslatePredicate(binary.Right, parameter, variable)})",
        BinaryExpression { NodeType: ExpressionType.OrElse } binary => $"({TranslatePredicate(binary.Left, parameter, variable)} OR {TranslatePredicate(binary.Right, parameter, variable)})",
        BinaryExpression binary when binary.NodeType is ExpressionType.Equal or ExpressionType.NotEqual or ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual or ExpressionType.LessThan or ExpressionType.LessThanOrEqual
            => $"{Property(variable, RequireMember(binary.Left), parameter)} {Operator(binary.NodeType)} {Literal(Evaluate(binary.Right))}",
        _ => throw new NotSupportedException("Where supports comparisons of mapped properties with constant values.")
    };

    private static MemberExpression RequireMember(Expression expression) => expression is MemberExpression member
        ? member
        : throw new NotSupportedException("The query must use a mapped property.");

    private string Property(string variable, MemberExpression member, ParameterExpression parameter)
    {
        // Direct property on the root parameter: n.Property or r.Property
        if (member.Expression == parameter)
        {
            if (!_mappedProperties.Contains(member.Member.Name))
                throw new InvalidOperationException($"Property '{member.Member.Name}' is not mapped for graph type '{_rootType.Name}'.");
            return $"{variable}.`{Escape(member.Member.Name)}`";
        }

        // Support nested access for edges: e => e.From.KeyProperty (translate to from.`KeyProperty`)
        if (_isEdge && member.Expression is MemberExpression inner && inner.Expression == parameter)
        {
            var edgeMapping = _ontology.GetEdge(_rootType);
            // Determine if inner.Member is FromKey or ToKey on the edge mapping
            var isFrom = edgeMapping.FromKey is not null && string.Equals(edgeMapping.FromKey.Name, inner.Member.Name, StringComparison.Ordinal);
            var isTo = edgeMapping.ToKey is not null && string.Equals(edgeMapping.ToKey.Name, inner.Member.Name, StringComparison.Ordinal);
            if (!isFrom && !isTo)
                throw new InvalidOperationException("Only navigation properties configured with From/To may be used for nested property access.");

            // member.Member is the property on the node type
            var nodeType = isFrom ? edgeMapping.FromType! : edgeMapping.ToType!;
            var nodeMapping = _ontology.GetNode(nodeType);
            if (nodeMapping.Key is null)
                throw new InvalidOperationException($"Node '{nodeType.Name}' has no configured key to project.");

            if (!_mappedProperties.Contains(member.Member.Name) && !nodeMapping.Properties.Any(p => p.Name == member.Member.Name) && nodeMapping.Key?.Name != member.Member.Name)
                throw new InvalidOperationException($"Property '{member.Member.Name}' is not mapped for node type '{nodeType.Name}'.");

            var endpointVar = isFrom ? "from" : "to";
            return $"{endpointVar}.`{Escape(member.Member.Name)}`";
        }

        throw new NotSupportedException("Only direct mapped property access is supported.");
    }

    private static IEnumerable<PropertyMapping> NodeProperties(NodeMapping mapping)
    {
        if (mapping.Key is not null)
            yield return mapping.Key;
        foreach (var property in mapping.Properties)
            yield return property;
    }

    private static IEnumerable<PropertyMapping> EdgeProperties(EdgeMapping mapping)
    {
        if (mapping.Key is not null)
            yield return mapping.Key;
        if (mapping.FromKey is not null)
            yield return mapping.FromKey;
        if (mapping.ToKey is not null)
            yield return mapping.ToKey;
        foreach (var property in mapping.Properties)
            yield return property;
    }

    private static object? Evaluate(Expression expression)
    {
        if (expression is ConstantExpression constant)
            return constant.Value;
        return Expression.Lambda(expression).Compile().DynamicInvoke();
    }

    private static string Operator(ExpressionType type) => type switch
    {
        ExpressionType.Equal => "=", ExpressionType.NotEqual => "<>", ExpressionType.GreaterThan => ">",
        ExpressionType.GreaterThanOrEqual => ">=", ExpressionType.LessThan => "<", ExpressionType.LessThanOrEqual => "<=",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static string Literal(object? value) => value switch
    {
        null => "null", string text => $"'{text.Replace("'", "\\'")}'", bool boolean => boolean ? "true" : "false",
        Guid guid => $"'{guid}'", DateTime dateTime => $"datetime('{dateTime.ToUniversalTime():O}')",
        _ when value is IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture)!,
        _ => throw new NotSupportedException($"Values of type '{value.GetType().Name}' are not supported.")
    };

    private static string Escape(string value) => value.Replace("`", "``");
}
