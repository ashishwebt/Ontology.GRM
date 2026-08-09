using Ontology.GRM.Ontology;
using Ontology.GRM.Providers;

namespace Ontology.GRM.Tests;

public sealed class CypherQueryTranslationTests
{
    [Fact]
    public void Translates_node_filters_and_anonymous_projection()
    {
        var query = CypherQueryable<Person>.ForNodes(CreateModel())
            .Where(person => person.Name == "Alice" && person.Age >= 18)
            .Select(person => new { person.Id, person.Name });

        Assert.Equal(
            "MATCH (n:`Person`) WHERE (n.`Name` = 'Alice' AND n.`Age` >= 18) RETURN n.`Id` AS `Id`, n.`Name` AS `Name`",
            query.ToCypher());
    }

    [Fact]
    public void Translates_edge_filters_using_relationship_mapping()
    {
        var query = CypherQueryable<Friendship>.ForEdges(CreateModel())
            .Where(edge => edge.Since >= new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(
            "MATCH (from)-[r:`FRIENDS_WITH`]->(to) WHERE r.`Since` >= datetime('2026-01-01T00:00:00.0000000Z') RETURN r",
            query.ToCypher());
    }

    [Fact]
    public void Rejects_queries_for_properties_missing_from_the_ontology()
    {
        var query = CypherQueryable<Person>.ForNodes(CreateModel()).Where(person => person.Unmapped == "secret");

        var exception = Assert.Throws<InvalidOperationException>(() => query.ToCypher());

        Assert.Equal("Property 'Unmapped' is not mapped for graph type 'Person'.", exception.Message);
    }

    private static OntologyModel CreateModel()
    {
        var builder = new OntologyBuilder();
        builder.DefineNode<Person>().HasKey(person => person.Id).HasProperty(person => person.Name).HasProperty(person => person.Age);
        builder.DefineEdge<Friendship>("FRIENDS_WITH")
            .From<Person>(edge => edge.FromPersonId)
            .To<Person>(edge => edge.ToPersonId)
            .WithProperty(edge => edge.Since);
        return builder.Build();
    }

    private sealed class Person
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public int Age { get; init; }
        public string Unmapped { get; init; } = string.Empty;
    }

    private sealed class Friendship
    {
        public Guid FromPersonId { get; init; }
        public Guid ToPersonId { get; init; }
        public DateTime Since { get; init; }
    }
}
