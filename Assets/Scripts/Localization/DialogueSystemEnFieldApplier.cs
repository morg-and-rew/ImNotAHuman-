using System.Collections;
using System.Text;
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

    [Tooltip("Сюда кладём копию русского из Dialogue Text перед подменой на EN — иначе при возврате на русский субтитры остаются английскими (только в памяти). Можно задать то же имя, что в базе, если русский уже вынесен в поле.")]
    [SerializeField] private string _russianStashFieldName = "ru";

    [Tooltip("В билде: PlayerPrefs LocDebug=1 — подробные логи локализации (иначе только Editor / Development).")]
    [SerializeField] private bool _forceVerboseLogsInRelease;

    private bool _coroutineStarted;

    private static bool VerboseLogsEnabled =>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        true;
#else
        PlayerPrefs.GetInt("LocDebug", 0) != 0;
#endif

    private bool ShouldLogVerbose => VerboseLogsEnabled || _forceVerboseLogsInRelease;

    private void OnEnable()
    {
        if (!_coroutineStarted)
        {
            _coroutineStarted = true;
            StartCoroutine(ApplyWhenReadyCoroutine());
        }
    }

    private IEnumerator ApplyWhenReadyCoroutine()
    {
        while (true)
        {
            if (!DialogueManager.hasInstance)
            {
                yield return null;
                continue;
            }

            DialogueSystemController dsc = DialogueManager.instance;
            if (dsc == null || dsc.databaseManager == null || dsc.masterDatabase == null)
            {
                yield return null;
                continue;
            }

            break;
        }

        yield return null;

        RefreshInternal(GameFlowController.Instance, "OnEnable/ApplyWhenReadyCoroutine");
    }

    /// <summary> Вызывать после смены языка в меню и после Init игры. </summary>
    public static void RefreshAfterLanguageChanged(GameFlowController flow)
    {
        var appliers = UnityEngine.Object.FindObjectsByType<DialogueSystemEnFieldApplier>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < appliers.Length; i++)
        {
            if (appliers[i] != null)
                appliers[i].RefreshInternal(flow, "ApplyUiLanguage/Init");
        }
    }

    private void RefreshInternal(GameFlowController flow, string reason)
    {
        // После подмены DialogueManager.instance на контроллер из сцены Awake может ещё не отработать — m_databaseManager null, masterDatabase бросает NRE.
        if (!DialogueManager.hasInstance)
        {
            LogLoc($"Refresh skipped ({reason}): DialogueManager.hasInstance is false.");
            return;
        }

        DialogueSystemController dsc = DialogueManager.instance;
        if (dsc == null || dsc.databaseManager == null)
        {
            LogLoc($"Refresh skipped ({reason}): DialogueSystemController or databaseManager not ready yet.");
            return;
        }

        DialogueDatabase db = dsc.masterDatabase;
        if (db == null)
        {
            LogLoc($"Refresh skipped ({reason}): masterDatabase is null.");
            return;
        }

        DumpLanguageSnapshot(flow, reason);

        bool useEnglish = ResolveUseEnglish(flow);
        LogLoc($"Refresh ({reason}): useEnglish={useEnglish}, will {(useEnglish ? "copy [en] -> Dialogue Text" : "leave Dialogue Text as in DB (expect Russian)")}.");

        if (useEnglish)
            ApplyEnglishToDialogueText(db, reason);
        else
        {
            ApplyRussianToDialogueText(db, reason);
            LogLoc($"Refresh ({reason}): Russian mode — Dialogue Text restored from [{_russianStashFieldName}] where present (see ApplyEnglish stash).");
        }

        LogRadioConversationsEnCoverage(db, reason);
    }

    private void DumpLanguageSnapshot(GameFlowController flow, string reason)
    {
        if (!ShouldLogVerbose)
            return;

        var sb = new StringBuilder(512);
        sb.Append("[Loc] Snapshot (").Append(reason).AppendLine(")");
        sb.Append("  PlayerPrefs[Language] = \"").Append(PlayerPrefs.GetString("Language", "")).AppendLine("\"");
        sb.Append("  UILocalizationManager.currentLanguage = \"");
        sb.Append(UILocalizationManager.instance != null ? (UILocalizationManager.instance.currentLanguage ?? "null") : "no instance").AppendLine("\"");
        sb.Append("  Localization.language = \"").Append(Localization.language ?? "null").AppendLine("\"");
        sb.Append("  GameFlowController.Instance ").Append(flow != null ? "exists" : "NULL").AppendLine();
        if (flow != null)
        {
            sb.Append("  GetUnifiedLanguage() = \"").Append(flow.CurrentUiLanguage).AppendLine("\"");
            sb.Append("  IsUiEnglishLocale = ").Append(flow.IsUiEnglishLocale).AppendLine();
        }

        int convCount = -1;
        if (DialogueManager.hasInstance && DialogueManager.instance != null && DialogueManager.instance.databaseManager != null
            && DialogueManager.masterDatabase != null)
            convCount = DialogueManager.masterDatabase.conversations.Count;
        sb.Append("  DialogueManager.SetLanguage not called here; database conv count = ").Append(convCount);
        Debug.Log(sb.ToString());
    }

    private static bool ResolveUseEnglish(GameFlowController flow)
    {
        if (flow != null)
            return flow.IsUiEnglishLocale;

        if (UILocalizationManager.instance != null)
        {
            string lang = UILocalizationManager.instance.currentLanguage ?? "";
            if (GameFlowController.LocaleIndicatesEnglish(lang))
                return true;
        }

        string prefs = PlayerPrefs.GetString("Language", "");
        if (GameFlowController.LocaleIndicatesEnglish(prefs))
            return true;

        return string.Equals(Localization.language, "en", System.StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyEnglishToDialogueText(DialogueDatabase db, string reason)
    {
        int convUpdated = 0;
        int entriesUpdated = 0;
        int entriesMissingEn = 0;
        int stashedRu = 0;

        for (int i = 0; i < db.conversations.Count; i++)
        {
            var conv = db.conversations[i];
            if (conv == null) continue;

            bool convTouched = false;
            for (int j = 0; j < conv.dialogueEntries.Count; j++)
            {
                var entry = conv.dialogueEntries[j];
                if (entry == null) continue;

                var en = Field.LookupValue(entry.fields, _englishFieldName);
                if (string.IsNullOrWhiteSpace(en))
                {
                    string dt = entry.DialogueText ?? "";
                    if (!string.IsNullOrWhiteSpace(dt))
                        entriesMissingEn++;
                    continue;
                }

                // Сохраняем русский из Dialogue Text, чтобы ApplyRussianToDialogueText мог вернуть его.
                if (entry.fields != null)
                {
                    string existingStash = Field.LookupValue(entry.fields, _russianStashFieldName);
                    string currentDt = entry.DialogueText ?? "";
                    if (string.IsNullOrWhiteSpace(existingStash) && !string.IsNullOrWhiteSpace(currentDt))
                    {
                        Field.SetValue(entry.fields, _russianStashFieldName, currentDt);
                        stashedRu++;
                    }
                }

                entry.DialogueText = en;
                entriesUpdated++;
                convTouched = true;
            }

            if (convTouched) convUpdated++;
        }

        LogLoc($"ApplyEnglish ({reason}): conversations touched={convUpdated}, entries updated={entriesUpdated}, stashedRussianTo[{_russianStashFieldName}]={stashedRu}, entries with text but empty [{_englishFieldName}]={entriesMissingEn}");
    }

    /// <summary> Восстанавливает русский в Dialogue Text из поля-стэша (после того как EN перезаписал строки). </summary>
    private void ApplyRussianToDialogueText(DialogueDatabase db, string reason)
    {
        int entriesRestored = 0;

        for (int i = 0; i < db.conversations.Count; i++)
        {
            var conv = db.conversations[i];
            if (conv == null) continue;

            for (int j = 0; j < conv.dialogueEntries.Count; j++)
            {
                var entry = conv.dialogueEntries[j];
                if (entry == null || entry.fields == null) continue;

                string ru = Field.LookupValue(entry.fields, _russianStashFieldName);
                if (string.IsNullOrWhiteSpace(ru)) continue;

                entry.DialogueText = ru;
                entriesRestored++;
            }
        }

        LogLoc($"ApplyRussian ({reason}): entries restored Dialogue Text from [{_russianStashFieldName}]={entriesRestored}");
    }

    private void LogRadioConversationsEnCoverage(DialogueDatabase db, string reason)
    {
        if (!ShouldLogVerbose)
            return;

        var sb = new StringBuilder(1024);
        sb.Append("[Loc] Radio* / Hero_Replic* coverage (").Append(reason).AppendLine(")");

        for (int i = 0; i < db.conversations.Count; i++)
        {
            var conv = db.conversations[i];
            if (conv == null || string.IsNullOrEmpty(conv.Title)) continue;

            string t = conv.Title;
            if (!t.StartsWith("Radio_", System.StringComparison.OrdinalIgnoreCase)
                && !t.StartsWith("Hero_Replic", System.StringComparison.OrdinalIgnoreCase))
                continue;

            int total = 0;
            int missingEn = 0;
            int withText = 0;
            string sampleMissing = null;

            for (int j = 0; j < conv.dialogueEntries.Count; j++)
            {
                var entry = conv.dialogueEntries[j];
                if (entry == null) continue;
                total++;
                string dt = entry.DialogueText ?? "";
                if (!string.IsNullOrWhiteSpace(dt)) withText++;
                var en = Field.LookupValue(entry.fields, _englishFieldName);
                if (string.IsNullOrWhiteSpace(en) && !string.IsNullOrWhiteSpace(dt))
                {
                    missingEn++;
                    if (sampleMissing == null)
                        sampleMissing = TrimForLog(dt, 80);
                }
            }

            sb.Append("  • ").Append(t).Append(": entries=").Append(total)
                .Append(" textLines=").Append(withText)
                .Append(" missing[").Append(_englishFieldName).Append("]=").Append(missingEn);
            if (sampleMissing != null)
                sb.Append(" sampleRU=\"").Append(sampleMissing).Append('\"');
            sb.AppendLine();
        }

        Debug.Log(sb.ToString());
    }

    private static string TrimForLog(string s, int maxLen)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace('\n', ' ');
        return s.Length <= maxLen ? s : s.Substring(0, maxLen) + "…";
    }

    private void LogLoc(string message)
    {
        if (ShouldLogVerbose)
            Debug.Log("[Loc] " + message);
    }
}
