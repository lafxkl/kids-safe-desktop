using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.ViewModels;

/// <summary>
/// 家长密码校验页。
/// </summary>
public class ParentGateViewModel : BaseViewModel
{
    private string _pin = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _hasPin;
    private bool _showError;

    public bool HasPin
    {
        get => _hasPin;
        set => SetProperty(ref _hasPin, value);
    }

    public string Pin
    {
        get => _pin;
        set => SetProperty(ref _pin, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
                ShowError = !string.IsNullOrEmpty(value);
        }
    }

    public bool ShowError
    {
        get => _showError;
        set => SetProperty(ref _showError, value);
    }

    /// <summary>密码正确时触发</summary>
    public event EventHandler? Success;

    public void Init()
    {
        HasPin = ServiceProvider.Get<ISettingsService>().HasPin;
        Pin = string.Empty;
        ErrorMessage = string.Empty;
    }

    /// <summary>尝试校验，返回是否通过</summary>
    public bool TrySubmit()
    {
        var settings = ServiceProvider.Get<ISettingsService>();
        if (!settings.HasPin)
        {
            // 尚未设置密码：直接进入（用于首次设置）
            Success?.Invoke(this, EventArgs.Empty);
            return true;
        }

        if (settings.VerifyPin(Pin))
        {
            ErrorMessage = string.Empty;
            Success?.Invoke(this, EventArgs.Empty);
            return true;
        }

        ErrorMessage = "密码不对，再试一次";
        return false;
    }
}
