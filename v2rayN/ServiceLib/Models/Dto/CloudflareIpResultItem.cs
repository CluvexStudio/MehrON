namespace ServiceLib.Models.Dto;

[Serializable]
public class CloudflareIpResultItem
{
    public string Ip { get; set; } = string.Empty;
    public int Port { get; set; }
    public int Ping { get; set; } // ms (-1 if failed)
    public string PingDisplay => Ping > 0 ? $"{Ping} ms" : "Timeout";
    public string DataCenter { get; set; } = "-";
    public string Status { get; set; } = string.Empty;
    public string Speed { get; set; } = "-";
    public bool IsSuccess { get; set; }
}
