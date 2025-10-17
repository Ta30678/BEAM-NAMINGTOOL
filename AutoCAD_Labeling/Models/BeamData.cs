using System.Text.Json.Serialization;

namespace BeamLabeler.Models
{
    /// <summary>
    /// 梁資料模型 - 對應 JSON 檔案格式
    /// </summary>
    public class BeamLabelingData
    {
        [JsonPropertyName("project")]
        public string Project { get; set; } = "";

        [JsonPropertyName("exportDate")]
        public string ExportDate { get; set; } = "";

        [JsonPropertyName("floors")]
        public List<FloorData> Floors { get; set; } = new();
    }

    public class FloorData
    {
        [JsonPropertyName("floorName")]
        public string FloorName { get; set; } = "";

        [JsonPropertyName("beams")]
        public List<BeamInfo> Beams { get; set; } = new();
    }

    public class BeamInfo
    {
        [JsonPropertyName("etabsId")]
        public string EtabsId { get; set; } = "";

        [JsonPropertyName("newLabel")]
        public string NewLabel { get; set; } = "";

        [JsonPropertyName("startPoint")]
        public PointInfo StartPoint { get; set; } = new();

        [JsonPropertyName("endPoint")]
        public PointInfo EndPoint { get; set; } = new();

        [JsonPropertyName("midPoint")]
        public PointInfo MidPoint { get; set; } = new();

        [JsonPropertyName("length")]
        public double Length { get; set; }

        [JsonPropertyName("section")]
        public string Section { get; set; } = "";

        [JsonPropertyName("isMainBeam")]
        public bool IsMainBeam { get; set; }
    }

    public class PointInfo
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }
    }
}
