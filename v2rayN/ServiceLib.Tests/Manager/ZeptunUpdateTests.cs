using System.IO.Compression;

namespace ServiceLib.Tests.Manager;

public class ZeptunUpdateTests
{
    [Test]
    public async Task Zeptun_ShouldBeRegisteredAsUpdatableCore()
    {
        await CoreInfoManager.Instance.IsCheckUpdateSupported(ECoreType.zeptun).Should().BeTrue();
        await Global.CoreUrls.ContainsKey(ECoreType.zeptun).Should().BeTrue();
    }

    [Test]
    public async Task ZeptunCoreInfo_ShouldResolveReleaseAssetsForEveryPlatform()
    {
        var coreInfo = CoreInfoManager.Instance.GetCoreInfo(ECoreType.zeptun);
        await coreInfo.Should().NotBeNull();
        await coreInfo!.CoreExes.Should().Contain("zeptun");
        await coreInfo.VersionArg.Should().BeEqualTo("version");

        var urls = new[]
        {
            coreInfo.DownloadUrlWin64,
            coreInfo.DownloadUrlWinArm64,
            coreInfo.DownloadUrlLinux64,
            coreInfo.DownloadUrlLinuxArm64,
            coreInfo.DownloadUrlLinuxRiscV64,
            coreInfo.DownloadUrlLinuxLoong64,
            coreInfo.DownloadUrlOSX64,
            coreInfo.DownloadUrlOSXArm64,
        };

        foreach (var url in urls)
        {
            await url.Should().NotBeNull();
            var resolved = string.Format(url!, "v1.1.1");
            await resolved.Should().StartWith("https://github.com/Noisemux/zeptun/releases/download/v1.1.1/");
            await resolved.Should().Contain("zeptun-");
        }
    }

    [Test]
    public async Task IsZipFile_ShouldTellArchivesFromBareBinaries()
    {
        var dir = Path.Combine(Path.GetTempPath(), Utils.GetGuid(false));
        Directory.CreateDirectory(dir);
        try
        {
            var bare = Path.Combine(dir, "zeptun-linux-x86_64");
            await File.WriteAllBytesAsync(bare, [0x7F, 0x45, 0x4C, 0x46, 0x02, 0x01, 0x01, 0x00]);
            await FileUtils.IsZipFile(bare).Should().BeFalse();

            var empty = Path.Combine(dir, "empty");
            await File.WriteAllBytesAsync(empty, []);
            await FileUtils.IsZipFile(empty).Should().BeFalse();

            var zip = Path.Combine(dir, "zeptun-windows-x86_64.zip");
            var payload = Path.Combine(dir, "zeptun.exe");
            await File.WriteAllTextAsync(payload, "binary");
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(payload, "zeptun-win-x86_64/zeptun.exe");
            }
            await FileUtils.IsZipFile(zip).Should().BeTrue();

            var extractTo = Path.Combine(dir, "out");
            Directory.CreateDirectory(extractTo);
            await FileUtils.ZipExtractToFile(zip, extractTo, "geo").Should().BeTrue();
            await File.Exists(Path.Combine(extractTo, "zeptun.exe")).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
