using Newtonsoft.Json.Linq;

namespace OBSSync.Websocket;

public class StopRecordRequest() : ObsRequest<StopRecordResponse>("StopRecord")
{
}

public class StopRecordResponse : ObsRequestResponse
{
    public string OutputPath { get; private set; } = "";

    protected override void SetResponseData(JObject data)
    {
        OutputPath = data["outputPath"]!.Value<string>()!;
    }
}
