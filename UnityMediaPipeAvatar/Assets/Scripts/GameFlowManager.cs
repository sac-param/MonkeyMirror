using System.Collections;
using UnityEngine;
using TMPro;

public class GameFlowManager : MonoBehaviour
{
    [Header("Required References")]
    public PipeServer pipeServer;
    public GameObject monkey;
    public TextMeshProUGUI timerText;

    [Header("Screens — assign images here")]
    public GameObject defaultScreen;    // idle screen
    public GameObject waveScreen;       // wave to begin (cyberpunk bg)
    public GameObject danceScreen;      // dance image
    public GameObject jumpScreen;       // jump image
    public GameObject poseScreen;       // pose image
    public GameObject thankYouScreen;   // thank you image

    [Header("Raise Hands Detection")]
    public float raiseHandsThreshold = 0.5f;
    public float raiseHandsHoldTime = 1.0f;

    [Header("Timings (seconds)")]
    public float defaultScreenTime = 5f;
    public float danceTime = 10f;
    public float jumpTime = 10f;
    public float signaturePoseCountdown = 3f;
    public float thankYouTime = 5f;

    [Header("Monkey Appear Animation")]
    public float appearDuration = 1.5f;

    private enum FlowState { Idle, WaitingForHandsRaise, Running }
    private FlowState _state = FlowState.Idle;
    private float _handsRaisedTimer = 0f;
    private bool _flowStarted = false;
    private Renderer[] _monkeyRenderers;

    private void Start()
    {
        _monkeyRenderers = monkey.GetComponentsInChildren<Renderer>();
        SetMonkeyVisible(false);
        SetTimer("");
        ShowScreen(defaultScreen);
        StartCoroutine(IntroSequence());
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

        if (screen != null) screen.SetActive(true);
    }

    // ─── Flow ─────────────────────────────────────────────────

    private IEnumerator IntroSequence()
    {
        ShowScreen(defaultScreen);
        yield return new WaitForSeconds(defaultScreenTime);
        ShowScreen(waveScreen);
        _state = FlowState.WaitingForHandsRaise;
    }

    private void Update()
    {
        if (_state == FlowState.WaitingForHandsRaise)
            CheckHandsRaised();

        // TEMP: Space to test
        if (Input.GetKeyDown(KeyCode.Space) && !_flowStarted)
        {
            _flowStarted = true;
            _state = FlowState.Running;
            ShowScreen(waveScreen);
            StartCoroutine(RunFlow());
        }
    }

    private void CheckHandsRaised()
    {
        if (pipeServer == null) return;

        Transform leftWrist    = pipeServer.GetLandmark(Landmark.LEFT_WRIST);
        Transform rightWrist   = pipeServer.GetLandmark(Landmark.RIGHT_WRIST);
        Transform leftShoulder = pipeServer.GetLandmark(Landmark.LEFT_SHOULDER);
        Transform rightShoulder= pipeServer.GetLandmark(Landmark.RIGHT_SHOULDER);

        if (leftWrist == null || rightWrist == null) return;

        bool leftRaised  = leftWrist.position.y  > leftShoulder.position.y  + raiseHandsThreshold;
        bool rightRaised = rightWrist.position.y > rightShoulder.position.y + raiseHandsThreshold;

        if (leftRaised && rightRaised)
        {
            _handsRaisedTimer += Time.deltaTime;
            if (_handsRaisedTimer >= raiseHandsHoldTime && !_flowStarted)
            {
                _flowStarted = true;
                _state = FlowState.Running;
                StartCoroutine(RunFlow());
            }
        }
        else
        {
            _handsRaisedTimer = 0f;
        }
    }

    private IEnumerator RunFlow()
    {
        // Monkey appears on wave screen
        yield return StartCoroutine(ShowMonkey());
        yield return new WaitForSeconds(0.5f);

        // Dance
        yield return RunTimedPhase(danceScreen, danceTime);

        // Jump
        yield return RunTimedPhase(jumpScreen, jumpTime);

        // Pose countdown
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

        // Thank you
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
        string fullPath = System.IO.Path.Combine(folder, filename)
                              .Replace("/", "\\");
        ScreenCapture.CaptureScreenshot(fullPath);
        Debug.Log("Screenshot saved: " + fullPath);
    }

    // ─── Restart ──────────────────────────────────────────────

    private void Restart()
    {
        _flowStarted = false;
        _handsRaisedTimer = 0f;
        _state = FlowState.Idle;
        SetMonkeyVisible(false);
        monkey.transform.localScale = new Vector3(2f, 2f, 2f);
        SetTimer("");
        ShowScreen(defaultScreen);
        StartCoroutine(IntroSequence());
    }

    private void SetTimer(string t) { if (timerText != null) timerText.text = t; }
}