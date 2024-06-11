using System;
using Newtonsoft.Json.Linq;

namespace OBSSync.Websocket;

public struct ObsRequestId
{
    internal ulong Id { get; set; }
}

public abstract class ObsRequest
{
    public string RequestType { get; }

    protected ObsRequest(string requestType)
    {
        RequestType = requestType;
    }

    public JObject GetRequestBody(string requestId)
    {
        var requestBody = new JObject
        {
            ["requestType"] = RequestType,
            ["requestId"] = requestId
        };

        JObject? data = GetRequestData();
        if (data != null) requestBody["requestData"] = data;

        return requestBody;
    }

    protected virtual JObject? GetRequestData()
    {
        return null;
    }

    public abstract ObsRequestResponse MakeResponseObject();
}

public abstract class ObsRequest<TResponse> : ObsRequest
    where TResponse : ObsRequestResponse, new()
{
    public override ObsRequestResponse MakeResponseObject() => new TResponse();

    protected ObsRequest(string requestType) : base(requestType)
    {
    }
}

public class ObsRequestResponse
{
    public ResponseStatus Status { get; private set; } = null!;

    public void SetRequestResponseBody(JObject body)
    {
        JObject status = body["requestStatus"]!.Value<JObject>()!;
        bool success = status["result"]!.Value<bool>();
        int code = status["code"]!.Value<int>();
        string comment = status["comment"]?.Value<string>() ?? string.Empty;
        Status = new ResponseStatus(success, code, comment);

        if (body.ContainsKey("responseData"))
        {
            SetResponseData(body["responseData"]!.Value<JObject>()!);
        }
    }

    protected virtual void SetResponseData(JObject data)
    {
    }

    public class ResponseStatus(bool success, int code, string comment)
    {
        public bool Success { get; } = success;
        public int Code { get; } = code;
        public string Comment { get; } = comment;
    }
}