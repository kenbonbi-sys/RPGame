using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace RPG.EditorTools
{
    /// <summary>
    /// Inspector for [SerializeReference, PickEffect] fields: a dropdown on each element picks the
    /// effect type (Damage, Projectile, Cue…), then its fields show as usual. This is what lets a
    /// designer build a new ability from data only.
    /// </summary>
    [CustomPropertyDrawer(typeof(PickEffectAttribute))]
    public class EffectPickerDrawer : PropertyDrawer
    {
        static Type[] types;

        static Type[] Types => types ??= TypeCache.GetTypesDerivedFrom<AbilityEffect>()
            .Where(t => !t.IsAbstract && !t.IsGenericType && t.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(t => t.Name).ToArray();

        public static string Nice(Type t) => t == null ? "(chọn khối)" : Regex.Replace(t.Name.Replace("Effect", ""), "(?<=[a-z])(?=[A-Z])", " ");

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUI.GetPropertyHeight(property, label, true);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }
            var current = property.managedReferenceValue?.GetType();
            var header = new Rect(position.x + EditorGUIUtility.labelWidth, position.y,
                                  position.width - EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);
            if (EditorGUI.DropdownButton(header, new GUIContent(Nice(current)), FocusType.Keyboard))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("(none)"), current == null, () => Set(property, null));
                foreach (var t in Types)
                {
                    var type = t;
                    menu.AddItem(new GUIContent(Nice(type)), type == current, () => Set(property, type));
                }
                menu.DropDown(header);
            }
            var shown = new GUIContent(current != null ? $"{label.text} · {Nice(current)}" : label.text, label.tooltip);
            EditorGUI.PropertyField(position, property, shown, true);
        }

        static void Set(SerializedProperty property, Type type)
        {
            property.serializedObject.Update();
            property.managedReferenceValue = type == null ? null : Activator.CreateInstance(type);
            property.serializedObject.ApplyModifiedProperties();
        }
    }
}
