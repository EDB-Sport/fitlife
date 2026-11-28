extern alias booking;

using booking::BookingService.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FitLife.Flows.IntegrationTests;

/// <summary>BookingService on the flow tests' containers. Only Membership is faked: it says every account is an active member.</summary>
internal sealed class BookingHost : WebApplicationFactory<booking::Program>
{
    /// <summary>The member behind every account.</summary>
    public Guid MemberId { get; } = Guid.NewGuid();

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContainers($"booking-{Guid.NewGuid():N}");
        builder.UseSetting("Seed:Enabled", "false");
        builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IMembershipClient>(new ActiveMember(MemberId))));
    }

    private sealed class ActiveMember(Guid memberId) : IMembershipClient
    {
        public Task<MemberLookup> FindMemberAsync(Guid accountId, string bearerToken, CancellationToken cancellationToken)
        {
            return Task.FromResult(MemberLookup.Found(memberId, isActive: true));
        }
    }
}
