namespace Ontology.GRM.Tracking;

/// <summary>Tracks the pending persistence state of graph entities by reference.</summary>
public sealed class ChangeTracker
{
    private readonly Dictionary<object, GraphChangeState> _changes =
        new(ReferenceEqualityComparer.Instance);

    public int Count => _changes.Count;

    public void TrackAdded(object entity) => Track(entity, GraphChangeState.Added);

    public void TrackModified(object entity) => Track(entity, GraphChangeState.Modified);

    public void TrackDeleted(object entity) => Track(entity, GraphChangeState.Deleted);

    public IReadOnlyList<GraphChange> GetChanges() =>
        _changes.Select(change => new GraphChange(change.Key, change.Value)).ToArray();

    public void Clear() => _changes.Clear();

    private void Track(object entity, GraphChangeState state)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (!_changes.TryGetValue(entity, out var existing))
        {
            _changes.Add(entity, state);
            return;
        }

        if (existing == GraphChangeState.Added && state == GraphChangeState.Modified)
            return;

        if (existing == GraphChangeState.Added && state == GraphChangeState.Deleted)
        {
            _changes.Remove(entity);
            return;
        }

        _changes[entity] = state;
    }
}
