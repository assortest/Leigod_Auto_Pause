# Leigod Auto Pause 核心原理

## 项目本质

这个项目不是一个独立监控器，而是一个“启动器 + Electron 主进程补丁”的组合方案：

1. C# 启动器先定位雷神加速器安装目录中的 `resources/app.asar`。
2. 启动器解包 `app.asar`，把仓库根目录中的 [`main.js`](x:\code\Leigod_Auto_Pause\main.js) 覆盖到 `dist/main/main.js`。
3. 重新打包后，再启动雷神原始程序 `leigod_launcher.exe`。
4. 被替换进去的 `main.js` 运行在雷神 Electron 主进程内，直接拦截雷神自己的 IPC、窗口事件和前端页面。

因此，本项目的核心能力不是“模拟用户点击”，而是“进入雷神应用内部，在它自己的执行链路里决定何时开始监控、何时暂停时长、何时更新界面状态”。

## 架构分层

### 1. 启动器层

关键文件：

- [`src/Leigod_Auto_Pause/Program.cs`](x:\code\Leigod_Auto_Pause\src\Leigod_Auto_Pause\Program.cs)
- [`src/Leigod_Auto_Pause/SettingsManager.cs`](x:\code\Leigod_Auto_Pause\src\Leigod_Auto_Pause\SettingsManager.cs)

职责：

- 要求管理员权限运行。
- 检查当前目录下是否存在 `resources/app.asar`。
- 下载远端最新补丁脚本 `main.js`。
- 解包、替换、重打包 `app.asar`。
- 用本地配置记录补丁后的 `app.asar` 哈希和已应用 `main.js` 哈希，避免每次启动都重复打补丁。

这里的更新判定有两层：

1. 当前 `app.asar` 哈希是否仍然等于上次打补丁后的哈希。
2. 远端 `main.js` 哈希是否与本地已应用版本一致。

只要任一条件不满足，就重新解包并覆盖。

### 2. 补丁脚本层

关键文件：

- [`main.js`](x:\code\Leigod_Auto_Pause\main.js)

职责：

- 在 Electron 主进程中拦截雷神原生 IPC。
- 获取登录 token、加速游戏信息、窗口实例。
- 维护“监控状态机”。
- 注入一个顶部状态指示器到雷神界面。
- 在关闭窗口或系统关机时，主动调用暂停接口。

### 3. 内置 asar 处理层

关键目录：

- [`src/Leigod_Auto_Pause/asarsharp`](x:\code\Leigod_Auto_Pause\src\Leigod_Auto_Pause\asarsharp)

职责：

- 解压和重打包 Electron 的 `app.asar`。
- 这是启动器实现补丁替换的基础能力，不负责业务判断。

## 核心工作流

### 一、首次运行或检测到更新

启动器在 [`Program.cs`](x:\code\Leigod_Auto_Pause\src\Leigod_Auto_Pause\Program.cs) 中执行以下流程：

1. 校验管理员权限。
2. 读取 `%APPDATA%/LeigodPatcher/settings.json`。
3. 计算当前 `resources/app.asar` 的 SHA-256。
4. 下载远端 `main.js` 并计算其 SHA-256。
5. 如果本地状态不存在、`app.asar` 已变化、或远端脚本已更新，则执行补丁。
6. 将补丁后的哈希写回设置文件。
7. 启动雷神原程序。

这意味着项目并不依赖手工维护版本号，而是依赖内容哈希做幂等更新。

### 二、雷神应用启动后

被植入的 [`main.js`](x:\code\Leigod_Auto_Pause\main.js) 在 `app.whenReady()` 后做三件事：

1. `patchIpcMain()`：拦截雷神 IPC 通道。
2. `injectStatusWidget()`：注入顶部状态组件，并捕获主窗口对象。
3. `patchMainWindowClose()`：接管窗口关闭和系统关机行为。

这三步分别对应“业务入口”“用户可见状态”“兜底暂停”。

## 自动暂停的核心机制

### 1. 通过 IPC 拦截加速开始事件

补丁重写了 `ipcMain.handle`，重点拦截几个通道：

- `leigod-simplify-login`
- `leigod-simplify-start-acc`
- `leigod-simplify-stop-acc`
- `leigod-simplify-pause-user-time`
- `leigod-simplify-open-external`

其中最关键的是 `leigod-simplify-start-acc`：

1. 用户在雷神里点击开始加速。
2. 原始监听器先照常执行。
3. 若返回结果表明加速成功，补丁再去解析这次加速目标对应的游戏进程名。
4. 拿到进程名后，启动监控状态机。

这保证项目不会破坏雷神本身的启动逻辑，而是在“原逻辑成功后”追加自动暂停能力。

### 2. 多来源获取游戏进程名

项目判断“游戏是否真的在运行”，依赖目标进程名列表。进程名来源分三级：

1. `CommunityGameDB`
2. IndexedDB 中的 `game_list`
3. 雷神内部 API `get-game-info`

优先级不是简单固定，而是带修正策略：

- 若社区维护表里存在该游戏，直接优先使用。
- 否则并行读取 IndexedDB 和 API。
- 若 API 返回了 `game_process`，优先用 API 的进程名。
- 若 IndexedDB 提供 `is_free`，会把这个属性补到 API 结果上。
- 若 API 无有效进程名，则回退到 IndexedDB。

这样设计的原因很明确：雷神自身数据并不稳定，某些游戏会缺失或给出错误进程名，所以作者引入了“社区修正表 + 双源回退”。

### 3. 通过本地进程探测判断游戏状态

真正的运行判断不依赖窗口标题，也不依赖网络状态，而是直接调用 Windows `tasklist`：

- 对目标进程名执行 `tasklist /FI "IMAGENAME eq xxx.exe"`。
- 只要任意一个候选进程存在，就视为游戏正在运行。

这使判断逻辑足够直接，避免被雷神前端页面状态误导。

## 状态机设计

[`main.js`](x:\code\Leigod_Auto_Pause\main.js) 中的 `MonitoringManager` 是整个项目的业务核心。它本质上是一个三态状态机。

### 1. `IDLE`

空闲状态，不监控任何游戏进程。

进入条件：

- 初始状态。
- 用户主动暂停时长。
- 监控停止并清空目标进程。

### 2. `ACTIVE`

已检测到游戏进程存在，进入正常监控。

行为：

- 每 10 秒检查一次目标进程是否仍在运行。
- 只要游戏还在，就保持激活状态。
- 一旦发现进程消失，立刻切换到 `COUNTING`。

### 3. `COUNTING`

宽限倒计时状态，默认 10 分钟。

行为：

- 每 100ms 刷新倒计时显示。
- 每 5 秒重新检查一次目标进程是否已重新启动。
- 倒计时结束时再做一次最终进程检查。

分支：

- 若期间游戏重新启动，回到 `ACTIVE`。
- 若最终仍未检测到游戏，则调用雷神内部命令：
  - `stop-acc`
  - `pause-user-time`

这就是“游戏未启动 10 分钟自动暂停”和“游戏退出后 10 分钟未回到游戏自动暂停”的统一实现。

## UI 与交互原理

项目不会额外创建独立窗口，而是直接把状态组件注入到雷神主界面顶部导航区域。

注入流程：

1. 监听 `browser-window-created`。
2. 识别 URL 含 `renderer.asar/index.html` 的窗口为主窗口。
3. 在页面加载完成后轮询查找 `.nav-control` 和 `.recharge-enrty`。
4. 找到后插入 `#leigod-monitor-Widget`。

组件用于展示四种状态：

- `IDLE`：自动监控待命
- `ACTIVE`：已检测到游戏运行
- `COUNTING`：倒计时中
- `MISSING`：无法获得有效进程名，提示用户提交补充

其中 `COUNTING` 状态支持点击中断倒计时，底层做法不是直接修改状态机，而是走一个自定义跳转：

- 打开确认弹窗。
- 用户确认后调用 `open-external("leigod-plugin://interrupt")`。
- IPC 拦截到该伪协议后，停止当前监控并进入 `MISSING`/空闲逻辑。

这本质上是借用了现有通道完成插件内部通信。

## 关闭窗口与系统关机的兜底暂停

这是项目比较关键、也比较“底层”的一部分。

### 1. 关闭主窗口时

`patchMainWindowClose()` 会拦截主窗口 `close` 事件：

1. 先 `preventDefault()` 阻止立即退出。
2. 在主窗口上下文执行 `window.leigodSimplify.invoke("pause-user-time")`。
3. 无论命令后续是否抛异常，最终都 `app.exit(0)`。

设计目的很明确：用户关闭雷神时，优先把时长暂停，再结束程序，避免误损耗时长。

### 2. Windows 关机时

项目还监听了 `session-end`：

1. 若已经在登录拦截阶段拿到 `account_token`，则直接构造 HTTP 请求。
2. 通过独立 `curl` 子进程向 `https://webapi.leigod.com/api/user/pause` 发起 POST。
3. 子进程以 `detached` 方式运行，并短暂等待它成功拉起。
4. 然后 Electron 进程退出。

这里没有再依赖渲染进程或雷神前端，因为系统关机时 Electron 自身随时可能被终止。作者选择直接发官方暂停 API，是为了提高“系统正在结束会话”场景下的成功率。

## 为什么这套方案有效

这个项目成立的关键，不是复杂算法，而是抓住了三个稳定切入点：

1. `app.asar` 可替换，因此可以把补丁脚本放进雷神自己的执行环境。
2. 雷神前后端通过 Electron IPC 通信，因此可以在主进程统一拦截“登录、开始加速、暂停时长、打开外链”等关键动作。
3. 游戏是否真的在运行，最终可以回到 Windows 进程列表判断，这比依赖前端状态更可靠。

用一句话概括：

> 启动器负责把补丁植入雷神，补丁脚本负责接管雷神关键事件，再通过“进程探测 + 10 分钟宽限状态机 + 关闭/关机兜底暂停”实现自动节省加速时长。

## 关键文件索引

- 启动与补丁应用：[`src/Leigod_Auto_Pause/Program.cs`](x:\code\Leigod_Auto_Pause\src\Leigod_Auto_Pause\Program.cs)
- 本地状态保存：[`src/Leigod_Auto_Pause/SettingsManager.cs`](x:\code\Leigod_Auto_Pause\src\Leigod_Auto_Pause\SettingsManager.cs)
- 业务主逻辑：[`main.js`](x:\code\Leigod_Auto_Pause\main.js)
- asar 解包/打包：[`src/Leigod_Auto_Pause/asarsharp`](x:\code\Leigod_Auto_Pause\src\Leigod_Auto_Pause\asarsharp)

## 当前实现的边界

从代码看，项目仍有几个天然边界：

- 依赖管理员权限和对安装目录中文件的写权限。
- 强依赖雷神 Electron 包结构未发生重大变化，尤其是 `dist/main/main.js` 路径。
- 依赖雷神 IPC 通道名和页面 DOM 结构保持基本稳定。
- 游戏识别准确率受 `CommunityGameDB`、IndexedDB 和 API 数据质量影响。
- 关机场景的暂停依赖 `account_token` 成功捕获，以及系统可用的 `curl`。

这些限制不影响项目原理，但决定了它是一个“跟随上游结构变化持续维护”的补丁型项目。
