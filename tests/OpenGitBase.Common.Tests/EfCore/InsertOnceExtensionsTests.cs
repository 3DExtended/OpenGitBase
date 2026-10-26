using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Common.Tests.EfCore;

public class InsertOnceExtensionsTests
{
    private static readonly Guid NoteId = Guid.Parse("0d8b7a4e-3c55-4f0e-9a51-6c1d2b8e7f90");

    [Fact]
    public async Task InsertOnceAsync_ReturnsTheEarlierAttemptWhenTheReplayCollidesWithItsLateCommit()
    {
        // On a stalled server the timed-out COMMIT may still be in flight when the retry starts: the replay does not
        // see the row yet, and its insert collides with it once that COMMIT lands.
        await using var connection = SqliteTestConnection.OpenInMemory();
        var lostCommit = new LostCommitSimulator();
        await using var context = await CreateContextAsync(connection, lostCommit.Configure);
        lostCommit.LoseNextCommitInserting("notes");
        var attempts = 0;
        var confirmations = 0;

        var result = await context.InsertOnceAsync(
            async ct =>
            {
                attempts++;
                context.Notes.Add(new Note { Id = NoteId, Title = $"attempt {attempts}" });
                await context.SaveChangesAsync(ct);
                return attempts;
            },
            async ct => ++confirmations > 1 && await context.Notes.AnyAsync(note => note.Id == NoteId, ct),
            CancellationToken.None
        );

        Assert.Equal(1, lostCommit.LostCommits);
        Assert.Equal(2, attempts);
        Assert.Equal(1, result);
        var stored = Assert.Single(await context.Notes.AsNoTracking().ToListAsync());
        Assert.Equal("attempt 1", stored.Title);
    }

    [Fact]
    public async Task InsertOnceAsync_RethrowsACollisionWithARowItDidNotInsert()
    {
        await using var connection = SqliteTestConnection.OpenInMemory();
        await using var context = await CreateContextAsync(connection);
        context.Notes.Add(new Note { Id = NoteId, Title = "already there" });
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            context.InsertOnceAsync(
                async ct =>
                {
                    context.Notes.Add(new Note { Id = NoteId, Title = "mine" });
                    await context.SaveChangesAsync(ct);
                    return true;
                },
                ct => context.Notes.AnyAsync(note => note.Id == NoteId, ct),
                CancellationToken.None
            )
        );
    }

    private static async Task<NotesContext> CreateContextAsync(
        SqliteConnection connection,
        Action<DbContextOptionsBuilder>? configure = null
    )
    {
        var options = new DbContextOptionsBuilder<NotesContext>().UseSqlite(connection);
        configure?.Invoke(options);
        var context = new NotesContext(options.Options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private sealed class NotesContext(DbContextOptions<NotesContext> options) : DbContext(options)
    {
        public DbSet<Note> Notes => Set<Note>();

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<Note>().ToTable("notes");
    }

    private sealed class Note
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;
    }
}
