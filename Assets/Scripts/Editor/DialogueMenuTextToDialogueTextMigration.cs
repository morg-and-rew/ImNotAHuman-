#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using PixelCrushers.DialogueSystem;

/// <summary>
/// Copies default-language text from Menu Text into Dialogue Text when Dialogue Text is empty.
/// Same rules as Tools/migrate_menu_to_dialogue_text.py (empty Dialogue Text only).
/// </summary>
public static class DialogueMenuTextToDialogueTextMigration
{
    private const string DatabasePath = "Assets/SystemDialog/DialogueDatabase.asset";

    [MenuItem("Tools/Dialogue/Fill Dialogue Text from Menu Text (empty only)")]
    public static void Run()
    {
        var db = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(DatabasePath);
        if (db == null)
        {
            Debug.LogError("DialogueMenuTextToDialogueTextMigration: missing " + DatabasePath);
            return;
        }

        int pairs = 0;
        int filled = 0;

        for (int ci = 0; ci < db.conversations.Count; ci++)
        {
            var conv = db.conversations[ci];
            if (conv == null || conv.dialogueEntries == null)
                continue;

            for (int ei = 0; ei < conv.dialogueEntries.Count; ei++)
            {
                var entry = conv.dialogueEntries[ei];
                if (entry == null)
                    continue;

                pairs++;
                string menu = entry.MenuText ?? string.Empty;
                string dlg = entry.DialogueText ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(menu) && string.IsNullOrWhiteSpace(dlg))
                {
                    entry.DialogueText = menu;
                    filled++;
                }
            }
        }

        if (filled > 0)
            EditorUtility.SetDirty(db);

        AssetDatabase.SaveAssets();
        Debug.Log($"DialogueMenuTextToDialogueTextMigration: scanned {pairs} entries, filled Dialogue Text from Menu Text in {filled} entries.");
    }

    /// <summary>
    /// Clears Menu Text when it equals Dialogue Text so localization does not see duplicate source lines.
    /// Same rules as Tools/clear_menu_text_when_duplicate_of_dialogue.py
    /// </summary>
    [MenuItem("Tools/Dialogue/Clear Menu Text when identical to Dialogue Text")]
    public static void ClearDuplicateMenuText()
    {
        var db = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(DatabasePath);
        if (db == null)
        {
            Debug.LogError("DialogueMenuTextToDialogueTextMigration: missing " + DatabasePath);
            return;
        }

        int pairs = 0;
        int cleared = 0;

        for (int ci = 0; ci < db.conversations.Count; ci++)
        {
            var conv = db.conversations[ci];
            if (conv == null || conv.dialogueEntries == null)
                continue;

            for (int ei = 0; ei < conv.dialogueEntries.Count; ei++)
            {
                var entry = conv.dialogueEntries[ei];
                if (entry == null)
                    continue;

                pairs++;
                string menu = entry.MenuText ?? string.Empty;
                string dlg = entry.DialogueText ?? string.Empty;

                if (menu.Length > 0 && string.Equals(menu, dlg, System.StringComparison.Ordinal))
                {
                    entry.MenuText = string.Empty;
                    cleared++;
                }
            }
        }

        if (cleared > 0)
            EditorUtility.SetDirty(db);

        AssetDatabase.SaveAssets();
        Debug.Log($"DialogueMenuTextToDialogueTextMigration: scanned {pairs} entries, cleared duplicate Menu Text in {cleared} entries.");
    }
}
#endif
