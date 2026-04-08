using System.Collections;
using UnityEngine;
using TMPro;

public class GameFlowManager : MonoBehaviour
{
    [Header("Required References")]
    public PipeServer pipeServer;
    public GameObject monkey;
    public TextMeshProUGUI messageText;
    public TextMeshProUGUI timerText;

    [Header("Raise Hands Detection")]
    public float raiseHandsThreshold = 0.5f;
    public float raiseHandsHoldTime = 1.0f;

    [Header("Timings (seconds)")]
    public float danceTime = 10f;
    public float jumpTime = 10f;
    public float signaturePoseCountdown = 3f;
    public float thankYouTime = 5f;

    [Header("Monkey Appear Animation")]
    public float appearDuration = 1.5f;

    private enum FlowState { WaitingForHandsRaise, Running }
    private FlowState _state = FlowState.WaitingForHandsRaise;
    private float _handsRaisedTimer = 0f;
    private bool _flowStarted = false;

    // Store monkey's renderer to show/hide visually without moving it
    private Renderer[] _monkeyRenderers;
    private Avatar _avatarScript;

    private void Start()
    {
        // Get all renderers on monkey (to fade in/out visually)
        _monkeyRenderers = monkey.GetComponentsInChildren<Renderer>();
        _avatarScript = monkey.GetComponent<Avatar>();

        // Hide monkey visually but keep it active so Avatar script works
        SetMonkeyVisible(false);

        ShowMessage("Raise both hands to begin!");
        SetTimer("");
    }

    private void Update()
    {
        if (_state == FlowState.WaitingForHandsRaise)
            CheckHandsRaised();
    }

    private void CheckHandsRaised()
    {
        if (pipeServer == null) return;

        Transform leftWrist = pipeServer.GetLandmark(Landmark.LEFT_WRIST);
        Transform rightWrist = pipeServer.GetLandmark(Landmark.RIGHT_WRIST);
        Transform leftShoulder = pipeServer.GetLandmark(Landmark.LEFT_SHOULDER);
        Transform rightShoulder = pipeServer.GetLandmark(Landmark.RIGHT_SHOULDER);

        if (leftWrist == null || rightWrist == null) return;

        bool leftRaised = leftWrist.position.y > leftShoulder.position.y + raiseHandsThreshold;
        bool rightRaised = rightWrist.position.y > rightShoulder.position.y + raiseHandsThreshold;

        if (leftRaised && rightRaised)
        {
            _handsRaisedTimer += Time.deltaTime;
            int dots = Mathf.Clamp((int)(_handsRaisedTimer / raiseHandsHoldTime * 3) + 1, 1, 3);
            ShowMessage("Hold it" + new string('.', dots));

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
            ShowMessage("Raise both hands to begin!");
        }
    }

    private IEnumerator RunFlow()
    {
        // Step 1 — Monkey fades/slides in
        ShowMessage("Welcome!");
        SetTimer("");
        yield return StartCoroutine(ShowMonkey());

        yield return new WaitForSeconds(0.5f);

        // Step 2 — Dance
        yield return RunTimedPhase("Teach me how to dance!", danceTime);

        // Step 3 — Jump
        yield return RunTimedPhase("Teach me how to jump!", jumpTime);

        // Step 4 — Signature pose
        ShowMessage("Do your signature pose!");
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
        ShowMessage("Pose captured! Great job!");
        SetTimer("");
        yield return new WaitForSeconds(2.5f);

        // Step 5 — Thank you
        ShowMessage("Thank you! See you next time!");
        SetTimer("");
        yield return StartCoroutine(HideMonkey());
        yield return new WaitForSeconds(thankYouTime);

        Restart();
    }

    private IEnumerator ShowMonkey()
    {
        // Monkey is already tracking — just reveal it visually
        SetMonkeyVisible(true);

        // Optional: scale up from 0 for a pop-in effect
        monkey.transform.localScale = Vector3.zero;
        float elapsed = 0f;
        Vector3 targetScale = new Vector3(4f, 4f, 4f); // match your monkey scale

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
        monkey.transform.localScale = new Vector3(4f, 4f, 4f);
    }

    private void SetMonkeyVisible(bool visible)
    {
        foreach (var r in _monkeyRenderers)
            r.enabled = visible;
    }

    private IEnumerator RunTimedPhase(string message, float duration)
    {
        ShowMessage(message);
        float remaining = duration;
        while (remaining > 0f)
        {
            SetTimer(Mathf.CeilToInt(remaining).ToString());
            remaining -= Time.deltaTime;
            yield return null;
        }
        SetTimer("");
    }

    private void TakeScreenshot()
    {
        string folder = System.IO.Path.Combine(Application.persistentDataPath, "Screenshots");
        System.IO.Directory.CreateDirectory(folder);
        string filename = "Pose_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
        string fullPath = System.IO.Path.Combine(folder, filename);
        ScreenCapture.CaptureScreenshot(fullPath);
        Debug.Log("Screenshot saved: " + fullPath);
    }

    private void Restart()
    {
        _flowStarted = false;
        _handsRaisedTimer = 0f;
        _state = FlowState.WaitingForHandsRaise;
        SetMonkeyVisible(false);
        monkey.transform.localScale = new Vector3(4f, 4f, 4f);
        ShowMessage("Raise both hands to begin!");
        SetTimer("");
    }

    private void ShowMessage(string msg) { if (messageText != null) messageText.text = msg; }
    private void SetTimer(string t) { if (timerText != null) timerText.text = t; }
}