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
    [SerializeField] private bool _returnAfterPhoneClose = true;

    private PlayerView _playerView;
    private PhoneController _phoneController;
    private PlayerController _playerController;
    private PlayerInteractionController _interactionController;
    private PlayerCameraBob _cameraController;
    private PlayerWindowView _playerWindowView;
    private PlayerLightSwitch _playerLightSwitch;
    private InteractionRaycastCache _raycastCache;

    private void Awake()
    {
        _playerView = _playerSpawner.SpawnPlayer();
        PlayerView playerView = _playerView;

        _raycastCache = new InteractionRaycastCache();

        _bindings = new PlayerKeyBindings();
        _rebindMenu.Initialize(_bindings);

        _clientInteraction.Initialize(playerView.PlayerCanvas, playerView.PlayerDialog, playerView.PlayerDialog1, (ICustomDialogueUI)_dialogueSystemController.DialogueUI);

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

        _interactionController = new PlayerInteractionController(playerView, input, hands, _clientInteraction, _raycastCache);

        _computer.Initialize(playerView.PlayerCamera);

        _gameFlowController.Init(playerView, _playerController, input, _clientInteraction, playerView.DeliveryNoteView,
            _dialogueSystemController?.DialogueUI as CustomDialogueUI);

        if (_phoneUIView != null)
            _phoneUIView.SetEventCamera(playerView.PlayerCamera);

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
            _raycastCache.Refresh(_playerView.PlayerCamera);

        _phoneController?.Tick();
        _playerController.Tick();
        _interactionController.Tick();
        _cameraController.Tick();
        _playerWindowView.Tick();
        _playerLightSwitch.Tick();
    }
}
