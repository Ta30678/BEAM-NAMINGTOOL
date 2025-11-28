using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(BeamLabelPlugin.BeamLabelCommands))]

namespace BeamLabelPlugin
{
    // ==================== 資料模型 ====================
    
    /// <summary>
    /// 從 CSV 讀取的梁數據
    /// </summary>
    public class BeamLabelData
    {
        public string Story { get; set; }           // 樓層名稱
        public string NewLabel { get; set; }        // 新編號
        public string EtabsLabel { get; set; }      // ETABS編號
        public string BaseGridX { get; set; }       // ETABS格線X
        public string BaseGridY { get; set; }       // ETABS格線Y
        public double MidpointX { get; set; }       // 梁中點X(m)
        public double MidpointY { get; set; }       // 梁中點Y(m)
        public bool IsMainBeam { get; set; }        // 是否大梁
    }

    /// <summary>
    /// 格線資訊
    /// </summary>
    public class GridInfo
    {
        public string Name { get; set; }            // 格線名稱 (X1, Y2...)
        public Point3d Position { get; set; }       // 格線位置座標
        public bool IsVertical { get; set; }        // 是否垂直格線
        public Point3d StartPoint { get; set; }     // 格線起點
        public Point3d EndPoint { get; set; }       // 格線終點
    }

    /// <summary>
    /// 樓層資訊
    /// </summary>
    public class FloorInfo
    {
        public string FloorName { get; set; }       // 樓層名稱
        public Point3d TitlePosition { get; set; }  // 樓層標記位置
        public double FrameOriginX { get; set; }    // 圖框原點X
        public List<GridInfo> Grids { get; set; }   // 該樓層的格線
    }

    // ==================== 主程式 ====================
    
    public class BeamLabelCommands
    {
        private const double LABEL_OFFSET = 400.0;  // 標註偏移距離 400mm
        private const double TEXT_HEIGHT = 300.0;   // 文字高度 300mm (30cm)
        private const string TEXT_STYLE = "中央";    // 文字樣式

        [CommandMethod("PLACEBEAMLABELS")]
        public void PlaceBeamLabelsCommand()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            ed.WriteMessage("\n╔══════════════════════════════════════╗");
            ed.WriteMessage("\n║   ETABS 梁自動編號外掛 v2.0          ║");
            ed.WriteMessage("\n╚══════════════════════════════════════╝");

            // ===== 步驟1: 選擇 CSV 檔案 =====
            string csvPath = GetCsvFilePath(ed);
            if (string.IsNullOrEmpty(csvPath)) return;

            // ===== 步驟2: 讀取 CSV 資料 =====
            List<BeamLabelData> beamDataList;
            try
            {
                beamDataList = ReadBeamDataFromCsv(csvPath);
                ed.WriteMessage($"\n✓ 成功讀取 {beamDataList.Count} 筆梁資料");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n✗ 讀取 CSV 失敗: {ex.Message}");
                return;
            }

            // ===== 步驟3: 使用者輸入參數 =====
            double frameWidth = GetDoubleInput(ed, "\n請輸入單一圖框寬度(mm)", 50000);
            if (frameWidth <= 0) return;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    // ===== 步驟4: 掃描 CAD 圖面 =====
                    ed.WriteMessage("\n");
                    ed.WriteMessage("\n[1/5] 正在掃描圖面格線系統...");
                    
                    var floorInfos = ScanFloorSystems(tr, db, frameWidth, ed);
                    
                    if (floorInfos.Count == 0)
                    {
                        ed.WriteMessage("\n✗ 找不到樓層資訊,請檢查 S-TITLE 圖層");
                        return;
                    }
                    
                    ed.WriteMessage($"\n  ✓ 找到 {floorInfos.Count} 個樓層");
                    foreach (var floor in floorInfos)
                    {
                        ed.WriteMessage($"\n    - {floor.FloorName}: {floor.Grids.Count} 條格線");
                    }

                    // ===== 步驟5: 建立格線對應表 =====
                    ed.WriteMessage("\n");
                    ed.WriteMessage("\n[2/5] 正在建立 ETABS ↔ CAD 格線對應表...");
                    
                    var gridMapping = BuildGridMapping(beamDataList, floorInfos, ed);
                    if (gridMapping == null)
                    {
                        ed.WriteMessage("\n✗ 使用者取消操作");
                        return;
                    }

                    // ===== 步驟6: 確保文字樣式存在 =====
                    ed.WriteMessage("\n");
                    ed.WriteMessage("\n[3/5] 正在檢查文字樣式...");
                    EnsureTextStyle(tr, db, TEXT_STYLE);
                    ed.WriteMessage($"\n  ✓ 文字樣式 '{TEXT_STYLE}' 已就緒");

                    // ===== 步驟7: 確保圖層存在 =====
                    ed.WriteMessage("\n");
                    ed.WriteMessage("\n[4/5] 正在檢查圖層...");
                    EnsureLayer(tr, db, "S-TEXTG", 2); // 黃色
                    EnsureLayer(tr, db, "S-TEXTB", 2); // 黃色
                    ed.WriteMessage("\n  ✓ 圖層 S-TEXTG, S-TEXTB 已就緒");

                    // ===== 步驟8: 放置標註 =====
                    ed.WriteMessage("\n");
                    ed.WriteMessage("\n[5/5] 正在放置標註...");
                    
                    int successCount = PlaceAllLabels(tr, db, beamDataList, floorInfos, 
                                                     gridMapping, ed);

                    tr.Commit();
                    
                    ed.WriteMessage("\n");
                    ed.WriteMessage("\n╔══════════════════════════════════════╗");
                    ed.WriteMessage($"\n║  ✓ 完成! 成功放置 {successCount} 個梁編號  ║");
                    ed.WriteMessage("\n╚══════════════════════════════════════╝");
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n✗ 執行失敗: {ex.Message}");
                    ed.WriteMessage($"\n  詳細資訊: {ex.StackTrace}");
                    tr.Abort();
                }
            }
        }

        // ==================== CSV 讀取 ====================
        
        private string GetCsvFilePath(Editor ed)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                Title = "請選擇 ETABS 梁編號 CSV 檔案"
            };
            return ofd.ShowDialog() == DialogResult.OK ? ofd.FileName : null;
        }

        private List<BeamLabelData> ReadBeamDataFromCsv(string filePath)
        {
            var list = new List<BeamLabelData>();
            var lines = File.ReadAllLines(filePath).Skip(1); // 跳過標頭

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                var values = line.Split(',');
                if (values.Length >= 8)
                {
                    list.Add(new BeamLabelData
                    {
                        Story = values[0].Trim(),
                        NewLabel = values[1].Trim(),
                        EtabsLabel = values[2].Trim(),
                        BaseGridX = values[3].Trim(),
                        BaseGridY = values[4].Trim(),
                        MidpointX = double.Parse(values[5], CultureInfo.InvariantCulture),
                        MidpointY = double.Parse(values[6], CultureInfo.InvariantCulture),
                        IsMainBeam = bool.Parse(values[7])
                    });
                }
            }
            return list;
        }

        // ==================== 格線系統掃描 ====================
        
        private List<FloorInfo> ScanFloorSystems(Transaction tr, Database db, double frameWidth, Editor ed)
        {
            var floorInfos = new List<FloorInfo>();
            BlockTableRecord modelSpace = GetModelSpace(tr, db);

            // 1. 掃描樓層標記 (S-TITLE)
            var floorTitles = GetTextsOnLayer(tr, modelSpace, "S-TITLE");
            if (floorTitles.Count == 0)
            {
                throw new Exception("找不到樓層標記 (S-TITLE 圖層)");
            }

            // 2. 掃描所有格線 BUBBLE (S-GRID-T)
            var gridBubbles = GetTextsOnLayer(tr, modelSpace, "S-GRID-T");
            if (gridBubbles.Count == 0)
            {
                throw new Exception("找不到格線標記 (S-GRID-T 圖層)");
            }

            // 3. 掃描所有格線 LINE (S-GRID)
            var gridLines = GetLinesOnLayer(tr, modelSpace, "S-GRID");
            if (gridLines.Count == 0)
            {
                throw new Exception("找不到格線 (S-GRID 圖層)");
            }

            ed.WriteMessage($"\n  - 樓層標記: {floorTitles.Count} 個");
            ed.WriteMessage($"\n  - 格線標記: {gridBubbles.Count} 個");
            ed.WriteMessage($"\n  - 格線: {gridLines.Count} 條");

            // 4. 為每個樓層建立格線地圖
            foreach (DBText titleText in floorTitles)
            {
                string floorName = ExtractFloorName(titleText.TextString);
                if (string.IsNullOrEmpty(floorName)) continue;

                double frameOriginX = CalculateFrameOriginX(titleText.Position, frameWidth);

                var floorInfo = new FloorInfo
                {
                    FloorName = floorName,
                    TitlePosition = titleText.Position,
                    FrameOriginX = frameOriginX,
                    Grids = new List<GridInfo>()
                };

                // 找出屬於這個樓層的 BUBBLE
                var floorBubbles = gridBubbles
                    .Where(b => IsInFloor(b.Position, frameOriginX, frameWidth))
                    .ToList();

                // 為每個 BUBBLE 找到對應的 LINE
                foreach (DBText bubble in floorBubbles)
                {
                    var matchedLine = FindClosestGridLine(bubble, gridLines, frameOriginX, frameWidth);
                    
                    if (matchedLine != null)
                    {
                        bool isVertical = Math.Abs(matchedLine.StartPoint.X - matchedLine.EndPoint.X) < 1.0;
                        
                        floorInfo.Grids.Add(new GridInfo
                        {
                            Name = bubble.TextString.Trim(),
                            Position = isVertical ? 
                                new Point3d(matchedLine.StartPoint.X, matchedLine.StartPoint.Y, 0) : 
                                new Point3d(matchedLine.StartPoint.X, matchedLine.StartPoint.Y, 0),
                            IsVertical = isVertical,
                            StartPoint = matchedLine.StartPoint,
                            EndPoint = matchedLine.EndPoint
                        });
                    }
                }

                if (floorInfo.Grids.Count > 0)
                {
                    floorInfos.Add(floorInfo);
                }
            }

            return floorInfos.OrderBy(f => f.FrameOriginX).ToList();
        }

        private Line FindClosestGridLine(DBText bubble, List<Line> allLines, 
                                        double frameOriginX, double frameWidth)
        {
            Point3d bubbleCenter = bubble.Position;
            Line closestLine = null;
            double minDistance = double.MaxValue;

            foreach (var line in allLines)
            {
                // 確保 LINE 在同一個圖框內
                if (!IsInFloor(line.StartPoint, frameOriginX, frameWidth)) 
                    continue;

                // 計算 BUBBLE 中心到 LINE 的距離
                double dist = DistancePointToLine(bubbleCenter, line.StartPoint, line.EndPoint);

                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestLine = line;
                }
            }

            // 如果距離太遠(>1000mm),可能配對錯誤
            if (minDistance > 1000)
                return null;

            return closestLine;
        }

        private double DistancePointToLine(Point3d point, Point3d lineStart, Point3d lineEnd)
        {
            Vector3d lineVec = lineEnd - lineStart;
            Vector3d pointVec = point - lineStart;
            
            double lineLengthSq = lineVec.LengthSqrd;
            if (lineLengthSq < 0.0001) // 線段長度接近 0
                return point.DistanceTo(lineStart);
            
            double t = Math.Max(0, Math.Min(1, pointVec.DotProduct(lineVec) / lineLengthSq));
            Point3d projection = lineStart + lineVec * t;
            
            return point.DistanceTo(projection);
        }

        // ==================== 格線對應表 ====================
        
        private Dictionary<string, string> BuildGridMapping(
            List<BeamLabelData> beamData, 
            List<FloorInfo> floorInfos,
            Editor ed)
        {
            // 1. 從 CSV 收集所有 ETABS 格線
            var etabsGridsX = beamData.Select(b => b.BaseGridX).Distinct().OrderBy(x => x).ToList();
            var etabsGridsY = beamData.Select(b => b.BaseGridY).Distinct().OrderBy(x => x).ToList();

            // 2. 從 CAD 收集所有格線 (使用第一個樓層的格線)
            var firstFloor = floorInfos.First();
            var cadGridsX = firstFloor.Grids
                .Where(g => g.IsVertical)
                .Select(g => g.Name)
                .Distinct()
                .OrderBy(x => x)
                .ToList();
            
            var cadGridsY = firstFloor.Grids
                .Where(g => !g.IsVertical)
                .Select(g => g.Name)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            // 3. 顯示資訊
            ed.WriteMessage("\n");
            ed.WriteMessage("\n  ┌─────────────────────────────────────┐");
            ed.WriteMessage("\n  │      格線對應表 (自動順序對應)      │");
            ed.WriteMessage("\n  ├─────────────────────────────────────┤");
            ed.WriteMessage($"\n  │ ETABS X軸: {string.Join(", ", etabsGridsX)}");
            ed.WriteMessage($"\n  │ CAD X軸:   {string.Join(", ", cadGridsX)}");
            ed.WriteMessage("\n  ├─────────────────────────────────────┤");
            ed.WriteMessage($"\n  │ ETABS Y軸: {string.Join(", ", etabsGridsY)}");
            ed.WriteMessage($"\n  │ CAD Y軸:   {string.Join(", ", cadGridsY)}");
            ed.WriteMessage("\n  └─────────────────────────────────────┘");

            // 4. 建立對應關係 (自動順序對應)
            var mapping = new Dictionary<string, string>();

            ed.WriteMessage("\n");
            ed.WriteMessage("\n  對應關係:");
            
            for (int i = 0; i < etabsGridsX.Count && i < cadGridsX.Count; i++)
            {
                mapping[etabsGridsX[i]] = cadGridsX[i];
                ed.WriteMessage($"\n    {etabsGridsX[i]} → {cadGridsX[i]}");
            }
            
            for (int i = 0; i < etabsGridsY.Count && i < cadGridsY.Count; i++)
            {
                mapping[etabsGridsY[i]] = cadGridsY[i];
                ed.WriteMessage($"\n    {etabsGridsY[i]} → {cadGridsY[i]}");
            }

            // 5. 讓使用者確認
            PromptKeywordOptions pko = new PromptKeywordOptions("\n  對應是否正確? [是(Y)/否(N)]");
            pko.Keywords.Add("Y");
            pko.Keywords.Add("N");
            pko.Keywords.Default = "Y";
            pko.AllowNone = true;
            
            PromptResult pr = ed.GetKeywords(pko);
            
            if (pr.Status != PromptStatus.OK || pr.StringResult == "N")
            {
                return null;
            }

            return mapping;
        }

        // ==================== 標註放置 ====================
        
        private int PlaceAllLabels(
            Transaction tr,
            Database db,
            List<BeamLabelData> beamData,
            List<FloorInfo> floorInfos,
            Dictionary<string, string> gridMapping,
            Editor ed)
        {
            int successCount = 0;
            int skipCount = 0;

            foreach (var beam in beamData)
            {
                try
                {
                    // 1. 找到對應的樓層
                    var floorInfo = floorInfos.FirstOrDefault(f => f.FloorName == beam.Story);
                    if (floorInfo == null)
                    {
                        skipCount++;
                        continue;
                    }

                    // 2. 取得 CAD 格線名稱
                    if (!gridMapping.TryGetValue(beam.BaseGridX, out string cadGridX))
                    {
                        skipCount++;
                        continue;
                    }
                    
                    if (!gridMapping.TryGetValue(beam.BaseGridY, out string cadGridY))
                    {
                        skipCount++;
                        continue;
                    }

                    // 3. 找到格線
                    var gridX = floorInfo.Grids.FirstOrDefault(g => g.Name == cadGridX && g.IsVertical);
                    var gridY = floorInfo.Grids.FirstOrDefault(g => g.Name == cadGridY && !g.IsVertical);
                    
                    if (gridX == null || gridY == null)
                    {
                        skipCount++;
                        continue;
                    }

                    // 4. 計算梁的中點(基於格線交點)
                    Point3d beamMidpoint = new Point3d(
                        gridX.StartPoint.X, 
                        gridY.StartPoint.Y, 
                        0
                    );

                    // 5. 計算標註位置和旋轉角度
                    var (labelPos, rotation) = CalculateLabelPositionAndRotation(
                        beam, beamMidpoint, gridX, gridY);

                    // 6. 決定圖層
                    string targetLayer = beam.IsMainBeam ? "S-TEXTG" : "S-TEXTB";

                    // 7. 放置標註
                    CreateLabel(tr, db, beam.NewLabel, labelPos, targetLayer, rotation);
                    successCount++;
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n  ⚠ 跳過梁 {beam.NewLabel}: {ex.Message}");
                    skipCount++;
                }
            }

            if (skipCount > 0)
            {
                ed.WriteMessage($"\n  ⚠ 跳過 {skipCount} 個標註 (格線不匹配或其他錯誤)");
            }

            return successCount;
        }

        private (Point3d position, double rotation) CalculateLabelPositionAndRotation(
            BeamLabelData beam, Point3d beamMidpoint, GridInfo gridX, GridInfo gridY)
        {
            // 計算梁的實際方向 (使用格線資訊推斷)
            double beamDeltaX = Math.Abs(gridX.EndPoint.X - gridX.StartPoint.X);
            double beamDeltaY = Math.Abs(gridY.EndPoint.Y - gridY.StartPoint.Y);

            // 判斷梁的類型
            bool isHorizontal = beamDeltaY < 1.0; // Y座標幾乎不變 = 水平梁
            bool isVertical = beamDeltaX < 1.0;   // X座標幾乎不變 = 垂直梁

            Point3d labelPos;
            double rotation;

            if (isHorizontal)
            {
                // 水平梁: 標註在上方,文字水平
                labelPos = new Point3d(beamMidpoint.X, beamMidpoint.Y + LABEL_OFFSET, 0);
                rotation = 0.0;
            }
            else if (isVertical)
            {
                // 垂直梁: 標註在左側,文字旋轉90度
                labelPos = new Point3d(beamMidpoint.X - LABEL_OFFSET, beamMidpoint.Y, 0);
                rotation = Math.PI / 2; // 90度 (逆時針)
            }
            else
            {
                // 斜梁: 計算梁的角度
                double angle = Math.Atan2(
                    gridY.EndPoint.Y - gridY.StartPoint.Y,
                    gridX.EndPoint.X - gridX.StartPoint.X
                );

                // 調整角度,確保文字方向為左→右或下→上
                if (angle > Math.PI / 2)
                    angle -= Math.PI;
                else if (angle < -Math.PI / 2)
                    angle += Math.PI;

                // 計算偏移向量 (垂直於梁的方向)
                double offsetAngle = angle + Math.PI / 2;
                double offsetX = LABEL_OFFSET * Math.Cos(offsetAngle);
                double offsetY = LABEL_OFFSET * Math.Sin(offsetAngle);

                labelPos = new Point3d(
                    beamMidpoint.X + offsetX,
                    beamMidpoint.Y + offsetY,
                    0
                );
                rotation = angle;
            }

            return (labelPos, rotation);
        }

        private void CreateLabel(Transaction tr, Database db, string labelText, 
                                Point3d position, string layer, double rotation)
        {
            BlockTableRecord modelSpace = GetModelSpace(tr, db);

            DBText text = new DBText
            {
                Position = position,
                AlignmentPoint = position,
                TextString = labelText,
                Height = TEXT_HEIGHT,
                Layer = layer,
                ColorIndex = 2, // 黃色
                Rotation = rotation,
                HorizontalMode = TextHorizontalMode.TextCenter,
                VerticalMode = TextVerticalMode.TextVerticalMid,
                TextStyleId = GetTextStyleId(tr, db, TEXT_STYLE)
            };

            modelSpace.AppendEntity(text);
            tr.AddNewlyCreatedDBObject(text, true);
        }

        // ==================== 文字樣式與圖層管理 ====================
        
        private void EnsureTextStyle(Transaction tr, Database db, string styleName)
        {
            TextStyleTable styleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            
            if (!styleTable.Has(styleName))
            {
                // 如果沒有指定樣式,使用 Standard
                return;
            }
        }

        private ObjectId GetTextStyleId(Transaction tr, Database db, string styleName)
        {
            TextStyleTable styleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            
            if (styleTable.Has(styleName))
            {
                return styleTable[styleName];
            }
            
            // 回退到 Standard
            return db.Textstyle;
        }

        private void EnsureLayer(Transaction tr, Database db, string layerName, short colorIndex)
        {
            LayerTable layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            
            if (!layerTable.Has(layerName))
            {
                layerTable.UpgradeOpen();
                
                LayerTableRecord layer = new LayerTableRecord
                {
                    Name = layerName,
                    Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                        Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex)
                };
                
                layerTable.Add(layer);
                tr.AddNewlyCreatedDBObject(layer, true);
                
                layerTable.DowngradeOpen();
            }
        }

        // ==================== 輔助函式 ====================
        
        private BlockTableRecord GetModelSpace(Transaction tr, Database db)
        {
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            return (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
        }

        private List<DBText> GetTextsOnLayer(Transaction tr, BlockTableRecord btr, string layerName)
        {
            var texts = new List<DBText>();
            
            foreach (ObjectId id in btr)
            {
                if (id.ObjectClass.IsDerivedFrom(RXObject.GetClass(typeof(DBText))))
                {
                    var text = (DBText)tr.GetObject(id, OpenMode.ForRead);
                    if (text.Layer.Equals(layerName, StringComparison.OrdinalIgnoreCase))
                    {
                        texts.Add(text);
                    }
                }
            }
            
            return texts;
        }

        private List<Line> GetLinesOnLayer(Transaction tr, BlockTableRecord btr, string layerName)
        {
            var lines = new List<Line>();
            
            foreach (ObjectId id in btr)
            {
                if (id.ObjectClass.IsDerivedFrom(RXObject.GetClass(typeof(Line))))
                {
                    var line = (Line)tr.GetObject(id, OpenMode.ForRead);
                    if (line.Layer.Equals(layerName, StringComparison.OrdinalIgnoreCase))
                    {
                        lines.Add(line);
                    }
                }
            }
            
            return lines;
        }

        private string ExtractFloorName(string titleText)
        {
            // 從 "二層結構平面圖(2F)" 提取 "二層結構平面圖"
            int idx = titleText.IndexOf('(');
            if (idx > 0)
                return titleText.Substring(0, idx).Trim();
            return titleText.Trim();
        }

        private double CalculateFrameOriginX(Point3d titlePosition, double frameWidth)
        {
            // 根據樓層標記位置,推算圖框原點
            return Math.Floor(titlePosition.X / frameWidth) * frameWidth;
        }

        private bool IsInFloor(Point3d position, double frameOriginX, double frameWidth)
        {
            return position.X >= frameOriginX && position.X < (frameOriginX + frameWidth);
        }

        private double GetDoubleInput(Editor ed, string prompt, double defaultValue)
        {
            PromptDoubleOptions pdo = new PromptDoubleOptions(prompt)
            {
                DefaultValue = defaultValue,
                AllowNegative = false,
                AllowZero = false,
                AllowNone = true
            };
            
            PromptDoubleResult pdr = ed.GetDouble(pdo);
            
            if (pdr.Status == PromptStatus.OK)
                return pdr.Value;
            else if (pdr.Status == PromptStatus.None)
                return defaultValue;
            else
                return -1;
        }
    }
}
