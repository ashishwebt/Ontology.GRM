using Ontology.GRM.Infrastructure;
using Ontology.GRM.Ontology;
using Ontology.GRM.Providers;
using Ontology.GRM.Tracking;

namespace Ontology.GRM.Tests;

public sealed class InfrastructureTests
{
    [Fact]
    public void Save_changes_logs_and_invokes_audit_hooks()
    {
        var provider = new RecordingProvider();
        using var context = new TestContext(provider);
        var logger = new GraphLogger();
        var interceptor = new RecordingInterceptor();
        context.UseLogger(logger);
        context.AddInterceptor(interceptor);
        context.ConfigureOntology(builder => builder.DefineNode<Person>().HasKey(person => person.Id));

        context.People.Add(new Person { Id = Guid.NewGuid() });
        context.SaveChanges();

        Assert.Equal(["saving", "saved"], interceptor.Events);
        Assert.Equal(1, provider.ExecuteCalls);
        Assert.Contains(logger.Entries, entry => entry.Message == "Graph ontology configured.");
        Assert.Contains(logger.Entries, entry => entry.Message == "Saving 1 graph change(s).");
        Assert.Contains(logger.Entries, entry => entry.Message == "Saved 1 graph change(s).");
    }

    [Fact]
    public void Save_changes_reports_unmapped_entity_with_a_clear_error()
    {
        using var context = new TestContext(new RecordingProvider());
        context.ConfigureOntology(builder => builder.DefineNode<Person>().HasKey(person => person.Id));
        context.OtherNodes.Add(new OtherPerson());

        var exception = Assert.Throws<InvalidOperationException>(context.SaveChanges);

        Assert.Equal("Cannot save 'OtherPerson': it is not configured as a node or edge in the current ontology.", exception.Message);
    }

    [Fact]
    public void Configure_ontology_reports_missing_conventional_edge_endpoint_property()
    {
        using var context = new TestContext(new RecordingProvider());

        var exception = Assert.Throws<InvalidOperationException>(() => context.ConfigureOntology(builder =>
        {
            builder.DefineNode<Person>().HasKey(person => person.Id);
            builder.DefineEdge<EdgeWithoutEndpoints>().From<Person>().To<Person>();
        }));

        Assert.Equal("Invalid ontology: edge 'EdgeWithoutEndpoints' has no source key. Configure From<TNode>(...).", exception.Message);
    }

    [Fact]
    public void Configure_ontology_rejects_an_edge_key_with_an_incompatible_endpoint_type()
    {
        using var context = new TestContext(new RecordingProvider());

        var exception = Assert.Throws<InvalidOperationException>(() => context.ConfigureOntology(builder =>
        {
            builder.DefineNode<Person>().HasKey(person => person.Id);
            builder.DefineEdge<InvalidFriendship>()
                .From<Person>(edge => edge.FromId)
                .To<Person>(edge => edge.ToId);
        }));

        Assert.Equal("Invalid ontology: source key 'FromId' on edge 'InvalidFriendship' has type 'String', but node 'Person' uses key 'Id' of type 'Guid'. Endpoint key must be either the node key type or the node CLR type for navigation.", exception.Message);
    }

    [Fact]
    public void Failed_ontology_configuration_is_logged()
    {
        using var context = new TestContext(new RecordingProvider());
        var logger = new GraphLogger();
        context.UseLogger(logger);

        Assert.Throws<InvalidOperationException>(() => context.ConfigureOntology(builder => builder.DefineNode<Person>()));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(GraphLogLevel.Error, entry.Level);
        Assert.Equal("Graph ontology configuration failed.", entry.Message);
        Assert.IsType<InvalidOperationException>(entry.Exception);
    }

    [Fact]
    public void Failed_save_logs_error_and_invokes_failure_hook()
    {
        var provider = new RecordingProvider { ExecuteException = new InvalidOperationException("Database unavailable") };
        using var context = new TestContext(provider);
        var logger = new GraphLogger();
        var interceptor = new RecordingInterceptor();
        context.UseLogger(logger);
        context.AddInterceptor(interceptor);
        context.ConfigureOntology(builder => builder.DefineNode<Person>().HasKey(person => person.Id));
        context.People.Add(new Person { Id = Guid.NewGuid() });

        var exception = Assert.Throws<InvalidOperationException>(context.SaveChanges);

        Assert.Equal("Database unavailable", exception.Message);
        Assert.Equal(["saving", "failed"], interceptor.Events);
        var entry = Assert.Single(logger.Entries, entry => entry.Level == GraphLogLevel.Error);
        Assert.Equal("Saving graph changes failed.", entry.Message);
        Assert.Same(exception, entry.Exception);
    }

    private sealed class TestContext : GraphContext
    {
        public TestContext(IGraphProvider provider)
        {
            UseProvider(provider);
            People = new NodeSet<Person>(this);
            OtherNodes = new NodeSet<OtherPerson>(this);
        }

        public NodeSet<Person> People { get; }
        public NodeSet<OtherPerson> OtherNodes { get; }
    }

    private sealed class RecordingInterceptor : GraphInterceptor
    {
        public List<string> Events { get; } = [];

        public override void SavingChanges(GraphInterceptionContext context) => Events.Add("saving");
        public override void SavedChanges(GraphInterceptionContext context) => Events.Add("saved");
        public override void SaveChangesFailed(GraphInterceptionContext context, Exception exception) => Events.Add("failed");
    }

    private sealed class RecordingProvider : IGraphProvider
    {
        public int ExecuteCalls { get; private set; }
        public Exception? ExecuteException { get; init; }

        public IQueryable<T> QueryNodes<T>(OntologyModel ontology) where T : class => Array.Empty<T>().AsQueryable();
        public IQueryable<T> QueryEdges<T>(OntologyModel ontology) where T : class => Array.Empty<T>().AsQueryable();
        public string GenerateBatch(IEnumerable<GraphChange> changes, OntologyModel ontology) => "batch";

        public void Execute(string batch)
        {
            ExecuteCalls++;
            if (ExecuteException is not null)
                throw ExecuteException;
        }

        public Task ExecuteAsync(string batch, CancellationToken cancellationToken = default)
        {
            Execute(batch);
            return Task.CompletedTask;
        }

        public void Dispose() { }
    }

    private sealed class Person { public Guid Id { get; init; } }
    private sealed class OtherPerson { }
    private sealed class EdgeWithoutEndpoints { }
    private sealed class InvalidFriendship { public string FromId { get; set; } = string.Empty; public Guid ToId { get; set; } }
}
