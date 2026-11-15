namespace Bastion.BuildingBlocks.Scraping.Tests;

public class RouteVpnCheckTests
{
    private static readonly string[] Prefixes = ["wg", "tun", "proton"];

    [Test]
    public void Reads_the_device_from_ip_route_output()
    {
        const string output = "13.227.146.73 dev proton0 table 245447468 src 10.2.0.2 uid 1000 \n    cache \n";

        Assert.That(RouteVpnCheck.ParseRouteDevice(output), Is.EqualTo("proton0"));
    }

    [TestCase("proton0", "", true)]
    [TestCase("wg0", "", true)]
    [TestCase("myvpn", "5: myvpn: <POINTOPOINT,NOARP,UP,LOWER_UP> mtu 1420 qdisc noqueue \\    link/none  promiscuity 0 \\    wireguard addrgenmode none ", true)]
    [TestCase("enp5s0", "2: enp5s0: <BROADCAST,MULTICAST,UP,LOWER_UP> mtu 1500 \\    link/ether 00:11:22:33:44:55 brd ff:ff:ff:ff:ff:ff", false)]
    [TestCase("wlan0", "3: wlan0: <BROADCAST,MULTICAST,UP,LOWER_UP> mtu 1500 \\    link/ether 00:11:22:33:44:66", false)]
    public void Recognizes_tunnels_by_name_or_link_type(string device, string linkDetails, bool isTunnel)
    {
        Assert.That(RouteVpnCheck.IsTunnel(device, linkDetails, Prefixes), Is.EqualTo(isTunnel));
    }
}
