using Ontology.GRM.Ontology;

namespace Ontology.GRM.Tests;

public sealed class OntologyBuilderTests
{
    [Fact]
    public void Builds_node_and_edge_metadata()
    {
        var builder = new OntologyBuilder();
        builder.DefineNode<Person>("Person").HasKey(person => person.Id).HasProperty(person => person.Name);
        builder.DefineEdge<Friendship>("FRIENDS_WITH").From<Person>(edge => edge.FromPersonId).To<Person>(edge => edge.ToPersonId).WithProperty(edge => edge.Since);

        var model = builder.Build();
        var node = model.GetNode(typeof(Person));
        var edge = model.GetEdge(typeof(Friendship));

        Assert.Equal("Person", node.Label);
        Assert.Equal("Id", node.Key!.Name);
        Assert.Contains(node.Properties, property => property.Name == "Name");
        Assert.Equal("FRIENDS_WITH", edge.RelationshipType);
        Assert.Equal(typeof(Person), edge.FromType);
        Assert.Contains(edge.Properties, property => property.Name == "Since");
    }

    [Fact]
    public void Rejects_edge_with_unmapped_endpoint()
    {
        var builder = new OntologyBuilder();
        builder.DefineNode<Person>().HasKey(person => person.Id);
        builder.DefineEdge<Friendship>().From<Person>(edge => edge.FromPersonId).To<OtherPerson>();

        var exception = Assert.Throws<InvalidOperationException>(builder.Build);
        Assert.Contains("endpoints", exception.Message);
    }

    [Fact]
    public void Rejects_node_without_a_key()
    {
        var builder = new OntologyBuilder();
        builder.DefineNode<Person>();

        var exception = Assert.Throws<InvalidOperationException>(builder.Build);

        Assert.Equal("Node 'Person' must configure a key using HasKey(...).", exception.Message);
    }

    [Fact]
    public void Rejects_duplicate_relationship_types()
    {
        var builder = new OntologyBuilder();
        builder.DefineNode<Person>().HasKey(person => person.Id);
        builder.DefineEdge<Friendship>("CONNECTED_TO").From<Person>(edge => edge.FromPersonId).To<Person>(edge => edge.ToPersonId);
        builder.DefineEdge<Colleague>("connected_to").From<Person>(edge => edge.FromPersonId).To<Person>(edge => edge.ToPersonId);

        var exception = Assert.Throws<InvalidOperationException>(builder.Build);

        Assert.Equal("Relationship type 'CONNECTED_TO' is configured more than once.", exception.Message);
    }

    [Fact]
    public void Rejects_duplicate_node_properties_at_configuration_time()
    {
        var builder = new OntologyBuilder();
        var node = builder.DefineNode<Person>().HasKey(person => person.Id).HasProperty(person => person.Name);

        var exception = Assert.Throws<InvalidOperationException>(() => node.HasProperty(person => person.Name));

        Assert.Equal("Property 'Name' is already configured for node 'Person'.", exception.Message);
    }

    [Fact]
    public void Rejects_empty_mapping_names()
    {
        var builder = new OntologyBuilder();

        var nodeException = Assert.Throws<ArgumentException>(() => builder.DefineNode<Person>(" "));
        var edgeException = Assert.Throws<ArgumentException>(() => builder.DefineEdge<Friendship>(""));

        Assert.Equal("A node label cannot be empty. (Parameter 'label')", nodeException.Message);
        Assert.Equal("An edge relationship type cannot be empty. (Parameter 'relationshipType')", edgeException.Message);
    }

    private sealed class Person { public Guid Id { get; set; } public string Name { get; set; } = string.Empty; }
    private sealed class OtherPerson { public Guid Id { get; set; } }
    private sealed class Friendship { public Guid FromPersonId { get; set; } public Guid ToPersonId { get; set; } public DateTime Since { get; set; } }
    private sealed class Colleague { public Guid FromPersonId { get; set; } public Guid ToPersonId { get; set; } }
}
