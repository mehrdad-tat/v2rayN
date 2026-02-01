namespace ServiceLib.Services;

public class AutoConfigFetcherService
{
    private static readonly string _tag = "AutoConfigFetcherService";
    private static readonly string _previousConfigsFile = "auto_config_previous.txt";
    private readonly Config _config;
    private HashSet<string> _previousConfigs = [];

    public AutoConfigFetcherService(Config config)
    {
        _config = config;
        LoadPreviousConfigs();
    }

    private string GetPreviousConfigsPath()
    {
        return Utils.GetConfigPath(_previousConfigsFile);
    }

    private void LoadPreviousConfigs()
    {
        try
        {
            var path = GetPreviousConfigsPath();
            if (File.Exists(path))
            {
                var lines = File.ReadAllLines(path);
                _previousConfigs = new HashSet<string>(lines, StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    private async Task SavePreviousConfigsAsync()
    {
        try
        {
            var path = GetPreviousConfigsPath();
            await File.WriteAllLinesAsync(path, _previousConfigs);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    public async Task<string?> FetchConfigsAsync(string url, bool useProxy = false)
    {
        if (url.IsNullOrEmpty())
        {
            return null;
        }

        try
        {
            Logging.SaveLog($"{_tag} - Fetching configs from: {url}");

            var downloadService = new DownloadService();
            var result = await downloadService.TryDownloadString(url, useProxy, string.Empty);

            return result;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return null;
        }
    }

    public async Task<string?> GetNewConfigsOnlyAsync(string url, bool useProxy = false)
    {
        var allConfigs = await FetchConfigsAsync(url, useProxy);
        if (allConfigs.IsNullOrEmpty())
        {
            return null;
        }

        var lines = allConfigs.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var newConfigs = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.IsNullOrEmpty())
            {
                continue;
            }

            if (!_previousConfigs.Contains(trimmed))
            {
                newConfigs.Add(trimmed);
                _previousConfigs.Add(trimmed);
            }
        }

        if (newConfigs.Count > 0)
        {
            await SavePreviousConfigsAsync();
            Logging.SaveLog($"{_tag} - Found {newConfigs.Count} new configs out of {lines.Length} total");
            return string.Join(Environment.NewLine, newConfigs);
        }

        Logging.SaveLog($"{_tag} - No new configs found");
        return null;
    }

    public async Task ClearPreviousConfigsAsync()
    {
        _previousConfigs.Clear();
        await SavePreviousConfigsAsync();
    }
}
