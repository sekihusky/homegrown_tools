# SSL 憑證到期檢測

## 功能

- 離線讀取 CRT、CER、PEM、PFX、P12 與 P7B 憑證，不上傳檔案。
- PEM 憑證鏈逐張列出，顯示到期日期、剩餘完整天數及日期狀態。
- 區分有效、30 天內到期、已過期、尚未生效，每 30 秒更新狀態。
- 顯示主體、簽發機構、生效日、SAN 適用名稱、序號與識別指紋。
- 日期固定使用台灣時間 UTC+08:00；判斷依據為電腦目前時間。
- PFX / P12 可輸入密碼，密碼不儲存，私鑰不寫入 Windows 憑證庫。

## 使用方式

1. 開啟 `release/SslCertificateChecker.exe`。
2. 按「選取憑證檔案…」，選擇本機憑證（最多 16 MB）。
3. 加密的 PFX / P12 輸入密碼後讀取。
4. 查看清單到期日，點選個別憑證查看詳細資料。

私鑰 `.key` 本身沒有到期日。此工具只讀取所選檔案與檢查日期，不驗證信任鏈、撤銷狀態或網站目前部署的憑證。PEM 中的私鑰區段會忽略；PFX 格式的演算法相容性取決於 Windows 版本。

## 系統需求

Windows 10 / 11，.NET Framework 4.8。單檔免安裝，不需管理員權限或網路連線。

## 重新建置

```powershell
powershell -ExecutionPolicy Bypass -File .\build-ssl-certificate-checker.ps1
```

圖示來源位於 `assets/icons/ssl-certificate-checker.ico`，可用 `build-ssl-certificate-checker-icon.ps1` 重新產生。
