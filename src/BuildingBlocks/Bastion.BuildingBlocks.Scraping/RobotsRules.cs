using System.Globalization;
using System.Text;
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

    private sealed class Rule
    {
        private readonly Regex _regex;

        public Rule(string pattern, bool allow)
        {
            // RFC 9309: paths are compared percent-encoded (request URLs already are), and '$'
            // means "end of URL" only as the last character; elsewhere it is a literal.
            Pattern = PercentEncodeNonAscii(pattern);
            Allow = allow;
            var anchored = Pattern.EndsWith('$');
            var body = anchored ? Pattern[..^1] : Pattern;
            _regex = new Regex(
                "^" + Regex.Escape(body).Replace(@"\*", ".*", StringComparison.Ordinal) + (anchored ? "$" : ""),
                RegexOptions.CultureInvariant);
        }

        public string Pattern { get; }

        public bool Allow { get; }

        public bool Matches(string pathAndQuery) => _regex.IsMatch(pathAndQuery);

        private static string PercentEncodeNonAscii(string value)
        {
            var result = new StringBuilder(value.Length);
            foreach (var rune in value.EnumerateRunes())
            {
                if (rune.IsAscii)
                {
                    result.Append((char)rune.Value);
                    continue;
                }

                foreach (var b in Encoding.UTF8.GetBytes(rune.ToString()))
                {
                    result.Append(CultureInfo.InvariantCulture, $"%{b:X2}");
                }
            }

            return result.ToString();
        }
    }
}
