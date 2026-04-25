using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Security.Cryptography;
using UnityEngine;
using TMPro;

public class GameFlowManager : MonoBehaviour
{
    [Header("Required References")]
    public PipeServer pipeServer;
    public GameObject monkey;
    public TextMeshProUGUI timerText;

    [Header("Screens — assign images here")]
    public GameObject defaultScreen;
    public GameObject waveScreen;
    public GameObject danceScreen;
    public GameObject jumpScreen;
    public GameObject poseScreen;
    public GameObject thankYouScreen;

    [Header("WebSocket Settings")]
    public int socketPort = 5005;

    [Header("Wave Detection")]
    public float waveMovementThreshold = 0.08f;
    public float waveCycleTime = 1.5f;
    public int waveCyclesRequired = 2;

    [Header("Timings (seconds)")]
    public float danceTime = 10f;
    public float jumpTime = 10f;
    public float signaturePoseCountdown = 3f;
    public float thankYouTime = 5f;

    [Header("Monkey Appear Animation")]
    public float appearDuration = 1.5f;

    [Header("Transition")]
    public GameObject getReadyScreen;
    public float transitionTime = 3f;

    private enum FlowState { Idle, WaitingForWave, Running }
    private FlowState _state = FlowState.Idle;
    private bool _flowStarted = false;
    private Renderer[] _monkeyRenderers;

    // Wave detection state
    private float _waveTimer = 0f;
    private int _waveCycleCount = 0;
    private float _lastWristX = 0f;
    private bool _movingRight = false;

    // Optional: uncomment _handsRaisedTimer to use for testing raise-hands logic
    // private float _handsRaisedTimer = 0f;

    // WebSocket
    private TcpListener _tcpListener;
    private Thread _socketThread;
    private volatile bool _startReceived = false;
    private volatile bool _appRunning = true;

    // Connected WebSocket clients (to send back screenshot path)
    private List<NetworkStream> _connectedStreams = new List<NetworkStream>();
    private readonly object _streamLock = new object();

    // ─── Start ────────────────────────────────────────────────

    private void Start()
    {
        _monkeyRenderers = monkey.GetComponentsInChildren<Renderer>();
        SetMonkeyVisible(false);
        SetTimer("");
        ShowScreen(defaultScreen);

        _socketThread = new Thread(ListenForWebSocket);
        _socketThread.IsBackground = true;
        _socketThread.Start();

        Debug.Log($"[WebSocket] Server started on port {socketPort}.");
    }

    // ─── WebSocket Server (background thread) ─────────────────

    private void ListenForWebSocket()
    {
        try
        {
            _tcpListener = new TcpListener(IPAddress.Any, socketPort);
            _tcpListener.Start();

            while (_appRunning)
            {
                if (!_tcpListener.Pending()) { Thread.Sleep(50); continue; }

                TcpClient client = _tcpListener.AcceptTcpClient();
                Thread clientThread = new Thread(() => HandleClient(client));
                clientThread.IsBackground = true;
                clientThread.Start();
            }
        }
        catch (System.Exception e)
        {
            if (_appRunning) Debug.LogError($"[WebSocket] Listener error: {e.Message}");
        }
    }

    private void HandleClient(TcpClient client)
    {
        NetworkStream stream = null;
        bool isWebSocket = false;

        try
        {
            stream = client.GetStream();

            // ── WebSocket Handshake ──
            byte[] buffer = new byte[4096];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            if (!request.Contains("Upgrade: websocket"))
            {
                // Plain TCP fallback
                string msg = request.Trim().ToLower();
                Debug.Log($"[Socket] Plain TCP received: '{msg}'");
                if (msg == "start") _startReceived = true;
                client.Close();
                return;
            }

            isWebSocket = true;

            // Extract key and build accept
            string key = "";
            foreach (string line in request.Split('\n'))
            {
                if (line.StartsWith("Sec-WebSocket-Key:"))
                {
                    key = line.Replace("Sec-WebSocket-Key:", "").Trim();
                    break;
                }
            }

            string acceptKey = Convert.ToBase64String(
                SHA1.Create().ComputeHash(
                    Encoding.UTF8.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")
                )
            );

            string response =
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                $"Sec-WebSocket-Accept: {acceptKey}\r\n\r\n";

            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
            stream.Write(responseBytes, 0, responseBytes.Length);

            Debug.Log("[WebSocket] TouchDesigner connected!");

            // Keep stream so we can send back screenshot path
            lock (_streamLock) { _connectedStreams.Add(stream); }

            // ── Read Frames ──
            while (_appRunning && client.Connected)
            {
                if (!stream.DataAvailable) { Thread.Sleep(10); continue; }

                byte[] frameBuffer = new byte[256];
                int read = stream.Read(frameBuffer, 0, frameBuffer.Length);
                if (read < 2) continue;

                bool masked = (frameBuffer[1] & 0x80) != 0;
                int payloadLen = frameBuffer[1] & 0x7F;
                int offset = 2;

                if (payloadLen == 126) offset += 2;
                else if (payloadLen == 127) offset += 8;

                byte[] maskKey = new byte[4];
                if (masked)
                {
                    System.Array.Copy(frameBuffer, offset, maskKey, 0, 4);
                    offset += 4;
                }

                byte[] payload = new byte[payloadLen];
                for (int i = 0; i < payloadLen; i++)
                {
                    payload[i] = masked
                        ? (byte)(frameBuffer[offset + i] ^ maskKey[i % 4])
                        : frameBuffer[offset + i];
                }

                string message = Encoding.UTF8.GetString(payload).Trim().ToLower();
                Debug.Log($"[WebSocket] Received: '{message}'");

                if (message == "start") _startReceived = true;
            }
        }
        catch (System.Exception e)
        {
            if (_appRunning) Debug.Log($"[WebSocket] Client disconnected: {e.Message}");
        }
        finally
        {
            if (isWebSocket && stream != null)
                lock (_streamLock) { _connectedStreams.Remove(stream); }
            client.Close();
        }
    }

    // ── Send WebSocket text frame back to TD ──
    private void SendWebSocketMessage(string message)
    {
        byte[] payload = Encoding.UTF8.GetBytes(message);
        byte[] frame = new byte[2 + payload.Length];
        frame[0] = 0x81; // FIN + text opcode
        frame[1] = (byte)payload.Length;
        System.Array.Copy(payload, 0, frame, 2, payload.Length);

        lock (_streamLock)
        {
            List<NetworkStream> toRemove = new List<NetworkStream>();
            foreach (NetworkStream s in _connectedStreams)
            {
                try { s.Write(frame, 0, frame.Length); }
                catch { toRemove.Add(s); }
            }
            foreach (NetworkStream s in toRemove) _connectedStreams.Remove(s);
        }
    }

    // ─── Update ───────────────────────────────────────────────

    private void Update()
    {
        if (_startReceived && _state == FlowState.Idle)
        {
            _startReceived = false;
            Debug.Log("[WebSocket] 'start' received → moving to wave screen");
            ShowScreen(waveScreen);
            _state = FlowState.WaitingForWave;
        }

        if (_state == FlowState.WaitingForWave)
            CheckWave();

        // TEMP: S key to simulate 'start' for testing
        if (Input.GetKeyDown(KeyCode.S) && _state == FlowState.Idle)
        {
            ShowScreen(waveScreen);
            _state = FlowState.WaitingForWave;
        }

        // TEMP: Space to trigger full flow directly
        if (Input.GetKeyDown(KeyCode.Space) && !_flowStarted)
        {
            _flowStarted = true;
            _state = FlowState.Running;
            StartCoroutine(RunFlow());
        }
    }

    // ─── Wave Detection ───────────────────────────────────────

    private void CheckWave()
    {
        if (pipeServer == null) return;

        Transform leftWrist = pipeServer.GetLandmark(Landmark.LEFT_WRIST);
        Transform rightWrist = pipeServer.GetLandmark(Landmark.RIGHT_WRIST);

        if (leftWrist == null && rightWrist == null) return;

        Transform wrist = leftWrist ?? rightWrist;
        float currentX = wrist.position.x;

        _waveTimer += Time.deltaTime;
        if (_waveTimer > waveCycleTime)
        {
            _waveTimer = 0f;
            _waveCycleCount = 0;
            _lastWristX = currentX;
            _movingRight = currentX > _lastWristX;
            return;
        }

        float delta = currentX - _lastWristX;

        if (_movingRight && delta < -waveMovementThreshold)
        {
            _waveCycleCount++;
            _movingRight = false;
            _lastWristX = currentX;
            _waveTimer = 0f;
        }
        else if (!_movingRight && delta > waveMovementThreshold)
        {
            _waveCycleCount++;
            _movingRight = true;
            _lastWristX = currentX;
            _waveTimer = 0f;
        }

        if (_waveCycleCount >= waveCyclesRequired && !_flowStarted)
        {
            _flowStarted = true;
            _state = FlowState.Running;
            StartCoroutine(RunFlow());
        }
    }

    // ─── Screen Helper ────────────────────────────────────────

    private void ShowScreen(GameObject screen)
    {
        defaultScreen?.SetActive(false);
        waveScreen?.SetActive(false);
        danceScreen?.SetActive(false);
        jumpScreen?.SetActive(false);
        poseScreen?.SetActive(false);
        thankYouScreen?.SetActive(false);
        getReadyScreen?.SetActive(false);

        if (screen != null) screen.SetActive(true);
    }

    // ─── Flow ─────────────────────────────────────────────────

    private IEnumerator RunFlow()
    {
        yield return StartCoroutine(ShowMonkey());
        yield return new WaitForSeconds(0.5f);

        yield return RunTimedPhase(danceScreen, danceTime);

        yield return StartCoroutine(TransitionToNextPhase());
        yield return RunTimedPhase(jumpScreen, jumpTime);

        yield return StartCoroutine(TransitionToNextPhase());
        ShowScreen(poseScreen);
        SetTimer("");
        yield return new WaitForSeconds(1.5f);

        for (int i = (int)signaturePoseCountdown; i >= 1; i--)
        {
            SetTimer(i.ToString());
            yield return new WaitForSeconds(1f);
        }

        SetTimer("");
        TakeScreenshot();
        yield return new WaitForSeconds(0.3f);

        ShowScreen(thankYouScreen);
        yield return StartCoroutine(HideMonkey());
        yield return new WaitForSeconds(thankYouTime);

        Restart();
    }

    private IEnumerator RunTimedPhase(GameObject screen, float duration)
    {
        ShowScreen(screen);
        float remaining = duration;
        while (remaining > 0f)
        {
            SetTimer(Mathf.CeilToInt(remaining).ToString());
            remaining -= Time.deltaTime;
            yield return null;
        }
        SetTimer("");
    }

    private IEnumerator TransitionToNextPhase()
    {
        yield return StartCoroutine(HideMonkey());
        ShowScreen(getReadyScreen);
        yield return new WaitForSeconds(transitionTime);
        yield return StartCoroutine(ShowMonkey());
    }

    // ─── Monkey ───────────────────────────────────────────────

    private IEnumerator ShowMonkey()
    {
        SetMonkeyVisible(true);
        monkey.transform.localScale = Vector3.zero;

        GlitchEffect glitch = monkey.GetComponent<GlitchEffect>();
        if (glitch != null) StartCoroutine(glitch.PlayGlitch());

        yield return new WaitForSeconds(0.3f);

        float elapsed = 0f;
        Vector3 targetScale = new Vector3(2f, 2f, 2f);
        while (elapsed < appearDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / appearDuration);
            monkey.transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, t);
            yield return null;
        }
        monkey.transform.localScale = targetScale;
    }

    private IEnumerator HideMonkey()
    {
        Vector3 startScale = monkey.transform.localScale;
        float elapsed = 0f;
        while (elapsed < appearDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / appearDuration);
            monkey.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }
        SetMonkeyVisible(false);
        monkey.transform.localScale = new Vector3(2f, 2f, 2f);
    }

    private void SetMonkeyVisible(bool visible)
    {
        foreach (var r in _monkeyRenderers) r.enabled = visible;
    }

    // ─── Screenshot ───────────────────────────────────────────

    private void TakeScreenshot()
    {
        string folder = System.IO.Path.Combine(
            Application.persistentDataPath.Trim(), "Screenshots");
        System.IO.Directory.CreateDirectory(folder);
        string filename = "Pose_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
        string fullPath = System.IO.Path.Combine(folder, filename).Replace("/", "\\");
        ScreenCapture.CaptureScreenshot(fullPath);
        Debug.Log("Screenshot saved: " + fullPath);

        // Send path back to TouchDesigner via same WebSocket connection
        SendWebSocketMessage(fullPath);
        Debug.Log($"[WebSocket] Emitted screenshot path to TD: {fullPath}");
    }

    // ─── Restart ──────────────────────────────────────────────

    private void Restart()
    {
        _flowStarted = false;
        _waveTimer = 0f;
        _waveCycleCount = 0;
        // _handsRaisedTimer = 0f; // uncomment if using hands raise timer
        _state = FlowState.Idle;
        SetMonkeyVisible(false);
        monkey.transform.localScale = new Vector3(2f, 2f, 2f);
        SetTimer("");
        ShowScreen(defaultScreen);
    }

    // ─── Cleanup ──────────────────────────────────────────────

    private void OnDestroy()
    {
        _appRunning = false;
        _tcpListener?.Stop();
        _socketThread?.Abort();
    }

    private void SetTimer(string t) { if (timerText != null) timerText.text = t; }
}