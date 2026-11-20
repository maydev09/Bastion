using System.Globalization;
using System.Text;
using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Places;

public static class PlaceNames
{
    private static readonly Dictionary<string, Voivodeship> Voivodeships = new()
    {
        [Normalize("dolnośląskie")] = Voivodeship.Dolnoslaskie,
        [Normalize("kujawsko-pomorskie")] = Voivodeship.KujawskoPomorskie,
        [Normalize("lubelskie")] = Voivodeship.Lubelskie,
        [Normalize("lubuskie")] = Voivodeship.Lubuskie,
        [Normalize("łódzkie")] = Voivodeship.Lodzkie,
        [Normalize("małopolskie")] = Voivodeship.Malopolskie,
        [Normalize("mazowieckie")] = Voivodeship.Mazowieckie,
        [Normalize("opolskie")] = Voivodeship.Opolskie,
        [Normalize("podkarpackie")] = Voivodeship.Podkarpackie,
        [Normalize("podlaskie")] = Voivodeship.Podlaskie,
        [Normalize("pomorskie")] = Voivodeship.Pomorskie,
        [Normalize("śląskie")] = Voivodeship.Slaskie,
        [Normalize("świętokrzyskie")] = Voivodeship.Swietokrzyskie,
        [Normalize("warmińsko-mazurskie")] = Voivodeship.WarminskoMazurskie,
        [Normalize("wielkopolskie")] = Voivodeship.Wielkopolskie,
        [Normalize("zachodniopomorskie")] = Voivodeship.Zachodniopomorskie,
    };

    // Lower case ASCII words joined by '-': "Bielsko-Biała" and OtoDom's "bielsko--biala" are both "bielsko-biala".
    public static string Normalize(string name)
    {
        var decomposed = name.ToLowerInvariant().Replace('ł', 'l').Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(c))
            {
                result.Append(c);
            }
            else if (result.Length > 0 && result[^1] != '-')
            {
                result.Append('-');
            }
        }

        return result.ToString().TrimEnd('-');
    }

    // Accepts "mazowieckie" and "województwo mazowieckie".
    public static Voivodeship? VoivodeshipOf(string name)
    {
        var normalized = Normalize(name);
        if (normalized.StartsWith("wojewodztwo-", StringComparison.Ordinal))
        {
            normalized = normalized["wojewodztwo-".Length..];
        }

        return Voivodeships.TryGetValue(normalized, out var voivodeship) ? voivodeship : null;
    }
}
