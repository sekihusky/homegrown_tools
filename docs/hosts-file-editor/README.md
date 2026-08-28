# Hosts 檔案編輯器

[返回工具總覽](../../README.md)

## 功能

- 搜尋、新增、編輯及刪除 hosts 規則
- 啟用或停用個別規則
- 儲存前在 `C:\Windows\System32\drivers\etc` 建立帶時間戳的備份檔
- 儲存後自動清除 DNS 快取

## 使用方式

執行 [`release/HostsFileEditor.exe`](../../release/HostsFileEditor.exe)。程式會要求 Windows 系統管理員權限，因為 hosts 是受保護的系統檔案。

這是單一 EXE，可直接複製到其他 Windows 電腦使用，不需要安裝。

## 系統需求

- Windows 10 或 Windows 11
- Windows 內建的 .NET Framework 4.x
- Windows 系統管理員權限

## 重新建置

在專案根目錄開啟 PowerShell，執行：

```powershell
powershell -ExecutionPolicy Bypass -File .\build-hosts-editor.ps1
```

成品會產生在 `release/HostsFileEditor.exe`。
