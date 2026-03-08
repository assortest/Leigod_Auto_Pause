# Dev Notes

## Formatting

本项目现在以根目录的 [`.editorconfig`](/x:/code/Leigod_Auto_Pause/.editorconfig) 作为统一格式协议，供 `dotnet format`、Visual Studio、Rider 这类 fmt 工具读取。

当前约定很简单：

- 全仓库统一 `utf-8`、`CRLF`、文件末尾保留换行
- `C#` 使用 4 空格缩进，花括号换行，`using` 里 `System.*` 优先
- `main.js` 保持 2 空格缩进
- `Markdown` 不强制裁剪行尾空格，避免列表/排版被误伤

## Recommended Command

优先使用这个命令做格式化：

```powershell
dotnet format src/Leigod_Auto_Pause.sln --no-restore
```

如果只想格式化某个文件，优先让 IDE 按 `.editorconfig` 规则执行，不要手工混入另一套风格。
