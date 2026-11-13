把安装包放在这个目录，下载页（/download.html）会自动列出：

  *.apk     → 标记为 Android，出现在「下载 Android 版」按钮上
  *.zip     → 标记为 Windows

规则：
  · 只识别 apk / aab / zip / msi / exe 五种扩展名，其他文件不会出现在列表里；
  · 列表按文件修改时间倒序，最新的排最前面；
  · 每个文件会自动计算 SHA-256 并显示在下载页上（文件不变则复用缓存）；
  · 不需要重启服务，刷新页面即可看到新文件。

如果安装包放在别处（对象存储 / 应用市场），在 appsettings.json 里配置：

  "Site": { "DownloadUrl": "https://example.com/kidssafe.apk" }

下载页会优先使用这个地址作为安卓主按钮。
