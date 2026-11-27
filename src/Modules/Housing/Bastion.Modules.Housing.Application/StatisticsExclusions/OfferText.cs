using System.Net;
using System.Text.RegularExpressions;

namespace Bastion.Modules.Housing.Application.StatisticsExclusions;

// What an offer's title and description tell of its kind (rules read on the stored offers, 2026-11-27). The words alone
// are weak: "miejsce postojowe" is mostly a flat with a parking space, "dla firm" an ordinary flat, so StatisticsExclusionService
// uses them only together with numbers that do not fit a flat.
public static partial class OfferText
{
    // A room in a shared flat: "Pokój do wynajęcia", "Pokój w mieszkaniu 3 pok.", "Pokoje dla studentów". Not "Pokój z
    // osobną kuchnią" or "Pokój/Kawalerka", which are flats of one room.
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
        @"^\W*pok[oó]j\b|\bpok[oó]j\s+(?:w|do|na|dla|jedno\w*|dwu\w*)\b|^\W*pokoje\s+(?:w|do|na|dla)\b|\bwynajm[ęe]\s+pok[oó]j\b",
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
