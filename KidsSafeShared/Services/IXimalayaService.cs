namespace KidsSafeDesktop.Services;

/// <summary>
/// 喜马拉雅儿童板块接入：按分类打开喜马拉雅儿童（优先启动 App，未安装则打开网页）。
/// </summary>
public interface IXimalayaService
{
    /// <summary>打开喜马拉雅儿童的某个分类（categoryKey 见媒体中心分类 Key）。</summary>
    Task OpenAsync(string categoryKey);
}
