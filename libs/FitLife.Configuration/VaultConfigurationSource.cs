using Microsoft.Extensions.Configuration;

namespace FitLife.Configuration;

/// <summary>The configuration source that <see cref="VaultConfigurationExtensions.AddVault"/> adds.</summary>
internal sealed class VaultConfigurationSource(VaultSettings settings, HttpMessageHandler? handler) : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new VaultConfigurationProvider(settings, handler);
    }
}
