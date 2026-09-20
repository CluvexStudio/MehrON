using System.Net;
using ServiceLib.Models.Dto;
using ServiceLib.Services;

namespace ServiceLib.ViewModels;

public partial class CloudflareScannerViewModel : MyReactiveObject, ICloseable
{
    public event EventHandler? RequestClose;
    public Interaction<string, RxVoid> SetClipboardDataInteraction { get; } = new();

    public BulkObservableCollection<CloudflareIpResultItem> Results { get; } = [];
    public BulkObservableCollection<ProfileItem> Profiles { get; } = [];

    [Reactive]
    public partial int Port { get; set; } = 443;

    [Reactive]
    public partial int ThreadCount { get; set; } = 20;

    [Reactive]
    public partial int SampleCount { get; set; } = 100;

    [Reactive]
    public partial string TestHost { get; set; } = "speed.cloudflare.com";

    [Reactive]
    public partial bool EnableSpeedTest { get; set; }

    [Reactive]
    public partial string SelectedRangePreset { get; set; } = "Popular Subnets";

    public List<string> RangePresets { get; } = ["Popular Subnets", "All Cloudflare Ranges", "Custom"];

    [Reactive]
    public partial string CustomSubnets { get; set; } = string.Empty;

    [Reactive]
    public partial CloudflareIpResultItem? SelectedResult { get; set; }

    [Reactive]
    public partial ProfileItem? SelectedProfile { get; set; }

    [Reactive]
    public partial bool IsScanning { get; set; }

    [Reactive]
    public partial int ScannedCount { get; set; }

    [Reactive]
    public partial int WorkingCount { get; set; }

    [Reactive]
    public partial double ProgressPercentage { get; set; }

    [Reactive]
    public partial string StatusMessage { get; set; } = string.Empty;

    public ReactiveCommand<RxVoid, RxVoid> StartScanCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> StopScanCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> CopySelectedCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> CopyAllWorkingCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> ApplyToProfileCmd { get; }

    private CancellationTokenSource? _cts;

    public CloudflareScannerViewModel()
    {
        _config = AppManager.Instance.Config;

        StartScanCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await StartScanAsync();
        });

        StopScanCmd = ReactiveCommand.Create(() =>
        {
            StopScan();
        });

        CopySelectedCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await CopySelectedAsync();
        });

        CopyAllWorkingCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await CopyAllWorkingAsync();
        });

        ApplyToProfileCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ApplyToProfileAsync();
        });

        _ = Init();
    }

    public async Task Init()
    {
        var allProfiles = await AppManager.Instance.ProfileItems(string.Empty) ?? [];
        var validProfiles = allProfiles
            .Where(p => !p.ConfigType.IsGroupType() && p.ConfigType != EConfigType.Custom)
            .OrderBy(p => p.Remarks)
            .ToList();

        Profiles.AddRange(validProfiles);
        SelectedProfile = Profiles.FirstOrDefault(p => p.IndexId == _config.IndexId) ?? Profiles.FirstOrDefault();
    }

    public async Task StartScanAsync()
    {
        if (IsScanning) return;

        IEnumerable<string> cidrs = SelectedRangePreset switch
        {
            "All Cloudflare Ranges" => CloudflareScannerService.AllCloudflareRanges,
            "Custom" when !CustomSubnets.IsNullOrEmpty() =>
                CustomSubnets.Split([',', ';', '\r', '\n', ' '], StringSplitOptions.RemoveEmptyEntries),
            _ => CloudflareScannerService.PopularSubnets
        };

        var ips = CloudflareScannerService.GenerateRandomIps(cidrs, Math.Clamp(SampleCount, 1, 2000));
        if (ips.Count == 0)
        {
            NoticeManager.Instance.Enqueue("No valid IP ranges found.");
            return;
        }

        Results.Clear();
        ScannedCount = 0;
        WorkingCount = 0;
        ProgressPercentage = 0;
        IsScanning = true;
        StatusMessage = ResUI.TbScanning;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var total = ips.Count;
        var scanned = 0;
        var working = 0;
        var host = TestHost.Trim();
        if (host.IsNullOrEmpty()) host = "speed.cloudflare.com";
        var port = Port;
        var speedTest = EnableSpeedTest;

        try
        {
            await Parallel.ForEachAsync(ips, new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Clamp(ThreadCount, 1, 64),
                CancellationToken = token
            }, async (ip, ct) =>
            {
                var result = await CloudflareScannerService.TestIpAsync(ip, port, host, speedTest, ct);
                var currentScanned = Interlocked.Increment(ref scanned);

                if (result.IsSuccess)
                {
                    Interlocked.Increment(ref working);
                    await RxSchedulers.MainThreadScheduler.Schedule(() =>
                    {
                        Results.Add(result);
                    });
                }

                await RxSchedulers.MainThreadScheduler.Schedule(() =>
                {
                    ScannedCount = currentScanned;
                    WorkingCount = working;
                    ProgressPercentage = (currentScanned * 100.0) / total;
                });
            });

            StatusMessage = $"{ResUI.TbScanCompleted} ({working} working IPs found)";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Scan cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    public void StopScan()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }
        IsScanning = false;
    }

    private async Task CopySelectedAsync()
    {
        if (SelectedResult == null)
        {
            NoticeManager.Instance.Enqueue(ResUI.TbPleaseSelectIp);
            return;
        }
        await SetClipboardDataInteraction.HandleSafe(SelectedResult.Ip);
        NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
    }

    private async Task CopyAllWorkingAsync()
    {
        var workingIps = Results.Where(r => r.IsSuccess).Select(r => r.Ip).ToList();
        if (workingIps.Count == 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.TbPleaseSelectIp);
            return;
        }
        await SetClipboardDataInteraction.HandleSafe(string.Join(Environment.NewLine, workingIps));
        NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
    }

    private async Task ApplyToProfileAsync()
    {
        if (SelectedResult == null || !SelectedResult.IsSuccess)
        {
            NoticeManager.Instance.Enqueue(ResUI.TbPleaseSelectIp);
            return;
        }
        if (SelectedProfile == null)
        {
            NoticeManager.Instance.Enqueue(ResUI.TbPleaseSelectProfile);
            return;
        }

        var originalAddress = SelectedProfile.Address;
        if (SelectedProfile.RequestHost.IsNullOrEmpty() && !IPAddress.TryParse(originalAddress, out _))
        {
            SelectedProfile.RequestHost = originalAddress;
        }
        if (SelectedProfile.Sni.IsNullOrEmpty() && !IPAddress.TryParse(originalAddress, out _))
        {
            SelectedProfile.Sni = originalAddress;
        }

        SelectedProfile.Address = SelectedResult.Ip;

        if (await ConfigHandler.AddServerCommon(_config, SelectedProfile) == 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.TbApplySuccess);
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
        }
    }
}
