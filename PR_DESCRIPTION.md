# Grid Bubble Toggle, Drag, and BUBBLELOC Support

## 📋 Summary

This PR implements comprehensive grid bubble functionality including toggle controls, drag interactions, and BUBBLELOC parsing from E2K files.

## ✨ Features

### 1. 🎯 Grid Bubble Toggle Control
- Added control panel to show/hide grid bubbles by coordinate system (GLOBAL, O2, A2, A3)
- Color-coded toggle switches for each grid system
- Independent visibility control for bubbles, text, and connectors

### 2. 🖱️ Interactive Drag Functionality
- Drag grid bubbles freely with mouse
- Rubber-band animation effect during drag
- Smooth snap-back to original position on release
- Prevents text selection during drag interaction

### 3. 📊 BUBBLELOC Parsing from E2K
- Parses BUBBLELOC attribute from E2K $ GRIDS format
- Correctly positions bubbles based on ETABS settings:
  - **DEFAULT**: left side (Y-axis) / top side (X-axis)
  - **SWITCHED**: right side (Y-axis) / bottom side (X-axis)
  - **BOTH**: display bubbles on both sides
- Handles missing BUBBLELOC (defaults to DEFAULT)

### 4. 🚀 Performance Optimizations
- Zoom/pan state preserved when changing floors
- requestAnimationFrame for smooth animations
- DOM query result caching
- No unnecessary view resets

## 🔧 Technical Changes

### UI Components
- Added `grid-bubble-control-panel` with toggle switches
- Added "🎯 Grid 控制" button in toolbar
- CSS styles for draggable bubbles and rubber-band animation

### Parsing Logic
- Modified `parseGrids()` to extract BUBBLELOC from $ GRIDS format
- Added `bubbleLoc` property to grid info objects
- Regex pattern: `/BUBBLELOC\s+"([^"]+)"/i`

### Rendering Logic
- Updated grid bubble rendering in `displayResults()`
- Changed conditional logic from "Start"/"End"/"Both" to DEFAULT/SWITCHED/BOTH
- Added `data-coordsystem` attributes to all grid elements

### Interaction Handlers
- `toggleGridBubbleControlPanel()` - Show/hide control panel
- `initializeGridBubbleControls()` - Create toggle switches
- `toggleGridSystem()` - Toggle system visibility
- `handleBubbleMouseDown/Move/Up()` - Drag interaction
- `getSVGPoint()` - SVG coordinate transformation

## 📸 User Experience

**Before dragging:**
- Hover over bubble → cursor changes to move
- Bubble slightly enlarges on hover

**During dragging:**
- Bubble follows mouse cursor
- Connector line stretches like rubber band
- Dashed line animation shows active drag
- No text selection interference

**After releasing:**
- Bubble smoothly returns to original position (0.3s ease-out)
- Text and connector synchronize
- Clean visual feedback

## 🧪 Testing

Tested with:
- E2K file with multiple COORDSYSTEMS (GLOBAL, O2, A2, A3)
- BUBBLELOC variations (DEFAULT, SWITCHED, missing)
- Floor switching with zoom/pan state preservation
- Grid bubble drag interactions

## 📚 Documentation

All changes follow existing code patterns and conventions.

## ⚠️ Breaking Changes

None. All changes are additive and backward compatible.

---

**Commits:**
1. `0884126` - feat: add grid bubble toggle and drag functionality
2. `562c156` - fix: improve grid bubble drag behavior
3. `f19ec5d` - feat: parse and apply BUBBLELOC from E2K files
4. `609a57c` - fix: improve grid bubble drag behavior
5. `56d00e4` - fix: correct grid line direction and expand clickable area
6. `29ff54a` - fix: reverse rotation direction and simplify text click handling
7. `c4c52b6` - fix: prevent duplicate event listener binding in bubble dragging
8. `bd7565d` - docs: update PR description with event listener fix
9. `e62d82d` - fix: 修復 BUBBLE 拖曳功能，確保沿 grid line 方向移動並帶阻尼回彈
10. `fb49b69` - docs: update PR description with complete drag fix details
11. `e257662` - fix: 修正 BUBBLE 拖曳方向，沿著 connector 方向（垂直於 grid line）移動
12. `9bef274` - docs: update PR description with direction fix explanation
13. `8eb8cf9` - fix: 優化 BUBBLE 拖曳體驗 - 修復斜向跳動並統一速度感受
14. `4330cf3` - docs: update PR description with drag optimization details
15. `705ccdf` - fix: 擴大 BUBBLE 點擊範圍並支持雙向拖曳
16. `7af8355` - docs: update PR description with hitarea fix details
17. `1aae41b` - fix: 修復 BUBBLE 文字點擊無法觸發拖曳的問題

**Branch:** `claude/draggable-bubble-damping-01XHvrwE4G7QSmJRF19Kognb`

## 🆕 Latest Update (1aae41b) - 修復文字點擊 ✅ 最終完善

**解決的問題**：
- ❌ **點擊 BUBBLE 內的文字無法拖曳** → ✅ 文字完全可點擊

**修復詳情**：

### 1. 添加文字 CSS 樣式 🎨
```css
.grid-bubble-text {
  pointer-events: all;    /* 讓文字接收點擊事件 */
  cursor: move;           /* 提示可拖曳 */
  user-select: none;      /* 防止拖曳時選中文字 */
}
```

### 2. 修復 handleTextMouseDown 函數 🔧
由於實際的 bubble 已設置 `pointer-events: none`，文字點擊需要找到對應的 hitarea：

```javascript
// 先找 hitarea（現在的點擊接收者）
let hitareas = svg.querySelectorAll(`.grid-bubble-hitarea[...]`);
hitareas.forEach(hitarea => {
  const cx = parseFloat(hitarea.getAttribute("cx"));
  const cy = parseFloat(hitarea.getAttribute("cy"));
  if (Math.abs(cx - textX) < 5 && Math.abs(cy - textY) < 5) {
    matchingElement = hitarea;  // 找到對應的 hitarea
  }
});

// 向後兼容：如果找不到 hitarea，嘗試找 bubble
if (!matchingElement) {
  // ... 查找 bubble 邏輯
}
```

**測試確認**：
- ✅ 點擊 BUBBLE 圓圈 → 可以拖曳
- ✅ 點擊 BUBBLE 內文字 → 可以拖曳
- ✅ 點擊 BUBBLE 周圍區域（hitarea）→ 可以拖曳

---

## 📝 Previous Update (705ccdf) - 完美點擊體驗

**解決的核心問題**：
1. ❌ **點擊判定太嚴格** → ✅ 整個圓圈都可點擊
2. ❌ **斜向 BUBBLE 無法往外拉** → ✅ 支持雙向拖曳

**修復詳情**：

### 1. 擴大點擊判定範圍 🎯
為每個 BUBBLE 添加不可見的 hitarea 圓圈：

```javascript
// 創建透明的 hitarea（半徑 +10，加上 20px 描邊）
const hitArea = document.createElementNS("http://www.w3.org/2000/svg", "circle");
hitArea.setAttribute("r", INITIAL_GRID_BUBBLE_RADIUS + 10);
hitArea.setAttribute("class", "grid-bubble-hitarea draggable");
hitArea.setAttribute("fill", "transparent");
hitArea.setAttribute("stroke-width", "20");  // 進一步擴大點擊範圍

// 實際的 bubble 不接收點擊事件
bubble.setAttribute("pointer-events", "none");
```

**效果**：從 BUBBLE 中心到最外圍邊緣的整個區域都可以點擊拖曳，不會再出現"點到了卻沒辦法拉動"的問題。

### 2. 支持雙向拖曳 ↔️
- BUBBLE 可以沿著 connector 方向**雙向移動**
- 既可以靠近 grid line（往內），也可以遠離 grid line（往外）
- 投影計算支持正負值，範圍 ±100 單位
- 斜向 BUBBLE 現在完全可以正常往外拉

### 3. 視覺效果
- hitarea 完全透明，不影響視覺
- hover 時 cursor 變為 move，提示可拖曳
- 所有 BUBBLE（top, bottom, left, right）統一處理

**測試確認**：
- ✅ 水平 BUBBLE - 點擊邊緣也能拖曳
- ✅ 垂直 BUBBLE - 點擊邊緣也能拖曳
- ✅ 斜向 BUBBLE - **可以往外拉** + 點擊靈敏

---

## 📝 Previous Update (8eb8cf9) - 完美拖曳體驗

**解決的問題**：
1. ❌ **斜向 BUBBLE 會跳動** → ✅ 平滑跟隨鼠標
2. ❌ **不同 BUBBLE 速度不一致** → ✅ 統一移動感受
3. ❌ **回彈動畫過慢** → ✅ 快速流暢回彈

**修復詳情**：

### 1. 修復斜向 BUBBLE 跳動問題
```javascript
// ❌ 之前（錯誤）：相對於 BUBBLE 原始位置計算
const mouseDx = pt.x - dragState.originalBubblePos.x;
const mouseDy = pt.y - dragState.originalBubblePos.y;
// 導致點擊時如果鼠標不在 BUBBLE 中心，會立即跳到投影位置

// ✅ 現在（正確）：相對於初始點擊位置計算
const mouseDx = pt.x - dragState.startMousePos.x;
const mouseDy = pt.y - dragState.startMousePos.y;
// BUBBLE 平滑跟隨鼠標移動，不會跳動
```

### 2. 統一拖曳速度感受
```javascript
// ❌ 之前：基於 bubble 半徑（不同 BUBBLE 不同範圍）
dragState.maxDragDistance = 5 * bubbleRadius;

// ✅ 現在：固定距離（所有 BUBBLE 一致）
dragState.maxDragDistance = 100;  // 統一 100 單位
```

### 3. 優化回彈動畫參數
| 參數 | 之前 | 現在 | 效果 |
|------|------|------|------|
| 彈簧剛度 (stiffness) | 0.15 | 0.25 | 回彈更快 ⚡ |
| 阻尼係數 (damping) | 0.70 | 0.75 | 減少震盪 🎯 |
| 停止閾值 (minDistance) | 0.1 | 0.5 | 更快停止 ✅ |

**視覺效果對比**：
- 水平 BUBBLE（X 軸）：⬅️➡️ 流暢拖曳 + 快速回彈
- 垂直 BUBBLE（Y 軸）：⬆️⬇️ 流暢拖曳 + 快速回彈
- 斜向 BUBBLE：↗️↘️ **不再跳動** + 一致速度感

---

## 📝 Previous Update (e257662) - 修正拖曳方向邏輯

**問題說明**：
之前的實現錯誤地將 connector 方向旋轉了 90 度，導致 BUBBLE 沿著 grid line 本身移動，而不是垂直於 grid line 的方向移動。

**修正內容**：
- ❌ **之前（錯誤）**：BUBBLE 沿著 grid line 切線方向移動（旋轉 90 度後）
  - Y 軸 BUBBLE（如 Y16-1）會水平移動 ⬅️➡️
  - X 軸 BUBBLE 會垂直移動 ⬆️⬇️

- ✅ **現在（正確）**：BUBBLE 沿著 connector 方向移動（垂直於 grid line）
  - Y 軸 BUBBLE（如 Y16-1）會垂直移動 ⬆️⬇️
  - X 軸 BUBBLE 會水平移動 ⬅️➡️

**技術細節**：
```javascript
// 之前的錯誤邏輯（旋轉 90 度）
dragState.gridLineDirection = {
  x: connectorUnitY,   // 順時針旋轉 90 度
  y: -connectorUnitX
};

// 現在的正確邏輯（直接使用 connector 方向）
dragState.gridLineDirection = {
  x: connectorUnitX,   // 沿著 connector 方向
  y: connectorUnitY
};
```

---

## 📝 Previous Update (e62d82d) - 完整修復拖曳功能

**主要修復問題**：
1. **事件綁定位置錯誤** - 將 `mousemove`/`mouseup` 從 SVG 移到 `document`
   - 修復：鼠標移出 SVG 範圍時拖曳會中斷的問題
   - 確保在整個頁面範圍內都能順暢拖曳

2. **元素匹配條件過於嚴格** - 從 1px 放寬到 10px
   - 修復：無法找到對應 connector 導致拖曳完全失效
   - 使用最近距離匹配，提高容錯性

3. **變量作用域問題** - `connectorUnitX/Y` 移到外層
   - 修復：console.log 中引用未定義變量導致 JavaScript 錯誤
   - 確保程式碼正確執行

4. **詳細調試日誌** - 添加 `[DEBUG]`, `[WARN]`, `[SUCCESS]` 標籤
   - 幫助快速診斷問題
   - 可以透過瀏覽器控制台追蹤拖曳流程

**功能特性（已完整實現）**：
✅ **沿 Grid Line 方向拖曳** - 使用向量投影確保移動軌跡正確
✅ **限制拖曳範圍** - ±5 個 bubble 半徑，防止拖曳過遠
✅ **跟隨鼠標移動** - 實時更新 bubble、text 和 connector 位置
✅ **橡皮筋視覺效果** - connector 拉伸動畫，虛線閃爍
✅ **阻尼回彈動畫** - 彈簧物理模擬（stiffness=0.15, damping=0.7）
✅ **平滑 60fps 動畫** - 使用 `requestAnimationFrame` 實現流暢回彈

**測試建議**：
1. 打開瀏覽器開發者工具的 Console 標籤
2. 上傳 E2K 文件並執行編號
3. 顯示 Grid Bubble（點擊 "🎯 Grid 控制"）
4. 點擊任一 BUBBLE，觀察 Console 輸出 `[SUCCESS] Started dragging...`
5. 拖動 BUBBLE，應該能沿著 grid line 方向順暢移動
6. 鬆開鼠標，觀察 BUBBLE 平滑回彈到原位（帶阻尼效果）
