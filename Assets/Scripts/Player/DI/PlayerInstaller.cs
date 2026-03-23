using PixelCrushers.DialogueSystem;
using System;
using UnityEngine;

public sealed class PlayerInstaller : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerSpawner _playerSpawner;
    [SerializeField] private PlayerConfig _playerConfig;

    [Header("Input")]
    [SerializeField] private InputRebindMenu _rebindMenu;
    [SerializeField] private PlayerKeyBindings _bindings;

    [Header("Cameras")]
    [SerializeField] private Computer _computer;
    [SerializeField] private WindowView _windowView;
    [Tooltip("World Space Canvas, для которого нужна камера игрока (Event Camera). Например Canvas под Dialogue Manager с кнопками компа.")]
    [SerializeField] private Canvas _eventCameraCanvas;

    [Header("Client")]
    [SerializeField] private ClientInteraction _clientInteraction;

    [SerializeField] private WarehouseExitTrigger _warehouseExit;
    [SerializeField] private GameFlowController _gameFlowController;
    [SerializeField] private GameSoundController _gameSoundController;

    [Header("Phone")]
    [SerializeField] private PhoneItemView _phoneItemView;
    [SerializeField] private PhoneUIView _phoneUIView;

    [Header("WarehouseDelivery")]
    [SerializeField] private WarehouseDeliveryController _warehouseDeliveryController;
    [SerializeField] private DialogueSystemController _dialogueSystemController;

    [Header("Phone Pose")]
    [SerializeField] private Transform _phoneUsePoint;
    [SerializeField] private Transform _phoneLookTarget;
    [Tooltip("При опускании/закрытии телефона возвращать игрока и камеру на место (камера никуда не уезжает).")]
    [SerializeField] private bool _returnAfterPhoneClose = true;

    [Header("Debug")]
    [Tooltip("Рисовать луч взаимодействия в сцене (линия от камеры до точки попадания).")]
    [SerializeField] private bool _drawInteractionRays;

    [Header("Screenshot / Build")]
    [Tooltip("Включить режим полёта камеры по сцене (WASD + Q/E вверх-вниз, мышь — обзор). Для скриншотов в билде.")]
    [SerializeField] private bool _flyModeForScreenshots;
    [SerializeField] private float _flySpeed = 8f;
    [SerializeField] private float _flyLookSpeed = 3f;

    private PlayerView _playerView;
    private IPlayerInput _playerInput;
    private PhoneController _phoneController;
    private PlayerController _playerController;
    private PlayerInteractionController _interactionController;
    private PlayerCameraBob _cameraController;
    private PlayerWindowView _playerWindowView;
    private PlayerLightSwitch _playerLightSwitch;
    private InteractionRaycastCache _raycastCache;
    private LineRenderer _interactionRayLine;
    private bool _flyModeWasActive;
    private float _flyPitch;
    private float _flyYaw;

    private void Awake()
    {
        _playerView = _playerSpawner.SpawnPlayer();
        PlayerView playerView = _playerView;
        if (_gameSoundController == null)
            _gameSoundController = GameSoundController.Instance;
        if (_gameSoundController != null)
            playerView.SetGameSoundController(_gameSoundController);

        _raycastCache = new InteractionRaycastCache();

        _bindings = new PlayerKeyBindings();
        _rebindMenu.Initialize(_bindings);

        _clientInteraction.Initialize(playerView.PlayerCanvas, playerView.PlayerDialog, playerView.PlayerDialog1, (ICustomDialogueUI)_dialogueSystemController.DialogueUI);

        if (GetComponent<ClientDialogueDepthOfFieldController>() == null)
            gameObject.AddComponent<ClientDialogueDepthOfFieldController>();

        PlayerModel model = new PlayerModel(_playerConfig);
        _playerInput = new PlayerInputPC(_bindings);

        _playerController = new PlayerController(model, playerView, _playerInput);
        _playerWindowView = new PlayerWindowView(_playerInput, _playerController, playerView);

        PlayerHands hands = new PlayerHands();
        HandsRegistry.Set(hands);

        _cameraController = new PlayerCameraBob();
        _cameraController.Initialize(_playerInput, _computer, _playerController);

        _playerLightSwitch = new PlayerLightSwitch();
        _playerLightSwitch.Initialize(_playerInput);

        _interactionController = new PlayerInteractionController(playerView, _playerInput, hands, _clientInteraction, _raycastCache, _gameFlowController);

        _computer.Initialize(playerView.PlayerCamera);

        // Ссылка нужна корутине стартового меню, которую GameFlowController запускает внутри Init().
        _gameFlowController.SetInputRebindMenu(_rebindMenu);

        _gameFlowController.Init(playerView, _playerController, _playerInput, _clientInteraction, playerView.DeliveryNoteView,
            _dialogueSystemController?.DialogueUI as CustomDialogueUI);

        if (_phoneUIView != null)
            _phoneUIView.SetEventCamera(playerView.PlayerCamera);

        if (_eventCameraCanvas != null && playerView.PlayerCamera != null)
        {
            _eventCameraCanvas.worldCamera = playerView.PlayerCamera;
        }

        Func<bool> isConversationActive = () => _clientInteraction != null && _clientInteraction.IsActive;

        if (_phoneItemView != null && _phoneUIView != null && _gameFlowController != null && _playerInput != null)
        {
            PhoneModel phoneModel = new PhoneModel();
            PhoneCallService phoneService = new PhoneCallService();

            _phoneController = new PhoneController(
                model: phoneModel,
                itemView: _phoneItemView,
                uiView: _phoneUIView,
                callService: phoneService,
                isConversationActive: isConversationActive,
                blocker: _playerController,
                flow: _gameFlowController,
                playerView: playerView,
                usePoint: _phoneUsePoint,
                lookTarget: _phoneLookTarget,
                returnOnClose: _returnAfterPhoneClose,
                gameSoundController: _gameSoundController
            );

            _ = new PhoneStoryWiring(phoneService, _gameFlowController, _gameSoundController);
        }

        _warehouseDeliveryController.Initialize(hands, playerView.DeliveryNoteView);
    }

    private void Update()
    {
        if (_flyModeForScreenshots)
        {
            if (!_flyModeWasActive)
            {
                _flyModeWasActive = true;
                _playerController?.SetBlock(true);
                if (_playerView != null && _playerView.Controller != null)
                    _playerView.Controller.enabled = false;
                if (_playerView?.PlayerCamera != null && _playerView.PlayerCamera.transform.parent != null)
                {
                    float x = _playerView.PlayerCamera.transform.parent.localEulerAngles.x;
                    _flyPitch = x > 180f ? x - 360f : x;
                }
                _flyYaw = _playerView != null ? _playerView.transform.eulerAngles.y : 0f;
                _gameFlowController?.SetFlyMode(true);
                if (DialogueManager.isConversationActive)
                    DialogueManager.StopConversation();
                TutorialHintView.Instance?.Hide();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                if (_clientInteraction != null) _clientInteraction.enabled = false;
            }
            if (DialogueManager.isConversationActive)
                DialogueManager.StopConversation();
            DoFlyMode();
            return;
        }

        if (_flyModeWasActive)
        {
            _flyModeWasActive = false;
            _playerController?.SetBlock(false);
            if (_playerView != null && _playerView.Controller != null)
                _playerView.Controller.enabled = true;
            _gameFlowController?.SetFlyMode(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (_clientInteraction != null) _clientInteraction.enabled = true;
        }

        if (_playerView?.PlayerCamera != null)
        {
            if (_drawInteractionRays)
            {
                _raycastCache.RefreshAndDrawDebug(_playerView.PlayerCamera);
                EnsureInteractionRayLine(_playerView.PlayerCamera);
                _raycastCache.GetDebugLine(out Vector3 origin, out Vector3 end);
                _interactionRayLine.SetPosition(0, origin);
                _interactionRayLine.SetPosition(1, end);
                _interactionRayLine.enabled = true;
            }
            else
            {
                _raycastCache.Refresh(_playerView.PlayerCamera);
                if (_interactionRayLine != null)
                    _interactionRayLine.enabled = false;
            }
        }

        _phoneController?.Tick();
        _playerController.Tick();
        _interactionController.Tick();
        _cameraController.Tick();
        _playerWindowView.Tick();
        _playerLightSwitch.Tick();
    }

    private void DoFlyMode()
    {
        if (_playerView == null || _playerView.PlayerCamera == null) return;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Transform cam = _playerView.PlayerCamera.transform;
        Transform holder = cam.parent;
        Transform body = _playerView.transform;

        Vector2 look = _playerInput != null ? _playerInput.LookDelta : new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        float mx = look.x * _flyLookSpeed;
        float my = look.y * _flyLookSpeed;

        _flyYaw += mx;
        _flyPitch = Mathf.Clamp(_flyPitch - my, -89f, 89f);

        body.rotation = Quaternion.Euler(0f, _flyYaw, 0f);
        if (holder != null && holder != body)
            holder.localEulerAngles = new Vector3(_flyPitch, 0f, 0f);
        else if (holder == body)
            body.rotation = Quaternion.Euler(_flyPitch, _flyYaw, 0f);

        Vector3 move = Vector3.zero;
        if (UnityEngine.Input.GetKey(KeyCode.W)) move += cam.forward;
        if (UnityEngine.Input.GetKey(KeyCode.S)) move -= cam.forward;
        if (UnityEngine.Input.GetKey(KeyCode.D)) move += cam.right;
        if (UnityEngine.Input.GetKey(KeyCode.A)) move -= cam.right;
        if (UnityEngine.Input.GetKey(KeyCode.E)) move += Vector3.up;
        if (UnityEngine.Input.GetKey(KeyCode.Q)) move -= Vector3.up;
        if (move.sqrMagnitude > 0.01f)
        {
            move.Normalize();
            body.position += move * _flySpeed * Time.deltaTime;
        }
    }

    private void EnsureInteractionRayLine(Camera camera)
    {
        if (_interactionRayLine != null) return;

        var go = new GameObject("InteractionRayLine");
        go.transform.SetParent(camera.transform, worldPositionStays: false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        _interactionRayLine = go.AddComponent<LineRenderer>();
        _interactionRayLine.useWorldSpace = true;
        _interactionRayLine.positionCount = 2;
        _interactionRayLine.startWidth = 0.02f;
        _interactionRayLine.endWidth = 0.005f;
        _interactionRayLine.startColor = Color.green;
        _interactionRayLine.endColor = Color.red;

        Shader shader = Shader.Find("Unlit/Color");
        if (shader != null)
            _interactionRayLine.material = new Material(shader) { color = Color.green };
        else
            _interactionRayLine.material = new Material(Shader.Find("Sprites/Default")) { color = Color.green };
    }
}
