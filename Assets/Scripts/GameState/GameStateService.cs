public static class GameStateService
{
    public static GameState CurrentState { get; private set; }
    public static bool PhoneUnlocked { get; private set; }

    public static int RequiredPackageNumber { get; private set; }
    public static bool EnforceRequiredPackageOnly { get;  set; }
    public static bool PackageDropLocked { get; private set; }

    public static void SetState(GameState state) => CurrentState = state;

    public static void UnlockPhone() => PhoneUnlocked = true;

    public static bool IsWarehouse => CurrentState == GameState.Warehouse;

    public static bool WrongPackageDialogueActive { get; private set; }

    public static void SetRequiredPackage(int number, bool enforceOnly)
    {
        RequiredPackageNumber = number;
        EnforceRequiredPackageOnly = enforceOnly;
    }

    public static void SetPackageDropLocked(bool locked)
    {
        PackageDropLocked = locked;
    }

    public static void SetWrongPackageDialogue(bool active)
    {
        WrongPackageDialogueActive = active;
    }

    /// <summary> Как при первом запуске: статическое состояние переживает LoadScene — сбрасываем при «Новая игра». </summary>
    public static void ResetForNewGame()
    {
        CurrentState = GameState.None;
        PhoneUnlocked = false;
        RequiredPackageNumber = 0;
        EnforceRequiredPackageOnly = false;
        PackageDropLocked = false;
        WrongPackageDialogueActive = false;
    }
}

public enum GameState
{
    None,
    Intro,
    Narration,
    Router,
    Phone,
    ClientDialog,
    Warehouse
}
