using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Создаёт отдельный Volume с Depth of Field (Bokeh) на время диалогов с клиентами.
/// Фокус на клиентах, фон размыт. После диалога Volume уничтожается.
/// </summary>
public sealed class ClientDialogueDepthOfFieldController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ClientInteraction _clientInteraction;

    [Header("Depth of Field (Bokeh, по скриншоту)")]
    [SerializeField, Min(0.1f)] private float _focusDistance = 2.7f;
    [SerializeField, Min(1f)] private float _focalLength = 140f;
    [SerializeField, Min(0.1f)] private float _aperture = 10.3f;
    [SerializeField, Min(3)] private int _bladeCount = 9;
    [SerializeField, Range(0f, 1f)] private float _bladeCurvature = 1f;
    [SerializeField, Range(0f, 360f)] private float _bladeRotation = 180f;

    private GameObject _dialogueVolumeGo;

    private void Awake()
    {
        if (_clientInteraction == null)
            _clientInteraction = FindFirstObjectByType<ClientInteraction>();

        if (_clientInteraction == null)
        {
            Debug.LogWarning("[ClientDialogueDepthOfField] ClientInteraction не найден.");
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (_clientInteraction == null) return;

        _clientInteraction.ClientConversationStarted += OnClientDialogueStarted;
        _clientInteraction.ClientDialogueFinished += OnClientDialogueFinished;
    }

    private void OnDisable()
    {
        if (_clientInteraction != null)
        {
            _clientInteraction.ClientConversationStarted -= OnClientDialogueStarted;
            _clientInteraction.ClientDialogueFinished -= OnClientDialogueFinished;
        }

        DestroyDialogueVolume();
    }

    private void OnClientDialogueStarted()
    {
        CreateDialogueVolume();
    }

    private void OnClientDialogueFinished()
    {
        DestroyDialogueVolume();
    }

    private void CreateDialogueVolume()
    {
        DestroyDialogueVolume();

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var dof = profile.Add<DepthOfField>(overrides: true);

        dof.active = true;
        dof.mode.Override(DepthOfFieldMode.Bokeh);
        dof.focusDistance.Override(_focusDistance);
        dof.focalLength.Override(_focalLength);
        dof.aperture.Override(_aperture);
        dof.bladeCount.Override(_bladeCount);
        dof.bladeCurvature.Override(_bladeCurvature);
        dof.bladeRotation.Override(_bladeRotation);

        _dialogueVolumeGo = new GameObject("ClientDialogueDoF_Volume");
        var volume = _dialogueVolumeGo.AddComponent<Volume>();
        volume.profile = profile;
        volume.isGlobal = true;
        volume.priority = 100;
        volume.weight = 1f;
    }

    private void DestroyDialogueVolume()
    {
        if (_dialogueVolumeGo != null)
        {
            if (_dialogueVolumeGo.TryGetComponent<Volume>(out var vol) && vol.profile != null)
                Destroy(vol.profile);
            Destroy(_dialogueVolumeGo);
            _dialogueVolumeGo = null;
        }
    }
}
