#if UNITY_EDITOR

using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(MatchFlowProfile))]
public class MatchFlowProfileEditor : Editor
{
    private SerializedProperty sessionsProperty;
    private ReorderableList sessionList;

    private const float Line = 18f;
    private const float Gap = 3f;
    private const float BoxPadding = 6f;

    private void OnEnable()
    {
        sessionsProperty = serializedObject.FindProperty("sessions");

        sessionList = new ReorderableList(serializedObject, sessionsProperty, true, true, true, true);

        sessionList.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, "Authored Match Sessions - drag to reorder");
        };

        sessionList.elementHeightCallback = GetElementHeight;
        sessionList.drawElementCallback = DrawSessionElement;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        MatchFlowProfile profile = (MatchFlowProfile)target;

        EditorGUILayout.Space(3f);

        EditorGUILayout.HelpBox(
            $"Planned playable timeline: {FormatTime(profile.GetPlannedDuration())}\n" +
            "ZoneLoot totals include Telegraph + Active Loot + FreePlay + Closing. " +
            "Checkout totals include Telegraph + Open Duration. " +
            "Checkout + Restock totals include Telegraph + Active + FreePlay + Closing.",
            MessageType.Info
        );

        DrawEndGameValidation(profile);

        EditorGUILayout.Space(6f);
        sessionList.DoLayoutList();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawEndGameValidation(MatchFlowProfile profile)
    {
        int endWrapCount = profile.GetEndGameWrapCount();

        if (endWrapCount == 0)
        {
            EditorGUILayout.HelpBox(
                "Missing EndGameWrap. Add exactly one EndGameWrap as the FINAL session. " +
                "The Director will not start a playable match without it.",
                MessageType.Error
            );
            return;
        }

        if (endWrapCount > 1)
        {
            EditorGUILayout.HelpBox(
                $"This profile contains {endWrapCount} EndGameWrap sessions. Keep exactly one, at the very end.",
                MessageType.Error
            );
            return;
        }

        if (!profile.IsEndGameWrapLast())
        {
            EditorGUILayout.HelpBox(
                "EndGameWrap exists but is not the FINAL session. Move it to the bottom of the list.",
                MessageType.Error
            );
            return;
        }

        EditorGUILayout.HelpBox(
            "Valid terminal flow: final EndGameWrap will request match end.",
            MessageType.None
        );
    }

    private float GetElementHeight(int index)
    {
        if (index < 0 || index >= sessionsProperty.arraySize) return Line * 4f;

        SerializedProperty element = sessionsProperty.GetArrayElementAtIndex(index);
        MatchFlowSessionType type = (MatchFlowSessionType)element.FindPropertyRelative("type").enumValueIndex;

        int lines;

        switch (type)
        {
            case MatchFlowSessionType.FreePlay:
                lines = 3;
                break;

            case MatchFlowSessionType.CartRestock:
                lines = 5;
                break;

            case MatchFlowSessionType.ZoneLoot:
                lines = 10;
                break;

            case MatchFlowSessionType.CheckoutWindow:
                lines = 6;
                break;

            case MatchFlowSessionType.CheckoutRestock:
                lines = 10;
                break;

            case MatchFlowSessionType.EndGameWrap:
                lines = 4;
                break;

            default:
                lines = 4;
                break;
        }

        return BoxPadding * 2f + lines * Line + (lines - 1) * Gap;
    }

    private void DrawSessionElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        if (index < 0 || index >= sessionsProperty.arraySize) return;

        SerializedProperty element = sessionsProperty.GetArrayElementAtIndex(index);

        SerializedProperty label = element.FindPropertyRelative("label");
        SerializedProperty type = element.FindPropertyRelative("type");
        SerializedProperty duration = element.FindPropertyRelative("duration");
        SerializedProperty telegraph = element.FindPropertyRelative("telegraphDuration");
        SerializedProperty closing = element.FindPropertyRelative("closingDuration");
        SerializedProperty freePlay = element.FindPropertyRelative("freePlayDuration");
        SerializedProperty budget = element.FindPropertyRelative("resourceBudget");
        SerializedProperty batch = element.FindPropertyRelative("batchSize");
        SerializedProperty zone = element.FindPropertyRelative("zone");
        SerializedProperty stations = element.FindPropertyRelative("checkoutStations");

        MatchFlowSessionType sessionType = (MatchFlowSessionType)type.enumValueIndex;

        Rect box = new Rect(rect.x, rect.y + 2f, rect.width, rect.height - 4f);
        GUI.Box(box, GUIContent.none, EditorStyles.helpBox);

        float y = box.y + BoxPadding;
        float x = box.x + BoxPadding;
        float width = box.width - BoxPadding * 2f;

        DrawProperty(ref y, x, width, label, $"Session {index} Label");
        DrawProperty(ref y, x, width, type, "Type");

        switch (sessionType)
        {
            case MatchFlowSessionType.FreePlay:
                DrawProperty(ref y, x, width, duration, "FreePlay Duration");
                break;

            case MatchFlowSessionType.CartRestock:
                DrawProperty(ref y, x, width, duration, "Distribution Duration");
                DrawProperty(ref y, x, width, budget, "Exact Cart Budget");
                DrawProperty(ref y, x, width, batch, "Normal Batch Size");
                break;

            case MatchFlowSessionType.ZoneLoot:
                DrawProperty(ref y, x, width, zone, "Zone");
                DrawProperty(ref y, x, width, telegraph, "Telegraph Duration");
                DrawProperty(ref y, x, width, duration, "Active Loot Duration");
                DrawProperty(ref y, x, width, freePlay, "FreePlay Duration");
                DrawProperty(ref y, x, width, closing, "Closing Duration");
                DrawReadOnlyTotal(
                    ref y,
                    x,
                    width,
                    Mathf.Max(0f, telegraph.floatValue) +
                    Mathf.Max(0f, duration.floatValue) +
                    Mathf.Max(0f, freePlay.floatValue) +
                    Mathf.Max(0f, closing.floatValue)
                );
                DrawProperty(ref y, x, width, budget, "Exact Zone Loot Budget");
                DrawProperty(ref y, x, width, batch, "Normal Batch Size");
                break;

            case MatchFlowSessionType.CheckoutWindow:
                DrawProperty(ref y, x, width, stations, "Open Stations");
                DrawProperty(ref y, x, width, telegraph, "Telegraph Duration");
                DrawProperty(ref y, x, width, duration, "Open Duration");
                DrawReadOnlyTotal(
                    ref y,
                    x,
                    width,
                    Mathf.Max(0f, telegraph.floatValue) +
                    Mathf.Max(0f, duration.floatValue)
                );
                break;

            case MatchFlowSessionType.CheckoutRestock:
                DrawProperty(ref y, x, width, stations, "Open Stations");
                DrawProperty(ref y, x, width, telegraph, "Telegraph Duration");
                DrawProperty(ref y, x, width, duration, "Open + Restock Duration");
                DrawProperty(ref y, x, width, freePlay, "FreePlay Duration");
                DrawProperty(ref y, x, width, closing, "Closing Duration");
                DrawReadOnlyTotal(
                    ref y,
                    x,
                    width,
                    Mathf.Max(0f, telegraph.floatValue) +
                    Mathf.Max(0f, duration.floatValue) +
                    Mathf.Max(0f, freePlay.floatValue) +
                    Mathf.Max(0f, closing.floatValue)
                );
                DrawProperty(ref y, x, width, budget, "Exact Restock Cart Budget");
                DrawProperty(ref y, x, width, batch, "Normal Batch Size");
                break;

            case MatchFlowSessionType.EndGameWrap:
                DrawProperty(ref y, x, width, duration, "Final Wrap Duration");
                DrawTerminalLabel(ref y, x, width);
                break;
        }
    }

    private void DrawProperty(ref float y, float x, float width, SerializedProperty property, string label)
    {
        Rect lineRect = new Rect(x, y, width, Line);
        EditorGUI.PropertyField(lineRect, property, new GUIContent(label));
        y += Line + Gap;
    }

    private void DrawReadOnlyTotal(ref float y, float x, float width, float total)
    {
        Rect lineRect = new Rect(x, y, width, Line);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUI.FloatField(lineRect, "Total Session Time", total);
        }

        y += Line + Gap;
    }

    private void DrawTerminalLabel(ref float y, float x, float width)
    {
        Rect lineRect = new Rect(x, y, width, Line);
        EditorGUI.LabelField(lineRect, "Terminal", "Director requests match end after this");
        y += Line + Gap;
    }

    private string FormatTime(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);

        int wholeSeconds = Mathf.CeilToInt(seconds);
        int minutes = wholeSeconds / 60;
        int remainingSeconds = wholeSeconds % 60;

        return $"{minutes:D2}:{remainingSeconds:D2} ({seconds:0.##} sec)";
    }
}

#endif
