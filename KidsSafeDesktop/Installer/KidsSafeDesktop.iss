; ===========================================================================
;  儿童安全桌面 · Windows 安装程序（Inno Setup 6）
;
;  构建：build-installer.bat            用 csproj 里的版本号
;        build-installer.bat 1.19       指定版本
;
;  产出：Website\downloads\KidsSafeDesktop-<版本>-setup.exe
;
;  两条发布路线并存，这里只说清区别：
;   · 免安装版（release.bat 打的 -win.zip）：解压即用，不写注册表，
;     因此没有桌面右键菜单——那是安装程序在安装时注册的。
;   · 安装版（本文件）：装到本机、注册开始菜单与桌面右键、可卸载。
;
;  桌面右键写的是 HKCU\Software\Classes\DesktopBackground\Shell，
;  用当前用户而不是 HKLM，安装时就不需要管理员提权（PrivilegesRequired=lowest）。
; ===========================================================================

#ifndef AppVersion
  #define AppVersion "1.18"
#endif

#define AppName "儿童安全桌面"
#define AppExeName "KidsSafeDesktop.exe"
#define AppPublisher "四光博客"
#define AppUrl "https://kids.siguangblog.cc/"
#define BuildRoot "..\bin\Release\publish-win-x64"

[Setup]
; AppId 一旦发布就不要改：它是"同一个应用"的身份，改了会变成并存的两个程序
AppId={{3C7E9A21-5B4D-4E8F-9A6C-2D1F8B7E4C05}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
AppUpdatesURL={#AppUrl}
VersionInfoVersion={#AppVersion}

DefaultDirName={localappdata}\Programs\KidsSafeDesktop
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=no
AllowNoIcons=yes

; 显式保留欢迎页：它是唯一一块能从容放下「官网」链接的空白区域，
; 许可页被隐私政策占满，目录页被路径输入框占满。
DisableWelcomePage=no

; 只装到当前用户，不需要管理员密码，右键菜单也写 HKCU，权限刚好够。
; 不要加 PrivilegesRequiredOverridesAllowed=dialog：那会让安装程序一启动就先弹
; 「为所有用户安装 / 仅为我安装」的对话框，多一步且容易误导用户选到需要提权的那条路。
PrivilegesRequired=lowest

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

OutputDir=..\Website\downloads
OutputBaseFilename=KidsSafeDesktop-{#AppVersion}-setup
SetupIconFile=appicon.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}

; 安装界面显示的协议即应用内《隐私政策》全文，由 Scripts\export-privacy.ps1 生成
LicenseFile=PrivacyPolicy.txt

Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ShowLanguageDialog=no
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
UsePreviousAppDir=yes

[Languages]
; Inno Setup 6 自带的 29 种语言里没有中文（中文属非官方翻译，不在 compiler:Languages 下），
; 所以把 ChineseSimplified.isl 随安装器一起放进本目录，用相对路径引用。
; 好处是换台机器构建不会因为没装语言包而编译失败。
; 来源：https://github.com/kira-96/Inno-Setup-Chinese-Simplified-Translation
Name: "chinesesimp"; MessagesFile: "ChineseSimplified.isl"

[Messages]
; 许可页默认是两行引导语，容易和我们的自定义文案重复。
; LicenseLabel 是页头那句，LicenseLabel2 是勾选框上方那句，各写一句不重复的话。
chinesesimp.LicenseLabel=安装前请阅读并同意以下《{#AppName}隐私政策》。
chinesesimp.LicenseLabel3=孩子的信息默认只保留在这台设备上：不上传、不投放广告、不向第三方提供。安装后可随时在「家长面板 → 隐私与合规」查看全文或撤回授权。
chinesesimp.LicenseAccepted=我接受这份隐私政策(&A)
chinesesimp.LicenseNotAccepted=我不接受(&D)
; 默认的「欢迎使用 [name] 安装向导」「完成 [name] 安装向导」在 125%/150% 缩放下
; 会超出向导右侧那块约 390px 的版心，最后一个字被窗口边缘切掉，所以缩短。
chinesesimp.WelcomeLabel1=欢迎安装 [name]
chinesesimp.FinishedHeadingLabel=完成 [name] 的安装

[Tasks]
Name: "desktopicon"; Description: "在桌面创建快捷方式"; GroupDescription: "附加任务："; Flags: checkedonce
Name: "desktopmenu";  Description: "在桌面空白处右键菜单中加入「{#AppName}」（Windows 11 需点「显示更多选项」）"; GroupDescription: "附加任务："; Flags: checkedonce
; 开机自启默认勾选：这是家长控制类软件的基本预期（不装自启等于随时可以绕过）。
; 注意写的是 --kiosk-guard 而不是裸 exe：守护模式会在主程序被结束后自动拉起，
; 裸 exe 被任务管理器结束一次就永久失效。家长也可以在「家长设置 → 系统守护」里关。
Name: "autostart";   Description: "开机自动进入{#AppName}（守护模式，被结束后自动重启）"; GroupDescription: "附加任务："; Flags: checkedonce

[Files]
; 排除三样东西：
;  · publish\       —— dotnet publish 会把同一套自包含产物再写一份到 win-x64\publish\，
;                     recursesubdirs 会把它们重复收进来，安装包体积直接翻倍。
;  · *.WebView2\    —— 本机运行留下的缓存与 Cookie，装到别人机器上没有意义。
;  · *.pdb / *.xml  —— 调试符号与 XML 文档注释，终端用户用不到。
Source: "{#BuildRoot}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; \
    Excludes: "publish,publish\*,KidsSafeDesktop.exe.WebView2\*,*.pdb,*.xml"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\卸载 {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; ── 桌面空白处右键菜单（仅安装版；免安装的 zip 不会写这些键） ──────────
Root: HKCU; Subkey: "Software\Classes\DesktopBackground\Shell\KidsSafeDesktop"; \
    ValueType: string; ValueName: "MUIVerb"; ValueData: "{#AppName}"; \
    Flags: uninsdeletekey; Tasks: desktopmenu
Root: HKCU; Subkey: "Software\Classes\DesktopBackground\Shell\KidsSafeDesktop"; \
    ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#AppExeName},0"; Tasks: desktopmenu
Root: HKCU; Subkey: "Software\Classes\DesktopBackground\Shell\KidsSafeDesktop"; \
    ValueType: string; ValueName: "Position"; ValueData: "Top"; Tasks: desktopmenu
Root: HKCU; Subkey: "Software\Classes\DesktopBackground\Shell\KidsSafeDesktop\command"; \
    ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"""; Tasks: desktopmenu

; 开机自启（守护模式）。卸载时必须删掉这个值，否则会留下一条指向空路径的启动项，
; 每次登录 Windows 都会尝试启动一个不存在的程序。
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "KidsSafeDesktop"; \
    ValueData: """{app}\{#AppExeName}"" --kiosk-guard"; \
    Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#AppExeName}"; Description: "立即启动{#AppName}"; \
    Flags: nowait postinstall skipifsilent

[UninstallDelete]
; 卸载时清掉运行时释放到安装目录的缓存目录（若存在）
Type: filesandordirs; Name: "{app}\KidsSafeDesktop.exe.WebView2"

[Code]

procedure OpenAppUrl(Sender: TObject);
var
  ErrorCode: Integer;
begin
  ShellExec('open', '{#AppUrl}', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

procedure InitializeWizard();
var
  UrlLabel: TNewStaticText;
  HintLabel: TNewStaticText;
begin
  { 官网入口放在欢迎页。
    不放在许可页是有原因的：许可页的正文区被隐私政策占满，正文下方的空间
    紧接着就是「我接受」单选按钮，再插一行文字会压到按钮上。欢迎页的下半部分
    是空的，位置稳定，且是用户第一眼看到的页面。
    隐私政策全文末尾本来就有「联系我们」和同一个网址，两处互相印证。 }
  UrlLabel := TNewStaticText.Create(WizardForm);
  UrlLabel.Parent := WizardForm.WelcomePage;
  UrlLabel.Caption := '官网：{#AppUrl}（点击打开）';
  UrlLabel.Cursor := crHand;
  UrlLabel.Font.Color := clBlue;
  UrlLabel.Font.Style := [fsUnderline];
  UrlLabel.OnClick := @OpenAppUrl;
  UrlLabel.Left := WizardForm.WelcomeLabel2.Left;
  UrlLabel.Top := WizardForm.WelcomeLabel2.Top + WizardForm.WelcomeLabel2.Height + ScaleY(10);

  HintLabel := TNewStaticText.Create(WizardForm);
  HintLabel.Parent := WizardForm.WelcomePage;
  HintLabel.Caption := '安装完成后，也可以在「家长面板 → 隐私与合规 → 隐私政策」随时查看全文并撤回授权。';
  HintLabel.Font.Color := clGray;
  HintLabel.Left := UrlLabel.Left;
  HintLabel.Top := UrlLabel.Top + ScaleY(24);
  { 这行字比欢迎页可用宽度长，必须关掉 AutoSize 才能按宽度折行，
    否则会横向溢出窗口右边被裁掉。 }
  HintLabel.AutoSize := False;
  HintLabel.WordWrap := True;
  HintLabel.Width := WizardForm.WelcomeLabel2.Width;
  HintLabel.Height := ScaleY(36);
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  { 完成页再放一次官网地址：用户装完往往就想看看官网还有什么 }
  if CurPageID = wpFinished then
    WizardForm.FinishedLabel.Caption :=
      WizardForm.FinishedLabel.Caption + #13#10#13#10 +
      '官网：{#AppUrl}' + #13#10 +
      '安装包与免安装版都可以在官网下载。';
end;
