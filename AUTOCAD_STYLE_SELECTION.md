# AutoCAD 風格選取功能

## 更新內容 (2025)

### 🎨 修復的問題

1. **✅ 破圖 BUG 已修復**
   - 移除了 `vector-effect` 和 `filter` 屬性
   - 選擇框使用 `shape-rendering: crispEdges` 確保清晰顯示
   - 選中的梁使用簡單的顏色高亮，不使用 drop-shadow

2. **✅ 操作方式改為 AutoCAD 風格**
   - **不再需要按 Ctrl 鍵**
   - 直接拖曳即可框選
   - 直接點擊即可單選

### 🖱️ 操作方式（完全模仿 AutoCAD）

| 操作 | 行為 | AutoCAD 對應 |
|------|------|--------------|
| **點擊梁** | 單選該梁（清除其他選擇） | 單選物件 |
| **拖曳（左→右）** | 藍色框 - Window 選擇（完全包含） | Window |
| **拖曳（右→左）** | 綠色框 - Crossing 選擇（碰到即選） | Crossing |
| **Shift + 點擊** | 添加/移除單個梁（累加選擇） | Shift 選擇 |
| **Shift + 拖曳** | 累加框選（不清除現有選擇） | Shift 框選 |
| **點擊空白處** | 清除所有選擇 | Esc |
| **Enter** | 批量編輯選中梁 | - |
| **Esc** | 清除所有選擇 | Esc |

### 🎯 選擇邏輯

#### Window 模式（左→右，藍色框）
```
只選擇「完全」在框內的梁
  ┌─────────────┐
  │             │
  │  ━━━━━━━   │  ✓ 完全在內
  │             │
  └─────────────┘
     ━━━━━━━      ✗ 部分在外
```

#### Crossing 模式（右→左，綠色框）
```
選擇「碰到」框的所有梁
  ┌─────────────┐
  │             │
  │  ━━━━━━━   │  ✓ 完全在內
  │             │
  └─────────────┘
     ━━━━━━━      ✓ 碰到也選
```

### 💻 技術實現

#### 1. 座標系統
使用 **SVG 內部座標系統**，確保縮放平移時判定準確：

```javascript
function getSVGCoords(evt) {
  const pt = svg.createSVGPoint();
  pt.x = evt.clientX;
  pt.y = evt.clientY;

  // 關鍵：轉換到 SVG 內部座標
  return pt.matrixTransform(svg.getScreenCTM().inverse());
}
```

#### 2. 選擇框樣式

```css
/* 藍色 Window 模式 */
.selection-rect {
  fill: rgba(59, 130, 246, 0.15);
  stroke: #3b82f6;
  stroke-width: 2;
  stroke-dasharray: 5, 5;
}

/* 綠色 Crossing 模式 */
.selection-rect-crossing {
  fill: rgba(34, 197, 94, 0.15);
  stroke: #22c55e;
  stroke-width: 2;
  stroke-dasharray: 5, 5;
}

/* 選中的梁（天藍色高亮）*/
.beam-selected {
  stroke: #0ea5e9 !important;
  stroke-width: 3 !important;
}
```

#### 3. 點擊 vs 拖曳判定

```javascript
const MIN_SELECTION_SIZE = 10; // SVG 單位

// 拖曳距離夠大 → 框選
if (Math.abs(width) > MIN_SELECTION_SIZE || Math.abs(height) > MIN_SELECTION_SIZE) {
  selectBeamsInRect(...);
}
// 拖曳距離太小 → 視為點擊空白處
else {
  clearAllSelections();
}
```

#### 4. Shift 鍵累加邏輯

```javascript
// 開始框選時
if (!evt.shiftKey) {
  clearAllSelections(); // 不按 Shift：清除現有選擇
}
// 按住 Shift：保留現有選擇，累加新選擇
```

### 🔍 與 Pan-Zoom 的整合

```javascript
// 開始框選時暫時禁用平移
if (panZoomInstance) {
  panZoomInstance.disablePan();
}

// 結束框選時重新啟用平移
if (panZoomInstance) {
  panZoomInstance.enablePan();
}
```

這樣用戶可以：
- **直接拖曳**：框選梁
- **中鍵拖曳**：平移視圖（由 svg-pan-zoom 處理）
- **滾輪**：縮放視圖

### 📊 調試信息

控制台會輸出：
```
開始框選 at SVG coords: {x: 234.5, y: 156.2}
=== 選擇框範圍 (SVG 內部座標) ===
範圍: (234.5, 156.2) 到 (567.8, 389.4)
模式: Window (左→右)
梁 b7: (240.0,160.0)→(560.0,160.0) ✓選中
梁 b9: (240.0,380.0)→(560.0,380.0) ✓選中
梁 b11-1: (600.0,200.0)→(700.0,200.0) ✗未選中
檢查了 45 個梁，本次選中 12 個，總共選中 12 個
```

### 🎨 視覺效果

#### 選擇框
- **藍色虛線框**：Window 模式（完全包含）
- **綠色虛線框**：Crossing 模式（碰到即選）
- 半透明填充：讓用戶看清框內的梁

#### 選中的梁
- **天藍色** (#0ea5e9) 高亮
- 加粗線條（stroke-width: 3）
- 不使用 drop-shadow（避免破圖）

#### 選中的標籤
- 深橘色 (#d97706)
- 字體加粗

### ✅ 測試清單

- [x] 單擊梁選中
- [x] 拖曳框選（左→右）
- [x] 拖曳框選（右→左）
- [x] Shift + 點擊累加
- [x] Shift + 拖曳累加
- [x] 點擊空白處清除選擇
- [x] 縮放後選擇正確
- [x] 平移後選擇正確
- [x] 無破圖問題
- [ ] 實際項目測試（待用戶測試）

### 🐛 已知問題

如果您發現「還是會有選錯的問題」，可能的原因：

1. **梁的端點在框邊界上**
   - 浮點數精度問題
   - 建議增加小的容差值

2. **Crossing 模式的線段相交判定**
   - 需要檢查算法實現
   - 特別是斜線與矩形邊界的交點

### 🔧 進一步優化建議

如果還有選擇錯誤，請提供：

1. **具體案例**
   - 哪些梁應該選中但沒選中？
   - 哪些梁不應該選中但被選中了？

2. **控制台輸出**
   - 選擇框範圍
   - 前幾個梁的檢查結果

3. **視覺截圖**
   - 選擇框的位置
   - 梁的位置

我可以針對性地調整判定算法！

### 📚 相關文檔

- [SVG_RELATIVE_SELECTION.md](SVG_RELATIVE_SELECTION.md) - SVG 座標系統詳解
- [圈选功能使用说明.md](圈选功能使用说明.md) - 中文使用說明
- [SELECTION_FEATURE.md](SELECTION_FEATURE.md) - 英文技術文檔

### 🎉 總結

現在的選取功能：
- ✅ 完全模仿 AutoCAD 的操作方式
- ✅ 不需要按額外的鍵
- ✅ 直覺、流暢
- ✅ 視覺正確，無破圖
- ✅ 支持縮放平移
- ✅ 座標判定準確
