# 圈選功能最終方案 - SVG 相對座標系統

## 問題根源

您的觀察完全正確！之前的方案有根本性問題：

❌ **屏幕絕對座標方案的問題**：
- 用戶會縮放和平移 SVG
- 使用屏幕座標會導致：框選的位置和判定範圍隨著縮放而錯位
- 不符合用戶直覺：看到的框 ≠ 實際判定範圍

## 正確解決方案

✅ **SVG 內部相對座標系統**：

```
用戶看到的 = 實際判定的

┌──────────────────────────────┐
│   SVG (可縮放、平移)         │
│                              │
│   選擇框 (SVG rect 元素)     │ ← 使用 SVG 內部座標
│   梁線 (SVG line 元素)       │ ← 使用 SVG 內部座標
│                              │
│   兩者在同一座標系統！        │
└──────────────────────────────┘
```

### 核心概念

1. **選擇框也是 SVG 元素**
   - 創建 SVG `<rect>` 元素作為選擇框
   - 使用 SVG 內部座標定位
   - 隨著 SVG 縮放而縮放

2. **座標系統統一**
   - 鼠標座標 → 轉換到 SVG 內部座標
   - 選擇框座標 → SVG 內部座標
   - 梁的座標 → SVG 內部座標
   - **全部在同一座標系統中比較**

3. **自動適應縮放**
   - 用戶縮放 SVG 時，選擇框也會跟著縮放
   - 判定邏輯不受縮放影響
   - 完全符合直覺

## 技術實現

### 1. 座標轉換

```javascript
function getSVGCoords(evt) {
  const svg = svgElement;
  const pt = svg.createSVGPoint();
  pt.x = evt.clientX;
  pt.y = evt.clientY;

  // 關鍵：使用 inverse transformation
  const svgPt = pt.matrixTransform(svg.getScreenCTM().inverse());
  return {
    x: svgPt.x,
    y: svgPt.y
  };
}
```

**解釋**：
- `getScreenCTM()` 獲取 SVG 當前的變換矩陣（包含縮放、平移）
- `.inverse()` 計算逆矩陣
- `matrixTransform()` 將屏幕座標轉換到 SVG 內部座標

### 2. 創建選擇框

```javascript
// 在 SVG 內創建矩形
selectionRect = document.createElementNS("http://www.w3.org/2000/svg", "rect");
selectionRect.setAttribute("x", selectionStart.x);
selectionRect.setAttribute("y", selectionStart.y);
selectionRect.setAttribute("class", "selection-rect");
svgElement.appendChild(selectionRect);
```

### 3. 更新選擇框

```javascript
const currentPoint = getSVGCoords(evt);
const width = currentPoint.x - selectionStart.x;
const height = currentPoint.y - selectionStart.y;

// 直接使用 SVG 座標更新
selectionRect.setAttribute("x", Math.min(selectionStart.x, currentPoint.x));
selectionRect.setAttribute("y", Math.min(selectionStart.y, currentPoint.y));
selectionRect.setAttribute("width", Math.abs(width));
selectionRect.setAttribute("height", Math.abs(height));
```

### 4. 判定邏輯

```javascript
// 梁的座標直接從 SVG 屬性讀取
const x1 = parseFloat(line.getAttribute('x1'));
const y1 = parseFloat(line.getAttribute('y1'));
const x2 = parseFloat(line.getAttribute('x2'));
const y2 = parseFloat(line.getAttribute('y2'));

// 選擇框的範圍（SVG 座標）
const minX = Math.min(selectionStart.x, endPoint.x);
const maxX = Math.max(selectionStart.x, endPoint.x);
const minY = Math.min(selectionStart.y, endPoint.y);
const maxY = Math.max(selectionStart.y, endPoint.y);

// Window 模式：完全包含
isInside = (x1 >= minX && x1 <= maxX && y1 >= minY && y1 <= maxY &&
           x2 >= minX && x2 <= maxX && y2 >= minY && y2 <= maxY);

// Crossing 模式：線段相交
isInside = lineIntersectsRect(x1, y1, x2, y2, minX, minY, maxX, maxY);
```

## 優勢分析

### 1. ✅ 相對性
- **無論怎麼縮放平移，判定邏輯都正確**
- 選擇框和梁在同一座標系統中

### 2. ✅ 直覺性
- 用戶看到的藍/綠色框 = 實際判定範圍
- 縮放時框和梁一起縮放

### 3. ✅ 簡潔性
- 不需要複雜的座標轉換
- 直接讀取 SVG 屬性即可

### 4. ✅ 性能
- 只在鼠標移動時轉換座標
- 判定時直接比較數值

## 與之前方案的對比

| 特性 | 屏幕座標方案 ❌ | SVG 座標方案 ✅ |
|------|----------------|-----------------|
| 縮放適應 | 錯位 | 完美適應 |
| 平移適應 | 錯位 | 完美適應 |
| 視覺一致性 | 不一致 | 100% 一致 |
| 代碼複雜度 | 高 | 低 |
| 用戶體驗 | 不直覺 | 符合直覺 |

## 使用示例

### 場景 1：縮小視圖後圈選

```
1. 用戶縮放 SVG 到 50%
2. 用戶按 Ctrl + 拖曳圈選
3. 選擇框顯示在正確位置（跟隨 SVG 縮放）
4. 判定結果正確（因為都在 SVG 內部座標）
```

### 場景 2：平移後圈選

```
1. 用戶平移 SVG 到其他位置
2. 用戶按 Ctrl + 拖曳圈選
3. 選擇框跟隨 SVG 平移
4. 判定結果正確
```

### 場景 3：縮放 + 平移後圈選

```
1. 用戶先縮放再平移
2. 用戶圈選
3. 一切正常！✅
```

## 調試輸出

```javascript
console.log('=== 選擇框範圍 (SVG 內部座標) ===');
console.log(`範圍: (${minX.toFixed(1)}, ${minY.toFixed(1)}) 到 (${maxX.toFixed(1)}, ${maxY.toFixed(1)})`);
console.log(`模式: ${isCrossingMode ? 'Crossing (右→左)' : 'Window (左→右)'}`);
console.log(`梁 ${beamName}: (${x1.toFixed(1)},${y1.toFixed(1)})→(${x2.toFixed(1)},${y2.toFixed(1)}) ${isInside ? '✓選中' : '✗未選中'}`);
```

**重點**：這些座標都是 SVG 內部座標，不受視圖縮放影響！

## 測試清單

請測試以下場景：

- [ ] **原始視圖**
  - [ ] 左→右框選（藍色 Window）
  - [ ] 右→左框選（綠色 Crossing）

- [ ] **縮小視圖（50%）**
  - [ ] 左→右框選
  - [ ] 右→左框選

- [ ] **放大視圖（200%）**
  - [ ] 左→右框選
  - [ ] 右→左框選

- [ ] **平移後**
  - [ ] 左→右框選
  - [ ] 右→左框選

- [ ] **縮放 + 平移組合**
  - [ ] 左→右框選
  - [ ] 右→左框選

## 預期結果

✅ **所有場景下：**
- 選擇框顯示位置正確
- 判定範圍與顯示一致
- 框內的梁被選中
- 框外的梁不被選中

## 技術總結

這是最自然、最正確的實現方式：

1. **不要對抗 SVG 的變換系統**
   - 利用 SVG 自帶的座標系統
   - 讓選擇框成為 SVG 的一部分

2. **保持座標系統一致**
   - 所有元素都在同一座標空間
   - 不需要來回轉換

3. **符合用戶直覺**
   - 看到的 = 得到的
   - 無論如何縮放平移

## 下一步

請測試這個版本，應該會完全解決座標不一致的問題！

如果還有任何問題，請提供：
1. 控制台輸出
2. 縮放比例
3. 具體的選擇情況

我會繼續優化！🎯
