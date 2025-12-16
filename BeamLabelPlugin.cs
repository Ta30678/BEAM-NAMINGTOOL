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
    // 用於儲存從 CSV 讀取的梁數據
    public class BeamLabelData
    {
        public string Story { get; set; }
        public string NewLabel { get; set; }
        public string EtabsLabel { get; set; }
        public string BaseGridX { get; set; }
        public double OffsetX { get; set; } // 單位: mm
        public string BaseGridY { get; set; }
        public double OffsetY { get; set; } // 單位: mm
    }

    // 用於儲存從 AutoCAD 讀取的格線資訊
    public class AcadGridLine
    {
        public string Name { get; set; }
        public bool IsVertical { get; set; }
        public double Position { get; set; } // X 或 Y 座標
        public Point3d StartPoint { get; set; }
        public Point3d EndPoint { get; set; }
    }

    public class BeamLabelCommands
    {
        [CommandMethod("PLACEBEAMLABELS")]
        public void PlaceBeamLabelsCommand()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            ed.WriteMessage("\nETABS 梁自動編號外掛啟動...");

            // 1. 讓使用者選擇 CSV 檔案
            string csvPath = GetCsvFilePath(ed);
            if (string.IsNullOrEmpty(csvPath)) return;

            // 2. 讀取 CSV 資料
            List<BeamLabelData> beamDataList;
            try
            {
                beamDataList = ReadBeamDataFromCsv(csvPath);
                ed.WriteMessage($"\n成功讀取 {beamDataList.Count} 筆梁資料。");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n讀取 CSV 檔案時發生錯誤: {ex.Message}");
                return;
            }

            // 3. 讓使用者輸入圖層名稱
            string gridLineLayer = GetUserInput(ed, "\n請輸入格線線條所在的圖層名稱: ", "GRID");
            if (gridLineLayer == null) return;

            string gridTextLayer = GetUserInput(ed, "\n請輸入格線文字所在的圖層名稱: ", "GRID-TEXT");
            if (gridTextLayer == null) return;
            
            string floorTextLayer = GetUserInput(ed, "\n請輸入樓層標示文字所在的圖層名稱 (例如 '1F', '2F'): ", "TEXT");
            if (floorTextLayer == null) return;


            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    // 4. 掃描 AutoCAD 圖面，建立格線地圖
                    ed.WriteMessage("\n正在掃描圖面以建立格線地圖...");
                    var floorGridMaps = ScanForGridSystems(tr, db, gridLineLayer, gridTextLayer, floorTextLayer);
                    if (floorGridMaps.Count == 0)
                    {
                        ed.WriteMessage("\n錯誤: 在指定圖層上找不到任何有效的格線系統。");
                        return;
                    }
                    ed.WriteMessage($"\n掃描完成，找到 {floorGridMaps.Count} 個樓層的格線系統。");

                    // 5. 遍歷 CSV 資料並放置標籤
                    int placedCount = 0;
                    foreach (var beamData in beamDataList)
                    {
                        if (floorGridMaps.TryGetValue(beamData.Story, out var gridMap))
                        {
                            // 找到基準格線
                            var baseGridX = gridMap.FirstOrDefault(g => g.Name == beamData.BaseGridX && g.IsVertical);
                            var baseGridY = gridMap.FirstOrDefault(g => g.Name == beamData.BaseGridY && !g.IsVertical);

                            if (baseGridX != null && baseGridY != null)
                            {
                                // 計算最終座標
                                double finalX = baseGridX.Position + beamData.OffsetX;
                                double finalY = baseGridY.Position + beamData.OffsetY;
                                Point3d insertPosition = new Point3d(finalX, finalY, 0);

                                // 放置文字
                                CreateLabel(tr, db, beamData.NewLabel, insertPosition);
                                placedCount++;
                            }
                        }
                    }

                    tr.Commit();
                    ed.WriteMessage($"\n處理完成！成功放置 {placedCount} 個梁編號。");
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n處理過程中發生錯誤: {ex.Message}");
                    tr.Abort();
                }
            }
        }

        private string GetCsvFilePath(Editor ed)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                Title = "請選擇 ETABS 梁格線相對座標.csv 檔案"
            };
            return ofd.ShowDialog() == DialogResult.OK ? ofd.FileName : null;
        }

        private List<BeamLabelData> ReadBeamDataFromCsv(string filePath)
        {
            var list = new List<BeamLabelData>();
            var lines = File.ReadAllLines(filePath).Skip(1); // 跳過標頭

            foreach (var line in lines)
            {
                var values = line.Split(',');
                if (values.Length >= 7)
                {
                    list.Add(new BeamLabelData
                    {
                        Story = values[0].Trim(),
                        NewLabel = values[1].Trim(),
                        EtabsLabel = values[2].Trim(),
                        BaseGridX = values[3].Trim(),
                        OffsetX = double.Parse(values[4], CultureInfo.InvariantCulture),
                        BaseGridY = values[5].Trim(),
                        OffsetY = double.Parse(values[6], CultureInfo.InvariantCulture)
                    });
                }
            }
            return list;
        }

        private string GetUserInput(Editor ed, string promptMessage, string defaultValue)
        {
            PromptStringOptions pso = new PromptStringOptions(promptMessage)
            {
                DefaultValue = defaultValue,
                AllowSpaces = true
            };
            PromptResult pr = ed.GetString(pso);
            return pr.Status == PromptStatus.OK ? pr.StringResult : null;
        }

        private Dictionary<string, List<AcadGridLine>> ScanForGridSystems(Transaction tr, Database db, string gridLineLayer, string gridTextLayer, string floorTextLayer)
        {
            var allGrids = new List<AcadGridLine>();
            var floorMarkers = new Dictionary<string, Point3d>();
            BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockTable(db)[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            // 1. 取得所有格線"線條"和"文字"物件
            var gridLines = GetEntitiesOnLayer<Curve>(tr, modelSpace, gridLineLayer);
            var gridTexts = GetEntitiesOnLayer<DBText>(tr, modelSpace, gridTextLayer);
            var floorTexts = GetEntitiesOnLayer<DBText>(tr, modelSpace, floorTextLayer);

            // 2. 處理樓層標示，建立樓層區域
            foreach (var text in floorTexts)
            {
                // 這裡可以加入更複雜的樓層名稱判斷邏輯
                if (!floorMarkers.ContainsKey(text.TextString.ToUpper()))
                {
                    floorMarkers.Add(text.TextString.ToUpper(), text.Position);
                }
            }
            if (floorMarkers.Count == 0) throw new System.Exception("在指定樓層圖層上找不到任何樓層標示文字。");

            // 3. 處理格線線條
            foreach (var curve in gridLines)
            {
                if (curve is Line line) // 只處理直線
                {
                    bool isVertical = Math.Abs(line.StartPoint.X - line.EndPoint.X) < 1.0; // 容許1mm誤差
                    bool isHorizontal = Math.Abs(line.StartPoint.Y - line.EndPoint.Y) < 1.0;

                    if (isVertical)
                    {
                        allGrids.Add(new AcadGridLine { IsVertical = true, Position = line.StartPoint.X, StartPoint = line.StartPoint, EndPoint = line.EndPoint });
                    }
                    else if (isHorizontal)
                    {
                        allGrids.Add(new AcadGridLine { IsVertical = false, Position = line.StartPoint.Y, StartPoint = line.StartPoint, EndPoint = line.EndPoint });
                    }
                }
            }

            // 4. 將格線文字與線條關聯
            foreach (var text in gridTexts)
            {
                var closestGrid = allGrids
                    .OrderBy(g => g.IsVertical ? Math.Abs(text.Position.X - g.Position) : Math.Abs(text.Position.Y - g.Position))
                    .FirstOrDefault();

                if (closestGrid != null && string.IsNullOrEmpty(closestGrid.Name))
                {
                    // 判斷文字是否真的靠近線條
                    double distance = closestGrid.IsVertical ? Math.Abs(text.Position.X - closestGrid.Position) : Math.Abs(text.Position.Y - closestGrid.Position);
                    if (distance < 500) // 假設文字離線條不超過 500mm
                    {
                        closestGrid.Name = text.TextString;
                    }
                }
            }

            // 5. 根據樓層區域，將格線分組
            var floorGridMaps = new Dictionary<string, List<AcadGridLine>>();
            foreach (var grid in allGrids.Where(g => !string.IsNullOrEmpty(g.Name)))
            {
                var gridCenter = new Point3d((grid.StartPoint.X + grid.EndPoint.X) / 2, (grid.StartPoint.Y + grid.EndPoint.Y) / 2, 0);

                // 找到格線屬於哪個樓層區域
                var closestFloor = floorMarkers
                    .OrderBy(kvp => kvp.Value.DistanceTo(gridCenter))
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(closestFloor.Key))
                {
                    if (!floorGridMaps.ContainsKey(closestFloor.Key))
                    {
                        floorGridMaps[closestFloor.Key] = new List<AcadGridLine>();
                    }
                    floorGridMaps[closestFloor.Key].Add(grid);
                }
            }

            return floorGridMaps;
        }

        private List<T> GetEntitiesOnLayer<T>(Transaction tr, BlockTableRecord container, string layerName) where T : Entity
        {
            var list = new List<T>();
            foreach (ObjectId id in container)
            {
                if (id.ObjectClass.IsDerivedFrom(RXObject.GetClass(typeof(T))))
                {
                    var ent = (T)tr.GetObject(id, OpenMode.ForRead);
                    if (ent.Layer.Equals(layerName, StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(ent);
                    }
                }
            }
            return list;
        }

        private void CreateLabel(Transaction tr, Database db, string labelText, Point3d position)
        {
            BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockTable(db)[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            DBText text = new DBText
            {
                Position = position,
                TextString = labelText,
                Height = 2.5, // 可以根據需求調整
                Layer = "0" // 可以根據需求調整
            };

            modelSpace.AppendEntity(text);
            tr.AddNewlyCreatedDBObject(text, true);
        }
    }
}