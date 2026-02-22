using System.Collections;
using UnityEngine;

public sealed class PlayerController : IPlayerBlocker
{
    private readonly PlayerModel _model;
    private readonly PlayerView _view;
    private readonly IPlayerInput _input;

    private bool _movementBlocked;
    private bool _lookBlocked;

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
        }
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

        controller.Move(_model.Velocity * Time.deltaTime);
    }

    public void SetBlock(bool isMoving)
    {
        // #region agent log
        Debug.Log($"[Day1.5.1] PlayerController.SetBlock: value=" + isMoving + " (движение/взор " + (isMoving ? "заблокированы" : "разблокированы") + ")");
        if (isMoving)
            AgentDebugLog.Log("PlayerController.SetBlock", "block", "{\"value\":true}", "H_block");
        if (!isMoving && _movementBlocked)
            AgentDebugLog.Log("PlayerController.cs:SetBlock", "unblock", "{\"value\":false}", "H_block_reset");
        // #endregion
        _movementBlocked = isMoving;
        _lookBlocked = isMoving;
    }
}
