# 圈選功能調試指南

## 問題描述

框選的判定範圍不正確 - 有時框選範圍內的梁不會被選到，甚至選到範圍外的物件。

## 已實施的修復

### 1. SVG 座標轉換修復

**修改前：**
```javascript
function getSVGPoint(evt) {
  const svg = document.getElementById("drawing-svg");
  const CTM = svg.getScreenCTM();
  return {
    x: (evt.clientX - CTM.e) / CTM.a,
    y: (evt.clientY - CTM.f) / CTM.d
  };
}
```

**修改後：**
```javascript
function getSVGPoint(evt) {
  const svg = document.getElementById("drawing-svg");
  const pt = svg.createSVGPoint();
  pt.x = evt.clientX;
  pt.y = evt.clientY;

  // 使用 inverse transformation 獲取正確的 SVG 坐標
  const svgP = pt.matrixTransform(svg.getScreenCTM().inverse());
  return {
    x: svgP.x,
    y: svgP.y
  };
}
```

**原因：** 當使用 svg-pan-zoom 時，SVG 元素會有複雜的變換矩陣，必須使用 `inverse()` 來正確轉換屏幕座標到 SVG 座標系統。

### 2. 添加詳細的調試日誌

現在在瀏覽器的開發者控制台中可以看到：

1. **選擇框座標**
   - 矩形框的 x, y, width, height 屬性
   - 計算的判定範圍 (minX, minY, maxX, maxY)

2. **選擇模式**
   - Window (左→右) 或 Crossing (右→左)

3. **每個梁的檢查結果**
   - 前 5 個梁的座標和是否被選中

4. **選擇統計**
   - 本次選中數量和總選中數量

## 如何調試

### 步驟 1: 打開開發者工具

1. 在瀏覽器中打開 [index.html](index.html)
2. 按 `F12` 打開開發者工具
3. 切換到 "Console" (控制台) 標籤

### 步驟 2: 測試圈選功能

1. 上傳 E2K 文件並執行編號
2. 按住 `Ctrl` 並拖曳鼠標創建選擇框
3. 釋放鼠標後，查看控制台輸出

### 步驟 3: 分析控制台輸出

**預期看到的輸出範例：**
```
=== 選擇框座標 ===
矩形框屬性: {x: "150.5", y: "200.3", width: "300.2", height: "150.7"}
計算的判定範圍: {minX: 150.5, minY: 200.3, maxX: 450.7, maxY: 351.0}
選擇框範圍: (150.50, 200.30) 到 (450.70, 351.00)
選擇模式: Window (左→右)
梁 B1: (160.0,210.0)→(440.0,210.0) ✓選中
梁 B2: (160.0,340.0)→(440.0,340.0) ✓選中
梁 B3: (500.0,250.0)→(700.0,250.0) ✗未選中
梁 B4: (100.0,150.0)→(200.0,150.0) ✗未選中
梁 B5: (300.0,220.0)→(300.0,330.0) ✓選中
本次選擇了 3 個梁，總共選中 3 個
```

### 步驟 4: 檢查問題

**如果選擇框座標和判定範圍不一致：**
- 這表示座標轉換有問題
- 檢查 `getSVGPoint()` 函數

**如果梁的座標在範圍內但未被選中：**
- 檢查選擇邏輯 (Window vs Crossing 模式)
- 查看 `selectBeamsInRect()` 函數

**如果範圍外的梁被選中：**
- 可能是 Crossing 模式的線段相交算法問題
- 查看 `lineIntersectsRect()` 函數

## 使用簡化測試工具

我們創建了一個獨立的測試頁面：[selection_debug.html](selection_debug.html)

**優點：**
- 簡化的環境，沒有複雜的 ETABS 數據
- 內建的視覺化日誌
- 固定的測試用梁，容易驗證

**使用方法：**
1. 在瀏覽器中打開 `selection_debug.html`
2. 按住 Ctrl 並拖曳來測試圈選
3. 查看頁面底部的實時日誌
4. 使用按鈕來清除選擇和日誌

## 常見問題排查

### 問題 1: 選擇框顯示位置不對

**症狀：** 拖曳時看到的藍/綠色框和最終的判定範圍不一致

**可能原因：**
- SVG 座標轉換不正確
- svg-pan-zoom 的變換矩陣沒有正確應用

**解決方案：**
- 確認使用 `createSVGPoint()` 和 `matrixTransform()`
- 檢查 `getScreenCTM().inverse()` 是否正確調用

### 問題 2: Window 模式選不到梁

**症狀：** 左→右拖曳（藍色框）時，明顯在框內的梁沒被選中

**可能原因：**
- 判定條件太嚴格
- 浮點數精度問題

**解決方案：**
```javascript
// 添加小的容差值
const TOLERANCE = 1; // 1個像素的容差
isInside = (x1 >= minX - TOLERANCE && x1 <= maxX + TOLERANCE &&
           y1 >= minY - TOLERANCE && y1 <= maxY + TOLERANCE &&
           x2 >= minX - TOLERANCE && x2 <= maxX + TOLERANCE &&
           y2 >= minY - TOLERANCE && y2 <= maxY + TOLERANCE);
```

### 問題 3: Crossing 模式選到不該選的梁

**症狀：** 右→左拖曳（綠色框）時，沒碰到框的梁也被選中

**可能原因：**
- 線段相交算法實現錯誤
- 邊界條件處理不當

**解決方案：**
- 檢查 `lineIntersectsLine()` 的數學邏輯
- 確認端點檢查是否正確

## 測試清單

使用以下清單來驗證功能是否正常：

- [ ] **Window 模式 (左→右)**
  - [ ] 完全在框內的水平梁被選中
  - [ ] 完全在框內的垂直梁被選中
  - [ ] 部分在框內的梁不被選中
  - [ ] 完全在框外的梁不被選中

- [ ] **Crossing 模式 (右→左)**
  - [ ] 完全在框內的梁被選中
  - [ ] 部分在框內的梁被選中
  - [ ] 與框邊界相交的梁被選中
  - [ ] 完全在框外的梁不被選中

- [ ] **視覺一致性**
  - [ ] 看到的藍/綠色框和判定範圍一致
  - [ ] 選中的梁正確高亮顯示
  - [ ] 控制台日誌的座標與視覺匹配

## 下一步

如果問題仍然存在，請提供：

1. **控制台截圖**：顯示完整的調試輸出
2. **視覺截圖**：顯示選擇框和梁的位置
3. **具體案例**：
   - 哪些梁應該被選中但沒有？
   - 哪些梁不應該被選中但被選中了？
   - 選擇框的大致位置和方向

這些信息將幫助我們精確定位問題所在。

## 臨時解決方案

如果需要立即使用，可以暫時禁用 pan-zoom 功能來簡化座標系統：

```javascript
// 在初始化時註釋掉 pan-zoom
// panZoomInstance = svgPanZoom("#drawing-svg", { ... });
```

這樣圈選功能會更穩定，但失去平移和縮放能力。
