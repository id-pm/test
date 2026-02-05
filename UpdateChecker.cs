using Microsoft.Win32;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

public class GitHubReleaseInfo
{
    public string Tag_Name { get; set; }
    public string Name { get; set; }
    public GitHubAsset[] Assets { get; set; }
    public bool Prerelease { get; set; }
    public bool Draft { get; set; }
}

public class GitHubAsset
{
    public string Name { get; set; }
    public string BrowserDownloadUrl { get; set; }
}

public static class UpdateChecker
{

    private const string RegistryPath = @"Software\Lingy";
    private const string Path = "InstallPath";
    private const string Version = "Version";
    private static string _currentVersion = string.Empty;
    private static string _currentPath = string.Empty;
    public static string GetPath()
    {
        using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath))
        {
            if (key != null)
            {
                string? version = key.GetValue(Version) as string;
                if (!string.IsNullOrEmpty(version))
                {
                    return version;
                }
            }
        }
        return "";
    }
    private static readonly HttpClient http = new HttpClient
    {
        BaseAddress = new Uri("https://api.github.com/"),
        Timeout = TimeSpan.FromSeconds(10)
    };
    public static bool IsInstalled()
    {
        return !string.IsNullOrEmpty(_currentVersion) && !string.IsNullOrEmpty(_currentPath);
    }
    static UpdateChecker()
    {
        http.DefaultRequestHeaders.UserAgent.ParseAdd("LingyUpdater/1.0");
        using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath))
        {
            if (key != null)
            {
                string? version = key.GetValue(Version) as string;
                if (!string.IsNullOrEmpty(version))
                {
                    _currentVersion = version;
                }
                string? path = key.GetValue(Path) as string;
                if(!string.IsNullOrEmpty(path))
                {
                    _currentPath = path;
                }
            }
        }
    }

    public static async Task<GitHubReleaseInfo?> GetLatestReleaseAsync(string owner, string repo)
    {
        var resp = await http.GetAsync($"repos/{owner}/{repo}/releases/latest");
        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<GitHubReleaseInfo>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    public static bool? CompareVersions(string local, string remote)
    {
        local = local.TrimStart('v', 'V');
        remote = remote.TrimStart('v', 'V');
        string[] a = local.Split('.');
        string[] b = remote.Split('.');
        int len = Math.Max(a.Length, b.Length);
        for (int i = 0; i < len; i++)
        {
            int va = i < a.Length ? int.Parse(a[i]) : 0;
            int vb = i < b.Length ? int.Parse(b[i]) : 0;
            if (va < vb) return true;
            if (va > vb) return false;
        }
        return null;
    }

    public static async Task<bool> CheckAndUpdateAsync(
        string owner,
        string repo,
        string localVersion,
        string appExePath)
    {
        var release = await GetLatestReleaseAsync(owner, repo);
        if (release == null)
        {
            Console.WriteLine("Не вдалося отримати дані про реліз.");
            return false;
        }

        if (CompareVersions(localVersion, release.Tag_Name) != true)
        {
            Console.WriteLine("Оновлення не потрібне.");
            return false;
        }

        Console.WriteLine($"Знайдено нову версію: {release.Tag_Name}");
        var asset = Array.Find(release.Assets, a => a.Name.EndsWith(".zip") || a.Name.EndsWith(".exe"));
        if (asset == null)
        {
            Console.WriteLine("Файл оновлення не знайдено.");
            return false;
        }

        string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), asset.Name);
        Console.WriteLine("Завантаження: " + asset.BrowserDownloadUrl);
        var bytes = await http.GetByteArrayAsync(asset.BrowserDownloadUrl);
        await File.WriteAllBytesAsync(tempFile, bytes);

        if (asset.Name.EndsWith(".zip"))
        {
            string extractDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Lingy_Update");
            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
            ZipFile.ExtractToDirectory(tempFile, extractDir);
            string newExe = System.IO.Path.Combine(extractDir, System.IO.Path.GetFileName(appExePath));

            Console.WriteLine("Оновлення застосунку...");
            // Копіюємо пізніше через апдейтер, щоб не заблокувати себе
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C timeout 1 && copy /Y \"{newExe}\" \"{appExePath}\" && start \"\" \"{appExePath}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });

            Environment.Exit(0);
        }
        else if (asset.Name.EndsWith(".exe"))
        {
            Console.WriteLine("Запуск оновлювача...");
            Process.Start(new ProcessStartInfo
            {
                FileName = tempFile,
                UseShellExecute = true
            });
            Environment.Exit(0);
        }

        return true;
    }
}
