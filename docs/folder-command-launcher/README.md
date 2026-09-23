# 資料夾命令提示字元

在指定資料夾中開啟 Windows 命令提示字元（`cmd.exe`）的小工具。

## 功能

- 以視窗選擇要使用的資料夾。
- 用一般使用者權限在該資料夾開啟命令提示字元。
- 用系統管理員權限在該資料夾開啟命令提示字元；Windows 會顯示 UAC 權限確認。
- 已選擇的資料夾不存在時會要求重新選擇。

## 使用方式

1. 執行 `release/FolderCommandLauncher.exe`。
2. 按「瀏覽…」並選取資料夾。
3. 按「一般方式開啟」或「以系統管理員身分開啟」。

## 系統需求

- Windows 10 或 Windows 11。
- .NET Framework 4.x（Windows 10/11 內建）。

## 重新建置

在專案根目錄以 PowerShell 執行：

```powershell
.\build-folder-command-launcher.ps1
```
