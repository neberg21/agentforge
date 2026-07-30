using Microsoft.EntityFrameworkCore;

namespace AgentForge.Areas.Abstractions;

public interface IDbProvider
{
    void Apply(DbContextOptionsBuilder options);
}
