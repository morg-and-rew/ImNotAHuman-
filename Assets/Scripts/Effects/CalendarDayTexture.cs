using UnityEngine;

/// <summary>
/// Меняет альbedo календаря в зависимости от дня: до шагов day2 — одна текстура, на day2+ — другая.
/// Вешается на объект с <see cref="Renderer"/> (или на родителя, если меш в дочернем объекте).
/// </summary>
[DisallowMultipleComponent]
public sealed class CalendarDayTexture : MonoBehaviour
{
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    [SerializeField] private GameFlowController _gameFlow;
    [SerializeField] private Texture2D _day1Texture;
    [SerializeField] private Texture2D _day2Texture;

    [Tooltip("Если у меша несколько материалов — включи, чтобы проставить текстуру во все слоты.")]
    [SerializeField] private bool _allMaterialSlots;

    private Renderer _renderer;
    private MaterialPropertyBlock _mpb;
    private Texture2D _lastApplied;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer == null)
            _renderer = GetComponentInChildren<Renderer>();
    }

    private void Start()
    {
        if (_gameFlow == null)
            _gameFlow = FindFirstObjectByType<GameFlowController>();
    }

    private void LateUpdate()
    {
        if (_renderer == null)
            return;

        bool day2 = _gameFlow != null && _gameFlow.IsDay2OrLater();
        Texture2D tex = day2 ? _day2Texture : _day1Texture;
        if (tex == null || tex == _lastApplied)
            return;

        _lastApplied = tex;
        _mpb ??= new MaterialPropertyBlock();
        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetTexture(BaseMapId, tex);
        _mpb.SetTexture(MainTexId, tex);

        if (_allMaterialSlots && _renderer.sharedMaterials != null && _renderer.sharedMaterials.Length > 1)
        {
            for (int i = 0; i < _renderer.sharedMaterials.Length; i++)
                _renderer.SetPropertyBlock(_mpb, i);
        }
        else
            _renderer.SetPropertyBlock(_mpb);
    }
}
