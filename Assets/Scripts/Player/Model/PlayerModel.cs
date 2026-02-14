using UnityEngine;

public class PlayerModel
{
    private Vector3 _velocity;
    private bool _isGrounded;
    private readonly PlayerConfig _config;

    public PlayerModel(PlayerConfig config)
    {
        _config = config;
    }

    public Vector3 Velocity => _velocity;
    public bool IsGrounded => _isGrounded;
    public float MoveSpeed => _config.MoveSpeed;
    public float SprintMultiplier => _config.SprintMultiplier;
    public float JumpSpeed => _config.JumpSpeed;

    public void SetGrounded(bool grounded)
    {
        _isGrounded = grounded;

        if (grounded && _velocity.y < 0f)
        {
            _velocity = new Vector3(_velocity.x, -1f, _velocity.z);
        }
    }

    public void SetHorizontalVelocity(Vector3 horizontal)
    {
        _velocity = new Vector3(horizontal.x, _velocity.y, horizontal.z);
    }

    public void AddVerticalVelocity(float vertical)
    {
        _velocity = new Vector3(_velocity.x, vertical, _velocity.z);
    }

    public void ApplyGravity(float deltaTime)
    {
        _velocity += Vector3.up * _config.Gravity * deltaTime;
    }
}
