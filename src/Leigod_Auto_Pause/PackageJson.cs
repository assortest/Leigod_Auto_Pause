using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public class PackageJson
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("productName")]
    public string ProductName { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    // private 是 C# 关键字,用 @ 转义,或改成别的名字 + JsonPropertyName
    [JsonPropertyName("private")]
    public bool Private { get; set; }

    [JsonPropertyName("author")]
    public string Author { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("main")]
    public string Main { get; set; }

    // 依赖包:包名 -> 版本号,用字典最合适
    [JsonPropertyName("dependencies")]
    public Dictionary<string, string> Dependencies { get; set; }

    [JsonPropertyName("overrides")]
    public Dictionary<string, string> Overrides { get; set; }
}