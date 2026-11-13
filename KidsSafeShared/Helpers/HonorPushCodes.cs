namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 荣耀推送错误码 → 中文解释 + 处理建议。
///
/// 错误码来自官方文档《错误码》(developer.hihonor.com, kitId=11002)。
/// 客户端只能把「10202」这串数字变成人话，真正的修复动作在开发者服务平台后台。
/// </summary>
public static class HonorPushCodes
{
    /// <summary>
    /// 把 bridge 写的诊断串（形如 "token:10202:parameter error"）翻译成人话。
    /// 不认识的原样返回。
    /// </summary>
    public static string Explain(string? diag)
    {
        if (string.IsNullOrWhiteSpace(diag)) return "(无)";

        var parts = diag.Split(':', 3);
        var codeText = parts.Length >= 2 ? parts[1] : "";
        if (!int.TryParse(codeText, out var code)) return diag;

        var (title, todo) = code switch
        {
            10202 => ("证书指纹或包名与开发者服务平台不一致",
                      "到平台核对：①应用包名是否为 com.companyname.kidssafedesktop；②SHA-256 指纹是否已登记。两者必须都与本机安装包一致。"),
            10211 => ("该设备今天注册 token 已达上限",
                      "等第二天再试，不要反复点。"),
            10214 => ("请求过于频繁（荣耀侧频控）",
                      "等几分钟再试。"),
            8001000 => ("设备不支持荣耀推送服务",
                        "必须是荣耀设备，且系统包含荣耀推送服务（部分老系统需升级 Magic UI）。"),
            8001001 => ("禁止在主线程调用",
                        "代码问题，需要改成子线程调用。"),
            8001002 => ("清单未配置 appId",
                        "AndroidManifest.xml 缺少 meta-data: com.hihonor.push.app_id。"),
            8001003 => ("应用证书指纹获取失败",
                        "确认安装包已签名（未被二次处理）。"),
            8001004 => ("找不到要操作的 token",
                        "先成功获取 token，再做删除等操作。"),
            8001005 => ("SDK 未初始化",
                        "需先调用 HonorPushClient.init()。"),
            8002001 => ("绑定推送服务失败",
                        "稍后重试；持续失败多为设备侧服务异常。"),
            8002003 => ("调用服务超时",
                        "设备服务拥堵，稍后重试。"),
            8002006 => ("荣耀推送服务校验失败",
                        "联系荣耀技术支持。"),
            8003004 => ("无效参数（AppId 格式不对）",
                        "检查 meta-data 里的 app_id 是否与 mcs-services.json 一致。"),
            8003006 => ("当前网络不可用",
                        "检查设备网络。"),
            8003007 => ("OOBE 阶段未同意隐私声明",
                        "完成系统首次开机引导后再试。"),
            8003008 => ("接口请求超时",
                        "网络问题，稍后重试。"),
            _ => ("", "")
        };

        if (title.Length == 0) return diag;

        return $"{code}：{title}\n→ {todo}";
    }
}
