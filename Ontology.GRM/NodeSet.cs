namespace Ontology.GRM;

public sealed class NodeSet<T> where T : class
{
    private readonly GraphContext _context;

    public NodeSet(GraphContext context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    public void Add(T node) => _context.Tracker.TrackAdded(node);
    public void Update(T node) => _context.Tracker.TrackModified(node);
    public void Remove(T node) => _context.Tracker.TrackDeleted(node);
    public IQueryable<T> Query() => _context.QueryNodes<T>();
}
