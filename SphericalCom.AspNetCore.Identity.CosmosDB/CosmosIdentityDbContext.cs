using System;
using System.Resources;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SphericalCom.AspNetCore.Identity.CosmosDB;

/// <summary>
/// Base class for the Entity Framework database context used for identity, modified to generate a database compatible with CosmosDB standards.
/// </summary>
public class CosmosIdentityDbContext<TUser, TRole, TKey, TUserClaim, TUserRole, TUserLogin, TRoleClaim, TUserToken> : IdentityDbContext<TUser, TRole, TKey, TUserClaim, TUserRole, TUserLogin, TRoleClaim, TUserToken>
    where TUser : IdentityUser<TKey>
    where TRole : IdentityRole<TKey>
    where TKey : IEquatable<TKey>
    where TUserClaim : IdentityUserClaim<TKey>
    where TUserRole : IdentityUserRole<TKey>
    where TUserLogin : IdentityUserLogin<TKey>
    where TRoleClaim : IdentityRoleClaim<TKey>
    where TUserToken : IdentityUserToken<TKey>
{
    public CosmosIdentityDbContext(DbContextOptions options) : base(options)
    {
    }

    /// <summary>
    /// Grandfathered from <see cref="IdentityUserContext{TUser}"/>.
    /// </summary>
    /// <returns>The <see cref="StoreOptions"/> object, if possible.</returns>
    private StoreOptions? GetStoreOptions() => this.GetService<IDbContextOptions>()
                        .Extensions.OfType<CoreOptionsExtension>()
                        .FirstOrDefault()?.ApplicationServiceProvider
                        ?.GetService<IOptions<IdentityOptions>>()
                        ?.Value?.Stores;

    /// <summary>
    /// Grandfathered from <see cref="IdentityUserContext{TUser}"/>.
    /// </summary>
    private sealed class PersonalDataConverter : ValueConverter<string, string>
    {
        public PersonalDataConverter(IPersonalDataProtector protector) : base(s => protector.Protect(s), s => protector.Unprotect(s), default)
        { }
    }

    private void EncryptPersonalProperties<T>(EntityTypeBuilder<T> builder) where T : class
    {
        PersonalDataConverter? converter = new PersonalDataConverter(this.GetService<IPersonalDataProtector>()); //Is this a singleton? Scoped?
        var personalDataProps = typeof(T).GetProperties().Where(
                        prop => Attribute.IsDefined(prop, typeof(ProtectedPersonalDataAttribute)));
        foreach (var p in personalDataProps)
        {
            if (p.PropertyType != typeof(string))
            {
                throw new InvalidOperationException("Can only protect strings with personal data protection.");
            }
            builder.Property(typeof(string), p.Name).HasConversion(converter);
        }
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        var storeOptions = GetStoreOptions();
        var maxKeyLength = storeOptions?.MaxLengthForKeys ?? 0;
        if (maxKeyLength == 0)
        {
            maxKeyLength = 128;
        }
        var encryptPersonalData = storeOptions?.ProtectPersonalData ?? false;

        builder.Entity<TUser>(b =>
        {
            b.ToContainer("AspNetUsers"); // Mimic convention.
            b.HasNoDiscriminator(); //Container will only ever hold this entity.
            b.HasPartitionKey(u => u.Id);
            b.HasKey(u => u.Id);
            b.Property(u => u.ConcurrencyStamp).IsETagConcurrency();

            b.Property(u => u.UserName).HasMaxLength(256);
            b.Property(u => u.NormalizedUserName).HasMaxLength(256);
            b.Property(u => u.Email).HasMaxLength(256);
            b.Property(u => u.NormalizedEmail).HasMaxLength(256);
            b.Property(u => u.PhoneNumber).HasMaxLength(256);

            if (encryptPersonalData)
            {
                EncryptPersonalProperties(b);
            }
        });

        // Consider adding a partition key to these entities. Right now, default is used.
        builder.Entity<TUserClaim>(b =>
        {
            b.ToContainer("AspNetUserClaims"); // Mimic convention.
            b.HasNoDiscriminator(); //Container will only ever hold this entity.
            b.HasKey(uc => uc.Id);
        });

        builder.Entity<TUserLogin>(b =>
        {
            b.ToContainer("AspNetUserLogins"); // Mimic convention.
            b.HasNoDiscriminator(); //Container will only ever hold this entity.
            b.HasKey(l => new { l.LoginProvider, l.ProviderKey });

            if (maxKeyLength > 0)
            {
                b.Property(l => l.LoginProvider).HasMaxLength(maxKeyLength);
                b.Property(l => l.ProviderKey).HasMaxLength(maxKeyLength);
            }
        });

        builder.Entity<TUserToken>(b =>
        {
            b.ToContainer("AspNetUserTokens"); // Mimic convention.
            b.HasNoDiscriminator(); //Container will only ever hold this entity.
            b.HasKey(t => new { t.UserId, t.LoginProvider, t.Name });

            if (maxKeyLength > 0)
            {
                b.Property(t => t.LoginProvider).HasMaxLength(maxKeyLength);
                b.Property(t => t.Name).HasMaxLength(maxKeyLength);
            }

            if (encryptPersonalData)
            {
                EncryptPersonalProperties(b);
            }
        });

        builder.Entity<TRole>(b =>
        {
            b.ToContainer("AspNetRoles"); // Mimic convention.
            b.HasNoDiscriminator(); //Container will only ever hold this entity.
            b.HasPartitionKey(r => r.Id);
            b.HasKey(r => r.Id);
            b.Property(u => u.ConcurrencyStamp).IsETagConcurrency();

            b.Property(u => u.Name).HasMaxLength(256);
            b.Property(u => u.NormalizedName).HasMaxLength(256);
        });

        builder.Entity<TRoleClaim>(b =>
        {
            b.ToContainer("AspNetRoleClaims"); // Mimic convention.
            b.HasNoDiscriminator(); //Container will only ever hold this entity.
            b.HasKey(rc => rc.Id); ;
        });

        builder.Entity<TUserRole>(b =>
        {
            b.ToContainer("AspNetUserRoles"); // Mimic convention.
            b.HasNoDiscriminator(); //Container will only ever hold this entity.
            b.HasKey(r => new { r.UserId, r.RoleId });
        });
    }

    protected CosmosIdentityDbContext()
    { }
}

/// <inheritdoc/>
public class CosmosIdentityDbContext<TUser, TRole, TKey> : CosmosIdentityDbContext<TUser, TRole, TKey, IdentityUserClaim<TKey>, IdentityUserRole<TKey>, IdentityUserLogin<TKey>, IdentityRoleClaim<TKey>, IdentityUserToken<TKey>>
    where TUser : IdentityUser<TKey>
    where TRole : IdentityRole<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// Initializes a new instance of the db context, with opinionated defaults.
    /// </summary>
    /// <param name="options">The options to be used by a <see cref="DbContext"/>.</param>
    public CosmosIdentityDbContext(DbContextOptions options) : base(options) { }

    /// <summary>
    /// Initializes a new instance of the class.
    /// </summary>
    protected CosmosIdentityDbContext()
    { }
}

/// <inheritdoc/>
public class CosmosIdentityDbContext<TUser> : CosmosIdentityDbContext<TUser, IdentityRole, string>
    where TUser : IdentityUser
{
    /// <summary>
    /// Initializes a new instance of the db context, with opinionated defaults.
    /// </summary>
    /// <param name="options">The options to be used by a <see cref="DbContext"/>.</param>
    public CosmosIdentityDbContext(DbContextOptions options) : base(options) { }

    protected CosmosIdentityDbContext()
    { }
}

/// <inheritdoc/>
public class CosmosIdentityDbContext : CosmosIdentityDbContext<IdentityUser, IdentityRole, string>
{
    /// <summary>
    /// Initializes a new instance of the db context, with opinionated defaults.
    /// </summary>
    /// <param name="options">The options to be used by a <see cref="DbContext"/>.</param>
    public CosmosIdentityDbContext(DbContextOptions options) : base(options) { }

    protected CosmosIdentityDbContext()
    { }
}