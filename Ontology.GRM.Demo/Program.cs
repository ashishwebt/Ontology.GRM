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
        context.Friendships.Add(new Friendship(alice, bob) { Id = Guid.NewGuid(), Since = DateTime.UtcNow });

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
                .From<Person>(f => f.From)
                .To<Person>(f => f.To);
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
        From = null!; // keep parameterless behavior for compiled callers; constructor below used by demo
        To = null!;
    }
    public Friendship(Person from, Person to)
    {
        From = from ?? throw new ArgumentNullException(nameof(from));
        To = to ?? throw new ArgumentNullException(nameof(to));
    }

    public Guid Id { get; init; }
    public Person From { get; init; }
    public Person To { get; init; }
    public DateTime Since { get; set; }

}
