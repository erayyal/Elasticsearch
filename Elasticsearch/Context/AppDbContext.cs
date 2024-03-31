using Elasticsearch.Models;
using Microsoft.EntityFrameworkCore;

namespace Elasticsearch.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Travel> Travels { get; set; }
}