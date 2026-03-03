using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Dialogue/Client Portrait Map (Sequence)", fileName = "ClientPortraitMap_Sequence")]
public sealed class ClientPortraitMap : ScriptableObject
{
    public enum SpeakerPriority
    {
        None,
        Left,
        Right
    }

    [System.Serializable]
    public struct PortraitRule
    {
        [Tooltip("ID реплики в этом разговоре. В Dialogue Editor: выбери conversation (например Client_Day1.4) → у каждой реплики в списке слева указан ID.")]
        public int entryID;

        [Tooltip("Левый спрайт (null — скрыть левый портрет)")]
        public Sprite leftSprite;

        [Tooltip("Правый спрайт (null — скрыть правый портрет)")]
        public Sprite rightSprite;

        [Header("Цвет (тинт) спрайтов")]
        [Tooltip("Цвет левого портрета (белый = без изменений). Меняет Image.color. (0,0,0,0) = белый.")]
        public Color leftSpriteColor;
        [Tooltip("Цвет правого портрета (белый = без изменений). (0,0,0,0) = белый.")]
        public Color rightSpriteColor;

        [Tooltip("Кто поверх: левый или правый портрет")]
        public SpeakerPriority priority;

        [Tooltip("Если true — портреты в центре (override позиции)")]
        public bool useCenteredPositionOverride;

        [Tooltip("Масштаб левого портрета для этой реплики. 0 = использовать из SO (Centered Left Scale). >0 = этот масштаб (например 1.1 — крупнее).")]
        [Min(0f)] public float leftScale;

        [Tooltip("Масштаб правого портрета для этой реплики. 0 = из SO. >0 = этот масштаб.")]
        [Min(0f)] public float rightScale;

        [Header("Своя позиция и размер для реплики")]
        [Tooltip("Включить: для этой реплики задать вручную позицию, масштаб и поворот портретов (игнорируются центрированные настройки).")]
        public bool useCustomPositionAndSize;

        [Tooltip("Позиция левого портрета на экране (X,Y — пиксели, Z — глубина слоя).")]
        public Vector3 customLeftAnchoredPos;
        [Tooltip("Масштаб левого портрета по осям (X,Y,Z). Оставь (0,0,0) — будет как 1,1,1.")]
        public Vector3 customLeftScale;
        [Tooltip("Поворот левого портрета в градусах (X, Y, Z — углы Эйлера).")]
        public Vector3 customLeftRotation;
        [Tooltip("Позиция правого портрета на экране (X,Y,Z).")]
        public Vector3 customRightAnchoredPos;
        [Tooltip("Масштаб правого портрета по осям. (0,0,0) = как 1,1,1.")]
        public Vector3 customRightScale;
        [Tooltip("Поворот правого портрета в градусах (X, Y, Z — углы Эйлера).")]
        public Vector3 customRightRotation;
    }

    [System.Serializable]
    public class Step
    {
        [Header("Шаг = один диалог (conversation) + правила по репликам (Entry ID)")]
        [Tooltip("Название разговора (Conversation) из Dialogue Database — как в списке диалогов, например: Client_Day1.4, Client_Day1.5")]
        public string conversation;

        [Tooltip("Правила портретов по репликам: каждый элемент = одна реплика. entryID внутри правила = ID реплики (Dialogue Entry) в этом разговоре.")]
        public List<PortraitRule> rules = new();

        [Tooltip("После этого Entry ID посылка убирается из рук. 0 = не убирать. Укажи ID реплики из этого же разговора (conversation), после которой посылка должна пропасть.")]
        public int removePackageFromHandsAfterEntryID;
    }

    [Header("Sequence (Queue)")]
    public List<Step> steps = new();

    [Header("Warehouse - wrong pickup")]
    public string wrongPackageConversation = "Warehouse_WrongPackage";
    public bool enforceCorrectAfterFirstWrong = true;

    [Header("Position Overrides (когда клиенты говорят между собой)")]
    [Tooltip("Позиция правого портрета (X, Y, Z). Z — глубина для сортировки/слоёв.")]
    public Vector3 centeredRightAnchoredPos = new Vector3(-43f, 73f, 0f);
    [Tooltip("Позиция левого портрета (X, Y, Z).")]
    public Vector3 centeredLeftAnchoredPos = new Vector3(100f, 73f, 0f);
    [Tooltip("Масштаб левого портрета в режиме «между собой» (1 = без изменения).")]
    [Min(0.01f)] public float centeredLeftScale = 1f;
    [Tooltip("Масштаб правого портрета в режиме «между собой» (например 1.1 — чуть крупнее).")]
    [Min(0.01f)] public float centeredRightScale = 1.1f;
    [Tooltip("Поворот правого портрета в режиме «между собой» (углы Эйлера: X, Y, Z в градусах).")]
    public Vector3 centeredRightRotation = Vector3.zero;

    private Dictionary<int, Dictionary<int, PortraitRule>> _lookupCache;


    private void OnEnable()
    {
        BuildLookupCache();
    }

    private void BuildLookupCache()
    {
        if (steps == null) return;
        _lookupCache = new Dictionary<int, Dictionary<int, PortraitRule>>();
        for (int i = 0; i < steps.Count; i++)
        {
            List<PortraitRule> stepRules = steps[i].rules;
            if (stepRules == null) continue;
            Dictionary<int, PortraitRule> stepDict = new Dictionary<int, PortraitRule>();
            foreach (PortraitRule rule in stepRules)
            {
                stepDict[rule.entryID] = rule;
            }
            _lookupCache[i] = stepDict;
        }
    }

    public int StepsCount => steps != null ? steps.Count : 0;

    public string GetConversation(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= StepsCount) return "";
        return steps[stepIndex].conversation;
    }

    public bool TryGetRule(int stepIndex, int entryID, out PortraitRule rule)
    {
        rule = default;
        if (_lookupCache == null) BuildLookupCache();
        Dictionary<int, PortraitRule> stepDict;
        if (_lookupCache == null || !_lookupCache.TryGetValue(stepIndex, out stepDict)) return false;
        return stepDict.TryGetValue(entryID, out rule);
    }
}