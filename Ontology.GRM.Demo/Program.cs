using Ontology.GRM;
using Ontology.GRM.Providers;

namespace Ontology.GRM.Demo;

public static class Program
{
    public static void Main()
    {
        Console.WriteLine("Starting SocialGraph demo...");

        using var context = CreateSocialGraphContext();

        var alice = new Person { Id = Guid.NewGuid(), Name = "Alice" };
        var bob = new Person { Id = Guid.NewGuid(), Name = "Bob" };

        context.People.Add(alice);
        context.People.Add(bob);
        context.Friendships.Add(new Friendship(alice.Id, bob.Id) { Id = Guid.NewGuid(), Since = DateTime.UtcNow });

        context.SaveChanges();

        Console.WriteLine("Saved social graph changes.");
    }

    private static SocialGraphContext CreateSocialGraphContext()
    {
        var uri = "bolt://localhost:7687";
        var user = "neo4j";
        var password = "password";

        return new SocialGraphContext(new Neo4jProvider(uri, user, password));
    }
}

public sealed class SocialGraphContext : GraphContext
{
    public NodeSet<Person> People { get; }
    public EdgeSet<Friendship> Friendships { get; }

    public SocialGraphContext(IGraphProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        People = new NodeSet<Person>(this);
        Friendships = new EdgeSet<Friendship>(this);

        UseProvider(provider);
        ConfigureOntology(builder =>
        {
            builder.DefineNode<Person>()
                .HasKey(person => person.Id)
                .HasProperty(person => person.Name);

            builder.DefineEdge<Friendship>()
                .From<Person>(p1 => p1.FromId)
                .To<Person>(p2 => p2.ToId);
        });
    }
}

public sealed class Person
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class Friendship
{
    public Friendship(Guid fromId, Guid toId)
    {
        FromId = fromId;
        ToId = toId;

    }
    public Guid Id { get; init; }
    public Guid FromId { get; init; }
    public Guid ToId { get; init; }
    public DateTime Since { get; set; }

}
