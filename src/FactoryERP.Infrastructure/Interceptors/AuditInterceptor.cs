using System.Text;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Interfaces;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FactoryERP.Infrastructure.Interceptors;

public sealed class AuditInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;

    public AuditInterceptor(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAudit(DbContext? context)
    {
        if (context == null) return;

        string user = string.IsNullOrWhiteSpace(_currentUserService.UserId) ? "system" : _currentUserService.UserId!;
        DateTime now = DateTime.UtcNow;

        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            if (entry.Entity is BaseEntity baseEntity)
            {
                if (entry.State == EntityState.Added)
                {
                    baseEntity.CreatedAt = now;
                    baseEntity.CreatedBy = user;
                }
                else if (entry.State == EntityState.Modified)
                {
                    baseEntity.UpdatedAt = now;
                    baseEntity.UpdatedBy = user;
                }
            }

            if (entry.Entity is AuditLog or DataProtectionKey) continue;

            string pk = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString() ?? "N/A";
            context.Set<AuditLog>().Add(new AuditLog
            {
                UserId = user,
                EntityName = entry.Entity.GetType().Name,
                EntityId = pk,
                Action = entry.State.ToString(),
                OldValues = entry.State is EntityState.Modified or EntityState.Deleted ? SerializeValues(entry, useOriginal: true) : null,
                NewValues = entry.State is EntityState.Added or EntityState.Modified ? SerializeValues(entry, useOriginal: false) : null,
                Timestamp = now
            });
        }
    }

    private static string SerializeValues(EntityEntry entry, bool useOriginal)
    {
        var sb = new StringBuilder();
        string[] hiddenFields = ["PasswordHash", "SecurityStamp", "ConcurrencyStamp"];

        foreach (var prop in entry.Properties)
        {
            if (prop.IsTemporary || prop.Metadata.IsPrimaryKey()) continue;
            if (hiddenFields.Contains(prop.Metadata.Name)) continue;

            object? val = useOriginal ? prop.OriginalValue : prop.CurrentValue;
            if (entry.Entity is SystemSetting s && prop.Metadata.Name == nameof(SystemSetting.Value) &&
                (s.Key.Contains("Password", StringComparison.OrdinalIgnoreCase) || s.Key.Contains("Secret", StringComparison.OrdinalIgnoreCase)))
            {
                val = "********";
            }

            if (val != null)
            {
                sb.Append($"{prop.Metadata.Name}: {val}, ");
            }
        }

        return sb.ToString().TrimEnd(',', ' ');
    }
}
