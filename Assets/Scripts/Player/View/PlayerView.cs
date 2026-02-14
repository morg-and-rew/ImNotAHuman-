using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CharacterController))]
public sealed class PlayerView : MonoBehaviour
{
    [SerializeField] private CharacterController _controller;
    [SerializeField] private Transform _cameraHolder;
    [SerializeField] private Camera _playerCamera;
    [SerializeField] private Canvas _playerCanvas;  
    [SerializeField] private Image _playerDialogLeftClient;
    [SerializeField] private Image _playerDialogRightClient;
    [SerializeField] private float _lookSpeed = 3f;
    [SerializeField] private Transform _handPoint;
    [SerializeField] private Transform _dropPoint;
    [SerializeField] private Transform _phoneHandPoint;
    [SerializeField] private DeliveryNoteView _deliveryNoteView;

    public Transform PhoneHandPoint => _phoneHandPoint;
    public Transform HandPoint => _handPoint;
    public Transform DropPoint => _dropPoint;
    private float _pitch;
    public Canvas PlayerCanvas => _playerCanvas;
    public CharacterController Controller => _controller;
    public Camera PlayerCamera => _playerCamera;
    public Image PlayerDialog => _playerDialogLeftClient;
    public Image PlayerDialog1 => _playerDialogRightClient;
    public DeliveryNoteView DeliveryNoteView => _deliveryNoteView;

    public void Rotate(Vector2 lookDelta, bool isBlock)
    {
        if (isBlock == true )
            return;

        float yaw = lookDelta.x * _lookSpeed;
        float pitchDelta = -lookDelta.y * _lookSpeed;

        transform.Rotate(Vector3.up, yaw, Space.Self);

        _pitch = Mathf.Clamp(_pitch + pitchDelta, -90f, 90f);
        _cameraHolder.localEulerAngles = new Vector3(_pitch, 0f, 0f);
    }

    public void TeleportTo(Vector3 position, Quaternion rotation)
    {
        bool wasEnabled = _controller != null && _controller.enabled;

        if (_controller != null)
            _controller.enabled = false;

        transform.SetPositionAndRotation(position, rotation);

        if (_controller != null)
            _controller.enabled = wasEnabled;
    }

    public void SetCameraPitch(float pitchDegrees)
    {
        _pitch = Mathf.Clamp(pitchDegrees, -90f, 90f);
        if (_cameraHolder != null)
            _cameraHolder.localEulerAngles = new Vector3(_pitch, 0f, 0f);
    }

    public void LookAtPoint(Vector3 worldPoint)
    {
        Vector3 dir = (worldPoint - _playerCamera.transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }
}
