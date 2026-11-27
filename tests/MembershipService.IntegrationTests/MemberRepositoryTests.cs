using MembershipService.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace MembershipService.IntegrationTests;

/// <summary>The stores in MongoDB: an account id belongs to at most one member, even when two joins race (#170).</summary>
public sealed class MemberRepositoryTests
{
    private readonly MongoDbFixture _mongo;
    private readonly MembershipFactory _factory;

    public MemberRepositoryTests()
    {
        _mongo = new MongoDbFixture();
        _factory = new MembershipFactory(_mongo);
    }

    [OneTimeSetUp]
    public async Task StartMongoDb()
    {
        await _mongo.StartAsync().ConfigureAwait(false);
    }

    [OneTimeTearDown]
    public async Task Stop()
    {
        await _factory.DisposeAsync().ConfigureAwait(false);
        await _mongo.DisposeAsync().ConfigureAwait(false);
    }

    [Test]
    public async Task TryAdd_ASecondMemberForTheSameAccount_IsRefusedByTheStore()
    {
        // Arrange: the handler's check is skipped, as when two joins arrive at the same time
        IMemberRepository members = _factory.Services.GetRequiredService<IMemberRepository>();
        string accountId = TestTokens.NewAccountId();
        await members.TryAddAsync(Member.Join(accountId), CancellationToken.None).ConfigureAwait(false);

        // Act
        bool added = await members.TryAddAsync(Member.Join(accountId), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(added, Is.False);
    }

    [Test]
    public async Task TryAdd_AfterTheUniqueIndexCouldNotBeCreatedOnce_CreatesItOnTheNextJoin()
    {
        // Arrange: a MongoDB of its own, where two stored members with the same account id make the unique index fail once,
        // as MongoDB not answering on the first join would
        var mongo = new MongoDbFixture();
        await mongo.StartAsync().ConfigureAwait(false);
        var factory = new MembershipFactory(mongo);
        try
        {
            await mongo.RunScriptAsync("db.getSiblingDB('membership').members.insertMany([{ AccountId: 'twice' }, { AccountId: 'twice' }])").ConfigureAwait(false);
            IMemberRepository members = factory.Services.GetRequiredService<IMemberRepository>();
            await Assert.ThatAsync(() => members.TryAddAsync(Member.Join(TestTokens.NewAccountId()), CancellationToken.None), Throws.Exception).ConfigureAwait(false);
            await mongo.RunScriptAsync("db.getSiblingDB('membership').members.deleteOne({ AccountId: 'twice' })").ConfigureAwait(false);

            // Act
            bool added = await members.TryAddAsync(Member.Join(TestTokens.NewAccountId()), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.That(added, Is.True);
        }
        finally
        {
            await factory.DisposeAsync().ConfigureAwait(false);
            await mongo.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Test]
    public async Task Add_AMembership_IsFoundAgainByItsMember()
    {
        // Arrange
        IMembershipRepository memberships = _factory.Services.GetRequiredService<IMembershipRepository>();
        var membership = Membership.Start(Guid.NewGuid(), new DateOnly(2026, 11, 24));
        await memberships.AddAsync(membership, CancellationToken.None).ConfigureAwait(false);

        // Act
        Membership? found = await memberships.FindByMemberIdAsync(membership.MemberId, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(found?.Id, Is.EqualTo(membership.Id));
        Assert.That(found?.StartDate, Is.EqualTo(new DateOnly(2026, 11, 24)));
        Assert.That(found?.Type, Is.EqualTo(MembershipType.Standard));
    }

    [Test]
    public async Task Save_AMembershipThatWasPaid_StoresItsNewStatus()
    {
        // Arrange
        IMembershipRepository memberships = _factory.Services.GetRequiredService<IMembershipRepository>();
        var membership = Membership.Start(Guid.NewGuid(), new DateOnly(2026, 11, 24));
        await memberships.AddAsync(membership, CancellationToken.None).ConfigureAwait(false);
        membership.MarkPaid();

        // Act
        await memberships.SaveAsync(membership, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Membership? found = await memberships.FindByMemberIdAsync(membership.MemberId, CancellationToken.None).ConfigureAwait(false);
        Assert.That(found?.Status, Is.EqualTo(MembershipStatus.Active));
    }

    [Test]
    public async Task Save_APausedMembership_ReadsThePausePeriodBack()
    {
        // Arrange
        IMembershipRepository memberships = _factory.Services.GetRequiredService<IMembershipRepository>();
        var today = new DateOnly(2026, 11, 24);
        var membership = Membership.Start(Guid.NewGuid(), today);
        membership.MarkPaid();
        await memberships.AddAsync(membership, CancellationToken.None).ConfigureAwait(false);
        membership.Pause(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), today);

        // Act
        await memberships.SaveAsync(membership, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Membership? found = await memberships.FindByMemberIdAsync(membership.MemberId, CancellationToken.None).ConfigureAwait(false);
        Assert.That(found?.PausedFrom, Is.EqualTo(new DateOnly(2026, 12, 1)));
        Assert.That(found?.PausedTo, Is.EqualTo(new DateOnly(2026, 12, 31)));
        Assert.That(found?.StatusOn(new DateOnly(2026, 12, 15)), Is.EqualTo(MembershipStatus.Paused));
    }

    [Test]
    public async Task Remove_AMembership_IsNotFoundAgain()
    {
        // Arrange
        IMembershipRepository memberships = _factory.Services.GetRequiredService<IMembershipRepository>();
        var membership = Membership.Start(Guid.NewGuid(), new DateOnly(2026, 11, 24));
        await memberships.AddAsync(membership, CancellationToken.None).ConfigureAwait(false);

        // Act
        await memberships.RemoveAsync(membership.Id, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(await memberships.FindByMemberIdAsync(membership.MemberId, CancellationToken.None).ConfigureAwait(false), Is.Null);
    }
}
