using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace AmazTool;

internal class UpgradeApp
{
    public static void Upgrade(string fileName)
    {
        Console.WriteLine($"{Resx.Resource.StartUnzipping}\n{fileName}");

        Utils.Waiting(5);

        if (!File.Exists(fileName))
        {
            Console.WriteLine(Resx.Resource.UpgradeFileNotFound);
            return;
        }

        Console.WriteLine(Resx.Resource.TryTerminateProcess);
        try
        {
            var procNames = new[] { Utils.V2rayN, "xray", "p-xray-p", "sing-box", "mihomo", "sni-spoof-rs", "sni-spoofing" };
            foreach (var name in procNames)
            {
                try
                {
                    var procs = Process.GetProcessesByName(name);
                    foreach (var pp in procs)
                    {
                        try
                        {
                            var path = pp.MainModule?.FileName ?? "";
                            if (string.IsNullOrEmpty(path) || path.StartsWith(Utils.StartupPath(), StringComparison.OrdinalIgnoreCase))
                            {
                                pp?.Kill();
                                pp?.WaitForExit(1000);
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            // Access may be denied without admin right. The user may not be an administrator.
            Console.WriteLine(Resx.Resource.FailedTerminateProcess + ex.StackTrace);
        }

        Console.WriteLine(Resx.Resource.StartUnzipping);
        StringBuilder sb = new();
        try
        {
            var thisAppOldFile = $"{Utils.GetExePath()}.tmp";
            File.Delete(thisAppOldFile);
            var splitKey = "/";

            using var archive = ZipFile.OpenRead(fileName);
            var nonEmptyEntries = archive.Entries.Where(e => e.Length > 0).ToList();
            bool hasTopLevelDir = false;
            var firstEntryWithSlash = nonEmptyEntries.FirstOrDefault(e => e.FullName.Contains('/'));
            if (firstEntryWithSlash != null)
            {
                var topDir = firstEntryWithSlash.FullName.Split('/')[0];
                if (!string.Equals(topDir, "bin", StringComparison.OrdinalIgnoreCase)
                    && nonEmptyEntries.All(e => e.FullName.StartsWith(topDir + "/", StringComparison.OrdinalIgnoreCase)))
                {
                    hasTopLevelDir = true;
                }
            }

            foreach (var entry in archive.Entries)
            {
                try
                {
                    if (entry.Length == 0 || entry.FullName.EndsWith('/'))
                    {
                        continue;
                    }

                    Console.WriteLine(entry.FullName);

                    string fullName;
                    if (hasTopLevelDir)
                    {
                        var lst = entry.FullName.Split(splitKey);
                        if (lst.Length <= 1)
                        {
                            continue;
                        }
                        fullName = string.Join(splitKey, lst[1..]);
                    }
                    else
                    {
                        fullName = entry.FullName;
                    }

                    var entryOutputPath = Utils.GetPath(fullName);

                    if (string.Equals(Utils.GetExePath(), entryOutputPath, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Move(Utils.GetExePath(), thisAppOldFile);
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(entryOutputPath)!);

                    TryExtractToFile(entry, entryOutputPath);

                    Console.WriteLine(entryOutputPath);
                }
                catch (Exception ex)
                {
                    sb.Append(ex.StackTrace);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(Resx.Resource.FailedUpgrade + ex.StackTrace);
            //return;
        }
        if (sb.Length > 0)
        {
            Console.WriteLine(Resx.Resource.FailedUpgrade + sb.ToString());
            //return;
        }

        Console.WriteLine(Resx.Resource.Restartv2rayN);
        Utils.Waiting(2);

        Utils.StartV2RayN();
    }

    private static bool TryExtractToFile(ZipArchiveEntry entry, string outputPath)
    {
        var retryCount = 5;
        var delayMs = 1000;

        for (var i = 1; i <= retryCount; i++)
        {
            try
            {
                entry.ExtractToFile(outputPath, true);
                return true;
            }
            catch
            {
                Thread.Sleep(delayMs * i);
            }
        }
        return false;
    }
}
