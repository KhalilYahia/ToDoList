using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OpsManager.Domain.Common;
using OpsManager.Domain.Entities;

namespace OpsManager.Repository.Configurations;

internal abstract class EntityConfigurationBase<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : BaseEntity
{
    public virtual void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();

        if (typeof(IAuditableEntity).IsAssignableFrom(typeof(TEntity)))
        {
            builder.Property(nameof(IAuditableEntity.CreatedAt)).IsRequired();
            builder.Property(nameof(IAuditableEntity.UpdatedAt)).IsRequired();
        }

        if (typeof(ITenantEntity).IsAssignableFrom(typeof(TEntity)))
        {
            builder.Property(nameof(ITenantEntity.OrganizationId)).IsRequired();
            builder.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(nameof(ITenantEntity.OrganizationId))
                .OnDelete(DeleteBehavior.Restrict);
        }

        if (typeof(ISoftDeletable).IsAssignableFrom(typeof(TEntity)))
        {
            builder.Property(nameof(ISoftDeletable.DeletedAt));
        }
    }
}

internal static class ConfigurationSupport
{
    public static PropertyBuilder<TEnum> AsString<TEnum>(this PropertyBuilder<TEnum> property)
        where TEnum : struct, Enum => property.HasConversion<string>().HasMaxLength(64);

    public static PropertyBuilder<TEnum?> AsNullableString<TEnum>(this PropertyBuilder<TEnum?> property)
        where TEnum : struct, Enum => property.HasConversion<string>().HasMaxLength(64);

    public static void HasJsonDictionary<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, Dictionary<string, string>>> propertyExpression)
        where TEntity : class
    {
        ValueConverter<Dictionary<string, string>, string> converter = new(
            dictionary => JsonSerializer.Serialize(dictionary, (JsonSerializerOptions?)null),
            json => JsonSerializer.Deserialize<Dictionary<string, string>>(json, (JsonSerializerOptions?)null) ?? new());
        ValueComparer<Dictionary<string, string>> comparer = new(
            (left, right) => Serialize(left) == Serialize(right),
            dictionary => Serialize(dictionary).GetHashCode(StringComparison.Ordinal),
            dictionary => new Dictionary<string, string>(dictionary, StringComparer.Ordinal));

        PropertyBuilder<Dictionary<string, string>> property = builder.Property(propertyExpression);
        property.HasConversion(converter).HasColumnType("jsonb");
        property.Metadata.SetValueComparer(comparer);
    }

    private static string Serialize(Dictionary<string, string>? dictionary) =>
        JsonSerializer.Serialize(dictionary ?? new Dictionary<string, string>(), (JsonSerializerOptions?)null);
}
