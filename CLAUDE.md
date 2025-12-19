# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ETABS beam labeling automation system with two components:
1. **HTML/JavaScript Tool** ([index.html](index.html)) - Parses ETABS E2K files, generates beam numbering with SVG preview
2. **AutoCAD C# Plugin** ([AutoCAD_Labeling/](AutoCAD_Labeling/)) - Annotates beam labels in AutoCAD drawings

**Workflow**: ETABS E2K export → HTML tool parses and generates JSON with coordinates → AutoCAD plugin places text labels

## Build Commands

### AutoCAD Plugin (.NET 8.0)
```bash
cd AutoCAD_Labeling
dotnet restore
dotnet build -c Release
```
Output: `AutoCAD_Labeling/bin/Release/net8.0/BeamLabeler.dll`

### Legacy Plugin (.NET Framework 4.8)
```bash
# Requires Visual Studio or MSBuild
msbuild BeamLabelPlugin.csproj /p:Configuration=Release
```

### HTML Tool
No build - open [index.html](index.html) directly in browser.

## AutoCAD Commands

After `NETLOAD → BeamLabeler.dll`:
- `LABELBEAMS` - Main labeling (coordinate-based, requires base point selection)
- `LABELBEAMSV2` - Grid-based labeling (auto-detects grid lines, recommended)
- `SHOWGRIDS` - Debug: display detected grid lines

## Architecture

### Two Labeling Approaches

**V1 (Coordinate-based)**: User manually selects base point + scale factor. Simple but error-prone.

**V2 (Grid-based, recommended)**: Auto-detects grid lines in AutoCAD, matches beams by grid references. Requires AutoCAD drawings with standard grid text annotations.

See [V1_vs_V2_比較.md](AutoCAD_Labeling/V1_vs_V2_比較.md) for detailed comparison.

### Coordinate System (V1)
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
      "isMainBeam": true,
      "gridInfo": { "alongGrid": "A", "between": ["2", "3"] }
    }]
  }]
}
```

### Key Components

**AutoCAD Plugin** ([AutoCAD_Labeling/](AutoCAD_Labeling/)):
- [Commands.cs](AutoCAD_Labeling/Commands.cs) - `LABELBEAMS`, `LABELBEAMSV2`, `SHOWGRIDS` commands
- [GridDetector.cs](AutoCAD_Labeling/GridDetector.cs) - Scans AutoCAD for grid text annotations
- [BeamMatcher.cs](AutoCAD_Labeling/BeamMatcher.cs) - Matches JSON beams to AutoCAD geometry
- [Models/](AutoCAD_Labeling/Models/) - JSON deserialization classes

**HTML Tool** ([index.html](index.html)):
- Single-file app with embedded JS/CSS
- Parses ETABS E2K text format (grid definitions, beam connectivity, sections)
- Generates SVG preview with pan/zoom
- Exports Excel (via SheetJS) and JSON for AutoCAD

**Layer Management:**
- `梁編號-大梁` - Main beams (G prefix)
- `梁編號-小梁` - Secondary beams (B, FB prefix)

## Configuration

### AutoCAD DLL References
Edit `<HintPath>` in `BeamLabeler.csproj` to match your AutoCAD installation:
```xml
<HintPath>C:\Program Files\Autodesk\AutoCAD 2024\acdbmgd.dll</HintPath>
```
Required: `acdbmgd.dll`, `acmgd.dll`, `accoremgd.dll`

## Known Limitations

1. V1: Manual scale factor and base point required per floor
2. V2: Requires standard grid text annotations in AutoCAD
3. Model space only (not layout/paper space)
4. Fails if target layers are locked
