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
        [Tooltip("DialogueEntry ID ?? Dialogue Database")]
        public int entryID;

        [Tooltip("????? ??????? (null -> ?????? ????? ???????)")]
        public Sprite leftSprite;

        [Tooltip("?????? ??????? (null -> ?????? ?????? ???????)")]
        public Sprite rightSprite;

        [Tooltip("??? ?????? ???? ??????? ?? ???? ???????")]
        public SpeakerPriority priority;

        [Tooltip("???? true, ????????? ????? ?????? ? ?????? (override ???????)")]
        public bool useCenteredPositionOverride;
    }

    [System.Serializable]
    public class Step
    {
        [Tooltip("??? Conversation ? Dialogue Database")]
        public string conversation;

        [Tooltip("??????? ????????? ??? ????? conversation (?? Entry ID)")]
        public List<PortraitRule> rules = new();
    }

    [Header("Sequence (Queue)")]
    public List<Step> steps = new();

    [Header("Warehouse - wrong pickup")]
    public string wrongPackageConversation = "Warehouse_WrongPackage";
    public bool enforceCorrectAfterFirstWrong = true;

    [Header("Position Overrides")]
    public Vector2 centeredRightAnchoredPos = new Vector2(180f, 0f);    
    public Vector2 centeredLeftAnchoredPos = new Vector2(-180f, 0f);

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
        Dictionary<int, PortraitRule> stepDict;
        if (_lookupCache == null || !_lookupCache.TryGetValue(stepIndex, out stepDict)) return false;
        return stepDict.TryGetValue(entryID, out rule);
    }
}