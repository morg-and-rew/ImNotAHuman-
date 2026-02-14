using UnityEngine;

[CreateAssetMenu(menuName = "Configs/PlayerConfig")]
public class PlayerConfig : ScriptableObject
{
    public float MoveSpeed = 5f;
    public float SprintMultiplier = 1.8f;
    public float JumpSpeed = 7f;
    public float Gravity = -20f;
}
