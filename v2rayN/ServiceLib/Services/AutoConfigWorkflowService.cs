namespace ServiceLib.Services;

public class AutoConfigWorkflowService
{
    private static readonly string _tag = "AutoConfigWorkflowService";
    private static readonly SemaphoreSlim _workflowSemaphore = new(1, 1);
    private readonly Config _config;
    private readonly Func<SpeedTestResult, Task>? _speedTestUpdateFunc;

    public AutoConfigWorkflowService(Config config, Func<SpeedTestResult, Task>? speedTestUpdateFunc = null)
    {
        _config = config;
        _speedTestUpdateFunc = speedTestUpdateFunc;
    }

    public async Task RunWorkflowAsync()
    {
        var autoConfig = _config.AutoConfigItem;

        // Debug logging
        Logging.SaveLog($"{_tag}: AutoConfigItem is null: {autoConfig == null}");
        if (autoConfig != null)
        {
            Logging.SaveLog($"{_tag}: Enabled={autoConfig.Enabled}, Url={autoConfig.Url ?? "NULL"}");
        }

        if (autoConfig == null || !autoConfig.Enabled || autoConfig.Url.IsNullOrEmpty())
        {
            Logging.SaveLog($"{_tag}: Auto config is disabled or URL is empty - SKIPPING");
            return;
        }

        if (!await _workflowSemaphore.WaitAsync(0))
        {
            Logging.SaveLog($"{_tag}: Workflow already running, skipping");
            return;
        }

        try
        {
            Logging.SaveLog($"{_tag}: Starting auto config workflow");
            NoticeManager.Instance.SendMessageEx("Starting auto config workflow...");

            // Step 1: Fetch and import new configs
            var importedCount = await ImportNewConfigsAsync(autoConfig.Url);

            // Step 2: Test configs if enabled
            if (autoConfig.TestAfterImport)
            {
                await TestAllConfigsAsync();
            }

            // Step 3: Remove invalid configs if enabled
            if (autoConfig.RemoveInvalidAfterTest)
            {
                await RemoveInvalidConfigsAsync();
            }

            // Step 4: Select best server if enabled
            if (autoConfig.SelectBestAfterTest)
            {
                await SelectBestServerAsync();
            }

            Logging.SaveLog($"{_tag}: Auto config workflow completed");
            NoticeManager.Instance.SendMessageEx("Auto config workflow completed");

            AppEvents.ProfilesRefreshRequested.Publish();
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        finally
        {
            _workflowSemaphore.Release();
        }
    }

    private async Task<int> ImportNewConfigsAsync(string url)
    {
        try
        {
            Logging.SaveLog($"{_tag}: Fetching configs from URL");

            var fetcherService = new AutoConfigFetcherService(_config);
            var newConfigs = await fetcherService.GetNewConfigsOnlyAsync(url, false);

            if (newConfigs.IsNullOrEmpty())
            {
                Logging.SaveLog($"{_tag}: No new configs to import");
                return 0;
            }

            var ret = await ConfigHandler.AddBatchServers(_config, newConfigs, _config.SubIndexId, false);
            if (ret > 0)
            {
                Logging.SaveLog($"{_tag}: Imported {ret} new servers");
                NoticeManager.Instance.SendMessageEx($"Imported {ret} new servers");
                AppEvents.SubscriptionsRefreshRequested.Publish();
            }
            return ret;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return 0;
        }
    }

    private async Task TestAllConfigsAsync()
    {
        try
        {
            var lstSelected = await AppManager.Instance.ProfileItems(_config.SubIndexId);
            if (lstSelected == null || lstSelected.Count == 0)
            {
                Logging.SaveLog($"{_tag}: No profiles to test");
                return;
            }

            Logging.SaveLog($"{_tag}: Testing {lstSelected.Count} servers");
            NoticeManager.Instance.SendMessageEx($"Testing {lstSelected.Count} servers...");

            var speedtestService = new SpeedtestService(_config, async (SpeedTestResult result) =>
            {
                if (_speedTestUpdateFunc != null)
                {
                    await _speedTestUpdateFunc(result);
                }
                await Task.CompletedTask;
            });

            await speedtestService.RunLoopAsync(ESpeedActionType.Mixedtest, lstSelected);

            Logging.SaveLog($"{_tag}: Speed test completed");
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    private async Task RemoveInvalidConfigsAsync()
    {
        try
        {
            var count = await ConfigHandler.RemoveInvalidServerResult(_config, _config.SubIndexId);
            if (count > 0)
            {
                Logging.SaveLog($"{_tag}: Removed {count} invalid servers");
                NoticeManager.Instance.SendMessageEx($"Removed {count} invalid servers");
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    private async Task SelectBestServerAsync()
    {
        try
        {
            var lstProfileExs = await ProfileExManager.Instance.GetProfileExs();
            var lstProfiles = await AppManager.Instance.ProfileItems(_config.SubIndexId);

            if (lstProfiles == null || lstProfiles.Count == 0)
            {
                return;
            }

            var validProfiles = (from p in lstProfiles
                                 join ex in lstProfileExs on p.IndexId equals ex.IndexId
                                 where ex.Delay > 0
                                 orderby ex.Delay ascending
                                 select new { p.IndexId, ex.Delay }).ToList();

            if (validProfiles.Count == 0)
            {
                Logging.SaveLog($"{_tag}: No valid profiles with positive delay found");
                return;
            }

            var bestProfile = validProfiles.First();
            Logging.SaveLog($"{_tag}: Best server has delay {bestProfile.Delay}ms");

            if (bestProfile.IndexId == _config.IndexId)
            {
                Logging.SaveLog($"{_tag}: Best server is already active");
                return;
            }

            if (await ConfigHandler.SetDefaultServerIndex(_config, bestProfile.IndexId) == 0)
            {
                Logging.SaveLog($"{_tag}: Set best server as active");
                NoticeManager.Instance.SendMessageEx($"Selected best server (Delay: {bestProfile.Delay}ms)");
                AppEvents.ReloadRequested.Publish();
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }
}
