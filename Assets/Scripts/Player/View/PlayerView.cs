using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CharacterController))]
public sealed class PlayerView : MonoBehaviour
{
    [SerializeField] private CharacterController _controller;
    [SerializeField] private Transform _cameraHolder;
    [SerializeField] private Camera _playerCamera;
    [SerializeField] private Canvas _playerCanvas;  
    [SerializeField] private Image _playerDialogLeftClient;
    [SerializeField] private Image _playerDialogRightClient;
    [SerializeField] private float _lookSpeed = 3f;
    [Tooltip("Максимальный угол наклона камеры вниз по оси X в обычном режиме (градусы).")]
    [SerializeField] private float _maxPitchDown = 45f;
    [Tooltip("Максимальный угол наклона камеры вниз при удержании коробки (градусы).")]
    [SerializeField] private float _maxPitchDownHoldingBox = 30f;
    [SerializeField] private Transform _handPoint;
    [SerializeField] private Transform _dropPoint;
    [SerializeField] private Transform _phoneHandPoint;
    [SerializeField] private DeliveryNoteView _deliveryNoteView;
    [SerializeField] private GameSoundController _gameSoundController;

    public Transform PhoneHandPoint => _phoneHandPoint;
    public Transform HandPoint => _handPoint;
    public Transform DropPoint => _dropPoint;
    private float _pitch;
    public Canvas PlayerCanvas => _playerCanvas;
    public CharacterController Controller => _controller;
    public Camera PlayerCamera => _playerCamera;
    /// <summary> Родитель камеры (pitch). Для коротких процедурных сцен (головокружение и т.п.). </summary>
    public Transform CameraHolder => _cameraHolder;
    public Image PlayerDialog => _playerDialogLeftClient;
    public Image PlayerDialog1 => _playerDialogRightClient;
    public DeliveryNoteView DeliveryNoteView => _deliveryNoteView;

    private float GetCurrentMaxPitchDown()
    {
        if (HandsRegistry.Hands != null && HandsRegistry.Hands.Current is PackageHoldable)
            return _maxPitchDownHoldingBox;
        return _maxPitchDown;
    }

    public void Rotate(Vector2 lookDelta, bool isBlock)
    {
        if (isBlock == true )
            return;

        float yaw = lookDelta.x * _lookSpeed;
        float pitchDelta = -lookDelta.y * _lookSpeed;
        float maxPitch = GetCurrentMaxPitchDown();

        transform.Rotate(Vector3.up, yaw, Space.Self);

        _pitch = Mathf.Clamp(_pitch + pitchDelta, -90f, maxPitch);
        _cameraHolder.localEulerAngles = new Vector3(_pitch, 0f, 0f);
    }

    public void TeleportTo(Vector3 position, Quaternion rotation)
    {
        bool wasEnabled = _controller != null && _controller.enabled;

        if (_controller != null)
            _controller.enabled = false;

        transform.SetPositionAndRotation(position, rotation);

        if (_controller != null)
            _controller.enabled = wasEnabled;
    }

    public void SetCameraPitch(float pitchDegrees)
    {
        float maxPitch = GetCurrentMaxPitchDown();
        _pitch = Mathf.Clamp(pitchDegrees, -90f, maxPitch);
        if (_cameraHolder != null)
            _cameraHolder.localEulerAngles = new Vector3(_pitch, 0f, 0f);
    }

    [SerializeField] private float _dialogueCameraYOffset = 0.24f;
    private bool _dialogueCameraOffsetActive;

    [Header("Walk bob (только визуал, не влияет на позицию игрока и спавны)")]
    [Tooltip("Включить покачивание камеры при ходьбе.")]
    [SerializeField] private bool _walkBobEnabled = true;
    [Tooltip("Амплитуда вертикального покачивания (м).")]
    [SerializeField] private float _walkBobVerticalAmplitude = 0.015f;
    [Tooltip("Амплитуда горизонтального покачивания (м).")]
    [SerializeField] private float _walkBobHorizontalAmplitude = 0.008f;
    [Tooltip("Скорость цикла покачивания (рад/с).")]
    [SerializeField] private float _walkBobFrequency = 14f;
    [Tooltip("Скорость возврата к нулю при остановке.")]
    [SerializeField] private float _walkBobSmoothTime = 0.1f;

    private bool _isWalkingForBob;
    private float _walkBobTimer;
    private Vector3 _currentBobOffset;
    private Vector3 _walkBobVelocity;
    /// <summary> Исходная localPosition холдера камеры из префаба (высота глаз). </summary>
    private Vector3 _cameraHolderRestLocalPos;

    private void Awake()
    {
        if (_cameraHolder != null)
            _cameraHolderRestLocalPos = _cameraHolder.localPosition;
    }

    /// <summary> Поднять камеру на offset по Y во время диалога. </summary>
    public void ApplyDialogueCameraOffset()
    {
        _dialogueCameraOffsetActive = true;
    }

    /// <summary> Вернуть камеру на место после диалога. </summary>
    public void ClearDialogueCameraOffset()
    {
        _dialogueCameraOffsetActive = false;
    }

    /// <summary> Вызывается контроллером: покачивание только когда игрок идёт и на земле. </summary>
    public void SetWalkingForBob(bool isWalking)
    {
        _isWalkingForBob = isWalking;
    }

    private void LateUpdate()
    {
        ApplyCameraHolderLocalPosition();
    }

    private void ApplyCameraHolderLocalPosition()
    {
        if (_cameraHolder == null) return;

        Vector3 basePos = _cameraHolderRestLocalPos;
        if (_dialogueCameraOffsetActive)
            basePos.y += _dialogueCameraYOffset;

        if (!_walkBobEnabled)
        {
            _cameraHolder.localPosition = basePos;
            return;
        }

        if (_isWalkingForBob)
            _walkBobTimer += Time.deltaTime * _walkBobFrequency;

        float targetX = _isWalkingForBob ? Mathf.Sin(_walkBobTimer * 2f) * _walkBobHorizontalAmplitude : 0f;
        float targetY = _isWalkingForBob ? Mathf.Sin(_walkBobTimer) * _walkBobVerticalAmplitude : 0f;
        Vector3 targetBob = new Vector3(targetX, targetY, 0f);

        _currentBobOffset = Vector3.SmoothDamp(_currentBobOffset, targetBob, ref _walkBobVelocity, _walkBobSmoothTime);
        _cameraHolder.localPosition = basePos + _currentBobOffset;
    }

    public void LookAtPoint(Vector3 worldPoint)
    {
        Vector3 dir = (worldPoint - _playerCamera.transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    /// <summary> Синхронизировать внутренний pitch и поворот игрока с текущим мировым поворотом камеры (чтобы следующий кадр ввода не сбрасывал вид). </summary>
    public void SyncRotationFromCamera()
    {
        if (_playerCamera == null || _cameraHolder == null) return;
        Vector3 forward = _playerCamera.transform.forward;
        float pitchFromForward = Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg;
        float yawDeg = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        float maxPitch = GetCurrentMaxPitchDown();
        _pitch = Mathf.Clamp(-pitchFromForward, -90f, maxPitch);
        transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
        _cameraHolder.localEulerAngles = new Vector3(_pitch, 0f, 0f);
    }

    /// <summary> Назначить контроллер звуков (вызывается из PlayerInstaller после спавна). </summary>
    public void SetGameSoundController(GameSoundController soundController)
    {
        _gameSoundController = soundController;
    }

    /// <summary> Воспроизвести один шаг (вызывается контроллером при ходьбе). </summary>
    public void PlayFootstep()
    {
        _gameSoundController?.PlayFootstep();
    }

    /// <summary> Остановить звук шага (при остановке — не доигрывать до конца). </summary>
    public void StopFootstep()
    {
        _gameSoundController?.StopFootstep();
    }
}
