using LexWatch.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Api.Data;

public class LexWatchDb(DbContextOptions<LexWatchDb> options) : DbContext(options)
{
    /// <summary>임베딩 차원 (docs/notes/2026-11-16-spike.md).</summary>
    public const int EmbedDim = 2048;
    /// <summary>조문 청크 임베딩 차원(Cloudflare bge-m3). 실제 값은 hybrid-rag 작업 11에서 확인한다.</summary>
    public const int ChunkEmbedDim = 1024;

    public DbSet<LawChange> LawChanges => Set<LawChange>();
    public DbSet<WorkflowRun> WorkflowRuns => Set<WorkflowRun>();
    public DbSet<GroupReview> GroupReviews => Set<GroupReview>();
    public DbSet<StepRun> StepRuns => Set<StepRun>();
    public DbSet<Evidence> Evidence => Set<Evidence>();
    public DbSet<ArticleChunk> ArticleChunks => Set<ArticleChunk>();
    public DbSet<AgentToolCall> AgentToolCalls => Set<AgentToolCall>();
    public DbSet<MemoDraft> MemoDrafts => Set<MemoDraft>();
    public DbSet<CriticFinding> CriticFindings => Set<CriticFinding>();
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PracticeGroup> PracticeGroups => Set<PracticeGroup>();
    public DbSet<ChatQuota> ChatQuotas => Set<ChatQuota>();
    public DbSet<CollectLog> CollectLogs => Set<CollectLog>();
    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasPostgresExtension("vector");

        b.Entity<LawChange>(e =>
        {
            e.HasIndex(x => x.IdempotencyKey).IsUnique();
            e.Property(x => x.LawId).HasMaxLength(64);
            e.Property(x => x.RawJson).HasColumnType("jsonb");
        });

        b.Entity<WorkflowRun>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.HasOne<LawChange>().WithMany().HasForeignKey(x => x.LawChangeId);
        });

        b.Entity<GroupReview>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Version).IsRowVersion();
            e.HasOne<WorkflowRun>().WithMany().HasForeignKey(x => x.RunId);
            e.HasIndex(x => new { x.RunId, x.GroupCode }).IsUnique();
        });

        b.Entity<StepRun>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.InputJson).HasColumnType("jsonb");
            e.Property(x => x.OutputJson).HasColumnType("jsonb");
            e.HasOne<WorkflowRun>().WithMany().HasForeignKey(x => x.RunId);
            e.HasIndex(x => new { x.Status, x.NotBefore });
        });

        b.Entity<Evidence>(e =>
        {
            e.HasKey(x => new { x.RunId, x.EvidenceId });
            e.HasOne<WorkflowRun>().WithMany().HasForeignKey(x => x.RunId);
        });

        b.Entity<ArticleChunk>(e =>
        {
            e.HasKey(x => new { x.RunId, x.ChunkId });
            e.Property(x => x.Embedding).HasColumnType($"vector({ChunkEmbedDim})");
            // 모든 청크는 원래 근거로 되돌아간다(RAG-01). 실행이 지워지면 근거와 함께 지워진다.
            e.HasOne<Evidence>().WithMany().HasForeignKey(x => new { x.RunId, x.EvidenceId }).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AgentToolCall>(e =>
        {
            e.Property(x => x.ArgsJson).HasColumnType("jsonb");
            e.HasOne<StepRun>().WithMany().HasForeignKey(x => x.StepRunId);
            e.HasIndex(x => new { x.StepRunId, x.Seq }).IsUnique();
        });

        b.Entity<MemoDraft>(e =>
        {
            e.Property(x => x.SentencesJson).HasColumnType("jsonb");
            e.Property(x => x.OpenQuestionsJson).HasColumnType("jsonb");
            e.HasOne<GroupReview>().WithMany().HasForeignKey(x => x.GroupReviewId);
            e.HasIndex(x => new { x.GroupReviewId, x.Version }).IsUnique();
        });

        b.Entity<CriticFinding>(e =>
            e.HasOne<MemoDraft>().WithMany().HasForeignKey(x => x.MemoDraftId));

        b.Entity<Approval>(e =>
        {
            e.Property(x => x.DiffJson).HasColumnType("jsonb");
            e.Property(x => x.FindingsJson).HasColumnType("jsonb");
            e.HasOne<GroupReview>().WithMany().HasForeignKey(x => x.GroupReviewId);
        });

        b.Entity<Notification>(e =>
            e.HasOne<GroupReview>().WithMany().HasForeignKey(x => x.GroupReviewId)
                .OnDelete(DeleteBehavior.Cascade));

        b.Entity<PracticeGroup>(e =>
        {
            e.HasKey(x => x.Code);
            e.Property(x => x.Code).HasMaxLength(32);
            e.Property(x => x.Embedding).HasColumnType($"vector({EmbedDim})");
            // 임베딩은 시드하지 않는다 (이후 작업에서 지연 채움).
            e.HasData(
                new PracticeGroup { Code = "labor", Name = "노동", InterestText = "근로기준·임금·근로시간·산업안전 관련 법령 개정에 관심이 있다." },
                new PracticeGroup { Code = "privacy", Name = "개인정보", InterestText = "개인정보 처리·정보통신망·위치정보 관련 법령 개정에 관심이 있다." },
                new PracticeGroup { Code = "finance", Name = "금융", InterestText = "금융소비자 보호·자본시장·전자금융 관련 법령 개정에 관심이 있다." });
        });

        b.Entity<ChatQuota>(e => e.HasKey(x => new { x.Day, x.IpHash }));

        b.Entity<CollectLog>(e => e.HasKey(x => x.Day));

        b.Entity<AiUsageLog>(e =>
        {
            e.Property(x => x.Purpose).HasMaxLength(32);
            e.Property(x => x.ActorRole).HasMaxLength(16);
            e.Property(x => x.Outcome).HasMaxLength(16);
            e.Property(x => x.ProviderKind).HasMaxLength(16);
            e.Property(x => x.ProviderName).HasMaxLength(64);
            e.Property(x => x.MaskedJson).HasColumnType("jsonb");
            e.HasIndex(x => x.At);
        });
    }
}
