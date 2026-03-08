using AsarSharp;
using Leigod_Auto_Pause.Installer;
using SettingManager;
using System.Diagnostics;
using System.Security.Principal;

class Program
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    public static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    private const string JsDownloadUrl = "https://gitee.com/assortest/Leigod_Auto_Pause/raw/main/main.js";
    private const string FileToReplace = "dist/main/main.js";

    static async Task Main(string[] args)
    {
        try
        {
            var bootstrapArgs = BootstrapArguments.Parse(args);
            var currentExePath = Environment.ProcessPath ?? throw new InvalidOperationException("无法获取当前可执行文件路径。");
            var currentDirectory = NormalizeDirectoryPath(AppContext.BaseDirectory);

            if (bootstrapArgs.InstallDirectory is not null)
            {
                await HandleElevatedInstallAsync(currentExePath, bootstrapArgs.InstallDirectory);
                return;
            }

            if (!bootstrapArgs.IsInstalledLaunch)
            {
                var locator = new LeigodInstallLocator(new SystemRegistryReader());
                var candidate = locator.LocateBestCandidate();
                var bootstrap = new LaunchBootstrap();
                var decision = bootstrap.Decide(
                    currentExePath,
                    currentDirectory,
                    candidate is null ? [] : [candidate]);

                if (decision.Action == BootstrapAction.InstallAndRelaunch)
                {
                    await StartInstallFlowAsync(currentExePath, decision.TargetDirectory!);
                    return;
                }

                if (decision.Action == BootstrapAction.Abort)
                {
                    ShowError(decision.ErrorMessage ?? "未找到雷神安装目录。", "安装失败");
                    return;
                }
            }

            if (!IsRunningAsAdmin())
            {
                RelaunchSelfWithArguments(args, runAsAdmin: true);
                return;
            }

            await RunPatchAndLaunchAsync(currentDirectory);
        }
        catch (Exception ex)
        {
            ShowError($"程序运行时发生未知错误：\n\n{ex.Message}", "致命错误");
        }
    }

    private static async Task StartInstallFlowAsync(string sourceExePath, string installDirectory)
    {
        if (IsRunningAsAdmin())
        {
            await HandleElevatedInstallAsync(sourceExePath, installDirectory);
            return;
        }

        RelaunchSelfWithArguments([BootstrapArguments.PerformInstallFlag, installDirectory], runAsAdmin: true);
    }

    private static async Task HandleElevatedInstallAsync(string sourceExePath, string installDirectory)
    {
        if (!IsRunningAsAdmin())
        {
            RelaunchSelfWithArguments([BootstrapArguments.PerformInstallFlag, installDirectory], runAsAdmin: true);
            return;
        }

        var installer = new SelfInstaller();
        var desktopDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var installedExePath = installer.Install(sourceExePath, installDirectory, desktopDirectory);

        RelaunchExecutable(installedExePath, [BootstrapArguments.InstalledLaunchFlag], runAsAdmin: true);
        await Task.CompletedTask;
    }

    private static async Task RunPatchAndLaunchAsync(string currentDirectory)
    {
        var asarPath = Path.Combine(currentDirectory, "resources", "app.asar");

        if (await NeedUpdate(asarPath))
        {
            AllocConsole();
            Console.WriteLine("检查到第一次运行或者程序更新。正在获取插件。");
            var patchSuccess = await ApplyPatch(asarPath);
            if (patchSuccess)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("正在试图启动雷神加速器...");
                LaunchLeigod(currentDirectory);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("补丁应用失败，按任意键退出。");
                Console.ResetColor();
                Console.ReadKey();
            }

            FreeConsole();
            return;
        }

        LaunchLeigod(currentDirectory);
    }

    public static async Task<bool> ApplyPatch(string asarPath)
    {
        string? tempDir = null;
        try
        {
            Console.WriteLine("正在查找目标文件");
            Thread.Sleep(2000);
            if (!File.Exists(asarPath))
            {
                throw new FileNotFoundException("未找到文件，请确保雷神已安装完成。");
            }

            Console.WriteLine("找到文件 app.asar ！");

            tempDir = Path.Combine(Path.GetTempPath(), "AsarPatcher_" + Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            Console.WriteLine("正在解压 app.asar 文件...");

            using (var extractor = new AsarExtractor(asarPath, tempDir))
            {
                extractor.Extract();
            }

            Console.WriteLine("目标解压完成");
            Console.WriteLine("正在从 GitHub 下载文件...");
            Console.WriteLine($"URL: {JsDownloadUrl}");

            byte[] fileBytes;
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Leigod Auto Pause Patch Tool");
                fileBytes = await client.GetByteArrayAsync(JsDownloadUrl);
                var fileToReplacePath = Path.Combine(tempDir, FileToReplace);
                await File.WriteAllBytesAsync(fileToReplacePath, fileBytes);
                Console.WriteLine("文件下载并替换成功！");
            }

            var backupAsarPath = asarPath + ".bak";
            if (!File.Exists(backupAsarPath))
            {
                File.Copy(asarPath, backupAsarPath, true);
                Console.WriteLine("正在备份原始文件");
            }

            Console.WriteLine("正在重新打包文件");
            using (var archiver = new AsarArchiver(tempDir, asarPath))
            {
                archiver.Archive();
            }

            Console.WriteLine("打包完成！");
            Console.WriteLine("正在用新文件覆盖原文件...");
            Console.WriteLine("操作成功！");
            Console.WriteLine("正在更新状态信息...");
            Thread.Sleep(2000);

            var newAsarHash = GetFileSha256(asarPath);
            var newJsHash = GetBytesSha256(fileBytes);
            Manager.Save(new AppSettings
            {
                PatchedAsarHash = newAsarHash,
                AppliedJsHash = newJsHash
            });
            Console.WriteLine("状态信息更新完毕！");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"处理过程中发生未知错误: {ex.Message}");
            return false;
        }
        finally
        {
            if (tempDir is not null && Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
                Console.WriteLine("清理目录");
            }
        }

        return true;
    }

    public static async Task<bool> NeedUpdate(string asarPath)
    {
        var settings = Manager.Load();
        if (settings is null || string.IsNullOrEmpty(settings.PatchedAsarHash))
        {
            return true;
        }

        var currentAsarHash = GetFileSha256(asarPath);
        if (!string.Equals(currentAsarHash, settings.PatchedAsarHash, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "Leigod Auto Pause Patch Tool");
        var jsBytes = await client.GetByteArrayAsync(JsDownloadUrl);
        var remoteJsHash = GetBytesSha256(jsBytes);

        return !string.Equals(remoteJsHash, settings.AppliedJsHash, StringComparison.OrdinalIgnoreCase);
    }

    public static string GetFileSha256(string filePath)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = sha256.ComputeHash(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    public static string GetBytesSha256(byte[] data)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(data);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    public static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void LaunchLeigod(string resourcesPath)
    {
        try
        {
            var leigodExePath = Path.Combine(resourcesPath, "leigod_launcher.exe");
            if (Path.Exists(leigodExePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = leigodExePath,
                    UseShellExecute = true,
                });
            }
            else
            {
                ShowError("未找到雷神加速器主程序，请确认雷神安装目录完整。", "启动失败");
            }
        }
        catch (Exception ex)
        {
            ShowError($"启动雷神加速器时发生未知错误：\n\n{ex.Message}", "致命错误");
        }
    }

    private static void RelaunchSelfWithArguments(IReadOnlyList<string> args, bool runAsAdmin)
    {
        var currentExePath = Environment.ProcessPath ?? throw new InvalidOperationException("无法获取当前可执行文件路径。");
        RelaunchExecutable(currentExePath, args, runAsAdmin);
    }

    private static void RelaunchExecutable(string executablePath, IReadOnlyList<string> args, bool runAsAdmin)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = JoinArguments(args),
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory
        };

        if (runAsAdmin)
        {
            startInfo.Verb = "runas";
        }

        Process.Start(startInfo);
    }

    private static string JoinArguments(IReadOnlyList<string> args)
    {
        return string.Join(" ", args.Select(QuoteArgument));
    }

    private static string QuoteArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg))
        {
            return "\"\"";
        }

        if (!arg.Contains(' ') && !arg.Contains('"'))
        {
            return arg;
        }

        return $"\"{arg.Replace("\"", "\\\"")}\"";
    }

    private static string NormalizeDirectoryPath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static void ShowError(string message, string caption)
    {
        MessageBox(IntPtr.Zero, message, caption, 0x10);
    }
}
