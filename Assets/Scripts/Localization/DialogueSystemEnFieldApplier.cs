using System.Collections;
using PixelCrushers;
using PixelCrushers.DialogueSystem;
using UnityEngine;

/// <summary>
/// The project stores Russian in Dialogue Text and English in a custom field named "en".
/// Pixel Crushers Dialogue System shows Dialogue Text by default, so we copy "en" -> DialogueText
/// in memory when the current locale is English.
/// </summary>
public sealed class DialogueSystemEnFieldApplier : MonoBehaviour
{
    [Tooltip("Field name that contains English text in DialogueDatabase entries.")]
    [SerializeField] private string _englishFieldName = "en";

    private bool _applied;

    private void OnEnable()
    {
        if (!_applied) StartCoroutine(ApplyWhenReady());
    }

    private IEnumerator ApplyWhenReady()
    {
        // Wait until Dialogue System has an instance + database loaded.
        while (DialogueManager.instance == null || DialogueManager.masterDatabase == null)
            yield return null;

        // Wait an extra frame to allow any initialization logic to finish.
        yield return null;

        if (!IsEnglishLocale())
            yield break;

        ApplyEnglishToDialogueText(DialogueManager.masterDatabase);
        _applied = true;
    }

    private static bool IsEnglishLocale()
    {
        // Prefer your game's locale flag if available; fallback to Dialogue System localization.
        var flow = GameFlowController.Instance;
        if (flow != null) return flow.IsUiEnglishLocale;

        var lang = "";
        if (UILocalizationManager.instance != null)
            lang = UILocalizationManager.instance.currentLanguage ?? "";
        return GameFlowController.LocaleIndicatesEnglish(lang) || string.Equals(Localization.language, "en", System.StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyEnglishToDialogueText(DialogueDatabase db)
    {
        if (db == null) return;

        for (int i = 0; i < db.conversations.Count; i++)
        {
            var conv = db.conversations[i];
            if (conv == null) continue;

            for (int j = 0; j < conv.dialogueEntries.Count; j++)
            {
                var entry = conv.dialogueEntries[j];
                if (entry == null) continue;

                var en = Field.LookupValue(entry.fields, _englishFieldName);
                if (string.IsNullOrWhiteSpace(en)) continue;

                entry.DialogueText = en;
            }
        }
    }
}

