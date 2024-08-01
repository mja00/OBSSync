using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using OBSSync.Websocket;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.InputSystem;

namespace OBSSync;

[BepInAutoPlugin]
public partial class ObsSyncPlugin : BaseUnityPlugin
{
    public static ObsSyncPlugin Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger { get; private set; } = null!;
    internal static Harmony? Harmony { get; set; }

    private readonly ObsWebsocket _obs = new();

    private SelectableLevel? _lastPlayedLevel;

    private string? LastPlanetName
    {
        get
        {
            if (_lastPlayedLevel == null) return null;
            int spaceIdx = _lastPlayedLevel.PlanetName.IndexOf(' ');
            return _lastPlayedLevel.PlanetName.Substring(spaceIdx + 1);
        }
    }

    private StreamWriter? _currentTimestampLog;
    private DateTime _currentLogStart;

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;
        Harmony = Harmony.CreateAndPatchAll(typeof(Patches), Id);

        BuildConfig();

        InitializeObsClient();
    }

    private void OnDestroy()
    {
        _currentTimestampLog?.Close();
    }

    private void Update()
    {
        if (Keyboard.current[_configManualEventKey.Value].wasPressedThisFrame)
            WriteTimestamppedEvent("Manual event");
    }

    private void InitializeObsClient()
    {
        _obs.Connected += ObsConnected;
        _obs.Disconnected += ObsDisconnected;

        _obs.RecordStateChanged += data =>
        {
            if (data.OutputState != RecordStateChangedEventData.StateStarted) return;

            string directory = Path.GetDirectoryName(data.OutputPath)!;
            string filename = Path.GetFileNameWithoutExtension(data.OutputPath)!;
            string timestampLogPath = Path.Combine(directory, filename + ".txt");

            _currentTimestampLog = new StreamWriter(timestampLogPath);
            _currentTimestampLog.AutoFlush = true;
            _currentLogStart = DateTime.Now;
        };

        DoConnect();
    }

    private void DoConnect()
    {
        try
        {
            _obs.Connect("ws://" + _configObsWebsocketAddress.Value, _configObsWebsocketPassword.Value);
        }
        catch (Exception e)
        {
            Logger.LogError(e);
        }
    }

    private void ObsConnected()
    {
        Logger.LogInfo("Connected to OBS websocket!");
    }

    private void ObsDisconnected(string reason)
    {
        Logger.LogWarning("Disconnected from OBS websocket: " + reason + ". Retrying in 5 seconds...");

        Task.Run(async () =>
        {
            await Task.Delay(5000);
            DoConnect();
        });
    }

    private void RenameLogFile(string outputFilePath, string filenameSuffix)
    {
        // Rename the output file
        string filePath = Path.GetDirectoryName(outputFilePath)!;
        string fileExtension = Path.GetExtension(outputFilePath);
        string fileName = Path.GetFileNameWithoutExtension(outputFilePath);
        string newFilePath = Path.Combine(filePath, fileName + "_" + filenameSuffix + fileExtension);
        File.Move(outputFilePath, newFilePath);

        // Also rename our timestamp log file
        if (_currentTimestampLog != null)
        {
            _currentTimestampLog.Close();
            _currentTimestampLog = null;
            string oldPath = Path.Combine(filePath, fileName + ".txt");
            string newPath = Path.Combine(filePath, fileName + "_" + filenameSuffix + ".txt");
            File.Move(oldPath, newPath);
        }
    }

    public async void StartRecording()
    {
        await _obs.MakeRequestAsync(new StartRecordRequest());
    }

    public async void StopRecording(string filenameSuffix)
    {
        var stopResponse = await _obs.MakeRequestAsync(new StopRecordRequest());

        // If the stop request was successful
        if (stopResponse.Status.Success)
        {
            // Wait for it to fully stop
            _obs.RecordStateChanged += WaitForRecordingStopped;

            void WaitForRecordingStopped(RecordStateChangedEventData e)
            {
                if (e.OutputState != RecordStateChangedEventData.StateStopped) return;
                _obs.RecordStateChanged -= WaitForRecordingStopped;

                RenameLogFile(e.OutputPath!, filenameSuffix);
            }
        }
    }

    public async Task SplitRecording(string filenameSuffix)
    {
        var stopResponse = await _obs.MakeRequestAsync(new StopRecordRequest());

        // If the stop request was successful
        if (stopResponse.Status.Success)
        {
            SemaphoreSlim waitHandle = new SemaphoreSlim(0, 1);
            _obs.RecordStateChanged += WaitForRecordingStopped;

            // Block until we get confirmation it's actually stopped
            await waitHandle.WaitAsync();

            async void WaitForRecordingStopped(RecordStateChangedEventData e)
            {
                // We'll hit this first as it stops
                if (e.OutputState == RecordStateChangedEventData.StateStopped)
                {
                    RenameLogFile(e.OutputPath!, filenameSuffix);

                    // Start the recording again
                    await Task.Delay(150);
                    await _obs.MakeRequestAsync(new StartRecordRequest());
                }
                
                // Then we'll hit this as it starts again
                else if (e.OutputState == RecordStateChangedEventData.StateStarted)
                {
                    _obs.RecordStateChanged -= WaitForRecordingStopped;
                    waitHandle.Release();
                }
            }

            // Let other tasks complete first
            await Task.Yield();
        }
        else
        {
            // Stop failed - maybe we weren't recording?
            // Just start a new recording
            await _obs.MakeRequestAsync(new StartRecordRequest());
        }
    }

    #region Callbacks

    internal void JoinedGame()
    {
        _lastPlayedLevel = null;

        if (_configAutoStartStop.Value)
        {
            StartRecording();
        }
    }

    internal void LeftGame()
    {
        if (_configAutoStartStop.Value)
        {
            string suffix = LastPlanetName ?? "end";
            StopRecording(suffix);
        }
    }

    internal async void RoundStarting()
    {
        if (_configAutoSplit.Value)
        {
            string suffix = LastPlanetName ?? "start";
            await SplitRecording(suffix);
        }
        else
        {
            WriteTimestamppedEvent("");
        }

        _lastPlayedLevel = StartOfRound.Instance.currentLevel;
        WriteTimestamppedEvent($"Landing on {LastPlanetName}");
    }

    internal void RoundFinished()
    {
        WriteTimestamppedEvent("Left moon, now in orbit");
    }

    internal void WriteTimestamppedEvent(string what)
    {
        TimeSpan time = DateTime.Now - _currentLogStart;
        string logEntry = $"[{time:hh':'mm':'ss}] {what}";
        Logger.LogDebug(logEntry);

        if (_currentTimestampLog == null)
        {
            Logger.LogWarning("Trying to write stuff but no logfile is opened... start a recording!");
        }
        else
        {
            if (string.IsNullOrEmpty(what)) _currentTimestampLog.WriteLine();
            else _currentTimestampLog.WriteLine(logEntry);
        }
    }

    #endregion

    #region Config

    private ConfigEntry<string> _configObsWebsocketAddress = null!;
    private ConfigEntry<string> _configObsWebsocketPassword = null!;

    private ConfigEntry<bool> _configAutoStartStop = null!;
    private ConfigEntry<bool> _configAutoSplit = null!;
    private ConfigEntry<Key> _configManualEventKey = null!;

    private void BuildConfig()
    {
        _configObsWebsocketAddress = Config.Bind("Connection", "WebsocketAddress", "[::1]:4455", "IP address / port of the computer running OBS");
        _configObsWebsocketPassword = Config.Bind("Connection", "WebsocketPassword", "", "Password to connect to OBS's websocket");

        _configAutoStartStop = Config.Bind("Recording", "AutoStartStop", true, "Automatically start recording when you start or join a game and automatically stop recording when you leave a game.");
        _configAutoSplit = Config.Bind("Recording", "AutoSplit", true, "Automatically stop and start a new recording between moons");
        _configManualEventKey = Config.Bind("Recording", "ManualEventKey", Key.PageUp, "The key that will add a manual event into the timestamp log");
    }

    #endregion
}