using Microsoft.EntityFrameworkCore;

namespace RakRao.Infrastructure.Persistence;

public sealed class RakRaoDbContext(DbContextOptions<RakRaoDbContext> options) : DbContext(options)
{
    // Milestone 1 creates migration infrastructure. Domain tables arrive with their reviewed workflows.
}
