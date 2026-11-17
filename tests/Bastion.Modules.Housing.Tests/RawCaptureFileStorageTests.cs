using System.Text;
using Bastion.Modules.Housing.Application.RawCaptures;
using Bastion.Modules.Housing.Domain;
using Bastion.Modules.Housing.Infrastructure.Files;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

namespace Bastion.Modules.Housing.Tests;

public class RawCaptureFileStorageTests
{
    private string _root = "";

    [SetUp]
    public void CreateRoot() => _root = Directory.CreateTempSubdirectory("bastion-raw-").FullName;

    [TearDown]
    public void DeleteRoot() => Directory.Delete(_root, recursive: true);

    [Test]
    public async Task Overwrites_a_file_left_from_a_deleted_database()
    {
        var storage = Storage();
        var capture = new RawCapture(1, Source.OtoDom, CaptureKind.ListingPage, "text/html", new Uri("https://www.otodom.pl/"), 200, DateTimeOffset.UtcNow, 0, "");
        await storage.WriteAsync(capture, Encoding.UTF8.GetBytes("old page"), CancellationToken.None);

        await storage.WriteAsync(capture, Encoding.UTF8.GetBytes("new page"), CancellationToken.None);
        var content = await storage.ReadAsync(capture, CancellationToken.None);

        Assert.That(Encoding.UTF8.GetString(content!), Is.EqualTo("new page"));
    }

    private RawCaptureFileStorage Storage() => new(
        Options.Create(new RawCaptureOptions { Path = _root }),
        new HostingEnvironment { ContentRootPath = _root });
}
