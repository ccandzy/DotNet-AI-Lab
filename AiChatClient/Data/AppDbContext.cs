using Microsoft.EntityFrameworkCore;
using AiChatClient.Entities;

namespace AiChatClient.Data;

public class AppDbContext : DbContext
{
    public DbSet<AIRoleEntity> AIRoles => Set<AIRoleEntity>();
    public DbSet<ConversationEntity> Conversations => Set<ConversationEntity>();
    public DbSet<ChatMessageEntity> ChatMessages => Set<ChatMessageEntity>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── AIRoleEntity ──────────────────────────────────────────────
        modelBuilder.Entity<AIRoleEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedNever();

            entity.Property(e => e.Name)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(e => e.Description)
                  .HasMaxLength(1000);

            entity.Property(e => e.Avatar)
                  .HasMaxLength(500);

            entity.Property(e => e.SystemPrompt)
                  .IsRequired();

            entity.Property(e => e.Model)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.Property(e => e.Temperature)
                  .HasDefaultValue(0.7);

            entity.Property(e => e.CreateTime);

            // Index for querying enabled roles
            entity.HasIndex(e => e.IsEnabled);
        });

        // ── ConversationEntity ─────────────────────────────────────────
        modelBuilder.Entity<ConversationEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedNever();

            entity.Property(e => e.Title)
                  .IsRequired()
                  .HasMaxLength(500);

            entity.Property(e => e.Model)
                  .HasMaxLength(100);

            entity.Property(e => e.CreatedTime);

            entity.Property(e => e.UpdatedTime);

            // Foreign key: Conversation -> AIRole
            entity.HasOne(e => e.AIRole)
                  .WithMany(r => r.Conversations)
                  .HasForeignKey(e => e.AIRoleId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Index for listing conversations by role
            entity.HasIndex(e => e.AIRoleId);

            // Index for ordering by updated time
            entity.HasIndex(e => e.UpdatedTime);
        });

        // ── ChatMessageEntity ──────────────────────────────────────────
        modelBuilder.Entity<ChatMessageEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedNever();

            entity.Property(e => e.Role)
                  .IsRequired()
                  .HasMaxLength(20);

            entity.Property(e => e.Content)
                  .IsRequired();

            entity.Property(e => e.Timestamp);

            // Foreign key: ChatMessage -> Conversation
            entity.HasOne(e => e.Conversation)
                  .WithMany(c => c.Messages)
                  .HasForeignKey(e => e.ConversationId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Index for querying messages of a conversation ordered by timestamp
            entity.HasIndex(e => new { e.ConversationId, e.Timestamp });
        });
    }
}
