using Ontology.GRM.Ontology;
using Ontology.GRM.Providers;
using Ontology.GRM.Tracking;

namespace Ontology.GRM.Neo4j.Tests;

public sealed class SessionBatchingTests
{
    [Fact]
    public void Generates_unwind_create_and_relationship_match_using_temp_ids()
    {
        var alice = new Person { Id = Guid.Parse("d2719f1f-2dac-4a90-b6a9-1a5c69aae2ad"), Name = "Alice" };
        var bob = new Person { Id = Guid.Parse("ab982d51-43a3-4b9d-9684-f2a1371a34ea"), Name = "Bob" };
        var friendship = new Friendship(alice, bob) { Id = Guid.Parse("6c3a2215-cf82-4984-a7b8-77f82cf45981"), Since = new DateTime(2026,8,9,9,30,0,DateTimeKind.Utc) };

        var model = CreateModel();

        var batch = new CypherGenerator().Build(new[]
        {
            new GraphChange(alice, GraphChangeState.Added),
            new GraphChange(bob, GraphChangeState.Added),
            new GraphChange(friendship, GraphChangeState.Added)
        }, model);

        Assert.Contains("UNWIND", batch);
        Assert.Contains("__session_temp_id", batch);
        // ensure relationship clause references temp ids t1 and t2 (order follows changes array)
        Assert.Contains("`__session_temp_id` : 't1'", batch);
        Assert.Contains("`__session_temp_id` : 't2'", batch);
        Assert.Contains("FRIENDS_WITH", batch);
    }

    [Fact]
    public void Query_edges_can_filter_on_navigation_properties_by_key_values()
    {
        var provider = new GraphQueryProvider();
        using var context = new TestContext(provider);
        context.ConfigureOntology(builder =>
        {
            builder.DefineNode<Person>("Person").HasKey(person => person.Id);
            builder.DefineEdge<Friendship>("FRIENDS_WITH")
                .From<Person>(edge => edge.From)
                .To<Person>(edge => edge.To)
                .HasKey(edge => edge.Id)
                .WithProperty(edge => edge.Since);
        });

        var cypher = context.Friendships.Query()
            .Where(edge => edge.From.Id == Guid.Parse("d2719f1f-2dac-4a90-b6a9-1a5c69aae2ad"))
            .ToCypher();

        Assert.Contains("from.`Id` = 'd2719f1f-2dac-4a90-b6a9-1a5c69aae2ad'", cypher);
    }

    private static OntologyModel CreateModel()
    {
        var builder = new OntologyBuilder();
        builder.DefineNode<Person>().HasKey(person => person.Id).HasProperty(person => person.Name);
        builder.DefineEdge<Friendship>("FRIENDS_WITH")
            .From<Person>(f => f.From)
            .To<Person>(f => f.To)
            .HasKey(edge => edge.Id)
            .WithProperty(edge => edge.Since);
        return builder.Build();
    }

    private sealed class TestContext : GraphContext
    {
        public TestContext(IGraphProvider provider)
        {
            UseProvider(provider);
            Friendships = new EdgeSet<Friendship>(this);
        }

        public EdgeSet<Friendship> Friendships { get; }
    }

    private sealed class GraphQueryProvider : IGraphProvider
    {
        private readonly Neo4jProvider _neo4jProvider = new("bolt://localhost:7687", "neo4j", "password");

        public IQueryable<T> QueryNodes<T>(OntologyModel ontology) where T : class => _neo4jProvider.QueryNodes<T>(ontology);
        public IQueryable<T> QueryEdges<T>(OntologyModel ontology) where T : class => _neo4jProvider.QueryEdges<T>(ontology);
        public string GenerateBatch(IEnumerable<GraphChange> changes, OntologyModel ontology) => throw new NotSupportedException();
        public void Execute(string batch) => throw new NotSupportedException();
        public Task ExecuteAsync(string batch, CancellationToken cancellationToken = default) => Task.FromException(new NotSupportedException());
        public void Dispose() => _neo4jProvider.Dispose();
    }

    private sealed class Person
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    private sealed class Friendship
    {
        public Friendship(Person from, Person to) { From = from; To = to; }
        public Guid Id { get; init; }
        public Person From { get; init; }
        public Person To { get; init; }
        public DateTime Since { get; init; }
    }
}
