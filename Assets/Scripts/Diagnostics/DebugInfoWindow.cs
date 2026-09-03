using TMPro;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using ElementWar.Net;
using System;
using System.Globalization;
using System.IO;

namespace ElementWar
{
    /// <summary>
    /// 运行时信息窗（游戏左上角）：帧率 / 帧耗时 / 分辨率 / 场景 / 网络状态。
    /// 运行时自建 Overlay Canvas + TMP，按 F3 开关，DontDestroyOnLoad 跨场景常驻。
    /// 由 NetworkLauncher（PVP）自动确保存在；PVE 场景可在向导里挂或手动拖到场景任意物体。
    /// </summary>
    public class DebugInfoWindow : MonoBehaviour
    {
        private TextMeshProUGUI _text;
        private Canvas _canvas;
        private float _fpsSmooth = 60f;
        private float _nextUiRefresh;
        private const float UiRefreshInterval = 0.2f; // 文本/性能指标 5Hz 更新，避免诊断窗自身制造 GC
        private StreamWriter _telemetryWriter;
        private string _telemetryPath;
        private string _summaryPath;
        private readonly string _runId = Guid.NewGuid().ToString("N").Substring(0, 12);
        private DateTime _startedUtc;
        private int _sampleCount;
        private float _fpsSum;
        private float _fpsMin = float.MaxValue;
        private float _fpsMax;
        private float _maxCorrection;
        private int _maxPending;
        private int _maxTickGap;
        private int _roundNumber;
        private bool _sawMatchEnd;
        private string _lastSessionId = "";
        private float _nextTelemetryFlush;
        private NetClient _netClient;
        private ProfilerRecorder _mainThreadRecorder;
        private ProfilerRecorder _renderThreadRecorder;
        private ProfilerRecorder _gcAllocRecorder;
        private ProfilerRecorder _systemMemoryRecorder;
        private ProfilerRecorder _drawCallsRecorder;
        private ProfilerRecorder _setPassRecorder;
        private ProfilerRecorder _batchesRecorder;
        private ProfilerRecorder _trianglesRecorder;

        private void Awake()
        {
            BuildUI();
            _netClient = FindObjectOfType<NetClient>();
            _mainThreadRecorder = StartRecorder(ProfilerCategory.Internal, "Main Thread");
            _renderThreadRecorder = StartRecorder(ProfilerCategory.Internal, "Render Thread");
            _gcAllocRecorder = StartRecorder(ProfilerCategory.Memory, "GC Allocated In Frame");
            _systemMemoryRecorder = StartRecorder(ProfilerCategory.Memory, "System Used Memory");
            _drawCallsRecorder = StartRecorder(ProfilerCategory.Render, "Draw Calls Count");
            _setPassRecorder = StartRecorder(ProfilerCategory.Render, "SetPass Calls Count");
            _batchesRecorder = StartRecorder(ProfilerCategory.Render, "Batches Count");
            _trianglesRecorder = StartRecorder(ProfilerCategory.Render, "Triangles Count");
            try
            {
                _startedUtc = DateTime.UtcNow;
                string fileName = $"PVP_Performance_{DateTime.Now:yyyyMMdd_HHmmss}_{_runId}.csv";
                _telemetryPath = Path.Combine(Application.persistentDataPath, fileName);
                _summaryPath = Path.ChangeExtension(_telemetryPath, ".summary.txt");
                _telemetryWriter = new StreamWriter(_telemetryPath, false, System.Text.Encoding.UTF8);
                _telemetryWriter.WriteLine("schemaVersion,utcTime,elapsedSec,runId,serverSessionId,round,scene,frame,fps,frameMs,mainThreadMs,renderThreadMs,gcKb,systemMemoryMb,drawCalls,setPass,batches,triangles,serverTickRate,playerId,serverTick,inputTick,tickGap,snapshotSequence,snapshotLoss,outOfOrder,correctionM,hardCorrection,pendingInputs,rejectedFire,health,x,y,z,grounded,sliding,matchEnded");
                _telemetryWriter.Flush();
                Debug.Log($"[DebugInfoWindow] 性能日志：{_telemetryPath}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DebugInfoWindow] 性能日志创建失败：{e.Message}");
            }
            Debug.Log("[DebugInfoWindow] 已创建（左上角信息窗，F3 开关）");
        }

        private static ProfilerRecorder StartRecorder(ProfilerCategory category, string name)
        {
            try { return ProfilerRecorder.StartNew(category, name, 15); }
            catch { return default; }
        }

        private void OnDestroy()
        {
            _mainThreadRecorder.Dispose(); _renderThreadRecorder.Dispose();
            _gcAllocRecorder.Dispose(); _systemMemoryRecorder.Dispose();
            _drawCallsRecorder.Dispose(); _setPassRecorder.Dispose();
            _batchesRecorder.Dispose(); _trianglesRecorder.Dispose();
            try { _telemetryWriter?.Flush(); _telemetryWriter?.Dispose(); } catch { }
            WriteSummary();
        }

        private static float NanosecondsToMs(ProfilerRecorder recorder)
            => recorder.Valid ? recorder.LastValue / 1000000f : -1f;

        private static string FormatMs(float value)
            => value >= 0f ? $"{value:F2} ms" : "--";

        private static string FormatMemory(ProfilerRecorder recorder)
            => recorder.Valid ? $"{recorder.LastValue / (1024f * 1024f):F0} MB" : "--";

        private static string FormatCount(ProfilerRecorder recorder)
            => recorder.Valid ? recorder.LastValue.ToString() : "--";

        private void Update()
        {
            // F3 开关
            if (Keyboard.current != null && Keyboard.current[Key.F3].wasPressedThisFrame)
                _canvas.gameObject.SetActive(!_canvas.gameObject.activeSelf);
            if (_text == null) return;

            // 帧率（unscaled：对局结束 timeScale=0 也显示真实帧率）
            float dt = Time.unscaledDeltaTime;
            float fps = 1f / Mathf.Max(0.0001f, dt);
            _fpsSmooth = Mathf.Lerp(_fpsSmooth, fps, 0.08f);
            if (Time.unscaledTime < _nextUiRefresh) return;
            _nextUiRefresh = Time.unscaledTime + UiRefreshInterval;
            float ms = dt * 1000f;
            UpdateRoundMarker();
            WriteTelemetry(fps, ms);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"FPS: {_fpsSmooth:F0}  帧间隔: {ms:F1} ms");
            sb.AppendLine($"CPU主线程: {FormatMs(NanosecondsToMs(_mainThreadRecorder))}  渲染线程: {FormatMs(NanosecondsToMs(_renderThreadRecorder))}");
            sb.AppendLine($"GC/帧: {(_gcAllocRecorder.Valid ? _gcAllocRecorder.LastValue / 1024f : -1f):F1} KB  内存: {FormatMemory(_systemMemoryRecorder)}");
            sb.AppendLine($"DrawCall: {FormatCount(_drawCallsRecorder)}  SetPass: {FormatCount(_setPassRecorder)}  Batches: {FormatCount(_batchesRecorder)}");
            sb.AppendLine($"三角形: {FormatCount(_trianglesRecorder)}");
            sb.AppendLine($"分辨率: {Screen.width}x{Screen.height}");
            sb.AppendLine($"场景: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");

            // 网络信息（PVP 场景存在 NetClient 时附加）
            // NetClient 在 PVP 场景生命周期内不变，避免每帧 FindObjectOfType 造成额外扫描和 FPS 抖动。
            if (_netClient == null) _netClient = FindObjectOfType<NetClient>();
            var net = _netClient;
            if (net != null)
            {
                    sb.AppendLine(net.Connected
                    ? $"网络: 在线  P{net.PlayerId}  玩家 {net.Scores.Count}"
                    : "网络: 连接中…");
                if (net.Connected)
                {
                    sb.AppendLine($"RTT: {net.EstimatedRttMs:F0} ms  ServerTick: {net.LastServerTick}");
                    sb.AppendLine($"会话: {net.ServerSessionId}  轮次: {_roundNumber}  Tick率: {net.ServerTickRate}  Tick差: {net.LastServerTick - net.InputTick}");
                    sb.AppendLine($"输入Tick: {net.InputTick}  快照序号: {net.LastSnapshotSequence}");
                    sb.AppendLine($"快照丢失: {net.SnapshotLossCount}  乱序: {net.OutOfOrderSnapshotCount}");
                    sb.AppendLine($"校正: {net.LastCorrectionDistance:F3} m  大校正: {net.HardCorrectionCount}");
                    sb.AppendLine($"待确认输入: {net.PendingInputCount}  拒绝开火: {net.RejectedFireCount}");
                    if (net.LocalMotor != null)
                    {
                        var p = net.LocalMotor.transform.position;
                        sb.AppendLine($"本地: ({p.x:F1},{p.y:F1},{p.z:F1})  地面:{net.LocalMotor.IsGrounded} 滑铲:{net.LocalMotor.IsSliding}");
                    }
                }
            }
            if (!string.IsNullOrEmpty(_telemetryPath))
            {
                sb.AppendLine($"运行: {_runId}  日志: {Path.GetFileName(_telemetryPath)}");
                sb.AppendLine($"摘要: {Path.GetFileName(_summaryPath)}");
            }
            _text.text = sb.ToString();
        }

        private void WriteTelemetry(float fps, float frameMs)
        {
            if (_telemetryWriter == null) return;
            var net = _netClient;
            var motor = net != null ? net.LocalMotor : null;
            Vector3 pos = motor != null ? motor.transform.position : Vector3.zero;
            string F(float value) => value.ToString("F4", CultureInfo.InvariantCulture);
            string I(long value) => value.ToString(CultureInfo.InvariantCulture);
            int tickGap = net != null ? net.LastServerTick - net.InputTick : -1;
            string sessionId = net != null ? net.ServerSessionId : "";
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            _telemetryWriter.WriteLine(string.Join(",", new[]
            {
                "2", Csv(DateTime.UtcNow.ToString("O")), F(Time.unscaledTime), Csv(_runId), Csv(sessionId), I(_roundNumber), Csv(scene), I(Time.frameCount), F(fps), F(frameMs),
                F(NanosecondsToMs(_mainThreadRecorder)), F(NanosecondsToMs(_renderThreadRecorder)),
                _gcAllocRecorder.Valid ? F(_gcAllocRecorder.LastValue / 1024f) : "-1",
                _systemMemoryRecorder.Valid ? F(_systemMemoryRecorder.LastValue / (1024f * 1024f)) : "-1",
                FormatCount(_drawCallsRecorder), FormatCount(_setPassRecorder), FormatCount(_batchesRecorder), FormatCount(_trianglesRecorder),
                net != null ? I(net.ServerTickRate) : "-1", net != null ? I(net.PlayerId) : "-1", net != null ? I(net.LastServerTick) : "-1", net != null ? I(net.InputTick) : "-1", I(tickGap),
                net != null ? I(net.LastSnapshotSequence) : "-1", net != null ? I(net.SnapshotLossCount) : "-1", net != null ? I(net.OutOfOrderSnapshotCount) : "-1",
                net != null ? F(net.LastCorrectionDistance) : "-1", net != null ? I(net.HardCorrectionCount) : "-1", net != null ? I(net.PendingInputCount) : "-1", net != null ? I(net.RejectedFireCount) : "-1",
                net != null ? I(net.LocalHealth) : "-1", F(pos.x), F(pos.y), F(pos.z), motor != null ? (motor.IsGrounded ? "1" : "0") : "-1", motor != null ? (motor.IsSliding ? "1" : "0") : "-1", net != null && net.MatchEnded ? "1" : "0"
            }));
            _sampleCount++; _fpsSum += fps; _fpsMin = Mathf.Min(_fpsMin, fps); _fpsMax = Mathf.Max(_fpsMax, fps);
            if (net != null) { _maxCorrection = Mathf.Max(_maxCorrection, net.LastCorrectionDistance); _maxPending = Mathf.Max(_maxPending, net.PendingInputCount); _maxTickGap = Mathf.Max(_maxTickGap, Mathf.Abs(tickGap)); }
            if (Time.unscaledTime >= _nextTelemetryFlush)
            {
                _telemetryWriter.Flush();
                WriteSummary();
                _nextTelemetryFlush = Time.unscaledTime + 1f;
            }
        }

        private static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }

        private void WriteSummary()
        {
            if (string.IsNullOrEmpty(_summaryPath)) return;
            try
            {
                var net = _netClient;
                float avg = _sampleCount > 0 ? _fpsSum / _sampleCount : 0f;
                File.WriteAllText(_summaryPath,
                    "ElementWar PVP 性能日志摘要\n" +
                    "schemaVersion=2\n" +
                    $"runId={_runId}\nserverSessionId={(net != null ? net.ServerSessionId : "")}\nplayerId={(net != null ? net.PlayerId : -1)}\n" +
                    $"scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}\nstartedUtc={_startedUtc:O}\nendedUtc={DateTime.UtcNow:O}\n" +
                    $"rounds={_roundNumber}\nsamples={_sampleCount}\nfps.avg={avg:F1}\nfps.min={(_sampleCount > 0 ? _fpsMin : 0):F1}\nfps.max={_fpsMax:F1}\n" +
                    $"maxCorrectionM={_maxCorrection:F3}\nmaxPendingInputs={_maxPending}\nmaxAbsTickGap={_maxTickGap}\n" +
                    $"snapshotLoss={(net != null ? net.SnapshotLossCount : -1)}\noutOfOrder={(net != null ? net.OutOfOrderSnapshotCount : -1)}\nhardCorrections={(net != null ? net.HardCorrectionCount : -1)}\n");
            }
            catch (Exception e) { Debug.LogWarning($"[DebugInfoWindow] 摘要写入失败：{e.Message}"); }
        }

        private void UpdateRoundMarker()
        {
            var net = _netClient;
            if (net == null || !net.Connected) return;
            string session = net.ServerSessionId ?? "";
            if (_roundNumber == 0) _roundNumber = 1;
            if (!string.IsNullOrEmpty(session) && !string.IsNullOrEmpty(_lastSessionId) && session != _lastSessionId)
            {
                _roundNumber = 1;
                _sawMatchEnd = false;
            }
            if (_sawMatchEnd && !net.MatchEnded)
            {
                _roundNumber++;
                _sawMatchEnd = false;
            }
            if (net.MatchEnded) _sawMatchEnd = true;
            _lastSessionId = session;
        }

        private void BuildUI()
        {
            // 与 PVPHealthUI 相同的画布模式（它能正常显示）：Canvas 作为本物体子物体，ScreenSpaceOverlay
            var canvasGo = new GameObject("DebugInfoCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 999;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // 半透明深色底：保证在任何背景上都清晰可见
            var bgGo = new GameObject("InfoBG", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 1f);
            bgRt.anchorMax = new Vector2(0f, 1f);
            bgRt.pivot = new Vector2(0f, 1f);
            bgRt.anchoredPosition = new Vector2(10, -8);
            bgRt.sizeDelta = new Vector2(760, 390);
            bgGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var textGo = new GameObject("InfoText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(canvasGo.transform, false);
            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(16, -14);
            rt.sizeDelta = new Vector2(780, 400);

            _text = textGo.GetComponent<TextMeshProUGUI>();
            _text.fontSize = 20;
            _text.color = new Color(1f, 1f, 1f, 1f);
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.enableWordWrapping = false;
            _text.text = "FPS: --\n分辨率: --\n场景: --";
        }
    }
}
