# Hosts 檔案編輯器

雙擊 `release\HostsFileEditor.exe` 即可使用。程式會要求 Windows 系統管理員權限，因為 hosts 是受保護的系統檔案。

功能：搜尋、新增、編輯、刪除、啟用／停用 hosts 規則。每次儲存前，程式會在 `C:\Windows\System32\drivers\etc` 建立帶時間戳的備份檔，並在儲存後清除 DNS 快取。

這是單一 EXE，可直接複製到其他 Windows 10/11 電腦使用，不需要安裝。程式使用 Windows 內建的 .NET Framework 4.x。

重新建置：

```powershell
powershell -ExecutionPolicy Bypass -File .\build-hosts-editor.ps1
```
