namespace Bastion.Modules.Housing.Application.ImportOffers;

public enum ImportMode
{
    // Stops after the page with an offer known from an earlier import.
    NewOffers,

    // Reads the whole listing and marks offers that are no longer in it.
    FullScan,
}
