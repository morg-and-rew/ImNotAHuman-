using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ClientPortraitMap.PortraitRule))]
public sealed class PortraitRuleDrawer : PropertyDrawer
{
    private const string UseCustomKey = "useCustomPositionAndSize";
    private static readonly string[] CustomFieldNames = {
        "customLeftAnchoredPos", "customLeftScale", "customLeftRotation",
        "customRightAnchoredPos", "customRightScale", "customRightRotation"
    };

    private static readonly GUIContent[] CustomFieldLabels = {
        new GUIContent("Левый портрет: позиция (X,Y,Z)"),
        new GUIContent("Левый портрет: масштаб (X,Y,Z)"),
        new GUIContent("Левый портрет: поворот (градусы)"),
        new GUIContent("Правый портрет: позиция (X,Y,Z)"),
        new GUIContent("Правый портрет: масштаб (X,Y,Z)"),
        new GUIContent("Правый портрет: поворот (градусы)")
    };

    private static bool IsCustomField(string name)
    {
        for (int i = 0; i < CustomFieldNames.Length; i++)
            if (CustomFieldNames[i] == name) return true;
        return false;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = 0f;
        SerializedProperty useCustom = property.FindPropertyRelative(UseCustomKey);
        bool showCustom = useCustom != null && useCustom.boolValue;

        SerializedProperty it = property.Copy();
        SerializedProperty end = property.GetEndProperty();
        it.NextVisible(true);
        while (!SerializedProperty.EqualContents(it, end))
        {
            if (it.name == UseCustomKey)
            {
                height += EditorGUIUtility.singleLineHeight;
                it.NextVisible(true);
                continue;
            }
            if (IsCustomField(it.name))
            {
                if (showCustom)
                    height += EditorGUI.GetPropertyHeight(it, true) + 2f;
                it.NextVisible(true);
                continue;
            }
            height += EditorGUI.GetPropertyHeight(it, true) + 2f;
            it.NextVisible(true);
        }
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty useCustom = property.FindPropertyRelative(UseCustomKey);
        bool showCustom = useCustom != null && useCustom.boolValue;

        Rect rect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        SerializedProperty it = property.Copy();
        SerializedProperty end = property.GetEndProperty();
        it.NextVisible(true);

        while (!SerializedProperty.EqualContents(it, end))
        {
            if (IsCustomField(it.name))
            {
                if (showCustom)
                {
                    int idx = System.Array.IndexOf(CustomFieldNames, it.name);
                    GUIContent fieldLabel = (idx >= 0 && idx < CustomFieldLabels.Length) ? CustomFieldLabels[idx] : null;
                    rect.x += 14f;
                    rect.width -= 14f;
                    float h = EditorGUI.GetPropertyHeight(it, true);
                    rect.height = h;
                    EditorGUI.PropertyField(rect, it, fieldLabel ?? new GUIContent(it.displayName), true);
                    rect.y += h + 2f;
                    rect.x -= 14f;
                    rect.width += 14f;
                }
                it.NextVisible(true);
                continue;
            }

            rect.height = EditorGUI.GetPropertyHeight(it, true);
            EditorGUI.PropertyField(rect, it, true);
            rect.y += rect.height + 2f;
            it.NextVisible(true);
        }
    }
}
