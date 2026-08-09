using System.Linq.Expressions;

namespace Ontology.GRM.Providers;

public static class CypherQueryableExtensions
{
    /// <summary>Translates a graph LINQ query into Cypher without executing it.</summary>
    public static string ToCypher(this IQueryable query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return query.Provider is CypherQueryProvider provider
            ? provider.Translate(query.Expression)
            : throw new ArgumentException("The query was not created by the graph query provider.", nameof(query));
    }
}
