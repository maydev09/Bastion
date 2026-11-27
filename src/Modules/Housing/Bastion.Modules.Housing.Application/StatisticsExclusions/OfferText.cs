using System.Net;
using System.Text.RegularExpressions;

namespace Bastion.Modules.Housing.Application.StatisticsExclusions;

// What an offer's title and description tell of its kind (rules read on the stored offers, 2026-11-27). The words alone
// are weak: "miejsce postojowe" is mostly a flat with a parking space, "dla firm" an ordinary flat, so StatisticsExclusionService
// uses them only together with numbers that do not fit a flat.
public static partial class OfferText
{
    // A room in a shared flat: a title that starts with it ("Pokój do wynajęcia", "Pokoje dla studentów"), or a phrase that
    // offers one anywhere ("Od zaraz pokój w mieszkaniu 3 pokojowym", "Wynajmę pokój", "pokój jednoosobowy"). Not a room of a
    // flat named in its title ("3 POKÓJ DO PRACY", "pokój dla dziecka"), nor "Pokój z osobną kuchnią" or "Pokój/Kawalerka",
    // which are flats of one room.
    public static bool IsRoom(string title) => Room().IsMatch(title) && !FlatOfOneRoom().IsMatch(title);

    // A parking space or a garage, and no word of a flat ("Mieszkanie 2-pokojowe z garażem" is a flat).
    public static bool IsParking(string title) => Parking().IsMatch(title) && !Flat().IsMatch(title);

    public static bool IsAuction(string? description) => Auction().IsMatch(Clean(description));

    // Workers' quarters, or a price per person or bed.
    public static bool IsPerPerson(string title, string? description) =>
        PerPersonTitle().IsMatch(title) || PerPersonDescription().IsMatch(Clean(description));

    public static bool IsRentForRenovation(string title, string? description) =>
        ForRenovation().IsMatch(title) || ForRenovation().IsMatch(Clean(description));

    private static string Clean(string? text) =>
        text is null ? "" : Spaces().Replace(WebUtility.HtmlDecode(Tags().Replace(text, " ")).Replace(' ', ' '), " ");

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(
        @"^\W*pok[oó]j\b|^\W*pokoje\s+(?:w|do|na|dla)\b|\bwynajm[ęe]\s+pok[oó]j\b|\bpok[oó]j\s+(?:do|na)\s+wynaj|\bpok[oó]j\s+(?:jedno|dwu)osob|\bpok[oó]j\s+(?:\S+\s+){0,3}w\s+(?:\S+\s+){0,2}mieszkani|\bpok[oó]j\s+dla\s+(?:student|os[oó]b|pracown|pani\b|pana\b|kobiet|m[eę][żz]czyzn|pary)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Room();

    [GeneratedRegex(@"\bpok[oó]j\s+z\s+(?:osobn\w*\s+)?(?:kuch|aneks)|kawaler|studio|\bapartament\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FlatOfOneRoom();

    [GeneratedRegex(@"mieszka|kawaler|apartament|studio|pok[oó]j|pokoi|pokoje|pokojow", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Flat();

    [GeneratedRegex(@"\bmiejsc[ea]\s+(?:postojow|parkingow)|\bgaraż", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Parking();

    // The municipal companies auction the debt of a flat ("Minimalna kwota wierzytelności do licytacji: 12 700 zł") or let
    // it by tender ("konkurs na najem", "wadium").
    [GeneratedRegex(@"licytacj|przetarg|konkurs\w*\s+na\s+najem|kwot\w*\s+wierzytelności|wadium",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Auction();

    [GeneratedRegex(@"pracowni|kwater|noclegow|hostel|\bdla\s+firm|\bdla\s+\d+\s*(?:os|pracown)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PerPersonTitle();

    // "35 zł od osoby", "650 zł/os.", "500 zł za łóżko".
    [GeneratedRegex(@"kwatery\s+pracownicze|najem\s+pracowniczy|\d\s*(?:zł|pln)\s*(?:/|za|od)\s*(?:os\.|osob|łóżk)|\bod\s+osoby",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PerPersonDescription();

    // "najem za remont", "w zamian za prace remontowe".
    [GeneratedRegex(@"\bza\s+remont|\bw\s+zamian\s+za\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForRenovation();
}
