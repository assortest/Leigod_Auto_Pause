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

  //社区维护的游戏进程名
  const CommunityGameDB = {
    1559: "VALORANT-Win64-Shipping.exe",
    2167: "Warframe.x64.exe",
    258: "Warframe.x64.exe",
    137: "vermintide2_dx12.exe,vermintide2.exe",
  };
  //steam epic 暴雪 育碧uplay eaapp  rockstar GOG
  const ExcludedGameIDs = [109, 437, 1544, 274, 1921, 1342, 860];

  //全局配置区域
  let GLOBAL_USER_TOKEN = "";
  const { app, ipcMain, BrowserWindow, Notification } = require("electron"); // 结构引入 Electron 使用的模块
  const { exec, spawn } = require("child_process");
  const path = require("path"); //用于处理路径
  const fs = require("fs"); //用于文件操作
  const https = require("https");
  const userDataPath = app.getPath("userData");
  const logFilePath = path.join(userDataPath, "leigod_Monitor_log.txt"); //和文件名拼接成完整的路径

  //辅助函数区域用于解析字符串
  function writeLog(message) {
    //用于记录日志
    const timestamp = new Date().toISOString(); //获取当前时间

    const logMessage = `[${timestamp}] ${message}\n---------------------------------------\n`;
    try {
      fs.appendFileSync(logFilePath, logMessage);
    } catch (err) {} //写入日志文件
  }

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
  //监控管理器 核心逻辑
  const MonitoringManager = {
    //创建一个单例
    targetProcesses: [],
    monitorIntervalId: null, //监控状态
    graceTimeoutId: null, //宽限期id
    graceCheckIntervalId: null, //宽限期检查的id

    start: function (processList) {
      writeLog(
        `[Monitor] Received start command for: ${processList.join(", ")}`
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
            "[Monitor] Game is already running. Entering active monitoring state."
          );

          this._enterActiveMonitoringState();
        } else {
          writeLog(
            "[Monitor] Game is not running. Entering grace period state."
          );
          this._enterGracePeriodState();
        }
      });
    },
    stop: function (clearList = true) {
      writeLog("[Monitor] Stop command received. Clearing all timers.");
      //清理掉所有定时器
      if (this.monitorIntervalId) clearInterval(this.monitorIntervalId);
      if (this.graceTimeoutId) clearTimeout(this.graceTimeoutId);
      if (this.graceCheckIntervalId) clearInterval(this.graceCheckIntervalId);

      if (clearList) {
        this.targetProcesses = [];
      }
      this.monitorIntervalId = null;
      this.graceTimeoutId = null;
      this.graceCheckIntervalId = null;
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
            exec(command, (error, stdout, stderr) => {
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
                  results
                )}`
              );
            } else {
              writeLog(
                `[Monitor] No target processes found running. Results: ${JSON.stringify(
                  results
                )}`
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

      this.monitorIntervalId = setInterval(() => {
        this._checkProcessExists().then((isProcessRunning) => {
          if (!isProcessRunning) {
            //如果程序没有运行进入宽恕期
            writeLog(
              "[Monitor] Game process has exited. Switching from active monitoring to grace period."
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
        false
      );
      this.graceTimeoutId = setTimeout(async () => {
        const mainWindow = BrowserWindow.getAllWindows()[0];
        writeLog(
          "[Monitor] 10-minute grace period ended. Game did not start. Pausing acceleration."
        );
        if (mainWindow) {
          try {
            showStartupNotification("等待期已过", "正在暂停加速器", false);
            await mainWindow.webContents.executeJavaScript(
              'window.leigodSimplify.invoke("stop-acc",{"reason": "other"})'
            );
            await mainWindow.webContents.executeJavaScript(
              'window.leigodSimplify.invoke("pause-user-time")'
            );
          } catch (e) {
            writeLog(
              `[Monitor] ERROR: Failed to execute JS for pausing. Error: ${e}`
            );
          }
        } else {
          writeLog(
            "[Monitor] ERROR: Could not find main window to pause acceleration."
          );
        }

        this.stop(true);
      }, 600000); //十分钟自动运行

      //设置轮询检查游戏是否重新启动 启动的话就进入_enterActiveMonitoringState
      this.graceCheckIntervalId = setInterval(() => {
        //每5秒检查一次如果启动了就吧宽恕期的定时器处理掉然后重新加入活动模式
        this._checkProcessExists().then((isProcessRunning) => {
          if (isProcessRunning) {
            writeLog(
              "[Monitor] Game has started during grace period. Switching to active monitoring state."
            );
            this.stop(false);
            this._enterActiveMonitoringState();
          }
        });
      }, 5000);
    },
  };

  // 拦截 IPC 通信，注入监控逻辑
  function patchIpcMain() {
    writeLog("[patchIpcMain] App is ready. Patching ipcMain.handle...");

    const originalIpcMainHandle = ipcMain.handle; //保存原始的 ipcMain.handle 方法

    ipcMain.handle = (channel, listener) => {
      // --- 拦截 leigod-simplify-login ---
      if (channel === "leigod-simplify-login") {
        const newListener = async (event, ...args) => {
          //建立一个新的函数
          const result = await listener(event, ...args); //执行原始函数得到参数
          try {
            if (result && result.result && result.result.account_token) {
              //拿到token
              GLOBAL_USER_TOKEN = result.result.account_token;
              writeLog(
                `[Token] Successfully obtained token. The token is ${GLOBAL_USER_TOKEN.substring(
                  0,
                  10
                )}...`
              );
            } else {
              writeLog(
                `[Token] Failed to obtain token : \n${JSON.stringify(
                  result,
                  null,
                  2
                )}.`
              );
            }
          } catch (e) {
            writeLog(`[Token] ERROR: Failed to extract token. Error: ${e}`);
          }
          return result;
        };
        return originalIpcMainHandle.call(ipcMain, channel, newListener);
      }

      // --- 拦截 start-acc ---
      if (channel === "leigod-simplify-start-acc") {
        const newListener = async (event, ...args) => {
          const gameInfoArg = args[0]; //获取参数
          writeLog(
            ` "start-acc" intercepted!\nInitial Data:\n${JSON.stringify(
              gameInfoArg,
              null,
              2
            )}`
          );

          const result = await listener(event, ...args);
          writeLog(
            ` "result" intercepted!\nInitial Data:\n${JSON.stringify(
              result,
              null,
              2
            )}`
          );
          if (result && result.error && result.error.message.code === 10007) {
            return result;
          }
          // 在原始加速逻辑成功后
          if (result && result.result.code === 200) {
            writeLog(
              "[patchIpcMain] Acceleration seems successful. Now fetching game info..."
            );
            const mainWindow = BrowserWindow.getAllWindows()[0];

            if (mainWindow && gameInfoArg && gameInfoArg.game_id) {
              // 检查窗口和参数
              try {
                // 获取游戏信息
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
                        })()
                       ;`;

                const GameInfo = await mainWindow.webContents.executeJavaScript(
                  QueryScript
                );
                writeLog(
                  `[patchIpcMain] query returned:\n${JSON.stringify(
                    GameInfo,
                    null,
                    2
                  )}`
                );
                if (GameInfo && GameInfo !== "") {
                  //如果GameInfo 不为空
                  let gameProcessList = [];
                  if (ExcludedGameIDs.includes(GameInfo.id)) {
                     showStartupNotification( "自动暂停已跳过",
                      "检测到当前加速项属于平台或排除项，自动暂停功能已跳过，请务必留意加速时长。",
                      false)
                    writeLog(   
                      `[patchIpcMain] Game ID ${GameInfo.id} is in the exclusion list. ignored.`
                    );
                    return result;
                  }

                  if (CommunityGameDB[String(GameInfo.id)]) {
                    //先检查社区游戏数据库，防止雷神数据库中的进程名有假
                    //如果社区游戏数据库中有此游戏
                    gameProcessList = parseGameProcess(
                      CommunityGameDB[String(GameInfo.id)]
                    );
                    writeLog(
                      `[patchIpcMain] Parsed CommunityGameDB processes: ${JSON.stringify(
                        gameProcessList
                      )}`
                    );
                    MonitoringManager.start(gameProcessList);
                  } else if (
                    GameInfo.game_process &&
                    GameInfo.game_process !== ""
                  ) {
                    //进入雷神数据库获取游戏进程（我服了，雷神的进程库还有假的进程名，这个和写假注释一样可恶！他猫猫的）
                    gameProcessList = parseGameProcess(GameInfo.game_process);
                    writeLog(
                      `[patchIpcMain] Parsed game processes: ${JSON.stringify(
                        gameProcessList
                      )}`
                    );
                    MonitoringManager.start(gameProcessList);
                  } else {
                    showStartupNotification(
                      "获取游戏进程失败",
                      "目标game_process字段中无法获取游戏名称,将无法启动自动暂停功能。",
                      false
                    );
                    writeLog(
                      `[patchIpcMain] No game_process found. Aborting monitoring.`
                    );
                    MonitoringManager.stop(true);
                  }
                }
              } catch (e) {
                writeLog(
                  `[patchIpcMain] ERROR: Failed to call "get-game-info".\nError: ${e}`
                );
              }
            }
          } else {
            writeLog(
              `[patchIpcMain] Acceleration did not start successfully. Aborting.`
            );
          }
          return result;
        };
        return originalIpcMainHandle.call(ipcMain, channel, newListener);
      }

      // --- 拦截 stop-acc 和 pause-user-time ---
      if (
        channel === "leigod-simplify-stop-acc" ||
        channel === "leigod-simplify-pause-user-time"
      ) {
        const newListener = async (event, ...args) => {
          writeLog(
            `[patchIpcMain] "${channel}" intercepted. Monitoring would stop here.`
          );
          if (channel === "leigod-simplify-pause-user-time") {
            MonitoringManager.stop(true);
            writeLog(
              `[patchIpcMain] "${channel}" intercepted. Stopping Monitor.`
            );
          } else {
            writeLog(
              `[patchIpcMain] "${channel}" intercepted. Keeping Monitor alive.`
            );
          }
          return listener(event, ...args);
        };
        return originalIpcMainHandle.call(ipcMain, channel, newListener);
      }

      return originalIpcMainHandle.call(ipcMain, channel, listener);
    };
  }
  function patchMainWindowClose() {
    writeLog("[patchMainWindowClose] Patching main window close event...");
    const mainWindow = BrowserWindow.getAllWindows()[0];
    if (!mainWindow) {
      writeLog(
        "[patchMainWindowClose] Could not find main window to patch close event."
      );
      return;
    }

    //监听session-end 用于在关机时暂停加速器
    mainWindow.on("session-end", (event) => {
      writeLog(
        "[Shutdown] session-end TRIGGERED! Windows is asking to shutdown."
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
          }
        );
        writeLog("[Shutdown] CURL Launched with JSON Payload.");

        child.unref(); //让父进程可以立即退出，不等待curl请求完成
        const start = Date.now();
        while (Date.now() - start < 1500) {} //空转1.5秒来等待拉起curl防止进程结束没发包

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
        "[Close Intercept] Window close event triggered. Preventing immediate close."
      );
      try {
        writeLog(
          '[Close Intercept] Attempting to execute "pause-user-time" command...'
        );
        await mainWindow.webContents.executeJavaScript(
          'window.leigodSimplify.invoke("pause-user-time")'
        );
      } catch (e) {
        /*这里执行暂停后会抛出异常但是无所谓了因为已经暂停了*/
        writeLog(
          "[Close Intercept] Caught expected exception after command execution. Ignoring."
        );
      } finally {
        //无论否成功，都强制退出程序
        writeLog(
          "[Close Intercept] All tasks finished. Forcing application quit."
        );
        app.exit(0);
      }
    });
  }
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
        `[Notification] Displayed: "${title}" - "${body}" (silent: ${silent})`
      );
    } catch (err) {
      writeLog(`[Notification] Failed to show: ${err.message}`);
    }
  }

  //程序入口与初始化
  try {
    fs.writeFileSync(logFilePath, "");
  } catch (err) {} //清空日志文件
  writeLog("[Main] Script loaded and log file cleared.");

  app.whenReady().then(() => {
    //完成初始化后执行下面操作
    showStartupNotification(
      "Leigod Smart Monitor 已启用",
      "leigod-auto-pause插件加载成功",
      false
    );
    patchIpcMain();
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
