把安卓与 Windows 安装包放在这个目录，文件名与官网下载区的链接保持一致：

  KidsSafeDesktop-1.19.apk          ← Android 直装包
  KidsSafeDesktop-1.19-setup.exe    ← Windows 安装版（开始菜单 + 桌面右键，可卸载）
  KidsSafeDesktop-1.19-win.zip      ← Windows 免安装版（解压即用，不写注册表，无右键）

Windows 两个包的分工：
  · setup.exe 由 build-installer.bat 产出（Inno Setup 6，只装当前用户、不弹 UAC），
    安装时才注册桌面空白处右键菜单；
  · -win.zip 由 release.bat 产出，两者内容一致，差别只在"装没装进系统"。

发版时（推荐用根目录的 release.bat，以下步骤它会自动做完）：

  release.bat 1.20 21          ← 示例：下一版是 1.20 / versionCode 21

  它会：写版本号 → 双端 Release 构建 → 按上面的命名把两个包放进来
  → 归档上一版到 D:\WorkBuddy\_release\v旧版本\ → 清掉服务端目录旧包并同步新包
  → 算 SHA-256 与大小 → 回填 index.html / permissions.html / changelog.html
  → 回填 SEO 元信息（JSON-LD 版本、sitemap lastmod、about 页版本数）
  → 提醒 Releases.cs 和 changelog 是否漏了本版条目。

  release.bat 不产出 setup.exe，装完上面两步后再跑一次：

  build-installer.bat          ← 默认用 csproj 里的版本号，也可以跟一个版本号

  脚本不做（要人写）：changelog.html 的本版 <article> 段落、Releases.cs 的本版要点。
  用法细节见 ../../Docs/发布与更新日志.md 第三节。

手工做的话：
  1. 用新版本号重新出包（csproj 的 ApplicationDisplayVersion / ApplicationVersion）；
  2. 把文件按上面的命名放进来（版本号跟着改）；
  3. 同步修改 index.html 下载区：卡片版本号与大小、两个下载链接的文件名、页脚版本号；
  4. permissions.html 的「适用版本」、changelog.html 顶部的版本信息与条目。

注意：
  · 超过约 100 MB 的 apk 不适合长期放在静态站点（部分托管有单文件大小限制），
    可改用对象存储，直接把下载链接换成 CDN 地址；
  · 上传前核对签名指纹，确保与上一版一致（否则老用户无法覆盖安装）；
  · 本目录里的 README.txt 不会被下载区列出，可以保留。
