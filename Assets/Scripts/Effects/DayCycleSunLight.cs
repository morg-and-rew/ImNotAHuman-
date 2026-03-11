using UnityEngine;

/// <summary>
/// Дневной цикл света из окна: базовую ориентацию луча не трогаем (как в редакторе), крутим только по оси Z.
/// Оттенок к концу дня теплеет (закат). Поставь луч как нужно (X, Y, Z в т.ч. Z: -90), скрипт сохранит это и добавит поворот по Z.
/// </summary>
public class DayCycleSunLight : MonoBehaviour
{
    [Header("Время дня")]
    [Tooltip("Длительность полного цикла день (секунды). Например 300 = 5 минут на весь день.")]
    [Min(1f)]
    public float dayLengthSeconds = 300f;

    [Tooltip("Текущее время дня 0–1. Можно гонять вручную или оставить авто.")]
    [Range(0f, 1f)]
    public float timeOfDay = 0.35f;

    [Tooltip("Включить автоматическое продвижение времени.")]
    public bool autoAdvanceTime = true;

    [Header("Поворот по оси Z (движение солнца за день)")]
    [Tooltip("Угол поворота по Z от времени дня. 0 = полдень, утро/вечер — в стороны. Градусы.")]
    public AnimationCurve sunRotationZ = new AnimationCurve(
        new Keyframe(0f, -20f), new Keyframe(0.5f, 0f), new Keyframe(1f, 20f));

    [Tooltip("Диапазон поворота по Z в градусах (утро/вечер).")]
    public float zAngleRange = 25f;

    [Header("Оттенок света")]
    public Color morningColor = new Color(0.98f, 0.95f, 0.88f, 0.32f);
    public Color noonColor = new Color(1f, 0.9f, 0.72f, 0.38f);
    public Color eveningColor = new Color(1f, 0.7f, 0.45f, 0.32f);

    [Header("Реальные источники света (если есть)")]
    [Tooltip("Интенсивность в полдень. Утро/вечер слабее.")]
    public float lightIntensityAtNoon = 0.95f;

    MeshRenderer[] _renderers;
    Light[] _lights;
    ParticleSystem[] _particleSystems;
    MaterialPropertyBlock _block;
    Quaternion _baseRotation;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        _baseRotation = transform.localRotation;
        _renderers = GetComponentsInChildren<MeshRenderer>(true);
        _lights = GetComponentsInChildren<Light>(true);
        _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        _block = new MaterialPropertyBlock();
    }

    void Update()
    {
        if (autoAdvanceTime)
        {
            timeOfDay += Time.deltaTime / dayLengthSeconds;
            if (timeOfDay >= 1f) timeOfDay -= 1f;
        }

        float t = Mathf.Clamp01(timeOfDay);
        float zT = (t <= 0.5f) ? t * 2f : (1f - t) * 2f;
        float zAngle = sunRotationZ.Evaluate(zT) * (zAngleRange / 20f);
        transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, zAngle);

        Color color;
        if (t < 0.5f)
            color = Color.Lerp(morningColor, noonColor, t * 2f);
        else
            color = Color.Lerp(noonColor, eveningColor, (t - 0.5f) * 2f);

        float intensityT = (t <= 0.5f) ? t * 2f : (1f - t) * 2f;
        float intensity = Mathf.Lerp(0.4f, 1f, intensityT) * lightIntensityAtNoon;

        if (_renderers != null && _renderers.Length > 0)
        {
            _block.SetColor(BaseColorId, color);
            foreach (var r in _renderers)
            {
                if (r != null && r.sharedMaterial != null)
                    r.SetPropertyBlock(_block);
            }
        }

        if (_lights != null)
        {
            Color lightColor = new Color(color.r, color.g, color.b);
            foreach (var light in _lights)
            {
                if (light == null) continue;
                light.color = lightColor;
                light.intensity = intensity;
            }
        }

        if (_particleSystems != null)
        {
            var particleColor = new Color(color.r, color.g, color.b, color.a);
            foreach (var ps in _particleSystems)
            {
                if (ps == null) continue;
                var main = ps.main;
                main.startColor = new ParticleSystem.MinMaxGradient(particleColor);
            }
        }
    }
}
