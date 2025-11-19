using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ContractProcessingSystem.Shared.Models;

namespace ContractProcessingSystem.DocumentUpload.Data;

public static class JsonHelpers
{
    public static string SerializeDict(Dictionary<string, object> dict)
    {
        return JsonSerializer.Serialize(dict, JsonSerializerOptions.Default);
    }
    
    public static Dictionary<string, object> DeserializeDict(string json)
    {
        return !string.IsNullOrEmpty(json) 
            ? JsonSerializer.Deserialize<Dictionary<string, object>>(json, JsonSerializerOptions.Default) ?? new()
            : new();
    }
}

public class DocumentContext : DbContext
{
    public DocumentContext(DbContextOptions<DocumentContext> options) : base(options)
    {
    }

    public DbSet<ContractDocumentEntity> Documents { get; set; }
    public DbSet<ContractMetadataEntity> Metadata { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ContractDocumentEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.UploadedBy).IsRequired().HasMaxLength(100);
            entity.Property(e => e.BlobPath).HasMaxLength(500);
            entity.HasIndex(e => e.UploadedAt);
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<ContractMetadataEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne<ContractDocumentEntity>()
                .WithOne(d => d.Metadata)
                .HasForeignKey<ContractMetadataEntity>(e => e.DocumentId);
            entity.Property(e => e.Title).HasMaxLength(500);
            entity.Property(e => e.ContractType).HasMaxLength(100);
            entity.Property(e => e.Currency).HasMaxLength(10);
            entity.Property(e => e.Parties).HasConversion(
                v => string.Join(';', v),
                v => v.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList());
            entity.Property(e => e.KeyTerms).HasConversion(
                v => string.Join(';', v),
                v => v.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList());
            entity.Property(e => e.CustomFields).HasConversion(
                v => JsonHelpers.SerializeDict(v),
                v => JsonHelpers.DeserializeDict(v));
        });
    }
}

public class ContractDocumentEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public string UploadedBy { get; set; } = string.Empty;
    public ContractStatus Status { get; set; } = ContractStatus.Uploaded;
    public string? BlobPath { get; set; }
    
    // Navigation property
    public ContractMetadataEntity? Metadata { get; set; }
}

public class ContractMetadataEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public string? Title { get; set; }
    public DateTime? ContractDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public decimal? ContractValue { get; set; }
    public string? Currency { get; set; }
    public List<string> Parties { get; set; } = new();
    public List<string> KeyTerms { get; set; } = new();
    public string? ContractType { get; set; }
    public Dictionary<string, object> CustomFields { get; set; } = new();
}