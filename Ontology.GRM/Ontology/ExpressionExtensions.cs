using System.Linq.Expressions;
using System.Reflection;

namespace Ontology.GRM.Ontology;

internal static class ExpressionExtensions
{
    public static PropertyInfo GetProperty<T>(Expression<Func<T, object?>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var body = expression.Body is UnaryExpression { NodeType: ExpressionType.Convert } convert
            ? convert.Operand
            : expression.Body;

        return body is MemberExpression { Member: PropertyInfo property }
            ? property
            : throw new ArgumentException("The expression must select a CLR property.", nameof(expression));
    }
}
