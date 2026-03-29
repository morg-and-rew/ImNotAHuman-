using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Временный глобальный Volume с Gaussian DoF (URP) для короткой кинематики.
/// Параметры — константы (без SerializeField): Gaussian размывает всё дальше <see cref="GaussianStart"/> от камеры.
/// </summary>
public sealed class RuntimeDizzyBlurVolume
{
    // Чем меньше Start — тем раньше по глубине начинается размытие; Max Radius до ~1 (URP без сильного шума).
    private const float GaussianStart = 0.12f;
    private const float GaussianEnd = 14f;
    private const float GaussianMaxRadius = 1f;
    private const int VolumePriority = 5000;
    private const bool HighQualitySampling = true;

    public const float MaxVolumeWeight = 0.98f;
    public const float RampInPhasePortion = 0.22f;
    /// <summary> С какой доли падения (0–1) нарастает расфокус до максимума. </summary>
    public const float DefocusFallBegin = 0.1f;

    private readonly GameObject _go;
    private readonly Volume _volume;
    private readonly DepthOfField _dof;
    private readonly float _gaussianStartBase;
    private readonly float _gaussianEndBase;
    private readonly float _gaussianRadiusBase;
    private VolumeProfile _profile;
    private bool _destroyed;

    private RuntimeDizzyBlurVolume()
    {
        float g0 = Mathf.Min(GaussianStart, GaussianEnd);
        float g1 = Mathf.Max(GaussianStart, GaussianEnd);
        if (g1 - g0 < 0.5f)
            g1 = g0 + 0.5f;

        _gaussianStartBase = g0;
        _gaussianEndBase = g1;
        _gaussianRadiusBase = GaussianMaxRadius;

        _profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _dof = _profile.Add<DepthOfField>(true);
        _dof.active = true;
        _dof.mode.Override(DepthOfFieldMode.Gaussian);
        _dof.gaussianStart.Override(g0);
        _dof.gaussianEnd.Override(g1);
        _dof.gaussianMaxRadius.Override(GaussianMaxRadius);
        _dof.highQualitySampling.Override(HighQualitySampling);

        _go = new GameObject("TempDizzyGaussianDoF_Volume");
        _volume = _go.AddComponent<Volume>();
        _volume.profile = _profile;
        _volume.isGlobal = true;
        _volume.priority = VolumePriority;
        _volume.weight = 0f;
    }

    /// <summary>Создаёт volume и при необходимости включает постобработку на камере (иначе DoF не рисуется).</summary>
    public static RuntimeDizzyBlurVolume TryCreate(Camera playerCamera)
    {
        EnsureCameraPostProcessing(playerCamera);
        return new RuntimeDizzyBlurVolume();
    }

    private static void EnsureCameraPostProcessing(Camera camera)
    {
        if (camera == null)
            return;
        UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
        if (data != null)
            data.renderPostProcessing = true;
    }

    public void SetWeight(float weight)
    {
        if (_destroyed || _volume == null)
            return;
        _volume.weight = Mathf.Clamp01(weight);
    }

    /// <summary>
    /// Доп. расфокус к концу: t=0 базовые параметры, t=1 сильнее «мыло» (радиус + ближе старт размытия).
    /// </summary>
    public void SetDefocusNormalized(float t)
    {
        if (_destroyed || _dof == null)
            return;
        t = Mathf.Clamp01(t);
        const float radiusStrong = 1.06f;
        const float startStrong = 0.05f;
        const float endStrong = 18f;
        _dof.gaussianMaxRadius.Override(Mathf.Lerp(_gaussianRadiusBase, radiusStrong, t));
        _dof.gaussianStart.Override(Mathf.Lerp(_gaussianStartBase, startStrong, t));
        _dof.gaussianEnd.Override(Mathf.Lerp(_gaussianEndBase, endStrong, t));
    }

    public void DestroySelf()
    {
        if (_destroyed)
            return;
        _destroyed = true;
        if (_profile != null)
        {
            Object.Destroy(_profile);
            _profile = null;
        }

        if (_go != null)
            Object.Destroy(_go);
    }
}
