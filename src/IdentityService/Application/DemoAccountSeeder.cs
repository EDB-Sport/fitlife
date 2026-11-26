using IdentityService.Domain;
using Microsoft.AspNetCore.Identity;

namespace IdentityService.Application;

/// <summary>
/// Creates the demo accounts, one per role, for the demo and the Postman collections (#237). Test accounts, not MitID
/// (CONTEXT-MAP section 2). An account that exists already is left as it is, so a second run changes nothing.
/// </summary>
public sealed class DemoAccountSeeder
{
    /// <summary>The setting that turns the seed on. It is off unless it is <c>true</c>.</summary>
    public const string EnabledConfigurationKey = "Seed:DemoAccounts";

    private static readonly (string Email, Role Role, string PasswordKey)[] DemoAccounts =
    [
        ("medlem@fitlife.example", Role.Medlem, "Seed:MedlemPassword"),
        ("traener@fitlife.example", Role.Traener, "Seed:TraenerPassword"),
        ("admin@fitlife.example", Role.Admin, "Seed:AdminPassword"),
    ];

    private readonly IAccountRepository _repository;
    private readonly IPasswordHasher<Account> _hasher;
    private readonly IConfiguration _configuration;

    /// <summary>Creates the seeder.</summary>
    /// <param name="repository">Where accounts are stored.</param>
    /// <param name="hasher">Hashes the passwords.</param>
    /// <param name="configuration">Holds the passwords at <c>Seed:MedlemPassword</c>, <c>Seed:TraenerPassword</c> and <c>Seed:AdminPassword</c>.</param>
    public DemoAccountSeeder(IAccountRepository repository, IPasswordHasher<Account> hasher, IConfiguration configuration)
    {
        _repository = repository;
        _hasher = hasher;
        _configuration = configuration;
    }

    /// <summary>Adds each demo account that doesn't exist yet.</summary>
    /// <param name="cancellationToken">Cancels the seed.</param>
    /// <returns>A task that completes when every demo account exists.</returns>
    /// <exception cref="InvalidOperationException">A password setting is missing. Nothing is stored then.</exception>
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        string[] passwords = [.. DemoAccounts.Select(demo => Password(demo.PasswordKey))];
        for (int index = 0; index < DemoAccounts.Length; index++)
        {
            (string email, Role role, _) = DemoAccounts[index];
            if (await _repository.FindByEmailAsync(email, cancellationToken).ConfigureAwait(false) is not null)
            {
                continue;
            }

            // ! PasswordHasher<TUser> doesn't read the user, and the account can't exist before its hash does.
            string passwordHash = _hasher.HashPassword(null!, passwords[index]);
            await _repository.AddAsync(Account.Create(email, passwordHash, role), cancellationToken).ConfigureAwait(false);
        }
    }

    private string Password(string key)
    {
        string? password = _configuration[key];
        if (string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException($"Found no {key} in configuration. Set it for the demo accounts (DEMO_*_PASSWORD in deploy/compose/.env).");
        }

        return password;
    }
}
