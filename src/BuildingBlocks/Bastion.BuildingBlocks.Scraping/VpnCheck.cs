using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bastion.BuildingBlocks.Scraping;

internal interface IVpnCheck
{
    Task EnsureTunnelAsync(string host, CancellationToken cancellationToken);
}

internal sealed partial class RouteVpnCheck(IOptions<ScrapingOptions> options, ILogger<RouteVpnCheck> logger) : IVpnCheck
{
    public async Task EnsureTunnelAsync(string host, CancellationToken cancellationToken)
    {
        if (!options.Value.RequireVpn)
        {
            LogVpnCheckDisabled(logger, host);
            return;
        }

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        }
        catch (System.Net.Sockets.SocketException e)
        {
            throw Fail(host, $"cannot resolve the host: {e.Message}");
        }

        // Every routable address is checked: HttpClient may connect to any of them. An address with
        // no route (typically IPv6 behind a VPN) cannot carry traffic, so it is skipped. Anything else fails closed.
        var checkedAddresses = 0;
        foreach (var address in addresses)
        {
            var route = await RunIp(host, cancellationToken, "route", "get", address.ToString());
            if (route.ExitCode != 0 && IsNoRoute(route.Error))
            {
                continue;
            }

            var device = ParseRouteDevice(Succeeded(host, route))
                ?? throw Fail(host, $"cannot find the network interface for {address}");
            var link = Succeeded(host, await RunIp(host, cancellationToken, "-d", "-o", "link", "show", "dev", device));
            if (!IsTunnel(device, link, options.Value.VpnInterfacePrefixList))
            {
                throw Fail(host, $"traffic to {address} goes through '{device}', which is not a VPN tunnel");
            }

            checkedAddresses++;
        }

        if (checkedAddresses == 0)
        {
            throw Fail(host, "no address of the host is reachable");
        }
    }

    internal static string? ParseRouteDevice(string ipRouteOutput)
    {
        var match = DevRegex().Match(ipRouteOutput);
        return match.Success ? match.Groups[1].Value : null;
    }

    internal static bool IsNoRoute(string ipError) => ipError.Contains("unreachable", StringComparison.OrdinalIgnoreCase);

    internal static bool IsTunnel(string device, string ipLinkDetails, IEnumerable<string> namePrefixes) =>
        namePrefixes.Any(p => device.StartsWith(p, StringComparison.OrdinalIgnoreCase))
        || LinkTypeRegex().IsMatch(ipLinkDetails);

    private static async Task<IpResult> RunIp(string host, CancellationToken cancellationToken, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("ip") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo)!;
            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = process.StandardError.ReadToEndAsync(cancellationToken);
            await Task.WhenAll(output, error);
            await process.WaitForExitAsync(cancellationToken);
            return new IpResult(string.Join(' ', arguments), process.ExitCode, output.Result, error.Result);
        }
        catch (Win32Exception e)
        {
            throw Fail(host, $"cannot run the 'ip' command: {e.Message}");
        }
    }

    private static string Succeeded(string host, IpResult result) =>
        result.ExitCode == 0 ? result.Output : throw Fail(host, $"'ip {result.Command}' failed: {result.Error.Trim()}");

    private static ScrapingException Fail(string host, string reason) =>
        new(ScrapingException.VpnRequired, $"VPN check failed for {host}: {reason}. Connect the VPN and try again.");

    private sealed record IpResult(string Command, int ExitCode, string Output, string Error);

    [GeneratedRegex(@"\bdev\s+(\S+)")]
    private static partial Regex DevRegex();

    [GeneratedRegex(@"\s(wireguard|tun)\s")]
    private static partial Regex LinkTypeRegex();

    [LoggerMessage(Level = LogLevel.Warning, Message = "VPN check is disabled (Scraping:RequireVpn = false), request to {Host} is not checked")]
    private static partial void LogVpnCheckDisabled(ILogger logger, string host);
}
