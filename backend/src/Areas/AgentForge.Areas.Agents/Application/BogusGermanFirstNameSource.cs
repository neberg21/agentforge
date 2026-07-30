using Bogus;

namespace AgentForge.Areas.Agents.Application;

public sealed class BogusGermanFirstNameSource : IAgentNameCandidateSource
{
    public string NextFirstName()
    {
        var faker = new Faker("de");
        return faker.Person.FirstName;
    }
}
