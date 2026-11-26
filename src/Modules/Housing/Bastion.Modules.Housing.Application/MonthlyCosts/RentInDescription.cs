using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace Bastion.Modules.Housing.Application.MonthlyCosts;

// The admin rent an offer's text states ("czynsz administracyjny: 544,08 zł", "+ czynsz 900 zł", "950 zł czynsz do
// wspólnoty"), or that it is included or there is none ("czynsz administracyjny w cenie", "w tym czynsz administracyjny",
// "bezczynszowe"). Only labels that always mean the admin rent count; "czynsz najmu" is the price.
// Precision over recall: an amount per person, per m² or with utilities ("w tym zaliczka na c.o.", "(media + Wi-Fi)")
// is left out, and the monthly cost takes no rent from a text with two different amounts.
public static partial class RentInDescription
{
    // Quote: the words around the amount, as the text writes them, so the user can check it.
    public sealed record Mention(decimal Amount, string Quote);

    // Amounts: every admin rent the text states, in its order; Included: the text says the rent is in the price or there is none.
    public sealed record Reading(IReadOnlyList<Mention> Amounts, bool Included, string? IncludedQuote);

    private const int LookAhead = 140;

    public static Reading Read(string? title, string? description)
    {
        var text = Clean($"{title}. {description}");
        var amounts = new List<Mention>();
        string? includedQuote = null;
        foreach (Match label in Label().Matches(text))
        {
            var rest = text.Substring(label.Index + label.Length, Math.Min(LookAhead, text.Length - label.Index - label.Length));
            if (Included().Match(rest) is { Success: true } included)
            {
                includedQuote ??= Quote(text, label.Index, label.Length + included.Length);
                continue;
            }

            if (AmountAfter().Match(rest) is { Success: true } after)
            {
                // The words up to the amount, and those after it up to the end of its item ("…700 zł - Zaliczka na prąd" is
                // the next item, not the rent's).
                var gap = rest[..after.Groups["whole"].Index];
                var tail = rest[(after.Index + after.Length)..];
                tail = tail[..ItemEnd(tail)];
                if (PerPerson().IsMatch(gap) || PerPerson().IsMatch(tail) || PerM2().IsMatch(tail)
                    || Utilities().IsMatch(gap) || UtilitiesAfter().IsMatch(tail) || WithUtilities().IsMatch(gap + tail))
                {
                    continue;
                }

                var value = ReadAmount(after);
                var quote = Quote(text, label.Index, label.Length + after.Index + after.Length);
                if (value == 0)
                {
                    includedQuote ??= quote;
                }
                else
                {
                    amounts.Add(new Mention(value, quote));
                }

                continue;
            }

            // "950 zł czynsz do wspólnoty", "+ 750 czynsz administracyjny": the amount right before the label.
            var start = Math.Max(0, label.Index - 40);
            if (AmountBefore().Match(text[start..label.Index]) is { Success: true } before
                && ReadAmount(before) is var amountBefore and > 0)
            {
                amounts.Add(new Mention(amountBefore, Quote(text, start + before.Index, label.Index + label.Length - start - before.Index)));
            }
        }

        if (includedQuote is null && NoRent().Match(text) is { Success: true } noRent)
        {
            includedQuote = Quote(text, noRent.Index, noRent.Length);
        }

        return new Reading(amounts, includedQuote is not null, includedQuote);
    }

    // Where an item of a list ends: a semicolon, a bullet, or a full stop or a dash before a capital letter.
    private static int ItemEnd(string text)
    {
        var end = ItemSeparator().Match(text);
        return end.Success ? end.Index : text.Length;
    }

    // Descriptions are stored as safe HTML: tags and entities go, spaces are joined.
    private static string Clean(string text)
    {
        var plain = WebUtility.HtmlDecode(Tags().Replace(text, " ")).Replace('\u00a0', ' ');
        return Spaces().Replace(plain, " ").Trim();
    }

    // "1 500", "1.500" and "1500" are thousands; ",08" or ".5" are grosze.
    private static decimal ReadAmount(Match amount)
    {
        var whole = decimal.Parse(amount.Groups["whole"].Value.Replace(" ", "").Replace(".", ""), CultureInfo.InvariantCulture);
        var fraction = amount.Groups["fraction"].Value;
        return fraction.Length == 0 ? whole : whole + decimal.Parse(fraction.PadRight(2, '0'), CultureInfo.InvariantCulture) / 100;
    }

    // From the label on, with a few words after the amount.
    private static string Quote(string text, int start, int length)
    {
        var to = Math.Min(text.Length, start + length + 25);
        return text[start..to].Trim();
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(
        @"czynsz\w*\s+(?:administracyjn\w*|adm(?:in)?\.(?!\w)|adm\b|eksploatacyjn\w*|(?:do|dla)\s+(?:wspólnoty|spółdzielni(?:\s+mieszkaniowej)?|administracji|sm\b))"
        + @"|opłat\w*\s+(?:administracyjn\w*|eksploatacyjn\w*|czynszow\w*|(?:do|dla)\s+(?:wspólnoty|spółdzielni(?:\s+mieszkaniowej)?|administracji))"
        + @"|(?:\+|plus)\s*(?!czynsz\w*\s+najmu)czynsz\w*(?:\s+(?:administracyjn\w*|eksploatacyjn\w*))?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Label();

    // What may stand between the label and the amount: "wynosi", "wynoszą", "w wysokości", "w kwocie", "w cenie", a comma, "ok.", "miesięcznie", a short bracket.
    [GeneratedRegex(
        @"^[\s:=\-–—,]*(?:\([^)]{0,40}\)\s*)?(?:wynos\w*|w\s+wysokości|w\s+kwocie|w\s+cenie(?=\s*\d)|to|około|ok\.?|ca\.?|~|aktualnie|obecnie|miesięcznie|mies\.?|:|,|\s|-|–)*"
        + @"(?<whole>\d{1,3}(?:[ .]\d{3})+|\d+)(?:[,.](?<fraction>\d{1,2}))?\s*(?:zł|zl|pln|złotych|,-)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AmountAfter();

    // An amount right before the label, not that of the deposit or the price ("kaucja 3000 zł czynsz administracyjny…").
    [GeneratedRegex(
        @"(?<!(?:kaucj\w*|depozyt\w*|cena|cen[aęy]\s+najmu|najmu|odstępne)\s*:?\s*)(?<![\d.,])(?<whole>\d{1,3}(?:[ .]\d{3})+|\d+)(?:[,.](?<fraction>\d{1,2}))?\s*(?:zł|zl|pln|,-)?\s*(?:[+\-–•]\s*)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AmountBefore();

    // A full stop or a dash starts a new item only before a capital letter: "700 zł - Zaliczka na prąd", not "1300 zł -
    // ryczałt z mediami" or "500 zł (zawiera m.in. zaliczki)".
    [GeneratedRegex(@"[;•]|\.(?=\s*\p{Lu})|\s[-–—]\s(?=\p{Lu})")]
    private static partial Regex ItemSeparator();

    // The text says the admin rent is in the price, or that there is none; never after "nie" ("cena nie obejmuje czynszu").
    [GeneratedRegex(
        @"(?<!\bnie\s+)(?:w\s+tym|zawiera\w*|obejmuj\w*|wliczon\w*|w\s+cen(?:ie|ę)(?:\s+najmu)?)(?:\s+(?:jest|są|już|również|także|zawart\w*|wliczon\w*))*\s*:?\s*(?:czynsz\s+najmu\s+(?:oraz|i)\s+)?czynsz\w*\s+(?:administracyjn\w*|adm\b|do\s+(?:spółdzielni|wspólnoty)|dla\s+(?:spółdzielni|wspólnoty))"
        + @"|bezczynszow\w*|bez\s+(?:dodatkowego\s+)?czynszu|brak\s+(?:dodatkowego\s+)?czynszu|nie\s+ma\s+(?:dodatkowego\s+|żadnego\s+)?czynszu"
        + @"|nie\s+ma\s+(?:dodatkowych\s+|żadnych\s+)?opłat\s+(?:typu\s+czynsz|za\s+zarządzanie|do\s+(?:wspólnoty|spółdzielni))"
        + @"|brak\s+(?:dodatkowych\s+)?opłat\s+(?:administracyjnych|do\s+(?:wspólnoty|spółdzielni))|(?<![\d.,]\s?)0\s*zł\s+(?:czynszu(?!\s+najmu)|opłaty\s+administracyjnej|do\s+wspólnoty)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NoRent();

    [GeneratedRegex(
        @"^[\s:\-–(]*(?:jest\s+)?(?:już\s+)?(?:w\s+cenie(?!\s*\d)|(?:wliczon|zawart|uwzględnion)\w*\s+w\s+(?:cen|czynsz)\w*|(?:wliczon|zawart|uwzględnion)\w*\s+w\s+cen\w*|po\s+stronie\s+właściciela|płaci\s+właściciel|opłaca\s+właściciel)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Included();

    [GeneratedRegex(
        @"^[^.;\n]{0,40}(?:/\s*os|osob\w*|\d\s*os\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PerPerson();

    // An amount per m² ("9,42 zł/mkw.") is a rate, not the rent.
    [GeneratedRegex(@"^\s*/\s*(?:m|mkw|m2|m²)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PerM2();

    // Utilities named between the label and the amount: "opłaty eksploatacyjne (media + Wi-Fi): 500 zł".
    [GeneratedRegex(@"media|wod[ay]|c\.\s?o\.|ogrzew|prąd|gaz|internet|wi-?fi", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Utilities();

    // Utilities listed in a bracket right after the amount: "870 zł (woda, c.o., śmieci)".
    [GeneratedRegex(@"^\s*\(\s*(?:media|wod|c\.\s?o|ogrzew|prąd|gaz)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UtilitiesAfter();

    [GeneratedRegex(
        @"z\s+mediami|wraz\s+z\s+mediami|ryczałt|w\s+tym\s+(?:media|prąd|gaz|ogrzewanie|c\.?o)|obejmuje\s+(?:media|wod|ogrzew|c\.?o)|zawiera\s+(?:media|wod|ogrzew)|zaliczk",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WithUtilities();
}
