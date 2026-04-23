using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Avatar : MonoBehaviour
{
    public Camera previewCamera;
    public Animator animator;
    public LayerMask ground;
    public bool footTracking = true;
    public float footGroundOffset = .1f;

    [Header("Smoothing")]
    [Range(1f, 20f)] public float limbSpeed = 12f;       // arms/legs
    [Range(1f, 20f)] public float spineSpeed = 8f;       // spine/hips
    [Range(1f, 20f)] public float headSpeed = 10f;       // head
    [Range(0f, 1f)] public float hipInfluence = 0.3f;   // how much hips twist
    [Range(0f, 1f)] public float spineInfluence = 0.3f; // how much spine bends

    [Header("Noise Filter")]
    [Range(0f, 1f)] public float noiseThreshold = 0.02f; // ignore tiny jitters

    [Header("Calibration")]
    public bool useCalibrationData = false;
    public PersistentCalibrationData calibrationData;

    public bool Calibrated { get; private set; }

    private PipeServer server;
    private Quaternion initialRotation;
    private Vector3 initialPosition;
    private Quaternion targetRot;

    // Smoothed rotation cache per bone
    private Dictionary<HumanBodyBones, Quaternion> _smoothedRotations
        = new Dictionary<HumanBodyBones, Quaternion>();

    private Dictionary<HumanBodyBones, CalibrationData> parentCalibrationData
        = new Dictionary<HumanBodyBones, CalibrationData>();

    private CalibrationData spineUpDown, hipsTwist, chest, head;

    private void Start()
    {
        initialRotation = transform.rotation;
        initialPosition = transform.position;
        if (calibrationData && useCalibrationData)
            CalibrateFromPersistent();
        server = FindObjectOfType<PipeServer>();
        if (server == null)
            Debug.LogError("You must have a PipeServer in the scene!");
    }

    public void CalibrateFromPersistent()
    {
        parentCalibrationData.Clear();
        _smoothedRotations.Clear();
        if (calibrationData)
        {
            foreach (PersistentCalibrationData.CalibrationEntry d in calibrationData.parentCalibrationData)
                parentCalibrationData.Add(d.bone, d.data.ReconstructReferences());
            spineUpDown = calibrationData.spineUpDown.ReconstructReferences();
            hipsTwist = calibrationData.hipsTwist.ReconstructReferences();
            chest = calibrationData.chest.ReconstructReferences();
            head = calibrationData.head.ReconstructReferences();
        }
        animator.enabled = false;
        Calibrated = true;
    }

    public void Calibrate()
    {
        print("Calibrating on " + gameObject.name);
        parentCalibrationData.Clear();
        _smoothedRotations.Clear();

        spineUpDown = new CalibrationData(animator.transform,
            animator.GetBoneTransform(HumanBodyBones.Spine),
            animator.GetBoneTransform(HumanBodyBones.Neck),
            server.GetVirtualHip(), server.GetVirtualNeck());
        hipsTwist = new CalibrationData(animator.transform,
            animator.GetBoneTransform(HumanBodyBones.Hips),
            animator.GetBoneTransform(HumanBodyBones.Hips),
            server.GetLandmark(Landmark.RIGHT_HIP),
            server.GetLandmark(Landmark.LEFT_HIP));
        chest = new CalibrationData(animator.transform,
            animator.GetBoneTransform(HumanBodyBones.Chest),
            animator.GetBoneTransform(HumanBodyBones.Chest),
            server.GetLandmark(Landmark.RIGHT_HIP),
            server.GetLandmark(Landmark.LEFT_HIP));
        head = new CalibrationData(animator.transform,
            animator.GetBoneTransform(HumanBodyBones.Neck),
            animator.GetBoneTransform(HumanBodyBones.Head),
            server.GetVirtualNeck(), server.GetLandmark(Landmark.NOSE));

        // Arms
        AddCalibration(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm,
            server.GetLandmark(Landmark.RIGHT_SHOULDER), server.GetLandmark(Landmark.RIGHT_ELBOW));
        AddCalibration(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            server.GetLandmark(Landmark.RIGHT_ELBOW), server.GetLandmark(Landmark.RIGHT_WRIST));
        AddCalibration(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
            server.GetLandmark(Landmark.LEFT_SHOULDER), server.GetLandmark(Landmark.LEFT_ELBOW));
        AddCalibration(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            server.GetLandmark(Landmark.LEFT_ELBOW), server.GetLandmark(Landmark.LEFT_WRIST));

        // Legs
        AddCalibration(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
            server.GetLandmark(Landmark.RIGHT_HIP), server.GetLandmark(Landmark.RIGHT_KNEE));
        AddCalibration(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
            server.GetLandmark(Landmark.RIGHT_KNEE), server.GetLandmark(Landmark.RIGHT_ANKLE));
        AddCalibration(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
            server.GetLandmark(Landmark.LEFT_HIP), server.GetLandmark(Landmark.LEFT_KNEE));
        AddCalibration(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
            server.GetLandmark(Landmark.LEFT_KNEE), server.GetLandmark(Landmark.LEFT_ANKLE));

        if (footTracking)
        {
            AddCalibration(HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes,
                server.GetLandmark(Landmark.LEFT_ANKLE), server.GetLandmark(Landmark.LEFT_FOOT_INDEX));
            AddCalibration(HumanBodyBones.RightFoot, HumanBodyBones.RightToes,
                server.GetLandmark(Landmark.RIGHT_ANKLE), server.GetLandmark(Landmark.RIGHT_FOOT_INDEX));
        }

        animator.enabled = false;
        Calibrated = true;
    }

    public void StoreCalibration()
    {
        if (!calibrationData) { Debug.LogError("Calibration data must be assigned."); return; }
        List<PersistentCalibrationData.CalibrationEntry> calibrations
            = new List<PersistentCalibrationData.CalibrationEntry>();
        foreach (KeyValuePair<HumanBodyBones, CalibrationData> k in parentCalibrationData)
            calibrations.Add(new PersistentCalibrationData.CalibrationEntry()
            { bone = k.Key, data = k.Value });
        calibrationData.parentCalibrationData = calibrations.ToArray();
        calibrationData.spineUpDown = spineUpDown;
        calibrationData.hipsTwist = hipsTwist;
        calibrationData.chest = chest;
        calibrationData.head = head;
        calibrationData.Dirty();
        print("Completed storing calibration data " + calibrationData.name);
    }

    private void AddCalibration(HumanBodyBones parent, HumanBodyBones child,
        Transform trackParent, Transform trackChild)
    {
        parentCalibrationData.Add(parent,
            new CalibrationData(animator.transform,
                animator.GetBoneTransform(parent),
                animator.GetBoneTransform(child),
                trackParent, trackChild));
    }

    // ─── Safe Quaternion helper ───────────────────────────────

    private Quaternion SafeQuaternion(Quaternion q)
    {
        if (q.x == 0 && q.y == 0 && q.z == 0 && q.w == 0)
            return Quaternion.identity;
        return Quaternion.Normalize(q);
    }

    // ─── Update ───────────────────────────────────────────────

    private void Update()
    {
        if (parentCalibrationData.Count == 0) return;

        // ── Foot grounding ──
        float displacement = 0;
        RaycastHit h1;
        if (Physics.Raycast(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position,
            Vector3.down, out h1, 100f, ground, QueryTriggerInteraction.Ignore))
            displacement = (h1.point - animator.GetBoneTransform(HumanBodyBones.LeftFoot).position).y;
        if (Physics.Raycast(animator.GetBoneTransform(HumanBodyBones.RightFoot).position,
            Vector3.down, out h1, 100f, ground, QueryTriggerInteraction.Ignore))
        {
            float d2 = (h1.point - animator.GetBoneTransform(HumanBodyBones.RightFoot).position).y;
            if (Mathf.Abs(d2) < Mathf.Abs(displacement)) displacement = d2;
        }
        transform.position = Vector3.Lerp(transform.position,
            initialPosition + Vector3.up * displacement + Vector3.up * footGroundOffset,
            Time.deltaTime * 5f);

        // ── Limbs — smooth Slerp with noise filter ──
        foreach (var i in parentCalibrationData)
        {
            Quaternion rawTarget = Quaternion.FromToRotation(
                i.Value.initialDir, i.Value.CurrentDirection) * i.Value.initialRotation;

            rawTarget = SafeQuaternion(rawTarget);

            // Noise filter — skip tiny movements
            if (!_smoothedRotations.ContainsKey(i.Key))
                _smoothedRotations[i.Key] = i.Value.parent.rotation;

            float angleDiff = Quaternion.Angle(_smoothedRotations[i.Key], rawTarget);
            if (angleDiff > noiseThreshold * 100f)
            {
                _smoothedRotations[i.Key] = Quaternion.Slerp(
                    _smoothedRotations[i.Key],
                    rawTarget,
                    Time.deltaTime * limbSpeed
                );
            }

            i.Value.parent.rotation = _smoothedRotations[i.Key];
        }

        // ── Spine & Hips ──
        Vector3 hd = head.CurrentDirection;
        Quaternion headRot = SafeQuaternion(
            Quaternion.FromToRotation(head.initialDir, hd));

        Quaternion twist = SafeQuaternion(Quaternion.FromToRotation(
            hipsTwist.initialDir,
            Vector3.Slerp(hipsTwist.initialDir, hipsTwist.CurrentDirection, hipInfluence)));

        Quaternion updown = SafeQuaternion(Quaternion.FromToRotation(
            spineUpDown.initialDir,
            Vector3.Slerp(spineUpDown.initialDir, spineUpDown.CurrentDirection, spineInfluence)));

        Quaternion h = SafeQuaternion(updown * updown * updown * twist * twist);
        Quaternion s = SafeQuaternion(h * twist * updown);
        Quaternion c = SafeQuaternion(s * twist * twist);

        hipsTwist.Tick(SafeQuaternion(h * hipsTwist.initialRotation), spineSpeed);
        spineUpDown.Tick(SafeQuaternion(s * spineUpDown.initialRotation), spineSpeed);
        chest.Tick(SafeQuaternion(c * chest.initialRotation), spineSpeed);
        head.Tick(SafeQuaternion(updown * twist * headRot * head.initialRotation), headSpeed);

        // ── Body rotation ──
        Vector3 dir = Vector3.Slerp(hipsTwist.initialDir,
            hipsTwist.CurrentDirection, hipInfluence);
        dir.y *= 0.5f;
        Quaternion deltaRot = SafeQuaternion(
            Quaternion.FromToRotation(hipsTwist.initialDir, dir));
        targetRot = SafeQuaternion(deltaRot * initialRotation);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, targetRot, Time.deltaTime * spineSpeed);
    }
}