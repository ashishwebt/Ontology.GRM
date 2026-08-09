using Ontology.GRM.Ontology;
using Ontology.GRM.Tracking;

namespace Ontology.GRM.Providers;

public interface IGraphProvider : IDisposable
{
    IQueryable<T> QueryNodes<T>(OntologyModel ontology) where T : class;

    IQueryable<T> QueryEdges<T>(OntologyModel ontology) where T : class;

    string GenerateBatch(IEnumerable<GraphChange> changes, OntologyModel ontology);

    void Execute(string batch);

    Task ExecuteAsync(string batch, CancellationToken cancellationToken = default);
}
