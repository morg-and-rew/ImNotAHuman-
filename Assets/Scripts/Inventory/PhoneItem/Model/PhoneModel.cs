public sealed class PhoneModel
{
    public bool IsOpen { get; private set; }
    public string Number { get; private set; } = "";

    public void Open()
    {
        IsOpen = true;
        Number = "";
    }

    public void Close()
    {
        IsOpen = false;
        Number = "";
    }

    public void AddDigit(char digit)
    {
        if (!IsOpen) return;
        if (digit < '0' || digit > '9') return;

        if (Number.Length >= 16) return;

        Number += digit;
    }

    public void Backspace()
    {
        if (!IsOpen) return;
        if (Number.Length == 0) return;

        Number = Number.Substring(0, Number.Length - 1);
    }

    public bool CanCall()
    {
        return IsOpen && Number.Length >= 3;
    }
}
