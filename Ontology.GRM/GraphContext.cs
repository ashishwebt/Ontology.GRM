using Ontology.GRM.Ontology;
using Ontology.GRM.Providers;
using Ontology.GRM.Tracking;
using Ontology.GRM.Infrastructure;

namespace Ontology.GRM;

public abstract class GraphContext : IDisposable
{
    private bool _disposed;

    protected IGraphProvider? Provider { get; private set; }
    protected OntologyModel? Ontology { get; private set; }
    internal ChangeTracker Tracker { get; } = new();
    private readonly List<GraphInterceptor> _interceptors = [];
    private readonly OntologyValidator _ontologyValidator = new();

    public GraphLogger? Logger { get; private set; }

    public void UseLogger(GraphLogger logger)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void AddInterceptor(GraphInterceptor interceptor)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _interceptors.Add(interceptor ?? throw new ArgumentNullException(nameof(interceptor)));
    }

    public void UseProvider(IGraphProvider provider)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(provider);
        Provider = provider;
    }

    public void ConfigureOntology(Action<OntologyBuilder> configure)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new OntologyBuilder();
        try
        {
            configure(builder);
            var ontology = builder.Build();
            _ontologyValidator.Validate(ontology);
            Ontology = ontology;
            Logger?.Log(GraphLogLevel.Information, "Graph ontology configured.");
        }
        catch (Exception exception)
        {
            Logger?.Log(GraphLogLevel.Error, "Graph ontology configuration failed.", exception);
            throw;
        }
    }

    public IQueryable<T> QueryNodes<T>() where T : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var provider = Provider ?? throw new InvalidOperationException("A graph provider must be configured before querying.");
        var ontology = Ontology ?? throw new InvalidOperationException("An ontology must be configured before querying.");
        ontology.GetNode(typeof(T));
        return provider.QueryNodes<T>(ontology);
    }

    public IQueryable<T> QueryEdges<T>() where T : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var provider = Provider ?? throw new InvalidOperationException("A graph provider must be configured before querying.");
        var ontology = Ontology ?? throw new InvalidOperationException("An ontology must be configured before querying.");
        ontology.GetEdge(typeof(T));
        return provider.QueryEdges<T>(ontology);
    }

    public void SaveChanges()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var provider = Provider ?? throw new InvalidOperationException("A graph provider must be configured before saving changes.");
        var ontology = Ontology ?? throw new InvalidOperationException("An ontology must be configured before saving changes.");

        ExecuteSave(provider, ontology);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var provider = Provider ?? throw new InvalidOperationException("A graph provider must be configured before saving changes.");
        var ontology = Ontology ?? throw new InvalidOperationException("An ontology must be configured before saving changes.");

        var changes = ValidatePendingChanges(ontology);
        if (changes.Count == 0)
            return;

        var context = new GraphInterceptionContext(changes, ontology);
        try
        {
            Logger?.Log(GraphLogLevel.Information, $"Saving {changes.Count} graph change(s).");
            InvokeSaving(context);
            var batch = provider.GenerateBatch(changes, ontology);
            await provider.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
            Tracker.Clear();
            InvokeSaved(context);
            Logger?.Log(GraphLogLevel.Information, $"Saved {changes.Count} graph change(s).");
        }
        catch (Exception exception)
        {
            InvokeFailed(context, exception);
            Logger?.Log(GraphLogLevel.Error, "Saving graph changes failed.", exception);
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Provider?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ExecuteSave(IGraphProvider provider, OntologyModel ontology)
    {
        var changes = ValidatePendingChanges(ontology);
        if (changes.Count == 0)
            return;

        var context = new GraphInterceptionContext(changes, ontology);
        try
        {
            Logger?.Log(GraphLogLevel.Information, $"Saving {changes.Count} graph change(s).");
            InvokeSaving(context);
            var batch = provider.GenerateBatch(changes, ontology);
            provider.Execute(batch);
            Tracker.Clear();
            InvokeSaved(context);
            Logger?.Log(GraphLogLevel.Information, $"Saved {changes.Count} graph change(s).");
        }
        catch (Exception exception)
        {
            InvokeFailed(context, exception);
            Logger?.Log(GraphLogLevel.Error, "Saving graph changes failed.", exception);
            throw;
        }
    }

    private IReadOnlyList<GraphChange> ValidatePendingChanges(OntologyModel ontology)
    {
        _ontologyValidator.Validate(ontology);
        var changes = Tracker.GetChanges();
        foreach (var change in changes)
        {
            var type = change.Entity.GetType();
            if (!ontology.Nodes.Any(node => node.ClrType == type) && !ontology.Edges.Any(edge => edge.ClrType == type))
                throw new InvalidOperationException($"Cannot save '{type.Name}': it is not configured as a node or edge in the current ontology.");
        }

        return changes;
    }

    private void InvokeSaving(GraphInterceptionContext context)
    {
        foreach (var interceptor in _interceptors)
            interceptor.SavingChanges(context);
    }

    private void InvokeSaved(GraphInterceptionContext context)
    {
        foreach (var interceptor in _interceptors)
            interceptor.SavedChanges(context);
    }

    private void InvokeFailed(GraphInterceptionContext context, Exception exception)
    {
        foreach (var interceptor in _interceptors)
            interceptor.SaveChangesFailed(context, exception);
    }
}
