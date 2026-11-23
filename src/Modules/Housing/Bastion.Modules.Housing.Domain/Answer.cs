namespace Bastion.Modules.Housing.Domain;

// A yes/no attribute of an offer as its portal states it. Stored as integer; never change the values.
// A null column means not known yet (e.g. the detail page is not fetched), unlike NotStated: the page says nothing.
public enum Answer
{
    Yes = 1,
    No = 2,
    NotStated = 3,
}
