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

**Branch:** `claude/grid-bubble-toggle-drag-01GaJgn6GKiKukhaF596u1JQ`
