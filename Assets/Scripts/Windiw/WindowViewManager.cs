using System.Collections.Generic;
using UnityEngine;

public sealed class WindowViewManager : MonoBehaviour
{
    private static WindowViewManager _instance;
    public static WindowViewManager Instance => _instance;

    private List<WindowView> _windows = new List<WindowView>();
    [SerializeField] private int _initialDay = 1;
    private int _currentDay = 1;
    public int CurrentDay => _currentDay;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            _currentDay = Mathf.Max(1, _initialDay);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void RegisterWindow(WindowView window)
    {
        if (!_windows.Contains(window))
        {
            _windows.Add(window);
            window.ApplyDayVisual(_currentDay);
        }
    }

    public void UnregisterWindow(WindowView window)
    {
        _windows.Remove(window);
    }

    public WindowView GetClosestWindow(Vector3 playerPosition)
    {
        WindowView closestWindow = null;
        float minSqrDistance = float.MaxValue;

        foreach (WindowView window in _windows)
        {
            if (!window.IsPlayerInZone) continue;

            float sqrDist = (window.transform.position - playerPosition).sqrMagnitude;
            if (sqrDist < minSqrDistance)
            {
                minSqrDistance = sqrDist;
                closestWindow = window;
            }
        }

        return closestWindow;
    }

    public void SetDay(int day)
    {
        _currentDay = Mathf.Max(1, day);
        for (int i = 0; i < _windows.Count; i++)
        {
            if (_windows[i] != null)
                _windows[i].ApplyDayVisual(_currentDay);
        }
    }
}