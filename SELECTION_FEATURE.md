# AutoCAD-Style Selection Feature

## Overview

This update adds AutoCAD-style selection functionality to the beam labeling tool, allowing users to select and batch edit beam labels interactively.

## Features Implemented

### 1. Window vs Crossing Selection

Just like AutoCAD:
- **Left to Right** (Blue box): Window selection - only selects beams completely inside
- **Right to Left** (Green box): Crossing selection - selects any beam that touches the box

### 2. Selection Methods

- **Ctrl + Drag**: Create selection box
- **Ctrl + Click**: Toggle individual beam selection
- **Shift + Click**: Remove from selection

### 3. Batch Editing

- **Enter**: Open batch edit dialog for selected beams
- Modify all selected beams to the same label
- Shows count of selected beams

### 4. Visual Feedback

- Selected beams: Yellow highlight with glow effect
- Selected labels: Bold text with darker color
- Selection box: Blue (window) or Green (crossing) dashed border

### 5. Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl + Drag | Box selection |
| Ctrl + Click | Toggle single beam |
| Shift + Click | Remove from selection |
| Enter | Batch edit dialog |
| Esc | Clear all selections |
| Delete | Reset selected beams to original labels |

## Technical Details

### Files Modified

- [index.html](index.html): Main application file with all functionality

### Key Components Added

1. **CSS Styles** (lines ~453-539)
   - `.selection-rect`: Blue window selection box
   - `.selection-rect-crossing`: Green crossing selection box
   - `.beam-selected`: Yellow highlight for selected beams
   - `.beam-label-selected`: Bold style for selected labels
   - `.batch-edit-dialog`: Modal dialog styling

2. **HTML Elements** (lines ~919-936)
   - Batch edit dialog
   - Overlay for modal
   - Input field for new label

3. **JavaScript Functions** (lines ~3980-4294)
   - `initializeSelectionFeature()`: Setup event listeners
   - `onSelectionStart()`: Handle mousedown
   - `onSelectionMove()`: Update selection box
   - `onSelectionEnd()`: Complete selection and check intersections
   - `selectBeamsInRect()`: Determine which beams are selected
   - `lineIntersectsRect()`: Crossing mode detection
   - `updateBeamVisualState()`: Apply/remove highlighting
   - `openBatchEditDialog()`: Show batch edit UI
   - `saveBatchEdit()`: Apply changes to all selected beams

### Integration with Existing Code

- **Compatibility with svg-pan-zoom**: Selection only activates when Ctrl/Shift is pressed, allowing normal panning otherwise
- **Data attributes**: Added `data-beam-name` to both beam lines and labels for tracking
- **State management**: Uses `selectedBeams` Set to track selections
- **Preserved functionality**: Original `openBeamEditDialog()` function retained for future use

## Usage Instructions

### Basic Workflow

1. Upload E2K file
2. Click "執行編號" to generate beam numbering
3. Use selection tools:
   - Hold Ctrl and drag to create selection box
   - Drag left→right for window (complete)
   - Drag right→left for crossing (partial)
   - Ctrl+Click to select individual beams
   - Shift+Click to deselect
4. Press Enter to batch edit
5. Enter new label and confirm

### Example Scenarios

**Scenario 1: Rename all stair beams to "g1"**
1. Hold Ctrl, drag right→left to select all stair beams
2. Press Enter
3. Type "g1"
4. Click "確定修改"

**Scenario 2: Select only complete beams in an area**
1. Hold Ctrl, drag left→right around target area
2. Only fully enclosed beams are selected
3. Batch edit as needed

**Scenario 3: Add one beam to selection**
1. Already have some beams selected
2. Ctrl+Click on additional beam
3. It's added to the selection set

## Code Architecture

### Selection State

```javascript
let isSelecting = false;        // Currently drawing selection box?
let selectionStart = null;      // Starting point of selection box
let selectionRect = null;       // SVG rect element for visual feedback
let selectedBeams = new Set();  // Set of selected beam names
let svgElement = null;          // Reference to SVG container
```

### Selection Algorithm

**Window Mode** (left→right):
```javascript
// Both endpoints must be inside rect
isInside = (x1 >= minX && x1 <= maxX && y1 >= minY && y1 <= maxY &&
           x2 >= minX && x2 <= maxX && y2 >= minY && y2 <= maxY);
```

**Crossing Mode** (right→left):
```javascript
// Check if line segment intersects with rectangle
isInside = lineIntersectsRect(x1, y1, x2, y2, minX, minY, maxX, maxY);
```

### Intersection Detection

Uses parametric line segment intersection algorithm:
```javascript
const denom = (y4 - y3) * (x2 - x1) - (x4 - x3) * (y2 - y1);
const ua = ((x4 - x3) * (y1 - y3) - (y4 - y3) * (x1 - x3)) / denom;
const ub = ((x2 - x1) * (y1 - y3) - (y2 - y1) * (x1 - x3)) / denom;
return ua >= 0 && ua <= 1 && ub >= 0 && ub <= 1;
```

## Browser Compatibility

Tested on:
- Chrome/Edge (Chromium-based)
- Firefox
- Safari (macOS)

Requires:
- ES6+ JavaScript support
- SVG support
- CSS3 features

## Performance Considerations

- Selection state uses Set for O(1) lookups
- Line intersection calculations only performed during selection
- Visual updates batch DOM operations
- Event handlers properly cleaned up

## Future Enhancements

Potential improvements:
- [ ] Auto-increment labels (g1, g2, g3...)
- [ ] Save/load selection sets
- [ ] Filter by properties (floor, section size)
- [ ] Undo/redo functionality
- [ ] Export selected beams to separate file
- [ ] "Select All" / "Invert Selection" commands

## Testing Checklist

- [x] Window selection (L→R) only selects fully enclosed beams
- [x] Crossing selection (R→L) selects partially enclosed beams
- [x] Ctrl+Click toggles individual beam selection
- [x] Shift+Click removes from selection
- [x] Enter opens batch edit dialog
- [x] Batch edit updates all selected beams
- [x] Esc clears all selections
- [x] Visual feedback (highlighting) works correctly
- [x] Compatibility with pan-zoom functionality
- [x] Works across different floors/stories

## Known Issues

None currently identified.

## License

Same as parent project.
