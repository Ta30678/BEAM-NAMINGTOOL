# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is an ETABS beam labeling automation system consisting of two main components:
1. **HTML/JavaScript Tool** ([index.html](index.html)) - Parses ETABS E2K files and generates beam numbering with visual preview
2. **AutoCAD C# Plugin** - Automatically annotates beam labels in AutoCAD drawings

**Workflow**: ETABS exports E2K file → HTML tool generates JSON with coordinates → AutoCAD plugin reads JSON and places text labels on drawings.

## Build Commands

### AutoCAD Plugin (Current V2)
```bash
cd AutoCAD_Labeling
dotnet restore
dotnet build -c Release
```
Output DLL: `AutoCAD_Labeling/bin/Release/net8.0/BeamLabeler.dll`

### Legacy V1 Plugin (BeamLabelPlugin folder)
```bash
cd BeamLabelPlugin
msbuild BeamLabelPlugin.csproj /p:Configuration=Release
```
Output DLL: `BeamLabelPlugin/bin/Release/BeamLabelPlugin.dll`

### HTML Tool
No build required - open [index.html](index.html) directly in a browser.

## Testing

### AutoCAD Plugin Testing
1. Load into AutoCAD 2024:
   ```
   NETLOAD → select BeamLabeler.dll
   ```
2. Run commands:
   - `LABELBEAMS` - Main labeling command (V2 absolute coordinates)
   - `SHOWGRIDS` - Display detected grid lines (debugging)

### HTML Tool Testing
1. Open [index.html](index.html) in browser
2. Upload a `.e2k` file
3. Click "執行編號" to generate labels
4. Export JSON for AutoCAD plugin or Excel for manual reference

## Architecture

### Two Plugin Versions

The project contains **two separate implementations**:

#### V2 (Current, Recommended) - Absolute Coordinates
- Location: [AutoCAD_Labeling/](AutoCAD_Labeling/)
- Target: .NET 8.0
- Approach: Uses absolute coordinates with manual/auto base point selection
- Command: `LABELBEAMS`
- Pros: Universal, works without grid labels in AutoCAD
- Cons: Requires base point selection and scale factor

#### V1 (Legacy) - Grid-Based (in Documentation)
- Location: [BeamLabelPlugin/](BeamLabelPlugin/) and root [BeamLabelPlugin.cs](BeamLabelPlugin.cs)
- Target: .NET Framework 4.8
- Approach: Originally designed for grid-based matching
- Note: The [V1_vs_V2_比較.md](AutoCAD_Labeling/V1_vs_V2_比較.md) describes a grid-based "V2" approach, but the current codebase in [AutoCAD_Labeling/](AutoCAD_Labeling/) uses coordinate-based approach

### Coordinate System (Current V2 Approach)

**Coordinate Conversion**:
```
AutoCAD position = basePoint + (ETABS coords × scale)
```
- ETABS uses meters (m)
- AutoCAD typically uses millimeters (mm)
- Default scale: 1000 (1m = 1000mm)

**Example**:
```
ETABS beam midpoint: (14.25, 2.6) m
User-selected base point: (5000, 3000) mm
Scale: 1000

Label position = (5000, 3000) + (14.25, 2.6) × 1000
              = (19250, 5600) mm
```

### JSON Data Format

```json
{
  "project": "ETABS梁編號專案",
  "exportDate": "2025-10-05T12:00:00Z",
  "floors": [
    {
      "floorName": "2F",
      "beams": [
        {
          "etabsId": "B65",
          "newLabel": "GAa-2",
          "startPoint": { "id": "270", "x": 11.7, "y": 2.6 },
          "endPoint": { "id": "271", "x": 16.8, "y": 2.6 },
          "midPoint": { "x": 14.25, "y": 2.6 },
          "length": 5.1,
          "section": "B50X70C280",
          "isMainBeam": true
        }
      ]
    }
  ]
}
```

### AutoCAD Plugin V2 Architecture

**Key Components**:
- [Commands.cs](AutoCAD_Labeling/Commands.cs) - Main command `LABELBEAMS`
- [GridDetector.cs](AutoCAD_Labeling/GridDetector.cs) - Auto-detection of grid lines for base point
- [BeamMatcher.cs](AutoCAD_Labeling/BeamMatcher.cs) - Beam matching logic (if grid-based approach is used)
- [Models/BeamData.cs](AutoCAD_Labeling/Models/BeamData.cs) - JSON deserialization models

**Layer Management**:
- `梁編號-大梁` - Main beams (labels starting with "G")
- `梁編號-小梁` - Secondary beams (labels starting with "B" or "FB")

**Base Point Selection**:
1. **Manual** - User clicks on base point (typically grid 0-A intersection)
2. **Auto** - Attempts to detect grid intersection automatically, searches for text "0" and "A"

## Project Configuration

### AutoCAD DLL References

Both plugin versions reference AutoCAD 2024 DLLs:
- `acdbmgd.dll` or `AcDbMgd.dll`
- `acmgd.dll` or `AcMgd.dll`
- `accoremgd.dll` or `AcCoreMgd.dll`

Default path: `C:\Program Files\Autodesk\AutoCAD 2024\`

**To support different AutoCAD versions**: Update `<HintPath>` in:
- [AutoCAD_Labeling/BeamLabeler.csproj](AutoCAD_Labeling/BeamLabeler.csproj) for V2
- [BeamLabelPlugin/BeamLabelPlugin.csproj](BeamLabelPlugin/BeamLabelPlugin.csproj) for V1

### Target Frameworks
- V2 Plugin: .NET 8.0 ([AutoCAD_Labeling/BeamLabeler.csproj](AutoCAD_Labeling/BeamLabeler.csproj))
- V1 Plugin: .NET Framework 4.8 ([BeamLabelPlugin/BeamLabelPlugin.csproj](BeamLabelPlugin/BeamLabelPlugin.csproj))

## Key Documentation Files

- [系統說明.md](系統說明.md) - Overall system architecture and workflow
- [AutoCAD_Labeling/README.md](AutoCAD_Labeling/README.md) - V2 plugin usage guide
- [AutoCAD_Labeling/快速入門.md](AutoCAD_Labeling/快速入門.md) - Quick start guide
- [AutoCAD_Labeling/V1_vs_V2_比較.md](AutoCAD_Labeling/V1_vs_V2_比較.md) - Comparison of approaches
- [README.md](README.md) - Main project README

## Development Notes

### E2K File Format
ETABS exports structural data in E2K format (text-based). The HTML tool in [index.html](index.html) parses:
- Joint coordinates (3D points)
- Frame (beam) definitions with start/end joints
- Section properties (beam sizes)
- Story/floor information

### Text Placement in AutoCAD
- Default text height: 250 units (suitable for 1:100 scale drawings)
- Text placed at beam midpoints
- Alignment: Horizontal and vertical center
- Text rotation: 0° (horizontal)

### Known Limitations
1. V2 requires manual scale factor input (cannot auto-detect AutoCAD units)
2. Base point must be set per floor
3. Only supports model space (not layout/paper space)
4. Will fail if target layers are locked or text style is missing
5. Grid auto-detection may fail if text objects are inside blocks or use non-standard formatting
