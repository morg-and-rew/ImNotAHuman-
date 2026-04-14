public interface IPlayerBlocker
{
    /// <summary> Текущее состояние блокировки движения/обзора (после последнего SetBlock). </summary>
    bool IsInputBlocked { get; }

    public void SetBlock(bool value);
}
