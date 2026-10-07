using UnityEngine;

public class StreetLampEffect : MonoBehaviour
{
    [SerializeField] private Light targetLight;
    [SerializeField] private Renderer bulbRenderer;
    [SerializeField] private float maxIntensity = 28f;
    [SerializeField] private bool autoFlicker = false;
    [SerializeField] private float flickerSpeed = 10f;

    private Material bulbMaterial;
    private Color originalEmissionColor = Color.white;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        if (targetLight == null)
        {
            targetLight = GetComponentInChildren<Light>();
        }

        if (bulbRenderer != null)
        {
            bulbMaterial = bulbRenderer.material;
            if (bulbMaterial.HasProperty(EmissionColorId))
            {
                originalEmissionColor = bulbMaterial.GetColor(EmissionColorId);
            }
        }
    }

    private void Update()
    {
        if (autoFlicker && targetLight != null)
        {
            float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, 0.0f);
            float currentIntensity = Mathf.Lerp(maxIntensity * 0.2f, maxIntensity, noise);
            SetIntensity(currentIntensity);
        }
    }

    public void SetIntensity(float intensity)
    {
        if (targetLight != null)
        {
            targetLight.intensity = intensity;
            targetLight.enabled = intensity > 0.01f;
        }

        if (bulbMaterial != null && bulbMaterial.HasProperty(EmissionColorId))
        {
            float normalized = Mathf.Clamp01(intensity / Mathf.Max(0.1f, maxIntensity));
            bulbMaterial.SetColor(EmissionColorId, originalEmissionColor * normalized);
        }
    }

    public void ToggleLight()
    {
        if (targetLight != null)
        {
            SetIntensity(targetLight.enabled ? 0f : maxIntensity);
        }
    }
}
