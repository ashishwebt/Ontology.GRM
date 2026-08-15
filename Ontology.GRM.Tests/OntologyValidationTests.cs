using Ontology.GRM.Infrastructure;
using Ontology.GRM.Ontology;

namespace Ontology.GRM.Tests;

public sealed class OntologyValidationTests
{
    [Fact]
    public void Valid_ontology_passes_validation()
    {
        var builder = new OntologyBuilder();
        builder.DefineNode<Person>().HasKey(person => person.Id).HasProperty(person => person.Name);
        builder.DefineNode<OtherPerson>().HasKey(person => person.Id);
        builder.DefineEdge<Friendship>()
            .HasKey(edge => edge.Id)
            .From<Person>(edge => edge.FromId)
            .To<Person>(edge => edge.ToId);

        var model = builder.Build();

        new OntologyValidator().Validate(model);
    }

    [Fact]
    public void Validation_rejects_edge_with_missing_source_key()
    {
        var builder = new OntologyBuilder();
        builder.DefineNode<Person>().HasKey(person => person.Id);
        builder.DefineEdge<EdgeWithoutSourceId>().From<Person>().To<Person>();

        var model = builder.Build();
        var exception = Assert.Throws<InvalidOperationException>(() => new OntologyValidator().Validate(model));
        Assert.Equal(
            "Invalid ontology: edge 'EdgeWithoutSourceId' has no source key. Configure From<TNode>(...).",
            exception.Message);
    }

    [Fact]
    public void Validation_rejects_edge_with_incompatible_endpoint_key_types()
    {
        var builder = new OntologyBuilder();
        builder.DefineNode<Person>().HasKey(person => person.Id);
        builder.DefineEdge<InvalidFriendship>()
            .From<Person>(edge => edge.FromId)
            .To<Person>(edge => edge.ToId);

        var model = builder.Build();
        var exception = Assert.Throws<InvalidOperationException>(() => new OntologyValidator().Validate(model));

        Assert.Equal(
            "Invalid ontology: source key 'FromId' on edge 'InvalidFriendship' has type 'String', but node 'Person' uses key 'Id' of type 'Guid'. Endpoint key must be either the node key type or the node CLR type for navigation.",
            exception.Message);
    }
}

public sealed class EntityConfigurationTests
{
    [Fact]
    public void ApplyConfiguration_uses_separate_mapping_class()
    {
        var builder = new OntologyBuilder();
        builder.ApplyConfiguration(new PersonEntityConfiguration());

        var model = builder.Build();
        var node = model.GetNode(typeof(Person));

        Assert.Equal("Person", node.Label);
        Assert.Equal("Id", node.Key!.Name);
        Assert.Contains(node.Properties, property => property.Name == "Name");
    }

    private sealed class PersonEntityConfiguration : IEntityConfiguration
    {
        public void Configure(OntologyBuilder builder)
        {
            builder.DefineNode<Person>("Person")
                .HasKey(person => person.Id)
                .HasProperty(person => person.Name);
        }
    }
}

internal sealed class Person { public Guid Id { get; set; } public string Name { get; set; } = string.Empty; }
internal sealed class OtherPerson { public Guid Id { get; set; } }
internal sealed class Friendship { public Guid Id { get; set; } public Guid FromId { get; set; } public Guid ToId { get; set; } }
internal sealed class EdgeWithoutSourceId { public Guid ToId { get; set; } }
internal sealed class InvalidFriendship { public string FromId { get; set; } = string.Empty; public Guid ToId { get; set; } }
