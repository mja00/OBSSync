using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OBSSync.Websocket;

public class ObsWebsocket
{
    private ClientWebSocket WebSocket { get; set; } = new();

    private string? Password { get; set; }

    private ulong NextRequestId { get; set; }

    private Dictionary<ulong, (ObsRequest, TaskCompletionSource<object>)> SentRequests { get; } = new();

    #region Public methods

    public async void Connect(string websocketAddress, string password)
    {
        Password = password;

        if (WebSocket.State != WebSocketState.None) WebSocket = new();
        
        try
        {
            await WebSocket.ConnectAsync(new Uri(websocketAddress), default);
        }
        catch (Exception e)
        {
            Disconnected?.Invoke(e.Message);
            return;
        }

        var bytes = new byte[2048];
        while (true)
        {
            var result = await WebSocket.ReceiveAsync(bytes, default);

            if (!result.EndOfMessage)
            {
                ObsSyncPlugin.Logger.LogError("Message was larger than 2048 bytes, bug the author about this.");
                return;
            }

            if (result.CloseStatus != null)
            {
                DisconnectInternal();
                Disconnected?.Invoke(result.CloseStatusDescription);
                return;
            }

            string res = Encoding.UTF8.GetString(bytes, 0, result.Count);
            ObsSyncPlugin.Logger.LogDebug("Recv: " + res);

            var msg = JsonConvert.DeserializeObject<ObsMessage>(res);
            if (msg != null) HandleMessage(msg);
        }
    }

    public void Disconnect()
    {
        DisconnectInternal();
    }

    public async Task<TResponse> MakeRequestAsync<TResponse>(ObsRequest<TResponse> request)
        where TResponse : ObsRequestResponse, new()
    {
        var requestId = new ObsRequestId
        {
            Id = NextRequestId++
        };

        SendMessage(new ObsMessage
        {
            Op = ObsMessageOpcode.Request,
            Data = request.GetRequestBody(requestId.Id.ToString())
        });

        var taskCompletionSource = new TaskCompletionSource<object>();
        SentRequests[requestId.Id] = (request, taskCompletionSource);

        return (TResponse)await taskCompletionSource.Task;
    }
    
    #endregion

    #region Message handling

    private void HandleMessage(ObsMessage msg)
    {
        switch (msg.Op)
        {
            case ObsMessageOpcode.Hello:
                HandleHello(msg.Data);
                break;
            case ObsMessageOpcode.Identified:
                HandleIdentified(msg.Data);
                break;
            case ObsMessageOpcode.Event:
                HandleEvent(msg.Data);
                break;
            case ObsMessageOpcode.RequestResponse:
                HandleRequestResponse(msg.Data);
                break;
            default:
                ObsSyncPlugin.Logger.LogWarning("Received unknown or unsupported opcode from OBS websocket: " + msg.Op);
                break;
        }
    }

    private void HandleHello(JObject data)
    {
        ObsMessage response = new ObsMessage();
        response.Op = ObsMessageOpcode.Identify;
        response.Data["rpcVersion"] = 1;

        if (data.ContainsKey("authentication"))
        {
            if (string.IsNullOrEmpty(Password))
            {
                DisconnectInternal();
                Disconnected?.Invoke("A password is required and not provided");
                return;
            }

            JObject authData = data["authentication"]!.Value<JObject>()!;
            string authChallenge = authData["challenge"]!.Value<string>()!;
            string authSalt = authData["salt"]!.Value<string>()!;

            response.Data["authentication"] = CreateAuthenticationData(authChallenge, authSalt);
        }

        SendMessage(response);
    }

    private void HandleIdentified(JObject data)
    {
        // We're good to go!
        Connected?.Invoke();
    }

    private void HandleEvent(JObject data)
    {
        string eventType = data["eventType"]!.Value<string>()!;

        switch (eventType)
        {
            case "RecordStateChanged":
                RecordStateChanged?.Invoke(data["eventData"]!.ToObject<RecordStateChangedEventData>()!);
                break;
        }
    }

    private void HandleRequestResponse(JObject data)
    {
        if (!ulong.TryParse(data["requestId"]!.Value<string>(), out ulong requestId))
        {
            ObsSyncPlugin.Logger.LogWarning("Received RequestResponse with non-integer request ID");
            return;
        }

        if (!SentRequests.TryGetValue(requestId, out var thing))
        {
            ObsSyncPlugin.Logger.LogWarning("Received RequestResponse with unknown request ID");
            return;
        }

        ObsRequest sentRequest = thing.Item1;
        TaskCompletionSource<object> taskCompletionSource = thing.Item2;
        
        string responseType = data["requestType"]!.Value<string>()!;
        if (responseType != sentRequest.RequestType)
        {
            ObsSyncPlugin.Logger.LogError($"Mismatched response type for request ({sentRequest.RequestType} != {responseType})");
            return;
        }

        ObsRequestResponse response = sentRequest.MakeResponseObject();
        response.SetRequestResponseBody(data);
        taskCompletionSource.SetResult(response);
    }

    #endregion

    #region Private methods

    private string CreateAuthenticationData(string challenge, string salt)
    {
        // This should be checked before calling
        if (string.IsNullOrEmpty(Password)) throw new InvalidOperationException();

        var sha256 = SHA256.Create();

        string step1Input = Password + salt;
        var step1Bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(step1Input));
        string step1Output = Convert.ToBase64String(step1Bytes);

        string step2Input = step1Output + challenge;
        var step2Bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(step2Input));
        string step2Output = Convert.ToBase64String(step2Bytes);

        return step2Output;
    }

    private void SendMessage(ObsMessage msg)
    {
        if (WebSocket.State != WebSocketState.Open) return;

        string payload = JsonConvert.SerializeObject(msg);
        byte[] data = Encoding.UTF8.GetBytes(payload);

        ObsSyncPlugin.Logger.LogDebug("Send: " + payload);
        WebSocket.SendAsync(data, WebSocketMessageType.Text, true, default);
    }

    private async void DisconnectInternal()
    {
        if (WebSocket.State != WebSocketState.Open || WebSocket.State != WebSocketState.CloseReceived) return;

        await WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", default);
    }

    #endregion

    #region Events

    public event Action? Connected;
    public event Action<string>? Disconnected;

    public event Action<RecordStateChangedEventData>? RecordStateChanged;
    
    #endregion

    # region Internal types

    private class ObsMessage
    {
        [JsonProperty(PropertyName = "op")]
        public ObsMessageOpcode Op { get; set; }

        [JsonProperty(PropertyName = "d")]
        public JObject Data { get; set; } = new();
    }

    private enum ObsMessageOpcode
    {
        Hello = 0,
        Identify = 1,
        Identified = 2,
        ReIdentify = 3,
        Event = 5,
        Request = 6,
        RequestResponse = 7,
        RequestBatch = 8,
        RequestBatchResponse
    }

    #endregion
}