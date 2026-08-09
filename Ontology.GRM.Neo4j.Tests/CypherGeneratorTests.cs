using Ontology.GRM.Ontology;
using Ontology.GRM.Providers;
using Ontology.GRM.Tracking;

namespace Ontology.GRM.Tests;

public sealed class CypherGeneratorTests
{
    [Fact]
    public void Generates_metadata_driven_node_statements()
    {
        var person = new Person { Id = Guid.Parse("d2719f1f-2dac-4a90-b6a9-1a5c69aae2ad"), Name = "Alice" };
        var model = CreateModel();

        var batch = new CypherGenerator().Build([new GraphChange(person, GraphChangeState.Added)], model);

        Assert.Equal("MERGE (n:`Person` {`Id`: 'd2719f1f-2dac-4a90-b6a9-1a5c69aae2ad'}) SET n.`Name` = 'Alice'", batch);
    }

    [Fact]
    public void Generates_relationship_statements_using_configured_endpoints()
    {
        var edge = new Friendship
        {
            Id = Guid.Parse("6c3a2215-cf82-4984-a7b8-77f82cf45981"),
            FromPersonId = Guid.Parse("d2719f1f-2dac-4a90-b6a9-1a5c69aae2ad"),
            ToPersonId = Guid.Parse("ab982d51-43a3-4b9d-9684-f2a1371a34ea"),
            Since = new DateTime(2026, 8, 9, 9, 30, 0, DateTimeKind.Utc)
        };
        var model = CreateModel();

        var batch = new CypherGenerator().Build([new GraphChange(edge, GraphChangeState.Added)], model);

        Assert.Equal(
            "MATCH (from:`Person` {`Id`: 'd2719f1f-2dac-4a90-b6a9-1a5c69aae2ad'}) MATCH (to:`Person` {`Id`: 'ab982d51-43a3-4b9d-9684-f2a1371a34ea'}) MERGE (from)-[r:`FRIENDS_WITH` {`Id`: '6c3a2215-cf82-4984-a7b8-77f82cf45981'}]->(to) SET r.`Since` = datetime('2026-08-09T09:30:00.0000000Z')",
            batch);
    }

    [Fact]
    public void Generates_delete_statement_for_node()
    {
        var person = new Person { Id = Guid.Parse("d2719f1f-2dac-4a90-b6a9-1a5c69aae2ad") };

        var batch = new CypherGenerator().Build([new GraphChange(person, GraphChangeState.Deleted)], CreateModel());

        Assert.Equal("MATCH (n:`Person` {`Id`: 'd2719f1f-2dac-4a90-b6a9-1a5c69aae2ad'}) DETACH DELETE n", batch);
    }

    private static OntologyModel CreateModel()
    {
        var builder = new OntologyBuilder();
        builder.DefineNode<Person>().HasKey(person => person.Id).HasProperty(person => person.Name);
        builder.DefineEdge<Friendship>("FRIENDS_WITH")
            .From<Person>(edge => edge.FromPersonId)
            .To<Person>(edge => edge.ToPersonId)
            .HasKey(edge => edge.Id)
            .WithProperty(edge => edge.Since);
        return builder.Build();
    }

    private sealed class Person
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    private sealed class Friendship
    {
        public Guid Id { get; init; }
        public Guid FromPersonId { get; init; }
        public Guid ToPersonId { get; init; }
        public DateTime Since { get; init; }
    }
}
