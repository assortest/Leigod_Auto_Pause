try {
  ("use strict");
  /*Can you hear forever, my heart beat
      君に届くようにと描いたのは
      Don’t you know everlasting stories
      見上げた瞳に映る三日月
      Can you hear forever, my heart beat
      もっと信じ合えたら共に行こう
      Don’t you know everlasting stories
      果てなく広がる空の彼方へ*/

  /*由于是采用注入无奈只能单文件所以只能采用这种方式进行标注*/

  //========== 全局变量 ==========
  let GLOBAL_USER_TOKEN = "";
  let mainWindow;
  //========== 常量 ==========
  //社区维护的游戏进程名
  /*
  有点怪，雷神居然有在维护这个indexdb，我一直以为是历史遗留问题，但是这次他居然吧战地6和星际战甲这类游戏的进程名放进来了。。。
  这不对了，他这个设计的意义是什么呢？
  */
  const CommunityGameDB = {
    1559: "VALORANT-Win64-Shipping.exe", //无畏契约
    // 2167: "Warframe.x64.exe", //星际战甲
    // 258: "Warframe.x64.exe", //星际战甲
    137: "vermintide2_dx12.exe,vermintide2.exe", //末世鼠疫2
    254: "EscapeFromTarkov.exe", //逃离塔科夫
    5226: "PioneerGame.exe", //ARC Raiders
    7288: "Aion2.exe", //永恒之塔2
    114: "League of Legends.exe", //英雄联盟
    931: "League of Legends.exe", //英雄联盟
    2661:"Discovery.exe,Discovery-d.exe,Discovery-e.exe", //THE FINALS
    6338:"F1_25.exe", //F1 25
    188:"Titanfall2.exe", //泰坦陨落2
  };
  const ExcludedGameIDs = [109, 437, 1544, 274, 1921, 1342, 860]; //steam epic 暴雪 育碧uplay eaapp  rockstar GOG
  const UI_STATES = {
    //监控中
    ACTIVE: {
      color: "#4caf50",
      bg: "rgba(76, 175, 80, 0.15)",
      text: "🟢 监控中",
      code: "active",
    },
    //倒计时
    COUNTING: {
      color: "#ff9800",
      bg: "rgba(255, 153, 0, 0.22)",
      text: "⏳ 倒计时",
      code: "counting",
    },
    //空闲
    IDLE: {
      color: "#a4a4a4",
      bg: "rgba(255,255,255,0.1)",
      text: "⚙️ 自动监控",
      code: "idle",
    },
    MISSING: {
      color: "#2196f3",
      bg: "rgba(33, 150, 243, 0.15)",
      text: "🔗 提交进程",
      code: "missing",
    },
  };
  //========== 模块引入 ==========
  const { app, ipcMain, Notification } = require("electron"); // 结构引入 Electron 使用的模块
  const { exec, spawn } = require("child_process");
  const path = require("path"); //用于处理路径
  const fs = require("fs"); //用于文件操作
  const userDataPath = app.getPath("userData");
  const logFilePath = path.join(userDataPath, "leigod_Monitor_log.txt"); //和文件名拼接成完整的路径

  // ========== 工具函数 ==========
  //该函数用于记录日志
  function writeLog(message) {
    //用于记录日志
    const timestamp = new Date().toISOString(); //获取当前时间

    const logMessage = `[${timestamp}] ${message}\n---------------------------------------\n`;
    try {
      fs.appendFileSync(logFilePath, logMessage);
      // eslint-disable-next-line no-unused-vars
    } catch (err) {
      /* empty */
    } //写入日志文件
  }
  //该函数用于解析游戏进程字符串
  function parseGameProcess(gameProcessStr) {
    //用于解析游戏进程字符串
    if (!gameProcessStr) {
      return []; // 空、null、undefined 都返回空数组
    }
    // 去除前后空格，按逗号分割，再过滤掉空字符串（防止 "a,,b" 出现空项）
    return gameProcessStr
      .split(",")
      .map((p) => p.trim())
      .filter((p) => p !== "");
  }
  //该函数用于处理时间吧时间转换为 mm:ss格式
  function formatTime(time) {
    if (time < 0) time = 0;
    const totalSeconds = Math.ceil(time / 1000); //毫秒转换为秒
    const m = Math.floor(totalSeconds / 60)
      .toString()
      .padStart(2, "0"); //转换为分钟
    const s = (totalSeconds % 60).toString().padStart(2, "0"); //转换为秒
    return `${m}:${s}`;
  }
  //该函数用于显示通知
  function showStartupNotification(title, body, silent = true) {
    // 仅在 Windows 平台上显示通知
    if (process.platform !== "win32") return;

    try {
      const notice = new Notification({
        title: title,
        body: body,
        silent: silent,
      });
      notice.show();
      writeLog(
        `[Notification] Displayed: "${title}" - "${body}" (silent: ${silent})`,
      );
    } catch (err) {
      writeLog(`[Notification] Failed to show: ${err.message}`);
    }
  }

  // ========== 核心管理器 ==========
  //状态机 监控管理器 核心逻辑
  const MonitoringManager = {
    //创建一个单例
    targetProcesses: [],
    monitorIntervalId: null, //监控状态
    graceCheckIntervalId: null, //宽限期id
    countdownIntervalId: null, //倒计时id

    start: function (processList) {
      writeLog(
        `[Monitor] Received start command for: ${processList.join(", ")}`,
      );

      this.stop(false); //清理掉所有定时器
      if (!processList || processList.length === 0) {
        //如果ProcessList是空的就返回
        showStartupNotification("获取游戏进程失败", "无法启动自动暂停", false);
        writeLog("[Monitor] Process list is empty. Monitoring aborted.");
        return;
      }
      this.targetProcesses = processList;
      writeLog(`[Monitor] Set target processes to: ${this.targetProcesses}`);
      //检查初始状态
      this._checkProcessExists().then((isProcessRunning) => {
        if (isProcessRunning) {
          //如果进程正在运行
          writeLog(
            "[Monitor] Game is already running. Entering active monitoring state.",
          );
          this._enterActiveMonitoringState();
        } else {
          writeLog(
            "[Monitor] Game is not running. Entering grace period state.",
          );
          this._enterGracePeriodState();
        }
      });
    },
    stop: function (clearList = true) {
      writeLog("[Monitor] Stop command received. Clearing all timers.");
      //清理掉所有定时器
      if (this.monitorIntervalId) clearInterval(this.monitorIntervalId);
      if (this.graceCheckIntervalId) clearInterval(this.graceCheckIntervalId);
      if (this.countdownIntervalId) clearInterval(this.countdownIntervalId);

      if (clearList) {
        updateUiState("IDLE");
        this.targetProcesses = [];
      }
      this.monitorIntervalId = null;
      this.graceCheckIntervalId = null;
      this.countdownIntervalId = null;
    },

    _checkProcessExists() {
      return new Promise((resolve) => {
        if (this.targetProcesses.length === 0) {
          resolve(false);
          return;
        }

        const checkProcess = (target) => {
          //该方法用于调用系统命令 将结果返回
          if (!target) return Promise.resolve(false);

          return new Promise((pResolve) => {
            const command = `tasklist /FI "IMAGENAME eq ${target.trim()}"`;
            exec(command, (error, stdout) => {
              if (error) {
                pResolve(false);
                return;
              }
              const lines = stdout.trim().split("\n"); //处理空格和回车符
              pResolve(lines.length > 2); //判断进程是否存在
            });
          });
        };
        Promise.all(this.targetProcesses.map(checkProcess))
          .then((results) => {
            //该方法将得到的结果检查如果有进程在就返回true
            const anyRunning = results.includes(true); //检查进程如果有进程运行就返回true
            if (anyRunning) {
              writeLog(
                `[Monitor] Found at least one running process. Results: ${JSON.stringify(
                  results,
                )}`,
              );
            } else {
              writeLog(
                `[Monitor] No target processes found running. Results: ${JSON.stringify(
                  results,
                )}`,
              );
            }
            resolve(anyRunning);
          })
          .catch((error) => {
            writeLog(`[Monitor] Error occurred during process check: ${error}`);
            resolve(false);
          });
      });
    },

    _enterActiveMonitoringState() {
      //设置轮询检查进程是否运行
      updateUiState("ACTIVE");
      this.monitorIntervalId = setInterval(() => {
        this._checkProcessExists().then((isProcessRunning) => {
          if (!isProcessRunning) {
            //如果程序没有运行进入宽恕期
            writeLog(
              "[Monitor] Game process has exited. Switching from active monitoring to grace period.",
            );
            this.stop(false);
            this._enterGracePeriodState();
          }
        });
      }, 10000);
    },

    _enterGracePeriodState() {
      showStartupNotification(
        "进入等待期",
        "程序进入等待期十分钟后会将会暂停加速",
        false,
      );
      const startTime = Date.now(); //等待期开始时间
      const endTime = startTime + 600000; //等待期结束时间
      let lastTimeStr = "";
      updateUiState("COUNTING", `⏳ ${formatTime(600000)}`);
      //看起来还需要一个定时器来自动刷新时间
      this.countdownIntervalId = setInterval(async () => {
        const remainingTime = endTime - Date.now();
        if (remainingTime <= 0) {
          //如果时间小于0就执行暂停加速然后最后一次判断有没有目标进程，如果有就进入活动期
          this.stop(false); //先停止定时器
          writeLog("[Monitor] Countdown finished. Performing final check..."); //做最后一次检查
          this._checkProcessExists().then(async (isProcessRunning) => {
            if (isProcessRunning) {
              writeLog(
                "[Monitor] Game has started during grace period. Switching to active monitoring state.",
              );
              this._enterActiveMonitoringState();
            } else {
              //确定没有运行就真正处理暂停
              this.stop(true);
              writeLog(
                "[Monitor] 10-minute grace period ended. Game did not start. Pausing acceleration.",
              );
              if (mainWindow) {
                try {
                  showStartupNotification(
                    "等待期已过",
                    "正在暂停加速器",
                    false,
                  );
                  await mainWindow.webContents.executeJavaScript(
                    'window.leigodSimplify.invoke("stop-acc",{"reason": "other"})',
                  );
                  await mainWindow.webContents.executeJavaScript(
                    'window.leigodSimplify.invoke("pause-user-time")',
                  );
                } catch (e) {
                  writeLog(
                    `[Monitor] ERROR: Failed to execute JS for pausing. Error: ${e}`,
                  );
                }
              } else {
                writeLog(
                  "[Monitor] ERROR: Could not find main window to pause acceleration.",
                );
              }
            }
          });
        } else {
          //这样设计是为了防止出现9:55 直接跳到了 9:53了 而没有9:54 这种情况 。
          //大概就是记录当前时间和上一次更新的时间，如果不一样才更新
          const currentTime = formatTime(remainingTime);
          if (currentTime !== lastTimeStr) {
            updateUiState("COUNTING", `⏳ ${currentTime}`);
            lastTimeStr = currentTime;
          }
        }
      }, 100);

      //设置轮询检查游戏是否重新启动 启动的话就进入_enterActiveMonitoringState
      this.graceCheckIntervalId = setInterval(() => {
        //每5秒检查一次如果启动了就吧宽恕期的定时器处理掉然后重新加入活动模式
        this._checkProcessExists().then((isProcessRunning) => {
          if (isProcessRunning) {
            writeLog(
              "[Monitor] Game has started during grace period. Switching to active monitoring state.",
            );
            this.stop(false);
            this._enterActiveMonitoringState();
          }
        });
      }, 5000);
    },
  };

  // ========== IPC拦截相关函数 ==========
  //该函数用于获得token为后续关机做准备
  function interceptedLogin(listener) {
    // --- 拦截 leigod-simplify-login ---
    return async (event, ...arg) => {
      const result = await listener(event, ...arg);
      try {
        if (result && result.result && result.result.account_token) {
          //拿到token
          GLOBAL_USER_TOKEN = result.result.account_token;
          writeLog(
            `[Token] Successfully obtained token. The token is ${GLOBAL_USER_TOKEN.substring(
              0,
              10,
            )}...`,
          );
        } else {
          writeLog(
            `[Token] Failed to obtain token : \n${JSON.stringify(
              result,
              null,
              2,
            )}.`,
          );
        }
      } catch (e) {
        writeLog(`[Token] ERROR: Failed to extract token. Error: ${e}`);
      }
      return result;
    };
  }
  //该函数用于拦截start-acc为后续做准备自动暂停准备
  function interceptedStartAcc(listener) {
    // --- 拦截 start-acc ---
    return async (event, ...args) => {
      const gameInfoArg = args[0]; //获取参数
      writeLog(
        `[patchIpcMain] "start-acc" intercepted!\nInitial Data:\n${JSON.stringify(
          gameInfoArg,
          null,
          2,
        )}`,
      );

      const result = await listener(event, ...args);
      writeLog(
        ` [patchIpcMain] "result" intercepted!\nInitial Data:\n${JSON.stringify(
          result,
          null,
          2,
        )}`,
      );

      if (result && result.error && result.error.message.code === 10007) {
        return result;
      }

      if (result && result.result.code === 200) {
        writeLog(
          "[interceptedStartAcc] Acceleration seems successful. Now fetching game info...",
        );
        handleGameProcessMonitoring(mainWindow, gameInfoArg);
      } else {
        writeLog(
          `[interceptedStartAcc] Acceleration did not start successfully. Aborting.`,
        );
      }
      return result;
    };
  }
  //该函数用于拦截stop-acc
  function interceptedStopAcc(listener, channel) {
    return async (event, ...args) => {
      writeLog(`[interceptedStopAcc] "${channel}" intercepted.`);
      if (channel === "leigod-simplify-pause-user-time") {
        MonitoringManager.stop(true);
        writeLog(`[patchIpcMain] "${channel}" intercepted. Stopping Monitor.`);
      } else {
        writeLog(
          `[patchIpcMain] "${channel}" intercepted. Keeping Monitor alive.`,
        );
      }

      return listener(event, ...args);
    };
  }

  /*function interceptedRecoverUserTime(listener, channel) {
    return async (event, ...args) => {
      writeLog(`[interceptedRecoverUserTime] "${channel}" intercepted.`);
      const result = await listener(event, ...args);
      MonitoringManager._enterGracePeriodState();
      return result;
    };
  }*/

  //该函数用于拦截分发
  function hookIpcHandle(channel, listener, originalIpcMainHandle) {
    let newListener;
    //修改为switch 方便以后拦截多个通道
    switch (channel) {
      case "leigod-simplify-login":
        newListener = interceptedLogin(listener);
        break;

      case "leigod-simplify-start-acc":
        newListener = interceptedStartAcc(listener);
        break;

      case "leigod-simplify-stop-acc":
      case "leigod-simplify-pause-user-time":
        newListener = interceptedStopAcc(listener, channel);
        break;
      // case "leigod-simplify-recover-user-time": //讲真，虽然我不认为真的会有人就解除暂停不加速游戏但是还是处理一下吧
      //   newListener = interceptedRecoverUserTime(listener, channel);
      //   break;

      default:
        // 不拦截其他通道，直接使用原始listener
        return originalIpcMainHandle.call(ipcMain, channel, listener);
    }
    //如果是目标就修改回调改成我们的
    return originalIpcMainHandle.call(ipcMain, channel, newListener);
  }
  //该函数用于拦截 IPC 通信，注入监控逻辑
  function patchIpcMain() {
    writeLog("[patchIpcMain] App is ready. Patching ipcMain.handle...");

    const originalIpcMainHandle = ipcMain.handle; //保存原始的 ipcMain.handle 方法

    ipcMain.handle = (channel, listener) => {
      return hookIpcHandle(channel, listener, originalIpcMainHandle);
    };
  }
  //该函数用于处理游戏进程为后续监控做准备
  async function handleGameProcessMonitoring(mainWindow, gameInfoArg) {
    if (!mainWindow || !gameInfoArg || !gameInfoArg.game_id) {
      // 检查窗口和参数
      return;
    }
    try {
      const QueryScript = `
                         (async () => {
                         const game = await (async (targetId) => {
                        const db = await new Promise((r, x) => {
                         const req = indexedDB.open('leigod_database_11.0.0.0');
                         req.onsuccess = () => r(req.result);
                         req.onerror = () => x(req.error);
                        });
                        return new Promise((r, x) => {
                        const q = db.transaction('game_list', 'readonly')
                        .objectStore('game_list')
                        .index('id')
                        .get(targetId);
                        q.onsuccess = () => r(q.result);
                        q.onerror = () => x(q.error);
                        });
                        })(${gameInfoArg.game_id});return game;
                        })();`;
      const GameInfo =
        await mainWindow.webContents.executeJavaScript(QueryScript);
      writeLog(
        `[GameMonitoring] Query returned:\n${JSON.stringify(GameInfo, null, 2)}`,
      );
      //判断进程
      if (GameInfo && GameInfo !== "") {
        //如果GameInfo 不为空
        let gameProcessList = [];
        /*判断是不是在排除项目，其次看看是不是免费加速的。如果是免费或者平台就不进入状态机*/
        if (ExcludedGameIDs.includes(GameInfo.id) || GameInfo.is_free === "1") {
          showStartupNotification(
            "自动暂停已跳过",
            "检测到当前加速项属于平台或免费项，自动暂停功能已跳过，请务必留意加速时长。",
            false,
          );
          writeLog(
            `[GameMonitoring] Game ID ${GameInfo.id} is in the exclusion list. ignored.`,
          );
          return;
        }
        //检查社区游戏数据库
        if (CommunityGameDB[String(GameInfo.id)]) {
          //先检查社区游戏数据库，防止雷神数据库中的进程名有假
          //如果社区游戏数据库中有此游戏
          gameProcessList = parseGameProcess(
            CommunityGameDB[String(GameInfo.id)],
          );
          writeLog(
            `[GameMonitoring] Parsed CommunityGameDB processes: ${JSON.stringify(
              gameProcessList,
            )}`,
          );
          MonitoringManager.start(gameProcessList);
        } else if (GameInfo.game_process && GameInfo.game_process !== "") {
          //进入雷神数据库获取游戏进程（我服了，雷神的进程库还有假的进程名，这个和写假注释一样可恶！他猫猫的）
          gameProcessList = parseGameProcess(GameInfo.game_process);
          writeLog(
            `[GameMonitoring] Parsed game processes: ${JSON.stringify(
              gameProcessList,
            )}`,
          );
          MonitoringManager.start(gameProcessList);
        } else {
          showStartupNotification(
            "获取游戏进程失败",
            "目标game_process字段中无法获取游戏名称,点击顶部状态栏“🔗 提交进程”进行反馈提交。",
            false,
          );
          writeLog(
            `[GameMonitoring] No game_process found. Aborting monitoring.`,
          );
          MonitoringManager.stop(true);
          updateUiState("MISSING");
        }
      }
    } catch (e) {
      writeLog(
        `[patchIpcMain] ERROR: Failed to call "get-game-info".\nError: ${e}`,
      );
    }
  }

  // ========== UI注入相关函数 ==========
  //该函数用于注入状态组件
  function injectStatusWidget() {
    app.on("browser-window-created", (event, window) => {
      writeLog("[Monitor] enter browser-window-created ...");
      try {
        window.webContents.on("did-finish-load", () => {
          const target = window.webContents.getURL();
          if (target && target.includes("renderer.asar/index.html")) {
            writeLog("[Monitor] Injecting UI widget...");
            mainWindow = window; //拿到主窗口后续用于注入状态组件以及获取游戏进程
            writeLog("[Monitor] Main Window registered.");
            const script = `const timer = setInterval(() => {
                        const navControl = document.querySelector(".nav-control");
                        const rechargeBtn = document.querySelector(".recharge-enrty");
                        if (navControl && rechargeBtn) {
                        clearInterval(timer);
                        if (document.getElementById("leigod-monitor-Widget")) return;
                        const div = document.createElement("div");
                        div.id = "leigod-monitor-Widget";
                        div.style.cssText = \`
                        height: 24px; 
                        min-width: 90px; 
                        background: rgba(255,255,255,0.1); 
                        border-radius: 12px; 
                        display: flex; 
                        align-items: center; 
                        justify-content: center; 
                        cursor: pointer; 
                        color: #a4a4a4; 
                        font-size: 12px; 
                        font-family: 'Microsoft YaHei'; 
                        -webkit-app-region: no-drag; 
                        transition: all 0.2s; 
                        padding: 0 10px; 
                        font-feature-settings: 'tnum';
                        user-select: none;
                        margin-right: 12px; 
                    \`;
                        //设置状态为空闲
                        div.dataset.state = "idle";
                        div.innerHTML = '<span id="leigod-status-text">⚙️ 自动监控</span>';
                        div.onmouseenter = () => {
                        if (div.dataset.state === "missing") {
                            div.style.background = "rgba(33, 150, 243, 0.4)";
                            div.style.color = "#1a75c2";
                        }
                        };
                        div.onmouseleave = () => {
                        if (div.dataset.state === "missing") {
                            div.style.background = "rgba(33, 150, 243, 0.1)";
                            div.style.color = "#2196f3";
                        }
                        };
                        div.onclick = () => {
                          if(div.dataset.state==="missing")
                          { //先弹github的提交进程的说明页面把,看后续是否需要。
                          window.leigodSimplify.invoke("open-external", "https://github.com/assortest/Leigod_Auto_Pause?tab=readme-ov-file#-%E8%B4%A1%E7%8C%AE%E6%8C%87%E5%8D%97");
                          }
                        
                        };
                        navControl.insertBefore(div, rechargeBtn);
                    }
                    }, 500);`;
            try {
              window.webContents.executeJavaScript(script);
              // eslint-disable-next-line no-unused-vars
            } catch (e) {
              writeLog("[Monitor] UI Injection Error");
            }
          }
        });
      } catch (e) {
        writeLog(`[Monitor] UI Injection Check Error: ${e.message}`);
      }
    });
  }
  //该函数用于更新ui状态
  function updateUiState(statecode, timeText = null) {
    //根据状态码拿到相应的配置
    const cfg = UI_STATES[statecode.toUpperCase()];
    if (!cfg) {
      //兜底检查
      writeLog(`[Update UI State] Invalid state code: ${statecode}`);
      return;
    }
    //判断是否需要显示时间
    const displayText = timeText ? timeText : cfg.text;
    const script = `(function (){
    const div = document.getElementById('leigod-monitor-Widget');
    const txt = document.getElementById('leigod-status-text');
    if(div && txt){
        div.style.color=\`${cfg.color}\`;
        div.style.background=\`${cfg.bg}\`;
        txt.innerText=\`${displayText}\`; 
        div.dataset.state = \`${cfg.code}\`; //告诉悬停
    }
  })() `;

    try {
      mainWindow.webContents.executeJavaScript(script);
    } catch (e) {
      writeLog("[Update UI State] Failed to update UI state: " + e.message);
    }
  }

  // ========== 关机和窗口关闭处理 ==========
  //该函数用于拦截主窗口关闭事件 用于在关机时暂停加速器
  function patchMainWindowClose() {
    writeLog("[patchMainWindowClose] Patching main window close event...");
    if (!mainWindow) {
      writeLog(
        "[patchMainWindowClose] Could not find main window to patch close event.",
      );
      return;
    }

    //监听session-end 用于在关机时暂停加速器
    mainWindow.on("session-end", (event) => {
      writeLog(
        "[Shutdown] session-end TRIGGERED! Windows is asking to shutdown.",
      );
      event.preventDefault(); //阻止关机，虽然并没有什么卵用
      writeLog("[Shutdown] Triggered. Launching CURL Missile...");

      if (!GLOBAL_USER_TOKEN) {
        //检查有没有用户令牌
        writeLog("[Shutdown] No user token found.");
        app.exit(0);
        return;
      }
      const API_URL = "https://webapi.leigod.com/api/user/pause";
      // {"account_token": "xxx", "lang": "zh_CN"}.
      //设置请求体
      const jsonBody = JSON.stringify({
        account_token: GLOBAL_USER_TOKEN,
        lang: "zh_CN",
      });

      try {
        //构造curl命令
        const child = spawn(
          "curl",
          [
            "-X",
            "POST",
            API_URL,
            "-H",
            "Content-Type: application/json",
            "-H",
            "User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/142.0.0.0 Safari/537.36", //模拟成浏览器
            "-d",
            jsonBody, // 数据体
            "-m",
            "3",
            "-s",
          ],
          {
            detached: true, //运行在后台
            stdio: "ignore", // 忽略输出
            windowsHide: true, //忽略Windows控制台
          },
        );
        writeLog("[Shutdown] CURL Launched with JSON Payload.");

        child.unref(); //让父进程可以立即退出，不等待curl请求完成
        const start = Date.now();
        //Domain Expansion
        while (Date.now() - start < 1500) {
          /*Infinite Void*/
        } //空转1.5秒来等待拉起curl防止进程结束没发包

        writeLog("[Shutdown] CURL launched. Electron exiting.");
      } catch (e) {
        writeLog(`[Shutdown] Spawn Error: ${e.message}`);
      }
      //晚安，世界。
      writeLog("[Shutdown] Good night, world.");
      app.exit(0);
    });

    mainWindow.on("close", async (event) => {
      //监听窗口关闭事件
      event.preventDefault(); //preventDefault
      writeLog(
        "[Close Intercept] Window close event triggered. Preventing immediate close.",
      );
      try {
        writeLog(
          '[Close Intercept] Attempting to execute "pause-user-time" command...',
        );
        await mainWindow.webContents.executeJavaScript(
          'window.leigodSimplify.invoke("pause-user-time")',
        );
        // eslint-disable-next-line no-unused-vars
      } catch (e) {
        /*这里执行暂停后会抛出异常但是无所谓了因为已经暂停了*/
        writeLog(
          "[Close Intercept] Caught expected exception after command execution. Ignoring.",
        );
      } finally {
        //无论否成功，都强制退出程序
        writeLog(
          "[Close Intercept] All tasks finished. Forcing application quit.",
        );
        app.exit(0);
      }
    });
  }
  // ========== 初始化 ==========
  //程序入口与初始化
  try {
    fs.writeFileSync(logFilePath, "");
    // eslint-disable-next-line no-unused-vars
  } catch (err) {
    /* empty */
  } //清空日志文件
  writeLog("[Main] Script loaded and log file cleared.");

  app.whenReady().then(() => {
    //完成初始化后执行下面操作
    showStartupNotification(
      "Leigod Smart Monitor 已启用",
      "leigod-auto-pause插件加载成功",
      false,
    );
    patchIpcMain();
    injectStatusWidget();
    setTimeout(() => {
      patchMainWindowClose(); // browser-window-created也行 但是不想写了 摸了
    }, 15000);
  });
  require("bytenode");
  require("./main.jsc");
} catch (e) {
  console.error("leigod-appmain.js error:", e);
  const { dialog } = require("electron");
  dialog.showErrorBox("leigod-appmain.js", e + "" + e.stack);
  process.exit(1);
}
