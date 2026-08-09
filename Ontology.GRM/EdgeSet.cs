namespace Ontology.GRM;

public sealed class EdgeSet<T> where T : class
{
    private readonly GraphContext _context;

    public EdgeSet(GraphContext context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    public void Add(T edge) => _context.Tracker.TrackAdded(edge);
    public void Update(T edge) => _context.Tracker.TrackModified(edge);
    public void Remove(T edge) => _context.Tracker.TrackDeleted(edge);
    public IQueryable<T> Query() => _context.QueryEdges<T>();
}
