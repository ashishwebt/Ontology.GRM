using Ontology.GRM.Ontology;
using Ontology.GRM.Tracking;

namespace Ontology.GRM.Infrastructure;

/// <summary>Provides hooks around persistence operations for auditing and diagnostics.</summary>
public abstract class GraphInterceptor
{
    public virtual void SavingChanges(GraphInterceptionContext context) { }

    public virtual void SavedChanges(GraphInterceptionContext context) { }

    public virtual void SaveChangesFailed(GraphInterceptionContext context, Exception exception) { }
}

public sealed class GraphInterceptionContext
{
    internal GraphInterceptionContext(IReadOnlyList<GraphChange> changes, OntologyModel ontology)
    {
        Changes = changes;
        Ontology = ontology;
    }

    public IReadOnlyList<GraphChange> Changes { get; }
    public OntologyModel Ontology { get; }
}
