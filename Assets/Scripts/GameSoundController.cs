using UnityEngine;

/// <summary>
/// Централизованное воспроизведение звуков игры: переход склад/зона, шаги и т.д.
/// </summary>
public sealed class GameSoundController : MonoBehaviour
{
    [Header("Переход склад ↔ зона")]
    [Tooltip("Звук при переходе на склад и обратно (тихий).")]
    [SerializeField] private AudioClip _travelTransitionClip;
    [SerializeField, Range(0.2f, 1f)] private float _travelTransitionVolume = 0.55f;

    [Header("Шаги")]
    [Tooltip("Источник для звуков шагов (если пусто — берётся AudioSource на этом же объекте).")]
    [SerializeField] private AudioSource _footstepSource;
    [Tooltip("Случайный клип из массива при каждом шаге. Если пусто — звуки шагов отключены.")]
    [SerializeField] private AudioClip[] _footstepClips;
    [SerializeField, Range(0.2f, 1f)] private float _footstepVolume = 0.5f;

    [Header("Диалоги")]
    [Tooltip("Звук при нажатии на вариант ответа в диалоге.")]
    [SerializeField] private AudioClip _dialogueResponseClickClip;
    [SerializeField, Range(0.2f, 1f)] private float _dialogueResponseClickVolume = 0.5f;

    [Header("Окна")]
    [Tooltip("Звук при открытии окна.")]
    [SerializeField] private AudioClip _windowOpenClip;
    [Tooltip("Звук при закрытии окна.")]
    [SerializeField] private AudioClip _windowCloseClip;
    [SerializeField, Range(0.2f, 1f)] private float _windowSoundVolume = 0.5f;

    [Header("Телефон — кнопки")]
    [Tooltip("Звуки при нажатии цифр: [0]=0, [1]=1, … [9]=9. По одному клипу на кнопку.")]
    [SerializeField] private AudioClip[] _phoneDigitClips = new AudioClip[10];
    [Tooltip("Звук при нажатии Backspace.")]
    [SerializeField] private AudioClip _phoneBackspaceClip;
    [Tooltip("Звук при нажатии кнопки звонка.")]
    [SerializeField] private AudioClip _phoneCallButtonClip;
    [Tooltip("Звук при нажатии кнопки закрытия.")]
    [SerializeField] private AudioClip _phoneCloseButtonClip;

    [Header("Телефон — звонок")]
    [Tooltip("Звук гудков (ожидание соединения).")]
    [SerializeField] private AudioClip _phoneBeepsClip;
    [Tooltip("Звук конца звонка.")]
    [SerializeField] private AudioClip _phoneCallEndClip;
    [Tooltip("Звук «номер набран неправильно».")]
    [SerializeField] private AudioClip _phoneWrongNumberClip;
    [Tooltip("Звук «нет соединения».")]
    [SerializeField] private AudioClip _phoneNoConnectionClip;
    [SerializeField, Range(0.2f, 1f)] private float _phoneSoundVolume = 0.5f;
    [Tooltip("Источник для длительных звуков телефона (гудки и т.д.). Останавливается при закрытии телефона.")]
    [SerializeField] private AudioSource _phoneOngoingSource;

    private AudioSource _cachedFootstepSource;

    private void Awake()
    {
        if (_footstepSource == null)
            _cachedFootstepSource = GetComponent<AudioSource>();
        else
            _cachedFootstepSource = _footstepSource;
        if (_cachedFootstepSource == null && _footstepClips != null && _footstepClips.Length > 0)
            _cachedFootstepSource = gameObject.AddComponent<AudioSource>();
        if (_phoneOngoingSource == null)
            _phoneOngoingSource = gameObject.AddComponent<AudioSource>();
    }

    /// <summary> Воспроизвести звук перехода склад/зона в заданной позиции. </summary>
    public void PlayTravelTransition(Vector3 position)
    {
        if (_travelTransitionClip == null) return;
        AudioSource.PlayClipAtPoint(_travelTransitionClip, position, _travelTransitionVolume);
    }

    /// <summary> Воспроизвести один шаг (при ходьбе). </summary>
    public void PlayFootstep()
    {
        AudioSource source = _cachedFootstepSource != null ? _cachedFootstepSource : _footstepSource;
        if (source == null || _footstepClips == null || _footstepClips.Length == 0) return;
        AudioClip clip = _footstepClips[Random.Range(0, _footstepClips.Length)];
        if (clip != null)
            source.PlayOneShot(clip, _footstepVolume);
    }

    /// <summary> Остановить звук шага (при остановке игрока — звук не доигрывается до конца). </summary>
    public void StopFootstep()
    {
        AudioSource source = _cachedFootstepSource != null ? _cachedFootstepSource : _footstepSource;
        if (source != null)
            source.Stop();
    }

    /// <summary> Воспроизвести звук открытия окна. </summary>
    public void PlayWindowOpen()
    {
        if (_windowOpenClip == null) return;
        Vector3 pos = Camera.main != null ? Camera.main.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(_windowOpenClip, pos, _windowSoundVolume);
    }

    /// <summary> Воспроизвести звук закрытия окна. </summary>
    public void PlayWindowClose()
    {
        if (_windowCloseClip == null) return;
        Vector3 pos = Camera.main != null ? Camera.main.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(_windowCloseClip, pos, _windowSoundVolume);
    }

    /// <summary> Воспроизвести звук при нажатии на вариант ответа в диалоге. </summary>
    public void PlayDialogueResponseClick()
    {
        if (_dialogueResponseClickClip == null) return;
        Vector3 pos = Camera.main != null ? Camera.main.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(_dialogueResponseClickClip, pos, _dialogueResponseClickVolume);
    }

    private void PlayPhoneAtCamera(AudioClip clip)
    {
        if (clip == null) return;
        Vector3 pos = Camera.main != null ? Camera.main.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(clip, pos, _phoneSoundVolume);
    }

    /// <param name="digit">Цифра '0'–'9'. Индекс в массиве _phoneDigitClips.</param>
    public void PlayPhoneDigit(char digit)
    {
        int i = digit >= '0' && digit <= '9' ? (digit - '0') : -1;
        if (i < 0 || _phoneDigitClips == null || i >= _phoneDigitClips.Length) return;
        AudioClip clip = _phoneDigitClips[i];
        if (clip != null) PlayPhoneAtCamera(clip);
    }
    public void PlayPhoneBackspace() => PlayPhoneAtCamera(_phoneBackspaceClip);
    public void PlayPhoneCallButton() => PlayPhoneAtCamera(_phoneCallButtonClip);
    public void PlayPhoneCloseButton() => PlayPhoneAtCamera(_phoneCloseButtonClip);

    /// <summary> Гудки — через отдельный источник, чтобы можно было остановить при закрытии телефона. </summary>
    public void PlayPhoneBeeps()
    {
        if (_phoneBeepsClip == null || _phoneOngoingSource == null) return;
        _phoneOngoingSource.Stop();
        _phoneOngoingSource.clip = _phoneBeepsClip;
        _phoneOngoingSource.volume = _phoneSoundVolume;
        _phoneOngoingSource.loop = true;
        _phoneOngoingSource.Play();
    }

    /// <summary> Остановить длительные звуки телефона (гудки и т.д.) при закрытии/положении телефона. </summary>
    public void StopPhoneSounds()
    {
        if (_phoneOngoingSource != null)
            _phoneOngoingSource.Stop();
    }

    public void PlayPhoneCallEnd() => PlayPhoneAtCamera(_phoneCallEndClip);
    public void PlayPhoneWrongNumber() => PlayPhoneAtCamera(_phoneWrongNumberClip);
    public void PlayPhoneNoConnection() => PlayPhoneAtCamera(_phoneNoConnectionClip);
}
