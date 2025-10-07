using System.Text.Json.Serialization;

namespace BeamLabeler.Models
{
    /// <summary>
    /// V2 版本 - 基於軸線相對位置的梁資料模型
    /// </summary>
    public class BeamLabelingDataV2
    {
        [JsonPropertyName("project")]
        public string Project { get; set; } = "";

        [JsonPropertyName("exportDate")]
        public string ExportDate { get; set; } = "";

        [JsonPropertyName("gridSystem")]
        public GridSystem GridSystem { get; set; } = new();

        [JsonPropertyName("floors")]
        public List<FloorDataV2> Floors { get; set; } = new();
    }

    public class GridSystem
    {
        [JsonPropertyName("xGrids")]
        public List<string> XGrids { get; set; } = new(); // ["0", "1", "2", "3", ...]

        [JsonPropertyName("yGrids")]
        public List<string> YGrids { get; set; } = new(); // ["A", "B", "C", ...]
    }

    public class FloorDataV2
    {
        [JsonPropertyName("floorName")]
        public string FloorName { get; set; } = "";

        [JsonPropertyName("beams")]
        public List<BeamInfoV2> Beams { get; set; } = new();
    }

    public class BeamInfoV2
    {
        [JsonPropertyName("etabsId")]
        public string EtabsId { get; set; } = "";

        [JsonPropertyName("newLabel")]
        public string NewLabel { get; set; } = "";

        [JsonPropertyName("gridInfo")]
        public GridInfo GridInfo { get; set; } = new();

        [JsonPropertyName("section")]
        public string Section { get; set; } = "";

        [JsonPropertyName("isMainBeam")]
        public bool IsMainBeam { get; set; }
    }

    public class GridInfo
    {
        /// <summary>
        /// 梁沿著哪條軸線 (例如: "A" 或 "2")
        /// </summary>
        [JsonPropertyName("alongGrid")]
        public string AlongGrid { get; set; } = "";

        /// <summary>
        /// 梁在哪兩條軸線之間 (例如: ["2", "3"])
        /// </summary>
        [JsonPropertyName("between")]
        public List<string> Between { get; set; } = new();

        /// <summary>
        /// 梁的方向: "horizontal" 或 "vertical"
        /// </summary>
        [JsonPropertyName("direction")]
        public string Direction { get; set; } = "";

        /// <summary>
        /// 距離起始軸線的偏移量 (公尺)
        /// </summary>
        [JsonPropertyName("offsetFromStart")]
        public double OffsetFromStart { get; set; }

        /// <summary>
        /// 梁長度 (公尺)
        /// </summary>
        [JsonPropertyName("length")]
        public double Length { get; set; }

        /// <summary>
        /// 容許誤差範圍 (公尺) - 用於匹配 AutoCAD 梁
        /// </summary>
        [JsonPropertyName("tolerance")]
        public double Tolerance { get; set; } = 0.1;
    }
}
