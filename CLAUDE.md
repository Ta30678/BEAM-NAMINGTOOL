# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is an ETABS beam labeling automation system that consists of two main components:
1. **HTML/JavaScript Tool** ([index.html](index.html)) - Parses ETABS E2K files and generates beam numbering
2. **AutoCAD C# Plugin** ([AutoCAD_Labeling/](AutoCAD_Labeling/)) - Automatically annotates beam labels in AutoCAD drawings

The workflow: ETABS exports E2K file → HTML tool generates JSON with coordinates → AutoCAD plugin reads JSON and places text labels on drawings.

## Build Commands

### AutoCAD Plugin (C# .NET)
```bash
cd AutoCAD_Labeling
dotnet restore
dotnet build -c Release
```

Output DLL location: `AutoCAD_Labeling/bin/Release/net8.0/BeamLabeler.dll`

### HTML Tool
No build required - open [index.html](index.html) directly in a browser.

## Testing

### AutoCAD Plugin Testing
1. Load into AutoCAD 2024:
   ```
   NETLOAD → select BeamLabeler.dll
   ```
2. Run commands:
   - `LABELBEAMS` - Main labeling command
   - `SHOWGRIDS` - Display detected grid lines (for debugging)

### HTML Tool Testing
1. Open [index.html](index.html) in browser
2. Upload test file: [2025-0916_WooGooWang227_Twin 2F.e2k](2025-0916_WooGooWang227_Twin 2F.e2k)
3. Click "執行編號" to generate labels
4. Export JSON to test with AutoCAD plugin

## Architecture

### Coordinate System Design
The system uses **absolute coordinates** instead of grid references:
- ETABS uses meters (m)
- AutoCAD typically uses millimeters (mm)
- Conversion: `AutoCAD position = basePoint + (ETABS coords × scale)`
- Default scale: 1000 (1m = 1000mm)

**Why coordinates over grid names?**
- Grid naming can differ between ETABS and AutoCAD
- Coordinates are universal - only need one base point reference
- More reliable for automation

### JSON Data Format
Intermediate format between HTML tool and AutoCAD plugin:
```json
{
  "floors": [
    {
      "floorName": "2F",
      "beams": [
        {
          "etabsId": "B65",
          "newLabel": "GAa-2",
          "midPoint": { "x": 14.25, "y": 2.6 },
          "isMainBeam": true
        }
      ]
    }
  ]
}
```

### AutoCAD Plugin Architecture

**Key Components:**
- [Commands.cs](AutoCAD_Labeling/Commands.cs) - Main command `LABELBEAMS` that orchestrates the labeling process
- [GridDetector.cs](AutoCAD_Labeling/GridDetector.cs) - Auto-detection of grid lines (command `SHOWGRIDS`)
- [Models/BeamData.cs](AutoCAD_Labeling/Models/BeamData.cs) - JSON deserialization models
- [BeamLabelPlugin.cs](BeamLabelPlugin.cs) - Legacy V1 plugin (uses CSV instead of JSON, relative grid coordinates)

**Two Plugin Versions:**
- **V2** (Current): JSON-based with absolute coordinates in [AutoCAD_Labeling/](AutoCAD_Labeling/)
- **V1** (Legacy): CSV-based with relative grid offsets in [BeamLabelPlugin.cs](BeamLabelPlugin.cs)

**Layer Management:**
- `梁編號-大梁` - Main beams (labels starting with "G")
- `梁編號-小梁` - Secondary beams (labels starting with "B" or "FB")

### Base Point Selection
Two modes available in `LABELBEAMS`:
1. **Manual** - User clicks on base point (typically grid 0-A intersection)
2. **Auto** - System attempts to detect grid intersection automatically
   - Falls back to manual if detection fails
   - Searches for text objects labeled "0" and "A"

## Project Configuration

### AutoCAD DLL References
Located in [BeamLabelPlugin.csproj](BeamLabelPlugin.csproj):
- `AcCoreMgd.dll`
- `AcDbMgd.dll`
- `AcMgd.dll`

Default path: `C:\Program Files\Autodesk\AutoCAD 2024\`

**To support different AutoCAD versions**: Update `<HintPath>` in the .csproj file.

### Target Framework
- AutoCAD Plugin: .NET Framework 4.8 ([BeamLabelPlugin.csproj](BeamLabelPlugin.csproj))
- New Plugin (V2): .NET 8.0 (in AutoCAD_Labeling folder, BeamLabeler.csproj - not present in root but referenced in docs)

## Key Files

- [系統說明.md](系統說明.md) - System architecture and technical details
- [AutoCAD_Labeling/快速入門.md](AutoCAD_Labeling/快速入門.md) - Quick start guide for users
- [AutoCAD_Labeling/README.md](AutoCAD_Labeling/README.md) - Plugin documentation
- [AutoCAD_Labeling/V1_vs_V2_比較.md](AutoCAD_Labeling/V1_vs_V2_比較.md) - Comparison of two plugin versions

## Development Notes

### E2K File Format
ETABS exports structural data in E2K format (text-based). The HTML tool parses:
- Joint coordinates
- Frame (beam) definitions
- Section properties
- Story/floor information

### Text Placement in AutoCAD
Default text height: 250 units (suitable for 1:100 scale drawings)
Text is placed at beam midpoints with horizontal/vertical center alignment.

### Known Limitations
1. Manual scale factor input required (cannot auto-detect AutoCAD units)
2. Base point must be set per floor
3. Only supports model space (not layout/paper space)
4. Will fail if target layers are locked

## Future Enhancements (from TODO lists in docs)
- Batch labeling for multiple floors
- Column numbering support
- Smart text overlap avoidance
- Revit integration
- Web-based AutoCAD support
