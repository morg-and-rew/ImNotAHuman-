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

    private PlayerView _playerView;
    private PhoneController _phoneController;
    private PlayerController _playerController;
    private PlayerInteractionController _interactionController;
    private PlayerCameraBob _cameraController;
    private PlayerWindowView _playerWindowView;
    private PlayerLightSwitch _playerLightSwitch;
    private InteractionRaycastCache _raycastCache;
    private LineRenderer _interactionRayLine;

    private void Awake()
    {
        _playerView = _playerSpawner.SpawnPlayer();
        PlayerView playerView = _playerView;

        _raycastCache = new InteractionRaycastCache();

        _bindings = new PlayerKeyBindings();
        _rebindMenu.Initialize(_bindings);

        _clientInteraction.Initialize(playerView.PlayerCanvas, playerView.PlayerDialog, playerView.PlayerDialog1, (ICustomDialogueUI)_dialogueSystemController.DialogueUI);

        if (GetComponent<ClientDialogueDepthOfFieldController>() == null)
            gameObject.AddComponent<ClientDialogueDepthOfFieldController>();

        PlayerModel model = new PlayerModel(_playerConfig);
        IPlayerInput input = new PlayerInputPC(_bindings);

        _playerController = new PlayerController(model, playerView, input);
        _playerWindowView = new PlayerWindowView(input, _playerController, playerView);

        PlayerHands hands = new PlayerHands();
        HandsRegistry.Set(hands);

        _cameraController = new PlayerCameraBob();
        _cameraController.Initialize(input, _computer, _playerController);

        _playerLightSwitch = new PlayerLightSwitch();
        _playerLightSwitch.Initialize(input);

        _interactionController = new PlayerInteractionController(playerView, input, hands, _clientInteraction, _raycastCache, _gameFlowController);

        _computer.Initialize(playerView.PlayerCamera);

        _gameFlowController.Init(playerView, _playerController, input, _clientInteraction, playerView.DeliveryNoteView,
            _dialogueSystemController?.DialogueUI as CustomDialogueUI);

        if (_phoneUIView != null)
            _phoneUIView.SetEventCamera(playerView.PlayerCamera);

        if (_eventCameraCanvas != null && playerView.PlayerCamera != null)
        {
            _eventCameraCanvas.worldCamera = playerView.PlayerCamera;
        }

        Func<bool> isConversationActive = () => _clientInteraction != null && _clientInteraction.IsActive;

        if (_phoneItemView != null && _phoneUIView != null && _gameFlowController != null)
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
                returnOnClose: _returnAfterPhoneClose
            );

            _ = new PhoneStoryWiring(phoneService, _gameFlowController);
        }

        _warehouseDeliveryController.Initialize(hands, playerView.DeliveryNoteView);
    }

    private void Update()
    {
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
