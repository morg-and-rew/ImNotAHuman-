using System.Collections;
using UnityEngine;

/// <summary>
/// Централизованное воспроизведение звуков игры: переход склад/зона, шаги и т.д.
/// </summary>
[DefaultExecutionOrder(-200)]
public sealed class GameSoundController : MonoBehaviour
{
    public static GameSoundController Instance { get; private set; }

    [Header("Переход склад ↔ зона")]
    [Tooltip("Звук при переходе на склад и обратно (тихий).")]
    [SerializeField] private AudioClip _travelTransitionClip;
    [SerializeField, Range(0.2f, 1f)] private float _travelTransitionVolume = 0.55f;

    [Header("Шаги")]
    [Tooltip("Только шаги. Если пусто — будет отдельный AudioSource на этом объекте (не делите его с телефоном/ветром).")]
    [SerializeField] private AudioSource _footstepSource;
    [Tooltip("Случайный клип из массива при каждом шаге. Если пусто — звуки шагов отключены.")]
    [SerializeField] private AudioClip[] _footstepClips;
    [SerializeField, Range(0.2f, 1f)] private float _footstepVolume = 0.5f;
    [Tooltip("Множитель pitch для steps_1 / единственного клипа шагов (0.0625 ≈ в ~16 раз медленнее оригинала).")]
    [SerializeField, Range(0.03f, 1f)] private float _footstepSteps1PitchScale = 0.0625f;

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

    [Header("Ветер")]
    [Tooltip("Зацикленный звук ветра. Запускается по сюжету (после Client_day2.1.2).")]
    [SerializeField] private AudioClip _windLoopClip;
    [SerializeField, Range(0f, 1f)] private float _windLoopVolume = 0.45f;
    [SerializeField, Min(0f)] private float _windLoopFadeInDuration = 2f;
    [Tooltip("Источник для ветра. Если пусто — будет создан автоматически.")]
    [SerializeField] private AudioSource _windLoopSource;

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

    /// <summary> Один голос для шага: без наслоения PlayOneShot. </summary>
    private AudioSource _footstepVoice;
    private Coroutine _windFadeCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        GameAudioSettings.EnsureLoaded();

        if (_footstepSource != null)
            _footstepVoice = _footstepSource;
        else if (_footstepClips != null && _footstepClips.Length > 0)
        {
            _footstepVoice = gameObject.AddComponent<AudioSource>();
            _footstepVoice.playOnAwake = false;
            _footstepVoice.loop = false;
            _footstepVoice.spatialBlend = 0f;
        }

        if (_phoneOngoingSource == null)
            _phoneOngoingSource = gameObject.AddComponent<AudioSource>();
        if (_windLoopSource == null && _windLoopClip != null)
            _windLoopSource = gameObject.AddComponent<AudioSource>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary> Воспроизвести звук перехода склад/зона в заданной позиции. </summary>
    public void PlayTravelTransition(Vector3 position)
    {
        if (_travelTransitionClip == null) return;
        AudioSource.PlayClipAtPoint(_travelTransitionClip, position, GameAudioSettings.ScaleSfx(_travelTransitionVolume));
    }

    /// <summary> Воспроизвести один шаг (при ходьбе). </summary>
    public void PlayFootstep()
    {
        if (_footstepVoice == null || _footstepClips == null || _footstepClips.Length == 0) return;
        AudioClip clip = _footstepClips[Random.Range(0, _footstepClips.Length)];
        if (clip == null) return;

        // Один активный шаг: иначе PlayOneShot даёт «оркестр» из длинных клипов при низком pitch.
        _footstepVoice.Stop();
        _footstepVoice.clip = clip;
        _footstepVoice.volume = GameAudioSettings.ScaleSfx(_footstepVolume);
        float pitchMul = (IsSteps1FootstepClip(clip) || _footstepClips.Length == 1) ? _footstepSteps1PitchScale : 1f;
        _footstepVoice.pitch = Mathf.Clamp(pitchMul, 0.03f, 3f);
        _footstepVoice.Play();
    }

    private static bool IsSteps1FootstepClip(AudioClip clip) =>
        clip != null && string.Equals(clip.name, "steps_1", System.StringComparison.OrdinalIgnoreCase);

    /// <summary> Остановить звук шага (при остановке игрока — звук не доигрывается до конца). </summary>
    public void StopFootstep()
    {
        if (_footstepVoice != null)
            _footstepVoice.Stop();
    }

    /// <summary> Воспроизвести звук открытия окна. </summary>
    public void PlayWindowOpen()
    {
        if (_windowOpenClip == null) return;
        Vector3 pos = Camera.main != null ? Camera.main.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(_windowOpenClip, pos, GameAudioSettings.ScaleSfx(_windowSoundVolume));
    }

    /// <summary> Воспроизвести звук закрытия окна. </summary>
    public void PlayWindowClose()
    {
        if (_windowCloseClip == null) return;
        Vector3 pos = Camera.main != null ? Camera.main.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(_windowCloseClip, pos, GameAudioSettings.ScaleSfx(_windowSoundVolume));
    }

    public void StartWindLoop()
    {
        if (_windLoopClip == null) return;
        if (_windLoopSource == null)
            _windLoopSource = gameObject.AddComponent<AudioSource>();

        _windLoopSource.clip = _windLoopClip;
        _windLoopSource.loop = true;
        if (!_windLoopSource.isPlaying)
            _windLoopSource.Play();
        if (_windFadeCoroutine != null)
            StopCoroutine(_windFadeCoroutine);
        _windFadeCoroutine = StartCoroutine(FadeWindLoopToTargetVolume());
    }

    public void StopWindLoop()
    {
        if (_windFadeCoroutine != null)
        {
            StopCoroutine(_windFadeCoroutine);
            _windFadeCoroutine = null;
        }
        if (_windLoopSource != null && _windLoopSource.isPlaying)
            _windLoopSource.Stop();
    }

    private IEnumerator FadeWindLoopToTargetVolume()
    {
        if (_windLoopSource == null)
            yield break;

        float target = GameAudioSettings.ScaleSfx(Mathf.Clamp01(_windLoopVolume));
        float duration = Mathf.Max(0f, _windLoopFadeInDuration);
        if (duration <= 0.001f)
        {
            _windLoopSource.volume = target;
            _windFadeCoroutine = null;
            yield break;
        }

        _windLoopSource.volume = 0f;
        float t = 0f;
        while (t < duration && _windLoopSource != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            _windLoopSource.volume = Mathf.Lerp(0f, target, k);
            yield return null;
        }

        if (_windLoopSource != null)
            _windLoopSource.volume = target;
        _windFadeCoroutine = null;
    }

    /// <summary> Воспроизвести звук при нажатии на вариант ответа в диалоге. </summary>
    public void PlayDialogueResponseClick()
    {
        if (_dialogueResponseClickClip == null) return;
        Vector3 pos = Camera.main != null ? Camera.main.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(_dialogueResponseClickClip, pos, GameAudioSettings.ScaleSfx(_dialogueResponseClickVolume));
    }

    private void PlayPhoneAtCamera(AudioClip clip)
    {
        if (clip == null) return;
        Vector3 pos = Camera.main != null ? Camera.main.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(clip, pos, GameAudioSettings.ScaleSfx(_phoneSoundVolume));
    }

    /// <param name="digit">Клавиша телефона: '0'–'9', '*' или '#'. Для цифр — индекс в _phoneDigitClips.</param>
    public void PlayPhoneDigit(char digit)
    {
        if (digit == '*' || digit == '#')
        {
            AudioClip fb = FirstAssignedPhoneDigitClip();
            if (fb != null) PlayPhoneAtCamera(fb);
            return;
        }

        int i = digit >= '0' && digit <= '9' ? (digit - '0') : -1;
        if (i < 0 || _phoneDigitClips == null || i >= _phoneDigitClips.Length) return;
        AudioClip clip = _phoneDigitClips[i];
        if (clip != null) PlayPhoneAtCamera(clip);
    }

    private AudioClip FirstAssignedPhoneDigitClip()
    {
        if (_phoneDigitClips == null) return null;
        for (int i = 0; i < _phoneDigitClips.Length; i++)
        {
            if (_phoneDigitClips[i] != null)
                return _phoneDigitClips[i];
        }
        return null;
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
        _phoneOngoingSource.volume = GameAudioSettings.ScaleSfx(_phoneSoundVolume);
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
