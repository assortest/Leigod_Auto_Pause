import os
import shutil
import requests 
import asar 
import sys
import ctypes
from pathlib import Path

def get_base_path():
    """获取资源的基准路径，兼容开发环境和 PyInstaller 打包后的环境"""
    if getattr(sys, 'frozen', False) and hasattr(sys, '_MEIPASS'):
        # 如果程序是被 PyInstaller 打包成了 exe
        # sys.executable 会返回 exe 文件的绝对路径
        return Path(sys.executable).parent
    else:
        # 如果是直接运行 .py 脚本
        # __file__ 会返回 .py 文件的路径
        return Path(__file__).parent

# --- 配置区 ---
BASE_DIR = get_base_path()
RESOURCES_DIR =  BASE_DIR / "resources"
FILE_TO_REPLACE = os.path.join("dist", "main", "main.js") # 要替换的文件在 asar 内的相对路径
JS_DOWNLOAD_URL = "https://raw.githubusercontent.com/assortest/Leigod_Auto_Pause/refs/heads/main/main.js"
# --- 配置结束 ---

def main() -> None:
    if not isadmin():
        print("请以管理员权限运行本程序！")
        return
    asar_path = RESOURCES_DIR / "app.asar"#拼接目录
    temp_dir =Path("temp_asar_unpack")

    print(f"正在查找目标文件: {asar_path}")

    if not os.path.exists(asar_path):
        print("错误: 找不到 app.asar 文件！请检查本程序是否被防止在雷神加速器的根目录下！")
        return

    # 1. 备份 (这是一个好习惯)
    backup_path = asar_path.with_suffix(asar_path.suffix + ".bak") #备份bak
    print(f"正在备份原始文件到: {backup_path}")
    shutil.copy2(asar_path, backup_path)

    # 2. 解压
    
    print(f"正在解压 app.asar 到临时目录: {temp_dir}")
    # 如果临时目录已存在，先删掉
    if os.path.exists(temp_dir):
        shutil.rmtree(temp_dir)
        
    asar.extract_archive(asar_path, temp_dir)
    print("解压完成！")

    # 3. 下载并替换文件
    try:
        print(f"正在从 GitHub 下载替换文件: {JS_DOWNLOAD_URL}")
        response = requests.get(JS_DOWNLOAD_URL, headers={'User-Agent': 'Patcher Tool'})
        response.raise_for_status() # 会抛出异常

        target_file_path = os.path.join(temp_dir, FILE_TO_REPLACE)
        with open(target_file_path, "wb") as f: # 使用 (二进制写入) 模式最安全
            f.write(response.content)#吧得到的内容写入文件
        print("文件下载并替换成功！")
    except requests.exceptions.RequestException as e:#错误处理
        print(f"下载文件时发生错误: {e}")
        shutil.rmtree(temp_dir) # 清理
        return

    # 4. 重新打包 (最关键的一步)
    print("正在重新打包文件...")
    asar.create_archive(temp_dir, asar_path)
    
   
    print("打包完成！")

    # 5. 清理临时文件
    print("正在清理临时文件...")
    shutil.rmtree(temp_dir)

    print("\n操作全部成功！")


def isadmin()->bool:
    try:
        return ctypes.windll.shell32.IsUserAnAdmin()
    except:
        return False

if __name__ == "__main__":
    main()
    input("按 Enter 键退出...")
