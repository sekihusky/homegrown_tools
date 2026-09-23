# Homegrown Tools

這裡收錄自行開發的 Windows 小工具。每個工具都可獨立使用，詳細功能、使用方式與建置指令請見各自的 README。

## 工具列表

| 工具 | 用途 | 執行檔 | 文件 |
| --- | --- | --- | --- |
| Port 連線測試工具 | 不依賴 Telnet，測試 IP／網域的 TCP Port 是否可連線 | [`PortChecker.exe`](release/PortChecker.exe) | [使用說明](docs/port-checker/README.md) |
| 網域 IP / DNS 查詢工具 | 查詢網域註冊日期、DNS，以及 IP 的 ISP／ASN／推估地區 | [`DomainDnsTool.exe`](release/DomainDnsTool.exe) | [使用說明](docs/domain-dns-tool/README.md) |
| Hosts 檔案編輯器 | 搜尋、編輯、停用及備份 Windows hosts 規則 | [`HostsFileEditor.exe`](release/HostsFileEditor.exe) | [使用說明](docs/hosts-file-editor/README.md) |
| 區域網路裝置掃描器 | 搜尋同一 C Class 網段內使用中的 IP、名稱、類型與 MAC | [`LanDeviceScanner.exe`](release/LanDeviceScanner.exe) | [使用說明](docs/lan-device-scanner/README.md) |
| SSH 站台管理器 | 管理 SSH 站台，以密碼或私鑰連線並支援多台終端並排檢視 | [`SshManager.exe`](release/SshManager.exe) | [使用說明](docs/ssh-manager/README.md) |
| 資料夾命令提示字元 | 選擇資料夾後，以一般或系統管理員權限開啟命令提示字元 | [`FolderCommandLauncher.exe`](release/FolderCommandLauncher.exe) | [使用說明](docs/folder-command-launcher/README.md) |

## 使用需求

- Windows 10 或 Windows 11
- 個別工具所需的執行環境請見其使用說明
- 工具皆以免安裝使用為原則

個別工具可能需要網路連線或系統管理員權限，請參閱各自的使用說明。

## 技術與部署原則

- 新增工具不綁定 C# 或 .NET Framework；依功能需求選擇合適技術即可。
- Windows 工具以免安裝為優先：使用者應能直接從 `release/` 執行，不要求另行安裝 SSH 客戶端或其他應用程式。
- 可假設目標主機已有 .NET Framework 4.x；若採用其他執行環境，必須隨成品提供可攜式 runtime，或確認為 Windows 10／11 內建元件。
- 成品不得要求使用者額外執行安裝程序；必要相依檔須與執行檔一同發行。

## 發行目錄規則

- `release/` 固定存放已建置、打包完成且可直接使用的成品；原始碼、建置暫存與開發用相依套件放在其他目錄。
- 可獨立執行的單檔 EXE 直接放在 `release/`，例如 `release/SshManager.exe`。
- 每個完成的 EXE 都必須內嵌專屬的 Windows 圖示（`.ico`），並在其建置腳本中以編譯器的圖示選項指定；圖示來源檔應保留在 `assets/icons/`。
- 若 EXE 需要附帶 DLL、設定範本、資源或可攜式 runtime 等相關檔案，必須在 `release/<工具名稱>/` 建立專屬資料夾，將 EXE 與全部必要檔案一起放入，保留執行所需的相對路徑。
- 多檔工具應能複製整個專屬資料夾後使用，不得依賴其他工具的發行資料夾或專案開發目錄。
- 建置腳本、工具列表與使用說明中的執行檔路徑須遵循上述規則並保持一致。

## 新增工具的文件規則

為了讓工具數量增加後仍容易瀏覽，新增工具時請遵循以下規則：

1. 使用小寫英文與連字號建立 `docs/<工具代號>/README.md`，例如 `docs/port-checker/README.md`。
2. README 至少包含「功能」、「使用方式」、「系統需求」及「重新建置」。
3. 成品依「發行目錄規則」放在 `release/` 或其工具專屬資料夾，檔名應能直接辨識工具。
4. 在本頁的「工具列表」新增一列，連結執行檔與個別文件。
5. 原始碼及建置腳本使用一致的工具名稱，避免新增後難以配對。
