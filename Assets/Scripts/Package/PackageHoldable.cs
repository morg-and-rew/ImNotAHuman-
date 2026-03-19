using UnityEngine;

public sealed class PackageHoldable : HoldableViewBase, IHandPointProvider
{
    private const float RotationStepDeg = 90f;
    private const float AngleEpsilon = 0.5f;

    public override HoldableAvailability Availability => HoldableAvailability.WarehouseOnly;
    public HandPointType HandPointType => HandPointType.Default;

    [SerializeField] private Rigidbody _rb;
    [SerializeField] private Collider _col;
    [SerializeField] private PackageItem _packageItem;
    [Header("Hint")]
    [SerializeField] private Sprite _hintSprite;
    [Tooltip("Подсказка «Q — повернуть», когда коробка не в 0°")]
    [SerializeField] private Sprite _hintSpriteRotate;
    [Tooltip("Не та посылка по сюжету — E (диалог). Если пусто, берётся подсказка взять.")]
    [SerializeField] private Sprite _hintSpriteWrongPackage;
    [Header("Rotation")]
    [SerializeField, Min(10f)] private float _rotationSpeedDegPerSec = 180f;
    [Tooltip("Если включено — угол «взять» берётся из сцены при Start(). Иначе используется угол под этим чекбоксом (градусы 0–360).")]
    [SerializeField] private bool _takeAngleFromScene = true;
    [Tooltip("Используется только если Take Angle From Scene выключен. Угол Y (0–360°), при котором коробка «правильно» (номер к игроку).")]
    [SerializeField, Range(0f, 360f)] private float _takeAngleYOverride = 0f;

    private float _targetAngleY;
    private bool _isRotating;
    /// <summary> Угол Y, при котором коробка «правильно» повёрнута (номер к игроку). Заполняется в Start() из сцены или из _takeAngleYOverride. </summary>
    private float _takeAngleY;

    public int Number => _packageItem != null ? _packageItem.Number : 0;
    /// <summary> Подсказка: «Q — повернуть» если коробка не в позиции «взять», иначе подсказка взять. </summary>
    public Sprite HintSprite => CanPickupByRotation ? _hintSprite : _hintSpriteRotate;

    public Sprite HintSpriteWrongPackage =>
        _hintSpriteWrongPackage != null ? _hintSpriteWrongPackage : _hintSprite;
    /// <summary> Подсказка «Q — повернуть». Нужна и для «не той» посылки, когда она ещё не в позиции «взять». </summary>
    public Sprite HintSpriteRotate => _hintSpriteRotate;
    /// <summary> Взять можно только когда коробка повёрнута в «правильную» ориентацию (как в сцене — номер к игроку) и не идёт анимация. </summary>
    public bool CanPickupByRotation => !_isRotating && IsAngleAtTakePosition(GetCurrentAngleY());

    private void Reset()
    {
        _rb = GetComponent<Rigidbody>();
        _col = GetComponent<Collider>();
        _packageItem = GetComponentInParent<PackageItem>();
    }

    private void Awake()
    {
        if (_packageItem == null)
            _packageItem = GetComponentInParent<PackageItem>();
    }

    private void Start()
    {
        // Угол «взять»: из сцены (как расставлено — номер к игроку) или ручной в инспекторе. Делаем в Start(), чтобы все Awake и расстановка уже применились.
        if (_takeAngleFromScene)
            _takeAngleY = GetCurrentAngleY();
        else
            _takeAngleY = _takeAngleYOverride;

        SetRandomRotationStep();
    }

    private void Update()
    {
        if (!_isRotating) return;

        float current = GetCurrentAngleY();
        float step = _rotationSpeedDegPerSec * Time.deltaTime;
        float next = Mathf.MoveTowardsAngle(current, _targetAngleY, step);
        SetAngleY(next);

        if (Mathf.Abs(Mathf.DeltaAngle(next, _targetAngleY)) < AngleEpsilon)
        {
            SetAngleY(_targetAngleY);
            _isRotating = false;
        }
    }

    /// <summary> Поворот по часовой стрелке на один шаг (90°). Вызывается из контроллера по Q. </summary>
    public void RotateClockwise()
    {
        if (_isRotating) return;

        float current = GetCurrentAngleY();
        if (IsAngleAtTakePosition(current)) return;

        _targetAngleY = current - RotationStepDeg;
        if (_targetAngleY < 0f) _targetAngleY += 360f;
        _isRotating = true;
    }

    private float GetCurrentAngleY()
    {
        return Mathf.Repeat(transform.eulerAngles.y, 360f);
    }

    /// <summary> Текущий угол совпадает с «правильной» ориентацией для взятия (номер к игроку). </summary>
    private bool IsAngleAtTakePosition(float angleY)
    {
        return Mathf.Abs(Mathf.DeltaAngle(angleY, _takeAngleY)) < AngleEpsilon;
    }

    private void SetAngleY(float angleY)
    {
        Vector3 e = transform.eulerAngles;
        transform.eulerAngles = new Vector3(e.x, angleY, e.z);
    }

    /// <summary> На старте каждая коробка получает случайный поворот по Y из шагов 0°, 45°, 90° … 315°. </summary>
    private void SetRandomRotationStep()
    {
        int steps = Mathf.RoundToInt(360f / RotationStepDeg);
        int index = Random.Range(0, steps);
        float y = index * RotationStepDeg;
        SetAngleY(y);
    }

    public override void OnTaken(Transform handPoint)
    {
        if (handPoint == null) return;

        _packageItem?.NotifyTakenFromWarehouse();

        transform.SetParent(handPoint, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.detectCollisions = false;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
        if (_col != null) _col.enabled = false;
    }

    public override void OnDropped(Vector3 worldPos, Quaternion worldRot)
    {
        transform.SetParent(null);
        transform.position = worldPos;
        transform.rotation = worldRot;

        if (_col != null) _col.enabled = true;

        if (_rb != null)
        {
            _rb.isKinematic = false;
            _rb.detectCollisions = true;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }
}
