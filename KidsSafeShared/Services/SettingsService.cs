using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KidsSafeDesktop.Models;
using Microsoft.Maui.Storage;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 配置服务：加载/保存设置、密码哈希与校验、首启种子数据。
/// </summary>
public class SettingsService : ISettingsService
{
    private ChildSettings _current = new();
    private bool _loaded;
    private const string StorageKey = "kids_settings_v1";

    public ChildSettings Current => _current;
    public bool HasPin => !string.IsNullOrEmpty(_current.PinHash);

    public async Task LoadAsync()
    {
        if (_loaded) return;
        _loaded = true;

        try
        {
            var json = await SecureStorage.Default.GetAsync(StorageKey);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var loaded = JsonSerializer.Deserialize<ChildSettings>(json);
                if (loaded is not null)
                    _current = loaded;
            }
        }
        catch
        {
            _current = new ChildSettings();
        }

        if (_current.AllowedApps.Count == 0)
            SeedDefaults();
    }

    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_current);
            await SecureStorage.Default.SetAsync(StorageKey, json);
        }
        catch
        {
            // 在某些环境下安全存储不可用，退化为明文偏好设置
            try
            {
                var json = JsonSerializer.Serialize(_current);
                Preferences.Default.Set(StorageKey, json);
            }
            catch { /* 忽略 */ }
        }
    }

    public bool VerifyPin(string pin) => Hash(pin) == _current.PinHash;

    public void SetPin(string pin) => _current.PinHash = Hash(pin);

    private static string Hash(string pin)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes("kids_salt_" + pin));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    /// <summary>
    /// 首启时给一个合理的默认应用集合（平台相关）。
    /// </summary>
    private void SeedDefaults()
    {
#if ANDROID
        // Android 上家长应在后台从已安装应用列表中选择；这里留空。
#elif WINDOWS
        _current.AllowedApps.Add(new AppItem { Name = "计算器", LaunchTarget = "calc.exe", Emoji = "🧮", AccentColor = "#2E7D32" });
        _current.AllowedApps.Add(new AppItem { Name = "画图", LaunchTarget = "mspaint.exe", Emoji = "🎨", AccentColor = "#AD1457" });
        _current.AllowedApps.Add(new AppItem { Name = "记事本", LaunchTarget = "notepad.exe", Emoji = "📝", AccentColor = "#1565C0" });
#endif
    }
}
