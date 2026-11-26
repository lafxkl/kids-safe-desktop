import QtQuick
import QtQuick.Controls
import QtQuick.Window
import KidsSafe 1.0

/*
 * 应用根窗口。
 *
 * 全屏、无边框、拒绝关闭：这三个是「体验层」的锁定手段，真正的锁在
 * kidssafe-session 会话脚本里（不启动 plasmashell、不注册全局快捷键）。
 * 详见 src/core/kiosk.h 的类注释。
 *
 * 结构分三层，从下往上：
 *   1. StackView —— 桌面 / 锁定页 / 家长后台等页面栈，整个使用期都在；
 *   2. 引导盖层 —— 首次启动才加载，铺满整屏压在壳上面；
 *   3. 闪屏 —— 纯视觉层，1.4 秒后淡出并卸载。
 *
 * 窗口类型用 QtQuick.Controls 的 ApplicationWindow，不用
 * Kirigami.ApplicationWindow：后者启动时会去找名为 Chameleon 的 Kirigami
 * 平台插件（deepin 上没有），每次都打一行 WARN。本应用的配色、字号全部
 * 自己写死，不需要 Kirigami 的主题体系，换成普通窗口既少一行噪音，
 * 也少一个运行时依赖。
 *
 * 这里刻意不再用 StackView 来切「引导 → 桌面」：
 * 早先的写法是引导页里调 StackView.view.replace(null, …)。栈里只有一个
 * 元素时，replace() 走的是 replaceEnter/replaceExit 过渡分支，而本文件
 * 只配了 push/pop 过渡，实测点「开始使用」界面一点变化都没有。
 * 现在引导只是一层 Loader，结束条件写在配置里（Settings.onboardingDone），
 * 由下面那段 Connections 把盖层收起来（隐藏，不销毁），完全不碰导航栈。
 */

ApplicationWindow {
    id: root

    /*
     * 尺寸：kiosk 模式下直接按屏幕尺寸给，不指望「全屏」这个请求。
     *
     * X11 上「全屏」不是应用自己能决定的事，它是个窗口管理器协议
     * （_NET_WM_STATE_FULLSCREEN）。而 kiosk 会话**故意不启动 kwin / plasmashell**，
     * 于是没人执行这个请求：窗口就停在原来写的 1280×800，贴在左上角，
     * 屏幕其余部分是黑色的根窗口 —— 就是「注销换成儿童桌面后只剩一小块」那张图。
     *
     * 自己把宽高绑到屏幕尺寸，有没有窗口管理器都能铺满；分辨率变化（VM 窗口
     * 拉伸、外接显示器）时 Screen.width/height 会跟着变，界面自动重排。
     * visibility 仍然保留 FullScreen：有 WM 时它还能帮忙盖住任务栏/面板。
     */
    width: Kiosk.windowed ? 1280 : Screen.width
    height: Kiosk.windowed ? 800 : Screen.height
    // 最小尺寸只在调试窗口模式下有意义。kiosk 模式下留着 800×560 的话，
    // 遇到比它小的屏幕会反过来把窗口撑出屏幕外。
    minimumWidth: Kiosk.windowed ? 800 : 0
    minimumHeight: Kiosk.windowed ? 560 : 0
    // 调试模式（kidssafe --windowed）下就是一个普通窗口：带标题栏、可缩放、
    // 能最小化到旁边看终端。C++ 侧 Kiosk::attach 会按同一开关决定要不要
    // 加无边框/置顶/全屏，两处判断的是同一个值，不会打架。
    visibility: Kiosk.windowed ? Window.Windowed : Window.FullScreen
    title: "儿童安全桌面"
    color: "#FFF8EE"

    // 家长只能用「退出儿童桌面」按钮走正常流程，不允许 Alt+F4 / 关闭按钮关掉。
    // 注：Wayland 下这个拦截只对应用自己发出的 close 生效；窗口管理器层面的
    // 关闭请求由会话脚本守护重启兜底。调试模式下不拦，直接点 × 就能关。
    onClosing: function (close) {
        if (!Kiosk.windowed) {
            close.accepted = false
        }
    }

    /*
     * 页面跳转统一走这两个函数，页面里用「根 Item 上的 navWin」调用
     * （`page.navWin.pushPage(…)`）。navWin 是页面根 Item 上的
     * `readonly property var navWin: Window.window`。
     *
     * 不能直接写 `Window.window.pushPage(…)`：`Window.window` 是附加属性，
     * 靠「item → 所在窗口」反查，而 TapHandler 不是 Item，取到的是 null，
     * 于是 `null.pushPage(…)` 抛 TypeError，界面上就是点了没反应。
     * deepin / Qt 6.8 上实测：MouseArea 里能用、TapHandler 里必 null。
     * 统一改成在根 Item 取一次，handler 里就不会再踩这个坑。
     *
     * 之前页面里都写 StackView.view.push(…)：它是「附加属性」，由 QML 引擎在
     * 第一次读取时创建，再按 parentItem 反查栈元素。这台机器（deepin + Qt 6.8）
     * 上，从页面里调用 stack 的三处（闪屏 replace、引导 replace、桌面 push）
     * 全都没反应，界面就停在原地。改成页面直接找窗口、由窗口按 id 操作
     * StackView，链路里没有任何隐式查找；日志里还会打出 depth 变化，
     * 下次真出问题时一眼能看出是「没进来」还是「push 被拒」。
     */
    function pushPage(url, props) {
        if (!url) {
            return;
        }

        /*
         * 语言闸门：简体中文模式不允许进「故事剧场」（吉里吉里）。
         *
         * 列表里已经按 Settings.allowVn 删掉了那张卡片，这里是最后一道 ——
         * 页面入口不止一处（首页磁贴、未来的深链），漏拦一次就等于没拦。
         * 用 url 字符串判断而不是解析后的地址：qrc 与 file 两种前缀都会出现，
         * 判子串最稳。
         */
        if (!Settings.allowVn && String(url).indexOf("VnPage.qml") >= 0) {
            console.warn("[nav] 当前语言为简体中文，故事剧场不可用");
            flashNavError(Settings.english ? "Not available in this language"
                                           : "简体中文版不含这个入口");
            return;
        }

        /*
         * 先体检再进栈。
         *
         * 踩过的坑：页面里写错一个属性名（比如 fullscreenSupportEnabled 少写一个
         * 大写 S），QML 只在控制台打一行 `Component is not ready` + 一条
         * `Cannot assign to non-existent property`，然后 StackView 照样往栈里
         * 塞一个空页面。孩子看到的是白屏，于是继续点，栈一层层堆到 depth 11，
         * 点「返回」一次只弹一层（日志 `pop 8 -> 7`），看上去就是「返回也没用」。
         *
         * 这里用 PreferSynchronous 提前同步编译拿到结果：
         *   编译失败 → 不推、打日志、弹一条可见的提示；
         *   编译成功 → 立刻销毁这个探测组件，仍然按 URL 交给 StackView 建页面，
         *              不改变原有生命周期（组件归 StackView 管，不给它留下泄漏）。
         */
        const probe = Qt.createComponent(url, Component.PreferSynchronous);
        if (probe === null) {
            console.warn("[nav] 页面地址无效，未进入页面栈：", url);
            flashNavError("这个页面打不开，请告诉家长");
            return;
        }
        if (probe.status === Component.Error) {
            console.warn("[nav] 页面编译失败，未进入页面栈：", url, "\n" + probe.errorString());
            probe.destroy();
            flashNavError("这个页面打不开，请告诉家长");
            return;
        }
        probe.destroy();

        const before = nav.depth;
        if (props === undefined) {
            nav.push(url);
        } else {
            nav.push(url, props);
        }
        console.log("[nav] push", url, "depth", before, "->", nav.depth);
    }

    // 页面打不开时的一句话提示（3 秒后自己淡出）。不挡住任何输入。
    function flashNavError(text) {
        navToastText.text = text;
        navToast.opacity = 1;
        navToastTimer.restart();
    }

    /*
     * 返回上一页：只认「弹当前页」，不接受 item 参数。
     *
     * 踩过的坑：Qt 6 里 nav.pop(item) 的语义是「一直弹到该 item 成为当前页
     * 为止」，而各个页面传进来的正是自己（当前页）——于是它判定「已经是目标
     * 页了」，一页都不弹，也不报错。日志表现为 `pop depth 3 -> 3`，界面上
     * 就是孩子点了返回毫无反应。所有页面统一改成无参调用 popPage()。
     *
     * 两道保险：
     *   1. 栈底不弹，否则会把桌面一起弹掉；
     *   2. 300ms 去抖。孩子连点两下返回很常见，第二次点击若也执行，会把
     *      「游戏大厅」一起弹走，直接跳回桌面。这里刻意不用 nav.busy 判断：
     *      万一某次过渡没收尾，busy 会一直是 true，返回就永久失效了；
     *      按时间判重则最坏只是多点一下。
     */
    property double lastPopAt: 0

    function popPage() {
        const before = nav.depth;
        if (before <= 1) {
            return;
        }
        const now = Date.now();
        if (now - lastPopAt < 300) {
            return;
        }
        lastPopAt = now;
        nav.pop();
        console.log("[nav] pop depth", before, "->", nav.depth);
    }

    /*
     * 一路弹回桌面（栈底）。家长后台的「回到桌面」、以及家长延长时长之后用它。
     *
     * 为什么不能复用 popPage()：
     *   1. 它只弹一层。进家长后台的路径是「桌面 → 家长验证 → 家长后台」，
     *      弹一层正好回到**家长验证页**。家长看到的是「请输入密码」，
     *      输完又进后台 —— 和录像里一样，只在家长界面里打转，回不到桌面。
     *   2. 它有 300ms 去抖（防孩子连点返回），连着调两次会被吞掉第二次。
     *
     * 用 while 逐层 pop，不用 nav.pop(某个 item)：pop(item) 的语义是
     * 「弹到该 item 成为当前页为止」，各个版本行为都踩过（见 popPage 注释）。
     * 循环上限取进入时的 depth：万一某个平台上 pop() 不立刻改 depth，
     * 也只会空转有限次，不会死循环。
     */
    function popToRoot() {
        const before = nav.depth;
        if (before <= 1) {
            return;
        }
        for (let guard = before; guard > 0 && nav.depth > 1; --guard) {
            nav.pop();
        }
        // 记时间给去抖用：紧接着的一次「返回」不该把桌面也弹掉（栈底本来也弹不动）。
        lastPopAt = Date.now();
        console.log("[nav] popToRoot depth", before, "->", nav.depth);
    }

    // ---- 第一层：页面栈 ----
    StackView {
        id: nav
        anchors.fill: parent
        /*
         * 栈底永远是桌面，锁定页是**压上去**的一层。
         *
         * 早先这里写的是 `initialItem: ScreenTime.lockedOut ? "LockoutPage.qml"
         * : "HomePage.qml"`，把锁定页当成栈底。看着省事，实际有个死结：
         * 栈底弹不掉，于是「家长解锁 15 分钟」过完密码、时间也确实加上了，
         * 界面还是那张「今天的时间用完啦」——家长没有任何路可走。
         * 改成压一层之后，解锁时 popToRoot() 就能回到真正的桌面。
         */
        initialItem: "HomePage.qml"

        Component.onCompleted: {
            if (ScreenTime.lockedOut) {
                nav.push("LockoutPage.qml");
                console.log("[nav] 今日时长已用完，桌面之上压入锁定页");
            }
        }

        pushEnter: Transition {
            NumberAnimation { property: "opacity"; from: 0; to: 1; duration: 220; easing.type: Easing.OutCubic }
            NumberAnimation { property: "scale"; from: 0.96; to: 1; duration: 220; easing.type: Easing.OutCubic }
        }
        pushExit: Transition {
            NumberAnimation { property: "opacity"; from: 1; to: 0.6; duration: 180 }
        }
        popEnter: Transition {
            NumberAnimation { property: "opacity"; from: 0.6; to: 1; duration: 180 }
        }
        popExit: Transition {
            NumberAnimation { property: "opacity"; from: 1; to: 0; duration: 180 }
        }
        // replace 目前没有调用点，但补上过渡之后，将来页面里再写
        // replace() 也不会出现「旧页留在最上层」的情况。
        replaceEnter: Transition {
            NumberAnimation { property: "opacity"; from: 0; to: 1; duration: 220 }
        }
        replaceExit: Transition {
            NumberAnimation { property: "opacity"; from: 1; to: 0; duration: 180 }
        }
    }

    // ---- 第二层：首次引导 ----
    // 用 source 显式赋值而不是绑定 Settings.onboardingDone：绑定会在按钮
    // 回调里同步求值，等于在那个按钮的处理器执行到一半时把盖层收走。
    Loader {
        id: onboarding

        anchors.fill: parent
        source: ""

        Component.onCompleted: {
            if (!Settings.onboardingDone) {
                onboarding.source = "OnboardingPage.qml";
            }
        }
    }

    // 引导页只负责把 onboardingDone 写进配置，收盖层的事在这里做。
    //
    // 这里原来是 Qt.callLater(() => onboarding.source = "")：延到本次事件处理
    // 之后再销毁页面，理论上最干净，不会「在自己的回调里销毁自己」。
    // 实测（Qt 6.x）点「开始使用」的那一下整个窗口直接消失 —— 进程没了，
    // 日志里连一行 QML 报错都没有。销毁一整个引导页要收尾场景图、焦点、
    // 输入抓取（鼠标此刻正压在那颗按钮上）三处，怀疑是从里面某一处在
    // Qt 内部炸掉的，从应用侧看不到任何线索。
    //
    // 改成「藏起来，不销毁」：视觉上和销毁一样（盖层消失、桌面立刻能点），
    // 但那颗按钮、那个 ListView 都还活着，不走任何析构路径。代价只是这一次
    // 运行里多留一个页面；下次启动时 Loader 的 source 本来就是空的，
    // 老用户不会再加载它。
    Connections {
        target: Settings

        function onChanged() {
            if (!Settings.onboardingDone) {
                return;
            }
            // item 为 null 说明盖层本来就没加载（老用户直接进桌面），
            // 那种情况下 ScreenTime 已经在 HomePage 里起过了。
            if (onboarding.item === null) {
                return;
            }
            if (onboarding.visible) {
                console.log("[onboard] 引导完成，收起盖层（隐藏，不销毁）");
                onboarding.visible = false;
                onboarding.item.enabled = false;
            }
            ScreenTime.start();
        }
    }

    // ---- 第三层：启动闪屏 ----
    // 纯视觉层，盖在导航之上，结束后自己卸载。
    // 它不接管输入（Item 没有鼠标处理），淡出期间下面的页面已经可以响应。
    Loader {
        id: splash

        anchors.fill: parent
        source: "SplashPage.qml"

        NumberAnimation on opacity {
            id: splashFade

            from: 1
            to: 0
            duration: 260
            running: false
            easing.type: Easing.OutCubic

            // 淡完再卸载，顺便把闪屏里的动画停掉
            onFinished: splash.source = ""
        }

        Timer {
            interval: 1400
            running: true
            onTriggered: splashFade.start()
        }
    }

    // ---- 第四层：护眼暖色遮罩 ----
    // 放在最上层，因为它必须盖住页面栈、引导页和闪屏。
    //
    // 两点必须守住：
    //   1. 不挂 MouseArea / TapHandler，并显式 enabled: false。没有输入处理的
    //      Rectangle 本来就不接收指针事件，点击会落到下面的页面；写明白是为了
    //      防止以后有人「顺手」给它加个点击穿透处理，反而把事件吃掉了。
    //   2. 颜色用暖橙而不是纯黄。纯黄会把画面里的蓝色（绘本的天空、动画的
    //      水）滤成灰绿，看着更难受；#FFB45A 只是把冷色往暖里推。
    //
    // 只覆盖本窗口：孩子被放行到外部应用时，那个窗口不受我们控制。
    // 这与 Windows 版一致，真正的系统级滤镜要写 KWin effect，不在本版范围。
    Rectangle {
        anchors.fill: parent
        color: "#FFB45A"
        opacity: EyeCare.opacityPercent / 100
        visible: EyeCare.active && EyeCare.opacityPercent > 0
        enabled: false
        z: 100
    }

    // ---- 顶端提示条：页面打不开时说一句 ----
    // 比白屏好：孩子知道「点了，但是坏了」，家长看到提示知道要去查日志。
    // 不挂任何输入处理，所以不会吃掉下面页面的点击。
    Rectangle {
        id: navToast

        anchors.horizontalCenter: parent.horizontalCenter
        anchors.top: parent.top
        anchors.topMargin: 26
        width: navToastText.implicitWidth + 48
        height: 46
        radius: 23
        color: "#4A3B32"
        opacity: 0
        visible: opacity > 0
        z: 200

        Behavior on opacity {
            NumberAnimation { duration: 180 }
        }

        Text {
            id: navToastText

            anchors.centerIn: parent
            color: "#FFFFFF"
            font.pixelSize: 15
        }

        Timer {
            id: navToastTimer

            interval: 2600
            onTriggered: navToast.opacity = 0
        }
    }
}
