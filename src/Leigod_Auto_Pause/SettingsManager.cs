using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;


namespace SettingManager
{
    internal static class Manager
    {
        //得到配置文件夹路径用于存储配置文件
        private static readonly string _configFolderPath = Path.Combine(Environment.GetFolderPath
            (Environment.SpecialFolder.ApplicationData)
            , "LeigodPatcher");
        //得到配置文件路径
        private static readonly string _settingsFilePath = Path.Combine(_configFolderPath,
            "settings.json");
        private static readonly JsonSerializerOptions _serializerOptions = new()
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            WriteIndented = true
        };



        public static AppSettings? Load()
        {
           
            if (!File.Exists(_settingsFilePath)) return null;

            //读取配置文件内容
            string json = File.ReadAllText(_settingsFilePath);
            try
            { //反序列化配置文件内容

                return JsonSerializer.Deserialize<AppSettings>(json, _serializerOptions);
            }
            catch
            {
             
                return null;
            }

        }

        public static void Save(AppSettings settings)
        {

            Directory.CreateDirectory(_configFolderPath);
          
            string json = JsonSerializer.Serialize(settings, _serializerOptions);
            File.WriteAllText(_settingsFilePath, json);

        }
    }
    public class AppSettings
    {
        public string PatchedAsarHash { get; set; } = string.Empty;
        public string AppliedJsHash { get; set; } = string.Empty;
    }
    
}
