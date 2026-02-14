public interface IPhoneCallService
{
    bool IsRinging { get; }

    void Call(string number);
    void StopRinging();
    bool TryCall(string number);
}
