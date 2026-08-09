using Ontology.GRM.Ontology;
using Ontology.GRM.Providers;
using Ontology.GRM.Tracking;

namespace Ontology.GRM.Neo4j.Tests;

public sealed class QueryLayerTests
{
    [Fact]
    public void GraphContext_QueryNodes_translates_filters_and_projections()
    {
        var provider = new GraphQueryProvider();
        using var context = new TestContext(provider);
        context.ConfigureOntology(builder =>
        {
            builder.DefineNode<Person>("Person")
                .HasKey(person => person.Id)
                .HasProperty(person => person.Name)
                .HasProperty(person => person.Age);
        });

        var cypher = context.QueryNodes<Person>()
            .Where(person => person.Name == "Alice" && person.Age >= 18)
            .Select(person => new { person.Id, person.Name })
            .ToCypher();

        Assert.Equal(
            "MATCH (n:`Person`) WHERE (n.`Name` = 'Alice' AND n.`Age` >= 18) RETURN n.`Id` AS `Id`, n.`Name` AS `Name`",
            cypher);
    }

    [Fact]
    public void GraphContext_QueryEdges_translates_relationship_filters()
    {
        var provider = new GraphQueryProvider();
        using var context = new TestContext(provider);
        context.ConfigureOntology(builder =>
        {
            builder.DefineNode<Person>("Person").HasKey(person => person.Id);
            builder.DefineEdge<Friendship>("FRIENDS_WITH")
                .From<Person>(edge => edge.FromPersonId)
                .To<Person>(edge => edge.ToPersonId)
                .WithProperty(edge => edge.Since);
        });

        var cypher = context.QueryEdges<Friendship>()
            .Where(edge => edge.Since >= new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            .ToCypher();

        Assert.Equal(
            "MATCH (from)-[r:`FRIENDS_WITH`]->(to) WHERE r.`Since` >= datetime('2026-01-01T00:00:00.0000000Z') RETURN r",
            cypher);
    }

    [Fact]
    public void NodeSet_Query_delegates_through_graph_context()
    {
        var provider = new GraphQueryProvider();
        using var context = new TestContext(provider);
        context.ConfigureOntology(builder =>
        {
            builder.DefineNode<Person>("Person")
                .HasKey(person => person.Id)
                .HasProperty(person => person.Name);
        });

        var cypher = context.People.Query()
            .Where(person => person.Name == "Bob")
            .ToCypher();

        Assert.Equal("MATCH (n:`Person`) WHERE n.`Name` = 'Bob' RETURN n", cypher);
    }

    [Fact]
    public void EdgeSet_Query_delegates_through_graph_context()
    {
        var provider = new GraphQueryProvider();
        using var context = new TestContext(provider);
        context.ConfigureOntology(builder =>
        {
            builder.DefineNode<Person>("Person").HasKey(person => person.Id);
            builder.DefineEdge<Friendship>("FRIENDS_WITH")
                .From<Person>(edge => edge.FromPersonId)
                .To<Person>(edge => edge.ToPersonId)
                .WithProperty(edge => edge.Since);
        });

        var cypher = context.Friendships.Query()
            .Where(edge => edge.Since >= new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            .ToCypher();

        Assert.Equal(
            "MATCH (from)-[r:`FRIENDS_WITH`]->(to) WHERE r.`Since` >= datetime('2026-01-01T00:00:00.0000000Z') RETURN r",
            cypher);
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
        public int Age { get; init; }
    }

    private sealed class Friendship
    {
        public Guid FromPersonId { get; init; }
        public Guid ToPersonId { get; init; }
        public DateTime Since { get; init; }
    }
}
