using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 配置持久化 + 家长密码校验。
/// </summary>
public interface ISettingsService
{
    ChildSettings Current { get; }
    bool HasPin { get; }
    Task LoadAsync();
    Task SaveAsync();
    bool VerifyPin(string pin);
    void SetPin(string pin);
}
