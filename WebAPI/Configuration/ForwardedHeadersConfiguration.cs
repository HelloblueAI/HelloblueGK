using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ForwardedIpNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

namespace HB_NLP_Research_Lab.WebAPI.Configuration;

public static class ForwardedHeadersConfiguration
{
    public const string SectionName = "ForwardedHeaders";

    public static void Configure(
        ForwardedHeadersOptions options,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var section = configuration.GetSection(SectionName);

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = section.GetValue<int?>("ForwardLimit") ?? 1;
        if (options.ForwardLimit < 1)
        {
            throw new InvalidOperationException("ForwardedHeaders:ForwardLimit must be at least 1.");
        }

        if (section.GetValue("TrustAll", false))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "ForwardedHeaders:TrustAll is only allowed in Development. Configure KnownProxies or KnownNetworks instead.");
            }

            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
            return;
        }

        foreach (var proxy in ReadStringList(section, "KnownProxies"))
        {
            if (!IPAddress.TryParse(proxy, out var address))
            {
                throw new InvalidOperationException($"ForwardedHeaders:KnownProxies contains invalid IP address '{proxy}'.");
            }

            options.KnownProxies.Add(address);
        }

        foreach (var network in ReadStringList(section, "KnownNetworks"))
        {
            options.KnownNetworks.Add(ParseNetwork(network));
        }
    }

    private static IEnumerable<string> ReadStringList(IConfiguration section, string key)
    {
        var childSection = section.GetSection(key);
        var children = childSection.GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToArray();

        if (children.Length > 0)
        {
            return children;
        }

        return (childSection.Value ?? string.Empty)
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static ForwardedIpNetwork ParseNetwork(string value)
    {
        var parts = value.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !IPAddress.TryParse(parts[0], out var prefix)
            || !int.TryParse(parts[1], out var prefixLength))
        {
            throw new InvalidOperationException(
                $"ForwardedHeaders:KnownNetworks contains invalid CIDR network '{value}'.");
        }

        var maxPrefixLength = prefix.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
        if (prefixLength < 0 || prefixLength > maxPrefixLength)
        {
            throw new InvalidOperationException(
                $"ForwardedHeaders:KnownNetworks contains invalid prefix length '{value}'.");
        }

        return new ForwardedIpNetwork(prefix, prefixLength);
    }
}
