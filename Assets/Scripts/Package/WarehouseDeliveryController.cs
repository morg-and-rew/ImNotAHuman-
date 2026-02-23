using System.Collections.Generic;
using UnityEngine;

public sealed class WarehouseDeliveryController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PackageRegistry _registry;

    private DeliveryNoteView _noteView;
    private PlayerHands _hands;

    public int RequiredNumber { get; private set; }
    public bool HasActiveTask { get; private set; }

    private readonly List<int> _rotation = new();
    private int _rotationIndex = 0;
    private int _lastIssuedNumber = -1;

    public void Initialize(PlayerHands hands, DeliveryNoteView noteView)
    {
        _hands = hands;

        if (_hands != null)
        {
            _hands.Taken -= OnItemTaken;
            _hands.Taken += OnItemTaken;
        }

        _noteView = noteView;
    }

    public void StartFixedDeliveryTask(int number, bool enforceOnlyAfterWrong = false)
    {
        if (number <= 0) return;
        RequiredNumber = number;
        HasActiveTask = false;
        GameStateService.SetRequiredPackage(RequiredNumber, enforceOnlyAfterWrong);
        GameStateService.SetPackageDropLocked(false);
        _noteView?.ShowNumber(RequiredNumber);
    }

    public void StartNewDeliveryTask(bool enforceOnlyAfterWrong = false)
    {
        if (_registry == null)
            return;

        RebuildRotation();

        if (_rotation.Count == 0)
            return;

        int number = _rotation[_rotationIndex];
        _rotationIndex++;

        if (_rotation.Count > 1 && number == _lastIssuedNumber)
        {
            if (_rotationIndex >= _rotation.Count)
                RebuildRotation();

            int alt = _rotation[_rotationIndex];
            _rotationIndex++;

            _lastIssuedNumber = alt;
            number = alt;
        }
        else
        {
            _lastIssuedNumber = number;
        }

        RequiredNumber = number;
        HasActiveTask = false;

        GameStateService.SetRequiredPackage(RequiredNumber, enforceOnlyAfterWrong);
        GameStateService.SetPackageDropLocked(false);

        _noteView?.ShowNumber(RequiredNumber);
    }

    private void RebuildRotation()
    {
        _rotation.Clear();
        _rotationIndex = 0;

        List<int> nums = _registry.GetNumbersForRandomDelivery();
        if (nums == null || nums.Count == 0)
            return;

        _rotation.AddRange(nums);

        for (int i = _rotation.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (_rotation[i], _rotation[j]) = (_rotation[j], _rotation[i]);
        }

        if (_rotation.Count > 1 && _rotation[0] == _lastIssuedNumber)
        {
            int swapIndex = 1;
            (_rotation[0], _rotation[swapIndex]) = (_rotation[swapIndex], _rotation[0]);
        }
    }

    public bool IsCorrectPackage(PackageHoldable package)
    {
        if (package == null) return false;
        return package.Number == RequiredNumber;
    }

    public void ClearTask()
    {
        HasActiveTask = true;
        RequiredNumber = 0;

        _noteView?.Hide();

        GameStateService.SetRequiredPackage(0, enforceOnly: false);
        GameStateService.SetPackageDropLocked(false);
    }

    public void ShowNoteForNumber(int number)
    {
        if (number > 0)
            _noteView?.ShowNumber(number);
        else
            _noteView?.Hide();
    }

    private void OnItemTaken(IHoldable holdable)
    {
        if (holdable is PackageHoldable)
        {
            _noteView?.Hide();
            GameStateService.SetPackageDropLocked(true);
        }
    }
}
