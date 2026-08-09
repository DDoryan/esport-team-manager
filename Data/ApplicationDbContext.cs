using Microsoft.EntityFrameworkCore;
using RepriseWeb.Models;

namespace RepriseWeb.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<TeamActivity> TeamActivities => Set<TeamActivity>();
}