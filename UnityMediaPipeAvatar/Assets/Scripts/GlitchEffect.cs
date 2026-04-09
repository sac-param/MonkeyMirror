using System.Collections;
using UnityEngine;

public class GlitchEffect : MonoBehaviour
{
    public float flickerSpeed = 0.03f;
    public float glitchDuration = 2f;
    public float jitterAmount = 0.15f;

    private Renderer[] _renderers;
    private Vector3 _originalPos;
    private MaterialPropertyBlock _propBlock;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        _originalPos = transform.position;
        _propBlock = new MaterialPropertyBlock();
    }

    public IEnumerator PlayGlitch()
    {
        float elapsed = 0f;
        _originalPos = transform.position;

        while (elapsed < glitchDuration)
        {
            bool visible = Random.value > 0.25f;

            // Position jitter on monkey only
            float jx = Random.Range(-jitterAmount, jitterAmount);
            float jy = Random.Range(-jitterAmount * 0.3f, jitterAmount * 0.3f);
            transform.position = _originalPos + new Vector3(jx, jy, 0);

            // Apply cyan/magenta color flash to monkey renderers only
            Color glitchCol = Random.value > 0.5f
                ? new Color(0f, 1f, 1f, 1f)    // cyan
                : new Color(1f, 0f, 1f, 1f);   // magenta

            foreach (var r in _renderers)
            {
                if (r == null) continue;
                r.enabled = visible;
                r.GetPropertyBlock(_propBlock);

                if (visible)
                    _propBlock.SetColor("_Color", Random.value > 0.6f ? glitchCol : Color.white);
                else
                    _propBlock.SetColor("_Color", Color.white);

                r.SetPropertyBlock(_propBlock);
            }

            elapsed += flickerSpeed;
            yield return new WaitForSeconds(flickerSpeed);
        }

        // Restore monkey to normal
        transform.position = _originalPos;
        transform.localScale = new Vector3(2f, 2f, 2f);

        foreach (var r in _renderers)
        {
            if (r == null) continue;
            r.enabled = true;
            r.GetPropertyBlock(_propBlock);
            _propBlock.SetColor("_Color", Color.white);
            r.SetPropertyBlock(_propBlock);
        }
    }
}