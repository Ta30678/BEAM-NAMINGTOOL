using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using BeamLabeler.Models;
using System.Text.RegularExpressions;

namespace BeamLabeler
{
    /// <summary>
    /// 基於軸線相對位置的梁匹配引擎
    /// </summary>
    public class BeamMatcher
    {
        private Dictionary<string, Point3d> gridPoints = new();
        private Document doc;
        private Editor ed;

        public BeamMatcher(Document document)
        {
            doc = document;
            ed = doc.Editor;
        }

        /// <summary>
        /// 步驟 1: 偵測 AutoCAD 中所有軸線的位置
        /// </summary>
        public bool DetectGrids(GridSystem expectedGrids)
        {
            ed.WriteMessage("\n=== 開始偵測軸線 ===");

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt![BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;

                // 搜尋所有軸線標註（通常在圓圈中）
                Regex gridPattern = new Regex(@"^[0-9A-Za-z']+$");
                List<GridText> foundGrids = new List<GridText>();

                foreach (ObjectId id in btr!)
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;

                    // 檢查 DBText 和 MText
                    string? label = null;
                    Point3d position = Point3d.Origin;

                    if (ent is DBText text)
                    {
                        label = text.TextString.Trim();
                        position = text.Position;
                    }
                    else if (ent is MText mtext)
                    {
                        label = mtext.Text.Trim();
                        position = mtext.Location;
                    }

                    if (label != null && gridPattern.IsMatch(label))
                    {
                        foundGrids.Add(new GridText { Label = label, Position = position });
                    }
                }

                // 分類為 X 軸線和 Y 軸線
                ClassifyGrids(foundGrids, expectedGrids);

                tr.Commit();
            }

            // 驗證是否找到所有必要的軸線
            bool allFound = true;
            foreach (var xGrid in expectedGrids.XGrids)
            {
                if (!gridPoints.ContainsKey($"X_{xGrid}"))
                {
                    ed.WriteMessage($"\n⚠ 警告: 未找到 X 軸線 '{xGrid}'");
                    allFound = false;
                }
            }
            foreach (var yGrid in expectedGrids.YGrids)
            {
                if (!gridPoints.ContainsKey($"Y_{yGrid}"))
                {
                    ed.WriteMessage($"\n⚠ 警告: 未找到 Y 軸線 '{yGrid}'");
                    allFound = false;
                }
            }

            if (allFound)
            {
                ed.WriteMessage($"\n✓ 成功偵測所有軸線！");
            }

            return allFound;
        }

        /// <summary>
        /// 步驟 2: 在 AutoCAD 中找到對應的梁
        /// </summary>
        public Line? FindMatchingBeam(BeamInfoV2 beamInfo, double tolerance = 500.0)
        {
            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                // 計算理論梁位置
                Point3d? theoreticalStart = CalculateBeamStartPoint(beamInfo);
                Point3d? theoreticalEnd = CalculateBeamEndPoint(beamInfo);

                if (!theoreticalStart.HasValue || !theoreticalEnd.HasValue)
                {
                    tr.Commit();
                    return null;
                }

                // 搜尋 AutoCAD 中的所有 LINE 物件
                BlockTable bt = tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt![BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;

                Line? bestMatch = null;
                double minDistance = double.MaxValue;

                foreach (ObjectId id in btr!)
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;

                    if (ent is Line line)
                    {
                        // 檢查圖層（可選）
                        // if (!line.Layer.Contains("梁") && !line.Layer.Contains("BEAM")) continue;

                        // 計算距離誤差
                        double distStart1 = line.StartPoint.DistanceTo(theoreticalStart.Value);
                        double distEnd1 = line.EndPoint.DistanceTo(theoreticalEnd.Value);
                        double distStart2 = line.StartPoint.DistanceTo(theoreticalEnd.Value);
                        double distEnd2 = line.EndPoint.DistanceTo(theoreticalStart.Value);

                        double distance = Math.Min(distStart1 + distEnd1, distStart2 + distEnd2);

                        if (distance < tolerance && distance < minDistance)
                        {
                            minDistance = distance;
                            bestMatch = line.Clone() as Line;
                        }
                    }
                }

                tr.Commit();

                if (bestMatch != null)
                {
                    ed.WriteMessage($"\n✓ 找到梁 {beamInfo.EtabsId} (誤差: {minDistance:F2})");
                }
                else
                {
                    ed.WriteMessage($"\n✗ 未找到梁 {beamInfo.EtabsId}");
                }

                return bestMatch;
            }
        }

        /// <summary>
        /// 分類軸線為 X 或 Y 軸
        /// </summary>
        private void ClassifyGrids(List<GridText> grids, GridSystem expectedGrids)
        {
            // 根據標籤內容和位置分類
            foreach (var grid in grids)
            {
                bool isXGrid = expectedGrids.XGrids.Contains(grid.Label);
                bool isYGrid = expectedGrids.YGrids.Contains(grid.Label);

                if (isXGrid)
                {
                    gridPoints[$"X_{grid.Label}"] = grid.Position;
                    ed.WriteMessage($"\n  X軸線 '{grid.Label}' @ ({grid.Position.X:F2}, {grid.Position.Y:F2})");
                }
                else if (isYGrid)
                {
                    gridPoints[$"Y_{grid.Label}"] = grid.Position;
                    ed.WriteMessage($"\n  Y軸線 '{grid.Label}' @ ({grid.Position.X:F2}, {grid.Position.Y:F2})");
                }
            }
        }

        /// <summary>
        /// 根據軸線資訊計算梁的起點
        /// </summary>
        private Point3d? CalculateBeamStartPoint(BeamInfoV2 beam)
        {
            if (beam.GridInfo.Direction == "horizontal")
            {
                // 水平梁：沿著 Y 軸線，在兩條 X 軸線之間
                string yGrid = beam.GridInfo.AlongGrid;
                string xGrid1 = beam.GridInfo.Between[0];

                if (!gridPoints.ContainsKey($"Y_{yGrid}") || !gridPoints.ContainsKey($"X_{xGrid1}"))
                    return null;

                Point3d yPos = gridPoints[$"Y_{yGrid}"];
                Point3d xPos = gridPoints[$"X_{xGrid1}"];

                return new Point3d(xPos.X, yPos.Y, 0);
            }
            else // vertical
            {
                // 垂直梁：沿著 X 軸線，在兩條 Y 軸線之間
                string xGrid = beam.GridInfo.AlongGrid;
                string yGrid1 = beam.GridInfo.Between[0];

                if (!gridPoints.ContainsKey($"X_{xGrid}") || !gridPoints.ContainsKey($"Y_{yGrid1}"))
                    return null;

                Point3d xPos = gridPoints[$"X_{xGrid}"];
                Point3d yPos = gridPoints[$"Y_{yGrid1}"];

                return new Point3d(xPos.X, yPos.Y, 0);
            }
        }

        /// <summary>
        /// 根據軸線資訊計算梁的終點
        /// </summary>
        private Point3d? CalculateBeamEndPoint(BeamInfoV2 beam)
        {
            if (beam.GridInfo.Direction == "horizontal")
            {
                string yGrid = beam.GridInfo.AlongGrid;
                string xGrid2 = beam.GridInfo.Between[1];

                if (!gridPoints.ContainsKey($"Y_{yGrid}") || !gridPoints.ContainsKey($"X_{xGrid2}"))
                    return null;

                Point3d yPos = gridPoints[$"Y_{yGrid}"];
                Point3d xPos = gridPoints[$"X_{xGrid2}"];

                return new Point3d(xPos.X, yPos.Y, 0);
            }
            else
            {
                string xGrid = beam.GridInfo.AlongGrid;
                string yGrid2 = beam.GridInfo.Between[1];

                if (!gridPoints.ContainsKey($"X_{xGrid}") || !gridPoints.ContainsKey($"Y_{yGrid2}"))
                    return null;

                Point3d xPos = gridPoints[$"X_{xGrid}"];
                Point3d yPos = gridPoints[$"Y_{yGrid2}"];

                return new Point3d(xPos.X, yPos.Y, 0);
            }
        }

        private class GridText
        {
            public string Label { get; set; } = "";
            public Point3d Position { get; set; }
        }
    }
}
