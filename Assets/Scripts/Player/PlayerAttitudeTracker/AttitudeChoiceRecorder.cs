using UnityEngine;
using PixelCrushers.DialogueSystem;

public sealed class AttitudeChoiceRecorder : MonoBehaviour
{
    [SerializeField] private string _variableName = "attitude";

    public PlayerAttitudeStats Stats { get; } = new PlayerAttitudeStats();

    public void RecordFromLua()
    {
        int v = DialogueLua.GetVariable(_variableName).AsInt;

        PlayerAttitude attitude =
            v == 1 ? PlayerAttitude.Mystical :
            v == 2 ? PlayerAttitude.Skeptical :
            PlayerAttitude.Neutral;

        Stats.Add(attitude);
    }
}
