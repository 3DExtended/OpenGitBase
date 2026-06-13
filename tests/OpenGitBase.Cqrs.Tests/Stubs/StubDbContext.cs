namespace OpenGitBase.Cqrs.Tests.Stubs;

public class StubDbContext(DbContextOptions<StubDbContext> options) : DbContext(options)
{
    public DbSet<StubEntity> StubEntities => Set<StubEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StubEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Name).IsRequired();
        });
    }
}
