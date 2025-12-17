# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ETABS beam labeling automation system with two components:
1. **HTML/JavaScript Tool** ([index.html](index.html)) - Parses ETABS E2K files, generates beam numbering
2. **AutoCAD C# Plugin** ([AutoCAD_Labeling/](AutoCAD_Labeling/)) - Annotates beam labels in AutoCAD drawings

**Workflow**: ETABS E2K export → HTML tool generates JSON with coordinates → AutoCAD plugin places text labels

## Build Commands

### AutoCAD Plugin V2 (Current - .NET 8.0)
```bash
cd AutoCAD_Labeling
dotnet restore
dotnet build -c Release
```
Output: `AutoCAD_Labeling/bin/Release/net8.0/BeamLabeler.dll`

### AutoCAD Plugin V1 (Legacy - .NET Framework 4.8)
```bash
# Requires Visual Studio or MSBuild for .NET Framework
msbuild BeamLabelPlugin.csproj /p:Configuration=Release
```
Output: `bin/Release/BeamLabelPlugin.dll`

### HTML Tool
No build - open [index.html](index.html) directly in browser. Uses CDN libraries (SheetJS, svg-pan-zoom).

## Testing

### AutoCAD Plugin
```
NETLOAD → select BeamLabeler.dll
LABELBEAMS  - Main labeling command
SHOWGRIDS   - Debug: display detected grid lines
```

### HTML Tool
1. Open [index.html](index.html)
2. Upload E2K file
3. Click "執行編號" (Execute Numbering)
4. Export JSON for AutoCAD plugin

## Architecture

### Coordinate System
Uses **absolute coordinates** (not grid references):
- ETABS: meters (m)
- AutoCAD: millimeters (mm)
- Formula: `AutoCAD position = basePoint + (ETABS coords × scale)`
- Default scale: 1000 (1m = 1000mm)

### JSON Data Format
```json
{
  "floors": [{
    "floorName": "2F",
    "beams": [{
      "etabsId": "B65",
      "newLabel": "GAa-2",
      "midPoint": { "x": 14.25, "y": 2.6 },
      "isMainBeam": true
    }]
  }]
}
```

### AutoCAD Plugin Components

**V2 Plugin** ([AutoCAD_Labeling/](AutoCAD_Labeling/)):
- [Commands.cs](AutoCAD_Labeling/Commands.cs) - `LABELBEAMS` command
- [GridDetector.cs](AutoCAD_Labeling/GridDetector.cs) - Auto-detection of grid lines (`SHOWGRIDS`)
- [BeamMatcher.cs](AutoCAD_Labeling/BeamMatcher.cs) - Beam matching logic
- [Models/BeamData.cs](AutoCAD_Labeling/Models/BeamData.cs) - JSON deserialization
- [Models/BeamDataV2.cs](AutoCAD_Labeling/Models/BeamDataV2.cs) - V2 data models

**V1 Plugin** (Legacy - root directory):
- [BeamLabelPlugin.cs](BeamLabelPlugin.cs) - CSV-based with relative grid offsets

**Layer Management:**
- `梁編號-大梁` - Main beams (G prefix)
- `梁編號-小梁` - Secondary beams (B, FB prefix)

### HTML Tool Components
- [index.html](index.html) - Main application (single-file with embedded JS/CSS)
- [sketch.js](sketch.js) - P5.js background animation
- External CDNs: SheetJS (Excel export), svg-pan-zoom (viewer)

## Configuration

### AutoCAD DLL References
Edit `<HintPath>` in .csproj files to match your AutoCAD installation:
```xml
<HintPath>C:\Program Files\Autodesk\AutoCAD 2024\acdbmgd.dll</HintPath>
```

Required DLLs: `acdbmgd.dll`, `acmgd.dll`, `accoremgd.dll`

## Key Documentation

- [系統說明.md](系統說明.md) - System architecture (Chinese)
- [AutoCAD_Labeling/快速入門.md](AutoCAD_Labeling/快速入門.md) - Quick start guide
- [AutoCAD_Labeling/V1_vs_V2_比較.md](AutoCAD_Labeling/V1_vs_V2_比較.md) - V1 vs V2 comparison

## Known Limitations

1. Manual scale factor required (no AutoCAD unit auto-detection)
2. Base point must be set per floor
3. Model space only (not layout/paper space)
4. Fails if target layers are locked
