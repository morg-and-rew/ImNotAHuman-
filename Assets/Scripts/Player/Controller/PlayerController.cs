using System.Collections;
using UnityEngine;

public sealed class PlayerController : IPlayerBlocker
{
    private readonly PlayerModel _model;
    private readonly PlayerView _view;
    private readonly IPlayerInput _input;

    private bool _movementBlocked;
    private bool _lookBlocked;

    public bool IsInputBlocked => _movementBlocked;
    private float _footstepCooldown;
    private const float FootstepInterval = 0.45f;

    public PlayerController(PlayerModel model, PlayerView view, IPlayerInput input)
    {
        _model = model;
        _view = view;
        _input = input;
    }

    public void Tick()
    {
        Vector2 input = _input.MoveAxis;
        bool isMoving = input.sqrMagnitude > 0.01f;

        if (!_lookBlocked)
        {
            HandleLook();
        }

        if (!_movementBlocked)
        {
            HandleMovement();
            if (isMoving && _model.IsGrounded)
            {
                _footstepCooldown -= Time.deltaTime;
                if (_footstepCooldown <= 0f)
                {
                    _view.PlayFootstep();
                    _footstepCooldown = FootstepInterval;
                }
            }
            else
            {
                _footstepCooldown = FootstepInterval;
                _view.StopFootstep();
            }
        }
        else
        {
            _footstepCooldown = FootstepInterval;
            _view.StopFootstep();
        }

        bool isWalkingForBob = !_movementBlocked && isMoving && _model.IsGrounded;
        _view.SetWalkingForBob(isWalkingForBob);
    }

    private void HandleLook()
    {
        Vector2 lookDelta = _input.LookDelta;
        _view.Rotate(lookDelta, _lookBlocked);
    }

    private void HandleMovement()
    {
        CharacterController controller = _view.Controller;

        if (controller == null) 
            return;

        _model.SetGrounded(controller.isGrounded);

        Vector2 moveInput = _input.MoveAxis;
        Vector3 moveLocal = new Vector3(moveInput.x, 0f, moveInput.y);
        moveLocal = Vector3.ClampMagnitude(moveLocal, 1f);

        float speed = _model.MoveSpeed;

        Vector3 moveWorld = _view.transform.TransformDirection(moveLocal) * speed;
        Vector3 horizontal = new Vector3(moveWorld.x, _model.Velocity.y, moveWorld.z);

        _model.SetHorizontalVelocity(horizontal);

        _model.ApplyGravity(Time.deltaTime);

        float yBefore = _view.transform.position.y;
        controller.Move(_model.Velocity * Time.deltaTime);
        if (_view.transform.position.y > yBefore)
        {
            Vector3 p = _view.transform.position;
            _view.transform.position = new Vector3(p.x, yBefore, p.z);
        }
    }

    public void SetBlock(bool isMoving)
    {
        _movementBlocked = isMoving;
        _lookBlocked = isMoving;
    }
}
