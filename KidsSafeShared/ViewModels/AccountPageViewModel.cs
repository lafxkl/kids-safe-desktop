using System.Collections.ObjectModel;
using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.ViewModels;

/// <summary>账号页的视图模型：展示/修改本地账号资料，连接或断开家长服务器。</summary>
public class AccountPageViewModel : BaseViewModel
{
    private readonly IAccountService _account;
    private string _name = "小朋友";
    private string _avatar = "🐯";
    private string _serverUrl = string.Empty;
    private string _serverUser = string.Empty;
    private string _serverPin = string.Empty;
    private string _status = string.Empty;
    private bool _isBusy;
    private bool _hasStatus;
    private AccountMode _mode = AccountMode.Offline;

    public ObservableCollection<string> Avatars { get; } = new()
    {
        "🐯", "🐰", "🐻", "🐼", "🦁", "🐸",
        "🐥", "🐨", "🦊", "🐶", "🐱", "🐵"
    };

    public AccountPageViewModel()
    {
        _account = ServiceProvider.Get<IAccountService>();
        Refresh();
    }

    /// <summary>账号服务变化时（如被服务器同步改了昵称）刷新界面。</summary>
    public void Refresh()
    {
        var c = _account.Current;
        Name = c.Name;
        Avatar = c.AvatarEmoji;
        Mode = c.Mode;
        ServerUrl = c.ServerUrl;
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Avatar
    {
        get => _avatar;
        set => SetProperty(ref _avatar, value);
    }

    public AccountMode Mode
    {
        get => _mode;
        set
        {
            if (SetProperty(ref _mode, value))
                OnPropertyChanged(nameof(ModeText));
        }
    }

    public string ModeText => Mode == AccountMode.Offline ? "离线" : "在线";

    public string ServerUrl
    {
        get => _serverUrl;
        set => SetProperty(ref _serverUrl, value);
    }

    /// <summary>家长账号（可选）。留空则用家长密码 / 6 位配对码登录。</summary>
    public string ServerUser
    {
        get => _serverUser;
        set => SetProperty(ref _serverUser, value);
    }

    public string ServerPin
    {
        get => _serverPin;
        set => SetProperty(ref _serverPin, value);
    }

    public string Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
                HasStatus = !string.IsNullOrEmpty(value);
        }
    }

    public bool HasStatus
    {
        get => _hasStatus;
        set => SetProperty(ref _hasStatus, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public bool IsOffline => Mode == AccountMode.Offline;

    /// <summary>应用版本展示文本（页脚显示）。</summary>
    public string VersionText => AppVersion.Display;

    public async Task SaveProfileAsync()
    {
        await _account.UpdateProfileAsync(Name, Avatar);
        Status = "资料已保存 ✓";
    }

    public async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(ServerUrl))
        {
            Status = "请先填写服务器地址";
            return;
        }
        if (string.IsNullOrWhiteSpace(ServerPin))
        {
            Status = "请填写密码或配对码";
            return;
        }
        IsBusy = true;
        Status = "正在连接家长服务器…";
        try
        {
            await _account.ConnectServerAsync(ServerUrl, ServerUser, ServerPin);
            Status = string.IsNullOrWhiteSpace(ServerUser)
                ? "已连接家长服务器 ✓（配对码模式，只读同步）"
                : "已连接家长服务器 ✓";
            ServerPin = string.Empty;
            Refresh();
        }
        catch (Exception ex)
        {
            Status = "连接失败：" + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task DisconnectAsync()
    {
        IsBusy = true;
        Status = "正在切换到离线模式…";
        await _account.DisconnectAsync();
        Status = "已切换到离线模式，数据仅保存在本机";
        IsBusy = false;
        Refresh();
    }
}
