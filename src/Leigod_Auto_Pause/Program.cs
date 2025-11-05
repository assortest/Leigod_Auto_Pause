
using AsarSharp;
using System.Security.Principal;
using SettingManager;
using System.Diagnostics;

class Program
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool AllocConsole();
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    public static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    static string jsDownloadUrl = "https://gitee.com/assortest/Leigod_Auto_Pause/raw/main/main.js"; //使用了
    static string fileToReplace = "dist/main/main.js";




    static async Task Main(string[] args)
    {
        try
        {
            if (!IsRunningAsAdmin())
            {
                AllocConsole();
                Console.WriteLine("请以管理员身份运行此程序！");
                Console.WriteLine("请右键点击程序，选择“以管理员身份运行”。");
                Console.ReadKey();
                FreeConsole();
                return;
            }

            string currentDirctory = currentDirctory = AppContext.BaseDirectory;//获取当前程序运行目录
            string asarpath = asarpath = Path.Combine(currentDirctory, "resources", "app.asar");//获取app.asar文件路径

            if (await NeedUpdate(asarpath))
            {
                AllocConsole();
                Console.WriteLine("检查到第一次运行或者程序更新。正在获取插件。");
                bool patchSuccess = await applyPatch(asarpath);
                if (patchSuccess)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("正在试图启动雷神加速器...");
                    LaunchLeigod(currentDirctory);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("补丁应用失败，按任意键退出。");
                    Console.ResetColor();
                    Console.ReadKey();
                }
                FreeConsole();
            }
            else
            {
                LaunchLeigod(currentDirctory);
            }
        }
        catch (Exception ex)
        {
            string errorMessage = $"程序运行时发生未知错误：\n\n{ex.Message}";
            MessageBox(IntPtr.Zero, errorMessage, "致命错误", 0x10);


        }
    }

    public static async Task<bool> applyPatch(string asarpath)
    {
        string tempDir = null;
        //检查替换文件打包逻辑
        try
        {
            //如果需要更新，执行更新逻辑 并且显示控制台窗口

            Console.WriteLine($"正在查找目标文件");
            Thread.Sleep(2000);
            if (!File.Exists(asarpath))
            {
                throw new FileNotFoundException("未找到文件,请吧当前软件放入雷神加速器的根目录！");
            }

            Console.WriteLine("找到文件 app.asar ！");

            tempDir = Path.Combine(Path.GetTempPath(), "AsarPatcher_" + Path.GetRandomFileName()); //创建临时目录带AsarPatcher_前缀
            Directory.CreateDirectory(tempDir); //创建目录
            Console.WriteLine("正在解压 app.asar 文件...");

            using (var extractor = new AsarExtractor(asarpath, tempDir))  //处理解压文件
            {
                extractor.Extract();
            }
            Console.WriteLine("目标解压完成");

            //处理下载文件
            Console.WriteLine("正在从 GitHub 下载文件...");
            Console.WriteLine($"URL: {jsDownloadUrl}");
            byte[] fileBytes;
            using (var Client = new HttpClient())
            {
                Client.DefaultRequestHeaders.Add("User-Agent", "Leigod Auto Pause Patch Tool");
                fileBytes = await Client.GetByteArrayAsync(jsDownloadUrl); //采用二进制防止编码问题
                string fileToreplacePath = Path.Combine(tempDir, fileToReplace);
                await File.WriteAllBytesAsync(fileToreplacePath, fileBytes);//替换文件
                Console.WriteLine("文件下载并替换成功！");

            }

            string backupAsarPath = asarpath + ".bak";
            if (!(File.Exists(backupAsarPath)))
            {//如果不存在备份文件则创建备份
                File.Copy(asarpath, backupAsarPath, true);//备份原始文件
                Console.WriteLine("正在备份原始文件");
            }
            Console.WriteLine("正在重新打包文件");

            var archiver = new AsarArchiver(tempDir, asarpath);
            
            archiver.Archive();
            archiver.Dispose();//我服了这里需要手动释放否则会文件被占用 弄死我了
            Console.WriteLine("打包完成！");
            Console.WriteLine("正在用新文件覆盖原文件...");
            Console.WriteLine("操作成功！");
            Console.WriteLine("正在更新状态信息...");
            Thread.Sleep(2000); //等待2秒 防止archiver没释放
            string newAsarHash = GetFileSha256(asarpath);
            string newJsHash = GetBytesSha256(fileBytes);

            var settings = new AppSettings
            {
                PatchedAsarHash = newAsarHash,
                AppliedJsHash = newJsHash
            };
            Manager.Save(settings);
            Console.WriteLine("状态信息更新完毕！");

        }
        catch (Exception ex)
        {
            Console.WriteLine($"处理过程中发生未知错误: {ex.Message}");
            return false;
        }
        finally
        {
            if (tempDir != null && Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true); //确保删除临时目录
                Console.WriteLine("清理目录");

            }
        }

        return true;
    }


    public static async Task<bool> NeedUpdate(string asarpath)
    {
        //这里可以添加实际的版本检查逻辑，例如从服务器获取最新版本号并与当前版本号比较
        var settings = Manager.Load();
        if (settings == null || string.IsNullOrEmpty(settings.PatchedAsarHash))
        { //检查配置文件是否存在不存在就说明需要更新
            return true;
        }
        //计算当前asar文件的哈希值
        string currentAsarHash = GetFileSha256(asarpath);
        if (!string.Equals(currentAsarHash, settings.PatchedAsarHash, StringComparison.OrdinalIgnoreCase))
        {
     
            return true;
        }

        //检查插件是否需要更新

        using (var Client = new HttpClient())
        {
            Client.DefaultRequestHeaders.Add("User-Agent", "Leigod Auto Pause Patch Tool");
            byte[] jsBytes = await Client.GetByteArrayAsync(jsDownloadUrl); //采用二进制防止编码问题

            string remoteJsHash = GetBytesSha256(jsBytes);
            if (!string.Equals(remoteJsHash, settings.AppliedJsHash, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

        }
        //不需要更新
        return false;
    }


    public static string GetFileSha256(string filePath)
    { //用于哈希计算
        using var sha256 = System.Security.Cryptography.SHA256.Create();//创建SHA256实例
        {
            using var stream = File.OpenRead(filePath);//打开文件流
            {
                byte[] hashBytes = sha256.ComputeHash(stream);//计算文件的哈希值

                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();//将字节数组转换为十六进制字符串
            }
        }
    }
    public static string GetBytesSha256(byte[] data)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(data);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }



    public static bool IsRunningAsAdmin()
    {
        using (WindowsIdentity identity = WindowsIdentity.GetCurrent()) //返回当前Windows用户的标识
        {
            WindowsPrincipal principal = new WindowsPrincipal(identity);//将身份包装成可进行角色检查的对象
            return principal.IsInRole(WindowsBuiltInRole.Administrator);//检查当前用户是否为管理员
        }
    }


    public static void LaunchLeigod(string resourcesPath)
    {
        try
        {
            string leigodExePath = Path.Combine(resourcesPath, "leigod_launcher.exe");
            if (Path.Exists(leigodExePath))
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = leigodExePath,
                    UseShellExecute = true,
                };
                Process.Start(startInfo);
            }
            else
            {
                string errorMessage = $"未找到雷神加速器主程序：\n\n" +
                    $"\n\n请确保本启动器与 leigod.exe 放置在同一个目录下。";
                MessageBox(IntPtr.Zero, errorMessage, "启动失败", 0x10); 

            }
        }
        catch (Exception ex)
        {
            string errorMessage = $"启动雷神加速器时发生未知错误：\n\n{ex.Message}";
            MessageBox(IntPtr.Zero, errorMessage, "致命错误", 0x10);
        }
    }
}


