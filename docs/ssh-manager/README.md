# SSH 站台管理器

Windows 本機使用的簡易 SSH 站台管理與終端工具，可保存常用伺服器，使用密碼或私鑰連線，並同時維持多個 SSH 終端。

## 功能

- 新增、編輯、刪除與搜尋 SSH 站台。
- 使用密碼或私鑰檔驗證。
- 多個連線分頁；每台主機的連線互不影響。
- 開啟前兩個連線的左右並排檢視，適合對照兩台主機的輸出。
- 基本互動終端：輸入指令、顯示輸出、方向鍵、複製貼上與捲動。
- 站台密碼與私鑰密碼使用 Windows DPAPI 加密，僅目前 Windows 使用者可以解密。
- 首次連線會記錄主機指紋；之後若伺服器主機金鑰變更，程式會拒絕連線，避免未預期的主機身分變更。

## 使用方式

1. 開啟 `release\SshManager.exe`。
2. 按「新增站台」，輸入站台名稱、IP／網域、Port 與使用者名稱。
3. 選擇「密碼」或「私鑰檔」，填入必要資料並儲存。
4. 在左側清單雙擊站台以建立連線。
5. 要對照兩台主機時，先開啟至少兩個連線，再按「並排檢視」。

站台資料保存於：`%LocalAppData%\HomegrownTools\SshManager\sites.json`。

## 系統需求

- Windows 10 或 Windows 11
- 已安裝 .NET Framework 4.x
- 不需要安裝額外 SSH 客戶端或執行安裝程式

SSH 函式庫已內嵌在 `SshManager.exe`，`release` 目錄只需要保留這一個 EXE。

## 已知限制

- 已有 OpenSSH RSA 私鑰回報 `openssh key type: ssh-rsa is not supported`，此相容性問題尚待修正。

- 此版本提供基本 ANSI 文字處理，未涵蓋完整 VT/xterm 終端模擬；部分全螢幕互動工具可能顯示不完整。
- 並排檢視固定帶出目前已開啟的前兩個連線。
- 不支援 SFTP、跳板機、Port forwarding 或多台廣播輸入。

## 重新建置

在專案根目錄的 PowerShell 執行：

```powershell
.\build-ssh-manager.ps1
```

建置後的單一執行檔位於 `release\SshManager.exe`。
