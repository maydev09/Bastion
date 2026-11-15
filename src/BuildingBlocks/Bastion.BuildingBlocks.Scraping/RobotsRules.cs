using System.Text.RegularExpressions;

namespace Bastion.BuildingBlocks.Scraping;

internal sealed class RobotsRules
{
    public static readonly RobotsRules AllowAll = new([]);

    private readonly IReadOnlyList<Rule> _rules;

    private RobotsRules(IReadOnlyList<Rule> rules)
    {
        _rules = rules;
    }

    public static RobotsRules Parse(string content, string userAgent)
    {
        var groups = new List<Group>();
        Group? current = null;
        var previousWasAgent = false;

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Split('#')[0].Trim();
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
            {
                continue;
            }

            var key = line[..colon].Trim().ToUpperInvariant();
            var value = line[(colon + 1)..].Trim();
            if (key == "USER-AGENT")
            {
                if (current is null || !previousWasAgent)
                {
                    current = new Group();
                    groups.Add(current);
                }

                current.Agents.Add(value.ToUpperInvariant());
                previousWasAgent = true;
                continue;
            }

            previousWasAgent = false;
            if (current is not null && value.Length > 0 && key is "ALLOW" or "DISALLOW")
            {
                current.Rules.Add(new Rule(value, key == "ALLOW"));
            }
        }

        var token = userAgent.Split('/', ' ')[0].ToUpperInvariant();
        var matching = groups.Where(g => token.Length > 0 && g.Agents.Contains(token)).ToList();
        if (matching.Count == 0)
        {
            matching = groups.Where(g => g.Agents.Contains("*")).ToList();
        }

        return new RobotsRules(matching.SelectMany(g => g.Rules).ToList());
    }

    public bool IsAllowed(string pathAndQuery)
    {
        // RFC 9309: the longest matching pattern wins; on a tie Allow wins.
        var best = _rules
            .Where(r => r.Matches(pathAndQuery))
            .OrderByDescending(r => r.Pattern.Length)
            .ThenByDescending(r => r.Allow)
            .FirstOrDefault();
        return best?.Allow ?? true;
    }

    private sealed class Group
    {
        public HashSet<string> Agents { get; } = [];

        public List<Rule> Rules { get; } = [];
    }

    private sealed class Rule(string pattern, bool allow)
    {
        private readonly Regex _regex = new(
            "^" + Regex.Escape(pattern).Replace(@"\*", ".*", StringComparison.Ordinal).Replace(@"\$", "$", StringComparison.Ordinal),
            RegexOptions.CultureInvariant);

        public string Pattern => pattern;

        public bool Allow => allow;

        public bool Matches(string pathAndQuery) => _regex.IsMatch(pathAndQuery);
    }
}
