using Ontology.GRM.Tracking;

namespace Ontology.GRM.Tests;

public sealed class ChangeTrackerTests
{
    [Fact]
    public void Added_then_modified_remains_added()
    {
        var tracker = new ChangeTracker();
        var entity = new object();
        tracker.TrackAdded(entity);
        tracker.TrackModified(entity);

        var change = Assert.Single(tracker.GetChanges());
        Assert.Equal(GraphChangeState.Added, change.State);
    }

    [Fact]
    public void Added_then_deleted_removes_pending_change()
    {
        var tracker = new ChangeTracker();
        var entity = new object();
        tracker.TrackAdded(entity);
        tracker.TrackDeleted(entity);

        Assert.Empty(tracker.GetChanges());
    }

    [Fact]
    public void Modified_then_deleted_becomes_deleted()
    {
        var tracker = new ChangeTracker();
        var entity = new object();
        tracker.TrackModified(entity);
        tracker.TrackDeleted(entity);

        Assert.Equal(GraphChangeState.Deleted, Assert.Single(tracker.GetChanges()).State);
    }
}
