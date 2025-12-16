using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System.Text.RegularExpressions;

namespace BeamLabeler
{
    /// <summary>
    /// 自動識別 AutoCAD 圖中的 Grid Line (軸線)
    /// </summary>
    public class GridDetector
    {
        /// <summary>
        /// 自動偵測軸線並找到指定的基準點
        /// </summary>
        public static Point3d? AutoDetectBasePoint(Document doc, string gridX, string gridY)
        {
            Editor ed = doc.Editor;

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                // 1. 搜尋所有文字物件
                var gridTexts = FindAllGridTexts(doc, tr);

                if (gridTexts.Count == 0)
                {
                    ed.WriteMessage("\n未找到軸線標註。");
                    return null;
                }

                // 2. 找到指定的軸線
                var targetXGrid = gridTexts.FirstOrDefault(g => g.Label == gridX && g.Direction == "X");
                var targetYGrid = gridTexts.FirstOrDefault(g => g.Label == gridY && g.Direction == "Y");

                if (targetXGrid == null)
                {
                    ed.WriteMessage($"\n未找到 X 軸線 '{gridX}'");
                    return null;
                }

                if (targetYGrid == null)
                {
                    ed.WriteMessage($"\n未找到 Y 軸線 '{gridY}'");
                    return null;
                }

                // 3. 找交點
                Point3d basePoint = new Point3d(targetXGrid.Position.X, targetYGrid.Position.Y, 0);

                ed.WriteMessage($"\n找到基準點: X軸線'{gridX}' 與 Y軸線'{gridY}' 的交點");
                ed.WriteMessage($"\n座標: ({basePoint.X:F2}, {basePoint.Y:F2})");

                tr.Commit();
                return basePoint;
            }
        }

        /// <summary>
        /// 搜尋圖中所有的軸線文字
        /// </summary>
        private static List<GridInfo> FindAllGridTexts(Document doc, Transaction tr)
        {
            List<GridInfo> grids = new List<GridInfo>();
            BlockTable bt = tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
            BlockTableRecord btr = tr.GetObject(bt![BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;

            // 軸線標註通常是數字或字母，且可能在圓圈中
            Regex gridPattern = new Regex(@"^[0-9A-Za-z']+$");

            foreach (ObjectId id in btr!)
            {
                Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;

                // 檢查 DBText
                if (ent is DBText text)
                {
                    string label = text.TextString.Trim();
                    if (gridPattern.IsMatch(label))
                    {
                        grids.Add(new GridInfo
                        {
                            Label = label,
                            Position = text.Position,
                            Direction = GuessDirection(text.Position, grids)
                        });
                    }
                }
                // 檢查 MText
                else if (ent is MText mtext)
                {
                    string label = mtext.Text.Trim();
                    if (gridPattern.IsMatch(label))
                    {
                        grids.Add(new GridInfo
                        {
                            Label = label,
                            Position = mtext.Location,
                            Direction = GuessDirection(mtext.Location, grids)
                        });
                    }
                }
            }

            return grids;
        }

        /// <summary>
        /// 猜測軸線方向 (X軸或Y軸)
        /// 根據已找到的軸線位置推測
        /// </summary>
        private static string GuessDirection(Point3d pos, List<GridInfo> existingGrids)
        {
            // 簡單邏輯：如果 Y 座標相近的多，可能是 X 軸線
            // 如果 X 座標相近的多，可能是 Y 軸線

            int sameY = existingGrids.Count(g => Math.Abs(g.Position.Y - pos.Y) < 1000);
            int sameX = existingGrids.Count(g => Math.Abs(g.Position.X - pos.X) < 1000);

            if (sameY > sameX) return "X";
            if (sameX > sameY) return "Y";

            // 無法判斷，根據標籤內容猜測
            return "Unknown";
        }

        /// <summary>
        /// 指令：顯示圖中所有偵測到的軸線
        /// </summary>
        [CommandMethod("SHOWGRIDS")]
        public void ShowGrids()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                var grids = FindAllGridTexts(doc, tr);

                if (grids.Count == 0)
                {
                    ed.WriteMessage("\n未找到軸線標註。");
                    tr.Commit();
                    return;
                }

                ed.WriteMessage($"\n找到 {grids.Count} 個軸線標註:");

                var xGrids = grids.Where(g => g.Direction == "X").OrderBy(g => g.Label);
                var yGrids = grids.Where(g => g.Direction == "Y").OrderBy(g => g.Label);

                if (xGrids.Any())
                {
                    ed.WriteMessage("\n\nX 軸線:");
                    foreach (var g in xGrids)
                    {
                        ed.WriteMessage($"\n  {g.Label} @ ({g.Position.X:F2}, {g.Position.Y:F2})");
                    }
                }

                if (yGrids.Any())
                {
                    ed.WriteMessage("\n\nY 軸線:");
                    foreach (var g in yGrids)
                    {
                        ed.WriteMessage($"\n  {g.Label} @ ({g.Position.X:F2}, {g.Position.Y:F2})");
                    }
                }

                tr.Commit();
            }
        }

        private class GridInfo
        {
            public string Label { get; set; } = "";
            public Point3d Position { get; set; }
            public string Direction { get; set; } = "Unknown";
        }
    }
}
