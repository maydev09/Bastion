namespace Bastion.BuildingBlocks.Scraping.Tests;

public class RobotsRulesTests
{
    private const string Browser = "Mozilla/5.0 (X11; Linux x86_64; rv:160.0) Gecko/20100101 Firefox/160.0";

    private const string OtoDomLike = """
        User-agent: *
        Disallow: /ajax/
        Disallow: /pl/login
        Disallow: /oferta/kontakt/
        Disallow: /*?*map=1
        Disallow: /uk/*
        Disallow: /api/query
        Allow: /api/query?crawl=true
        Allow: /
        Sitemap: https://www.example.com/sitemap.xml
        """;

    [TestCase("/pl/wyniki/wynajem/mieszkanie/cala-polska?by=LATEST&direction=DESC&limit=72", true)]
    [TestCase("/pl/oferta/sample-flat-1-ID0SAMPLE1", true)]
    [TestCase("/pl/login", false)]
    [TestCase("/oferta/kontakt/123", false)]
    [TestCase("/pl/wyniki/sprzedaz?map=1", false)]
    [TestCase("/uk/anything", false)]
    [TestCase("/api/query?x=1", false)]
    [TestCase("/api/query?crawl=true", true)]
    public void Applies_longest_matching_rule(string path, bool allowed)
    {
        var rules = RobotsRules.Parse(OtoDomLike, Browser);

        Assert.That(rules.IsAllowed(path), Is.EqualTo(allowed));
    }

    [Test]
    public void Specific_agent_group_replaces_the_star_group()
    {
        const string content = """
            User-agent: *
            Disallow: /

            User-agent: mozilla
            Disallow: /private
            """;

        var rules = RobotsRules.Parse(content, Browser);

        Assert.Multiple(() =>
        {
            Assert.That(rules.IsAllowed("/public"), Is.True);
            Assert.That(rules.IsAllowed("/private/1"), Is.False);
        });
    }

    [Test]
    public void Allow_wins_a_tie_and_dollar_anchors_the_end()
    {
        const string content = """
            User-agent: *
            Disallow: /page
            Allow: /page
            Disallow: /*.pdf$
            """;

        var rules = RobotsRules.Parse(content, Browser);

        Assert.Multiple(() =>
        {
            Assert.That(rules.IsAllowed("/page"), Is.True);
            Assert.That(rules.IsAllowed("/file.pdf"), Is.False);
            Assert.That(rules.IsAllowed("/file.pdf?download=1"), Is.True);
        });
    }

    [Test]
    public void Empty_file_allows_everything()
    {
        Assert.That(RobotsRules.Parse("", Browser).IsAllowed("/anything"), Is.True);
    }
}
