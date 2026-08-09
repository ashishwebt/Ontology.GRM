using System.Collections;
using System.Linq.Expressions;
using Ontology.GRM.Ontology;

namespace Ontology.GRM.Providers;

/// <summary>A composable query root whose LINQ expression can be translated to Cypher.</summary>
public sealed class CypherQueryable<T> : IOrderedQueryable<T>
{
    private readonly CypherQueryProvider _provider;

    private CypherQueryable(CypherQueryProvider provider, Expression expression)
    {
        _provider = provider;
        Expression = expression;
    }

    public Type ElementType => typeof(T);
    public Expression Expression { get; }
    public IQueryProvider Provider => _provider;

    public static CypherQueryable<T> ForNodes(OntologyModel ontology) => Create(ontology, false);
    public static CypherQueryable<T> ForEdges(OntologyModel ontology) => Create(ontology, true);

    private static CypherQueryable<T> Create(OntologyModel ontology, bool isEdge)
    {
        ArgumentNullException.ThrowIfNull(ontology);
        var provider = new CypherQueryProvider(ontology, typeof(T), isEdge);
        return new CypherQueryable<T>(provider, Expression.Constant(null, typeof(IQueryable<T>)));
    }

    public IEnumerator<T> GetEnumerator() => throw new NotSupportedException(
        "Cypher query execution is not available through synchronous enumeration. Call ToCypher() to inspect the query or execute it with a provider query executor.");

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal sealed class CypherQueryProvider(OntologyModel ontology, Type rootType, bool isEdge) : IQueryProvider
{
    public IQueryable CreateQuery(Expression expression)
    {
        var elementType = expression.Type.GetGenericArguments().Single();
        return (IQueryable)Activator.CreateInstance(typeof(CypherQuery<>).MakeGenericType(elementType), this, expression)!;
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new CypherQuery<TElement>(this, expression);

    public object? Execute(Expression expression) => throw new NotSupportedException("Use ToCypher() to translate a graph query.");
    public TResult Execute<TResult>(Expression expression) => throw new NotSupportedException("Use ToCypher() to translate a graph query.");

    public string Translate(Expression expression) => new CypherQueryTranslator(ontology, rootType, isEdge).Translate(expression);
}

internal sealed class CypherQuery<T>(CypherQueryProvider provider, Expression expression) : IOrderedQueryable<T>
{
    public Type ElementType => typeof(T);
    public Expression Expression { get; } = expression;
    public IQueryProvider Provider { get; } = provider;
    public IEnumerator<T> GetEnumerator() => throw new NotSupportedException("Use ToCypher() to translate a graph query.");
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
