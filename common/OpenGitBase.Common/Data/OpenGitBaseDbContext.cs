using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;

namespace OpenGitBase.Common.Data;

// Partial DbContext: additional partials can live in this project.
// Feature projects extend the model via IEntityTypeConfiguration without editing this file.
public partial class OpenGitBaseDbContext : DbContext
{
    private readonly IFeatureAssemblyProvider _featureAssemblyProvider;

    public OpenGitBaseDbContext(
        DbContextOptions<OpenGitBaseDbContext> options,
        IFeatureAssemblyProvider featureAssemblyProvider
    )
        : base(options)
    {
        _featureAssemblyProvider = featureAssemblyProvider;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var assembly in _featureAssemblyProvider.Assemblies)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }
    }
}
