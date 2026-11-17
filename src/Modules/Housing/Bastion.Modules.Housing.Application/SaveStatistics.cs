using Bastion.Modules.Housing.Application.Abstractions;

namespace Bastion.Modules.Housing.Application;

public sealed class SaveStatistics
{
    public int Added { get; private set; }

    public int Updated { get; private set; }

    public int Unchanged { get; private set; }

    public int Skipped { get; private set; }

    public void Add(SaveCounts saved, int skipped)
    {
        Added += saved.Added;
        Updated += saved.Updated;
        Unchanged += saved.Unchanged;
        Skipped += skipped;
    }
}
