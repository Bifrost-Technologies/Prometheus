namespace Prometheus.Server.Requests
{
    public class RequestBluepint
    {
        public string Prompt { get; set; } = string.Empty;
    }
    public class BlueprintResponse
    {
        public string Blueprint { get; set; } = string.Empty;
    }
    public class CheckStatusRequest
    {
        public string JobId { get; set; } = string.Empty;
    }
    public class BlueprintJobResponse
    {
        public string JobId { get; set; } = string.Empty;
    }
    public class StatusResponse
    {
        public string Status { get; set; } = string.Empty;
    }
    public class BlueprintCompleteResponse
    {
        public string Blueprint { get; set; } = string.Empty;
    }
}
