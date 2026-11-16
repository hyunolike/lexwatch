using LexWatch.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Api.Data;

public class LexWatchDb(DbContextOptions<LexWatchDb> options) : DbContext(options)
{
    /// <summary>임베딩 차원 (docs/notes/2026-11-16-spike.md).</summary>
    public const int EmbedDim = 2048;

    public DbSet<LawChange> LawChanges => Set<LawChange>();
    public DbSet<WorkflowRun> WorkflowRuns => Set<WorkflowRun>();
    public DbSet<GroupReview> GroupReviews => Set<GroupReview>();
    public DbSet<StepRun> StepRuns => Set<StepRun>();
    public DbSet<Evidence> Evidence => Set<Evidence>();
    public DbSet<AgentToolCall> AgentToolCalls => Set<AgentToolCall>();
    public DbSet<MemoDraft> MemoDrafts => Set<MemoDraft>();
    public DbSet<CriticFinding> CriticFindings => Set<CriticFinding>();
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PracticeGroup> PracticeGroups => Set<PracticeGroup>();
    public DbSet<ChatQuota> ChatQuotas => Set<ChatQuota>();

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
            e.HasOne<GroupReview>().WithMany().HasForeignKey(x => x.GroupReviewId);
        });

        b.Entity<Notification>(e =>
            e.HasOne<GroupReview>().WithMany().HasForeignKey(x => x.GroupReviewId));

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
    }
}
