using Newtonsoft.Json;

namespace OBSSync.Websocket;

public class RecordStateChangedEventData
{
    [JsonProperty(PropertyName = "outputActive")]
    public bool OutputActive { get; set; }
    
    [JsonProperty(PropertyName = "outputPath")]
    public string? OutputPath { get; set; }

    [JsonProperty(PropertyName = "outputState")]
    public string OutputState { get; set; } = null!;

    public const string StateStarting = "OBS_WEBSOCKET_OUTPUT_STARTING";
    public const string StateStarted = "OBS_WEBSOCKET_OUTPUT_STARTED";
    public const string StateStopping = "OBS_WEBSOCKET_OUTPUT_STOPPING";
    public const string StateStopped = "OBS_WEBSOCKET_OUTPUT_STOPPED";
}