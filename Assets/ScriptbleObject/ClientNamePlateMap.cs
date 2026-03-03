using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Отдельная мапа спрайтов имён говорящих (плашки «Бабушка», «Клиент» и т.д.).
/// Если для реплики нет правила с таким entryID — используется правило с предыдущим entryID (ничего не меняем).
/// </summary>
[CreateAssetMenu(menuName = "Game/Dialogue/Client Name Plate Map", fileName = "ClientNamePlateMap")]
public sealed class ClientNamePlateMap : ScriptableObject
{
    [Serializable]
    public struct NameRule
    {
        [Tooltip("ID реплики (Dialogue Entry). Если для текущей реплики нет правила — используется правило с наибольшим entryID меньше текущего (предыдущий).")]
        public int entryID;
        [Tooltip("Спрайт с именем левого говорящего. null — не показывать.")]
        public Sprite nameSprite;
        [Tooltip("Цвет спрайта имени левого. (0,0,0,0) = белый.")]
        public Color nameSpriteColor;
        [Tooltip("Спрайт с именем правого говорящего. null — не показывать.")]
        public Sprite nameSpriteRight;
        [Tooltip("Цвет спрайта имени правого. (0,0,0,0) = белый.")]
        public Color nameSpriteColorRight;
    }

    [Serializable]
    public class Step
    {
        [Tooltip("Название разговора (Conversation), например Client_Day1.4")]
        public string conversation;
        [Tooltip("Правила по репликам. Сортировка по entryID: для реплики без своей записи берётся последнее правило с entryID ≤ ID реплики.")]
        public List<NameRule> rules = new List<NameRule>();
    }

    [Header("Шаги = диалоги (порядок и conversation как в Client Portrait Map)")]
    public List<Step> steps = new List<Step>();

    private List<List<NameRule>> _sortedRulesCache;

    private void OnEnable()
    {
        BuildCache();
    }

    private void BuildCache()
    {
        if (steps == null)
        {
            _sortedRulesCache = null;
            return;
        }
        _sortedRulesCache = new List<List<NameRule>>(steps.Count);
        for (int i = 0; i < steps.Count; i++)
        {
            var list = steps[i]?.rules;
            if (list == null || list.Count == 0)
            {
                _sortedRulesCache.Add(new List<NameRule>());
                continue;
            }
            var sorted = new List<NameRule>(list);
            sorted.Sort((a, b) => a.entryID.CompareTo(b.entryID));
            _sortedRulesCache.Add(sorted);
        }
    }

    /// <summary> Индекс шага по названию разговора. </summary>
    public int FindStepIndexByConversation(string conversation)
    {
        if (steps == null || string.IsNullOrEmpty(conversation)) return -1;
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] != null && string.Equals(steps[i].conversation, conversation, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    /// <summary> Правило для имён: берётся правило с наибольшим entryID ≤ текущего (если id нет — предыдущий). </summary>
    public bool TryGetRule(int stepIndex, int entryID, out NameRule rule)
    {
        rule = default;
        if (_sortedRulesCache == null) BuildCache();
        if (stepIndex < 0 || stepIndex >= _sortedRulesCache.Count) return false;
        var sorted = _sortedRulesCache[stepIndex];
        if (sorted == null || sorted.Count == 0) return false;
        int bestIndex = -1;
        for (int i = 0; i < sorted.Count; i++)
        {
            if (sorted[i].entryID <= entryID)
                bestIndex = i;
            else
                break;
        }
        if (bestIndex < 0) return false;
        rule = sorted[bestIndex];
        return true;
    }
}
