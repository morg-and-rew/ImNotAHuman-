using System;
using System.Collections.Generic;

public sealed class PhoneCallService : IPhoneCallService
{
    public bool IsRinging { get; private set; }
    private readonly Dictionary<string, Action> _calls = new();

    public void Register(string number, Action action)
    {
        number = Normalize(number);
        if (string.IsNullOrEmpty(number)) return;

        _calls[number] = action;
    }

    public bool TryCall(string number)
    {
        number = Normalize(number);

        Action action;
        if (_calls.TryGetValue(number, out action))
        {
            IsRinging = true;
            action?.Invoke();
            return true;
        }

        return false;
    }

    public void StopRinging()
    {
        IsRinging = false;
    }

    public void Call(string number)
    {
        TryCall(number);
    }

    private static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace(" ", "").Replace("-", "");
    }
}
