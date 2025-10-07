using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using BeamLabeler.Models;
using System.Text.Json;

namespace BeamLabeler
{
    public class Commands
    {
        /// <summary>
        /// 主命令：讀取 JSON 並自動標註梁編號
        /// 使用方式：在 AutoCAD 命令列輸入 LABELBEAMS
        /// </summary>
        [CommandMethod("LABELBEAMS")]
        public void LabelBeams()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            try
            {
                // 1. 選擇 JSON 檔案
                PromptOpenFileOptions fileOpts = new PromptOpenFileOptions("\n選擇梁資料 JSON 檔案:")
                {
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
                };

                PromptFileNameResult fileResult = ed.GetFileNameForOpen(fileOpts);
                if (fileResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n操作已取消。");
                    return;
                }

                // 2. 讀取並解析 JSON
                string jsonContent = File.ReadAllText(fileResult.StringResult);
                BeamLabelingData? data = JsonSerializer.Deserialize<BeamLabelingData>(jsonContent);

                if (data == null || data.Floors.Count == 0)
                {
                    ed.WriteMessage("\nJSON 檔案格式錯誤或無資料。");
                    return;
                }

                // 3. 讓使用者選擇要標註的樓層
                string selectedFloor = SelectFloor(ed, data.Floors);
                if (string.IsNullOrEmpty(selectedFloor))
                {
                    ed.WriteMessage("\n操作已取消。");
                    return;
                }

                FloorData? floorData = data.Floors.FirstOrDefault(f => f.FloorName == selectedFloor);
                if (floorData == null || floorData.Beams.Count == 0)
                {
                    ed.WriteMessage($"\n樓層 {selectedFloor} 無梁資料。");
                    return;
                }

                // 4. 選擇基準點設定方式
                ed.WriteMessage($"\n\n=== 開始標註 {selectedFloor} ===");

                PromptKeywordOptions modeOpts = new PromptKeywordOptions("\n選擇基準點設定方式:");
                modeOpts.Keywords.Add("Manual");
                modeOpts.Keywords.Add("Auto");
                modeOpts.Keywords.Default = "Manual";
                modeOpts.Message = "\n[Manual/Auto] <Manual>:";

                PromptResult modeResult = ed.GetKeywords(modeOpts);
                Point3d basePoint;

                if (modeResult.Status == PromptStatus.OK && modeResult.StringResult == "Auto")
                {
                    // 自動偵測基準點
                    ed.WriteMessage("\n嘗試自動偵測軸線...");
                    Point3d? autoPoint = GridDetector.AutoDetectBasePoint(doc, "0", "A");

                    if (autoPoint.HasValue)
                    {
                        basePoint = autoPoint.Value;
                    }
                    else
                    {
                        ed.WriteMessage("\n自動偵測失敗，請手動點選基準點:");
                        PromptPointOptions ppo = new PromptPointOptions("\n點選基準點:")
                        {
                            AllowNone = false
                        };

                        PromptPointResult ppr = ed.GetPoint(ppo);
                        if (ppr.Status != PromptStatus.OK)
                        {
                            ed.WriteMessage("\n操作已取消。");
                            return;
                        }
                        basePoint = ppr.Value;
                    }
                }
                else
                {
                    // 手動點選基準點
                    ed.WriteMessage("\n請點選該樓層圖框的基準點 (通常是軸線 0-A 的交點):");
                    PromptPointOptions ppo = new PromptPointOptions("\n點選基準點:")
                    {
                        AllowNone = false
                    };

                    PromptPointResult ppr = ed.GetPoint(ppo);
                    if (ppr.Status != PromptStatus.OK)
                    {
                        ed.WriteMessage("\n操作已取消。");
                        return;
                    }
                    basePoint = ppr.Value;
                }

                ed.WriteMessage($"\n基準點設定為: ({basePoint.X:F2}, {basePoint.Y:F2})");

                // 5. 詢問座標縮放比例 (ETABS 單位是公尺，AutoCAD 可能是毫米)
                PromptDoubleOptions scaleOpts = new PromptDoubleOptions("\n輸入座標縮放比例 (ETABS→AutoCAD, 例如 1000 表示 1m=1000mm):")
                {
                    DefaultValue = 1000.0,
                    AllowNegative = false,
                    AllowZero = false
                };

                PromptDoubleResult scaleResult = ed.GetDouble(scaleOpts);
                double scale = scaleResult.Status == PromptStatus.OK ? scaleResult.Value : 1000.0;

                // 6. 詢問文字高度
                PromptDoubleOptions heightOpts = new PromptDoubleOptions("\n輸入標註文字高度:")
                {
                    DefaultValue = 250.0,
                    AllowNegative = false,
                    AllowZero = false
                };

                PromptDoubleResult heightResult = ed.GetDouble(heightOpts);
                double textHeight = heightResult.Status == PromptStatus.OK ? heightResult.Value : 250.0;

                // 7. 開始標註
                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    BlockTable bt = tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = tr.GetObject(bt![BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                    int successCount = 0;

                    foreach (var beam in floorData.Beams)
                    {
                        try
                        {
                            // 計算在 AutoCAD 中的實際座標
                            Point3d textPosition = new Point3d(
                                basePoint.X + beam.MidPoint.X * scale,
                                basePoint.Y + beam.MidPoint.Y * scale,
                                0
                            );

                            // 建立文字物件
                            using (DBText text = new DBText())
                            {
                                text.Position = textPosition;
                                text.Height = textHeight;
                                text.TextString = beam.NewLabel;
                                text.HorizontalMode = TextHorizontalMode.TextCenter;
                                text.VerticalMode = TextVerticalMode.TextVerticalMid;
                                text.AlignmentPoint = textPosition;

                                // 設定圖層 (如果不存在會自動建立)
                                text.Layer = beam.IsMainBeam ? "梁編號-大梁" : "梁編號-小梁";

                                btr!.AppendEntity(text);
                                tr.AddNewlyCreatedDBObject(text, true);
                                successCount++;
                            }
                        }
                        catch (System.Exception ex)
                        {
                            ed.WriteMessage($"\n標註 {beam.NewLabel} 時發生錯誤: {ex.Message}");
                        }
                    }

                    tr.Commit();
                    ed.WriteMessage($"\n\n✅ 標註完成！成功標註 {successCount}/{floorData.Beams.Count} 根梁");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n發生錯誤: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 讓使用者選擇要標註的樓層
        /// </summary>
        private string SelectFloor(Editor ed, List<FloorData> floors)
        {
            ed.WriteMessage("\n\n可用樓層:");
            for (int i = 0; i < floors.Count; i++)
            {
                ed.WriteMessage($"\n  {i + 1}. {floors[i].FloorName} ({floors[i].Beams.Count} 根梁)");
            }

            PromptStringOptions pso = new PromptStringOptions($"\n請輸入樓層編號 (1-{floors.Count}):")
            {
                AllowSpaces = false
            };

            PromptResult pr = ed.GetString(pso);
            if (pr.Status != PromptStatus.OK) return "";

            if (int.TryParse(pr.StringResult, out int index) && index >= 1 && index <= floors.Count)
            {
                return floors[index - 1].FloorName;
            }

            ed.WriteMessage("\n無效的樓層編號。");
            return "";
        }
    }
}
