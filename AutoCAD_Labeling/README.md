# AutoCAD 梁編號自動標註外掛

## 功能說明

這個 AutoCAD 外掛可以讀取 ETABS 梁編號工具匯出的 JSON 檔案，並自動在 AutoCAD 圖檔中標註梁編號。

## 使用流程

### 1. 準備 JSON 檔案

1. 開啟 ETABS 梁編號工具 ([index.html](../index.html))
2. 載入 E2K 檔案
3. 執行編號
4. 點選「**匯出 JSON (給AutoCAD用)**」按鈕
5. 儲存 JSON 檔案

### 2. 編譯外掛

```bash
cd AutoCAD_Labeling
dotnet build -c Release
```

編譯完成後會生成 `bin\Release\net8.0\BeamLabeler.dll`

### 3. 載入外掛到 AutoCAD

1. 開啟 AutoCAD 2024
2. 在命令列輸入 `NETLOAD`
3. 選擇編譯好的 `BeamLabeler.dll`

### 4. 執行標註

1. 開啟你的 DWG 結構平面圖
2. 在命令列輸入 `LABELBEAMS`
3. 依照提示操作：
   - 選擇 JSON 檔案
   - 選擇要標註的樓層 (例如輸入 1 選擇第一個樓層)
   - **點選該樓層圖框的基準點** (通常是軸線交點，如 0-A)
   - 輸入座標縮放比例 (預設 1000，表示 ETABS 的 1m = AutoCAD 的 1000mm)
   - 輸入文字高度 (預設 250)

## 重要參數說明

### 基準點選擇
- ETABS 的座標原點 (0,0) 對應到 AutoCAD 圖上的哪一點
- 通常是軸線 0-A 的交點
- **關鍵：確保每個樓層圖框都以相同的軸線為基準點**

### 座標縮放比例
- ETABS 使用公尺 (m)
- AutoCAD 通常使用毫米 (mm)
- 預設值 1000 表示 1m = 1000mm
- 如果你的 AutoCAD 單位不同，請調整此值

### 文字高度
- 標註文字的高度
- 預設 250 適合 1:100 比例的圖面
- 根據你的圖面比例調整

## 圖層說明

標註會自動建立兩個圖層：
- `梁編號-大梁`：大梁 (G 開頭的編號)
- `梁編號-小梁`：小梁 (B 開頭的編號)

你可以在 AutoCAD 中調整這些圖層的顏色、線寬等屬性。

## 進階功能 (TODO)

未來可以擴充的功能：
- [ ] 自動識別軸線圓圈並自動設定基準點
- [ ] 支援批次標註多個樓層
- [ ] 支援更新已存在的標註
- [ ] 標註樣式自訂 (顏色、字體等)

## 系統需求

- AutoCAD 2024 (64-bit)
- .NET 8.0 Runtime
- Windows 10/11

## 故障排除

### 找不到 AutoCAD DLL
編輯 `BeamLabeler.csproj`，調整 AutoCAD DLL 的路徑為你的實際安裝路徑。

### 標註位置不正確
1. 檢查基準點是否選擇正確
2. 檢查座標縮放比例是否正確
3. 確認 ETABS 和 AutoCAD 的座標系統一致

### 文字太大或太小
調整文字高度參數，或執行命令後使用 AutoCAD 的 SCALETEXT 命令調整。

## 聯絡資訊

如有問題請回報。
