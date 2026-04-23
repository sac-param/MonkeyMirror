using UnityEngine;
using TMPro;

[RequireComponent(typeof(RectTransform), typeof(TMP_Text))]
public class ScrollingBorderText : MonoBehaviour
{
    public float scrollSpeed = 120f;
    public float direction = 1f;      // 1 = right/up, -1 = left/down
    public bool useYAxis = false;

    [Header("Loop Settings")]
    public float minLoopLength = 4000f;
    public float overlap = 2f;        // increase to 3 or 4 if you still see a tiny seam
    public string separator = "    ";

    private RectTransform _rectA;
    private RectTransform _rectB;
    private TMP_Text _tmpA;

    private float _startAxis;
    private float _step;
    private float _offset;

    void Start()
    {
        _rectA = GetComponent<RectTransform>();
        _tmpA = GetComponent<TMP_Text>();

        _startAxis = GetAxis(_rectA);

        BuildLongText(_tmpA);
        _tmpA.ForceMeshUpdate();

        float renderedWidth = Mathf.Max(_tmpA.preferredWidth, _tmpA.textBounds.size.x);
        _step = Mathf.Max(1f, renderedWidth - overlap);

        // Clone the same object
        GameObject clone = Instantiate(gameObject, transform.parent);
        clone.name = gameObject.name + "_B";

        // Remove this script from clone so it doesn't clone itself again
        ScrollingBorderText cloneScript = clone.GetComponent<ScrollingBorderText>();
        if (cloneScript != null)
            Destroy(cloneScript);

        _rectB = clone.GetComponent<RectTransform>();

        CopyRectTransform(_rectA, _rectB);

        // Put clone exactly behind first tile
        ApplyPositions();
    }

    void Update()
    {
        _offset = Mathf.Repeat(_offset + scrollSpeed * Time.deltaTime, _step);
        ApplyPositions();
    }

    void ApplyPositions()
    {
        float sign = direction >= 0f ? 1f : -1f;

        float posA = _startAxis + (sign * _offset);
        float posB = posA - (sign * _step);

        SetAxis(_rectA, posA);
        SetAxis(_rectB, posB);
    }

    void BuildLongText(TMP_Text tmp)
    {
        string original = tmp.text.Trim();
        if (string.IsNullOrEmpty(original))
            return;

        string tiled = original;
        tmp.text = tiled;
        tmp.ForceMeshUpdate();

        int safety = 0;
        while (tmp.preferredWidth < minLoopLength && safety < 50)
        {
            tiled += separator + original;
            tmp.text = tiled;
            tmp.ForceMeshUpdate();
            safety++;
        }

        // Add separator at the end so object boundary matches internal spacing
        tmp.text += separator;
        tmp.ForceMeshUpdate();
    }

    float GetAxis(RectTransform r)
    {
        return useYAxis ? r.anchoredPosition.y : r.anchoredPosition.x;
    }

    void SetAxis(RectTransform r, float value)
    {
        Vector2 p = r.anchoredPosition;
        if (useYAxis)
            p.y = value;
        else
            p.x = value;

        r.anchoredPosition = p;
    }

    void CopyRectTransform(RectTransform source, RectTransform target)
    {
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.sizeDelta = source.sizeDelta;
        target.anchoredPosition = source.anchoredPosition;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
        target.SetSiblingIndex(source.GetSiblingIndex() + 1);
    }
}