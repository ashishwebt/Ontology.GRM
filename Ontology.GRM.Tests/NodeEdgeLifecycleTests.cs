using Ontology.GRM.Ontology;
using Ontology.GRM.Providers;
using Ontology.GRM.Tracking;

namespace Ontology.GRM.Tests;

public sealed class NodeEdgeLifecycleTests
{
    [Fact]
    public void NodeSet_Add_tracks_added_change()
    {
        var provider = new RecordingProvider();
        using var context = new TestContext(provider);
        context.ConfigureOntology(builder => builder.DefineNode<Person>().HasKey(person => person.Id));

        var person = new Person { Id = Guid.NewGuid() };
        context.People.Add(person);
        context.SaveChanges();

        var change = Assert.Single(provider.Changes);
        Assert.Same(person, change.Entity);
        Assert.Equal(GraphChangeState.Added, change.State);
    }

    [Fact]
    public void NodeSet_Update_tracks_modified_change()
    {
        var provider = new RecordingProvider();
        using var context = new TestContext(provider);
        context.ConfigureOntology(builder => builder.DefineNode<Person>().HasKey(person => person.Id));

        var person = new Person { Id = Guid.NewGuid() };
        context.People.Update(person);
        context.SaveChanges();

        var change = Assert.Single(provider.Changes);
        Assert.Same(person, change.Entity);
        Assert.Equal(GraphChangeState.Modified, change.State);
    }

    [Fact]
    public void NodeSet_Remove_tracks_deleted_change()
    {
        var provider = new RecordingProvider();
        using var context = new TestContext(provider);
        context.ConfigureOntology(builder => builder.DefineNode<Person>().HasKey(person => person.Id));

        var person = new Person { Id = Guid.NewGuid() };
        context.People.Remove(person);
        context.SaveChanges();

        var change = Assert.Single(provider.Changes);
        Assert.Same(person, change.Entity);
        Assert.Equal(GraphChangeState.Deleted, change.State);
    }

    [Fact]
    public void EdgeSet_Add_tracks_added_change()
    {
        var provider = new RecordingProvider();
        using var context = new TestContext(provider);
        context.ConfigureOntology(builder =>
        {
            builder.DefineNode<Person>().HasKey(person => person.Id);
            builder.DefineEdge<Friendship>()
                .HasKey(edge => edge.Id)
                .From<Person>(edge => edge.FromId)
                .To<Person>(edge => edge.ToId);
        });

        var friendship = new Friendship { Id = Guid.NewGuid(), FromId = Guid.NewGuid(), ToId = Guid.NewGuid() };
        context.Friendships.Add(friendship);
        context.SaveChanges();

        var change = Assert.Single(provider.Changes);
        Assert.Same(friendship, change.Entity);
        Assert.Equal(GraphChangeState.Added, change.State);
    }

    [Fact]
    public void EdgeSet_Update_tracks_modified_change()
    {
        var provider = new RecordingProvider();
        using var context = new TestContext(provider);
        context.ConfigureOntology(builder =>
        {
            builder.DefineNode<Person>().HasKey(person => person.Id);
            builder.DefineEdge<Friendship>()
                .HasKey(edge => edge.Id)
                .From<Person>(edge => edge.FromId)
                .To<Person>(edge => edge.ToId);
        });

        var friendship = new Friendship { Id = Guid.NewGuid(), FromId = Guid.NewGuid(), ToId = Guid.NewGuid() };
        context.Friendships.Update(friendship);
        context.SaveChanges();

        var change = Assert.Single(provider.Changes);
        Assert.Same(friendship, change.Entity);
        Assert.Equal(GraphChangeState.Modified, change.State);
    }

    [Fact]
    public void EdgeSet_Remove_tracks_deleted_change()
    {
        var provider = new RecordingProvider();
        using var context = new TestContext(provider);
        context.ConfigureOntology(builder =>
        {
            builder.DefineNode<Person>().HasKey(person => person.Id);
            builder.DefineEdge<Friendship>()
                .HasKey(edge => edge.Id)
                .From<Person>(edge => edge.FromId)
                .To<Person>(edge => edge.ToId);
        });

        var friendship = new Friendship { Id = Guid.NewGuid(), FromId = Guid.NewGuid(), ToId = Guid.NewGuid() };
        context.Friendships.Remove(friendship);
        context.SaveChanges();

        var change = Assert.Single(provider.Changes);
        Assert.Same(friendship, change.Entity);
        Assert.Equal(GraphChangeState.Deleted, change.State);
    }

    [Fact]
    public void NodeSet_Add_then_remove_clears_pending_change()
    {
        var provider = new RecordingProvider();
        using var context = new TestContext(provider);
        context.ConfigureOntology(builder => builder.DefineNode<Person>().HasKey(person => person.Id));

        var person = new Person { Id = Guid.NewGuid() };
        context.People.Add(person);
        context.People.Remove(person);
        context.SaveChanges();

        Assert.Empty(provider.Changes);
        Assert.Equal(0, provider.ExecuteCalls);
    }

    [Fact]
    public void EdgeSet_Add_then_remove_clears_pending_change()
    {
        var provider = new RecordingProvider();
        using var context = new TestContext(provider);
        context.ConfigureOntology(builder =>
        {
            builder.DefineNode<Person>().HasKey(person => person.Id);
            builder.DefineEdge<Friendship>()
                .HasKey(edge => edge.Id)
                .From<Person>(edge => edge.FromId)
                .To<Person>(edge => edge.ToId);
        });

        var friendship = new Friendship { Id = Guid.NewGuid(), FromId = Guid.NewGuid(), ToId = Guid.NewGuid() };
        context.Friendships.Add(friendship);
        context.Friendships.Remove(friendship);
        context.SaveChanges();

        Assert.Empty(provider.Changes);
        Assert.Equal(0, provider.ExecuteCalls);
    }

    private sealed class TestContext : GraphContext
    {
        public TestContext(IGraphProvider provider)
        {
            UseProvider(provider);
            People = new NodeSet<Person>(this);
            Friendships = new EdgeSet<Friendship>(this);
        }

        public NodeSet<Person> People { get; }
        public EdgeSet<Friendship> Friendships { get; }
    }

    private sealed class RecordingProvider : IGraphProvider
    {
        public List<GraphChange> Changes { get; } = new();
        public int ExecuteCalls { get; private set; }

        public IQueryable<T> QueryNodes<T>(OntologyModel ontology) where T : class => Array.Empty<T>().AsQueryable();
        public IQueryable<T> QueryEdges<T>(OntologyModel ontology) where T : class => Array.Empty<T>().AsQueryable();
        public string GenerateBatch(IEnumerable<GraphChange> changes, OntologyModel ontology)
        {
            Changes.AddRange(changes);
            return "batch";
        }

        public void Execute(string batch)
        {
            ExecuteCalls++;
        }

        public Task ExecuteAsync(string batch, CancellationToken cancellationToken = default)
        {
            ExecuteCalls++;
            return Task.CompletedTask;
        }

        public void Dispose() { }
    }

    private sealed class Person
    {
        public Guid Id { get; init; }
    }

    private sealed class Friendship
    {
        public Guid Id { get; init; }
        public Guid FromId { get; init; }
        public Guid ToId { get; init; }
    }
}
