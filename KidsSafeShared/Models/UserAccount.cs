namespace KidsSafeDesktop.Models;

/// <summary>
/// 账号运行模式。
/// </summary>
public enum AccountMode
{
    /// <summary>离线模式：未连接家长服务器，所有数据仅保存在本机，完全可离线使用。</summary>
    Offline,

    /// <summary>在线模式：已连接家长端服务器，配置可由服务器统一下发/同步。</summary>
    Online
}

/// <summary>
/// 当前儿童账号（本地优先）。
/// 无论是否登录家长服务器，应用都会有一个本地账号，保证「没有账号时也能以离线模式运行」。
/// </summary>
public class UserAccount
{
    /// <summary>本地账号唯一 Id（首次运行自动生成）。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>小朋友昵称（与 ChildSettings.Nickname 同步）。</summary>
    public string Name { get; set; } = "小朋友";

    /// <summary>专属头像 emoji（由昵称稳定生成，可手动改）。</summary>
    public string AvatarEmoji { get; set; } = "🐯";

    /// <summary>运行模式：离线 / 在线。</summary>
    public AccountMode Mode { get; set; } = AccountMode.Offline;

    /// <summary>已连接的家长服务器地址（仅在线模式有效）。</summary>
    public string ServerUrl { get; set; } = string.Empty;
}
