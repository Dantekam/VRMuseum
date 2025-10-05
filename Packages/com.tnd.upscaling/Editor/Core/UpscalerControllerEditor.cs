using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace TND.Upscaling.Framework
{
    public abstract class UpscalerControllerEditor : Editor
    {
        private const string ShowAdvancedKey = "TND.Upscaling.Framework.Editor_ShowAdvancedSettings";
        private const string FoldoutStateKey = "TND.Upscaling.Framework.Editor_FoldoutState_";
        
        private class SettingsEditorState
        {
            public Editor editor;
            public string identifier;
            public string displayName;
            public bool foldOut;
        }

        private readonly List<IUpscalerPlugin> _sortedUpscalerPlugins = new();
        private readonly List<SettingsEditorState> _sortedUpscalerSettingsEditors = new();

        private SerializedProperty _upscalerChainProperty;
        private SerializedProperty _upscalerQualityProperty;
        private SerializedProperty _upscalerSharpeningProperty;
        private SerializedProperty _upscalerSharpnessProperty;
        private SerializedProperty _upscalerSettingsProperty;
        private SerializedProperty _upscalerEnableAutoReactiveProperty;
        private SerializedProperty _upscalerAutoReactiveSettingsProperty;
        private SerializedProperty _upscalerEnableCustomReactiveProperty;
        private SerializedProperty _upscalerCustomReactiveLayerProperty;

        private ReorderableList _upscalerChainList;
        private bool _showAdvancedSettings;

        protected virtual void OnEnable()
        {
            var upscalerScript = serializedObject.targetObject as UpscalerController;
            if (upscalerScript == null)
                return;
            
            serializedObject.Update();

            _upscalerChainProperty = serializedObject.FindProperty(nameof(UpscalerController.upscalerChain));
            _upscalerQualityProperty = serializedObject.FindProperty(nameof(UpscalerController.qualityMode));
            _upscalerSharpeningProperty = serializedObject.FindProperty(nameof(UpscalerController.enableSharpening));
            _upscalerSharpnessProperty = serializedObject.FindProperty(nameof(UpscalerController.sharpness));
            _upscalerSettingsProperty = serializedObject.FindProperty(nameof(UpscalerController.upscalerSettings));
            _upscalerEnableAutoReactiveProperty = serializedObject.FindProperty(nameof(UpscalerController.autoGenerateReactiveMask));
            _upscalerAutoReactiveSettingsProperty = serializedObject.FindProperty(nameof(UpscalerController.autoReactiveSettings));
            _upscalerEnableCustomReactiveProperty = serializedObject.FindProperty(nameof(UpscalerController.enableCustomReactiveMask));
            _upscalerCustomReactiveLayerProperty = serializedObject.FindProperty(nameof(UpscalerController.customReactiveMaskLayer));

            _sortedUpscalerPlugins.Clear();
            _sortedUpscalerPlugins.AddRange(UpscalerPluginRegistry.GetUpscalerPlugins());
            _sortedUpscalerPlugins.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCulture));

            _sortedUpscalerSettingsEditors.Clear();
            for (int i = 0; i < _upscalerSettingsProperty.arraySize; ++i)
            {
                var pairProperty = _upscalerSettingsProperty.GetArrayElementAtIndex(i);
                var identifierProperty = pairProperty.FindPropertyRelative(nameof(UpscalerController.UpscalerSettingsPair.identifier));
                var settingsProperty = pairProperty.FindPropertyRelative(nameof(UpscalerController.UpscalerSettingsPair.settings));

                var upscalerPlugin = _sortedUpscalerPlugins.Find(p => p.Identifier == identifierProperty.stringValue);
                if (upscalerPlugin != null)
                {
                    // Ensure there is always a valid settings object
                    if (settingsProperty.objectReferenceValue == null)
                    {
                        settingsProperty.objectReferenceValue = upscalerPlugin.CreateSettings();
                        serializedObject.ApplyModifiedProperties();
                    }
                    
                    _sortedUpscalerSettingsEditors.Add(new SettingsEditorState
                    {
                        editor = CreateEditor(settingsProperty.objectReferenceValue),
                        identifier = upscalerPlugin.Identifier,
                        displayName = upscalerPlugin.DisplayName,
                        foldOut = EditorPrefs.GetBool(FoldoutStateKey + upscalerPlugin.Identifier),
                    });
                }
            }
            _sortedUpscalerSettingsEditors.Sort((a, b) => string.Compare(a.displayName, b.displayName, StringComparison.CurrentCulture));

            // Create a nice reorderable list view for the fallback chain, with a constrained set of upscaler options that may be added
            _upscalerChainList = new ReorderableList(serializedObject, _upscalerChainProperty, true, true, true, true)
            {
                drawHeaderCallback = (rect) =>
                {
                    EditorGUI.LabelField(rect, "Upscalers by Priority", EditorStyles.boldLabel);
                },
                drawElementCallback = (rect, index, _, _) =>
                {
                    var element = _upscalerChainProperty.GetArrayElementAtIndex(index);
                    var upscalerPlugin = _sortedUpscalerPlugins.Find(p => p.Identifier == element.stringValue);
                    var upscalerDisplayName = upscalerPlugin != null ? upscalerPlugin.DisplayName : "[Unknown] " + element.stringValue;
                    EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), upscalerDisplayName, EditorStyles.label);
                },
                onAddDropdownCallback = (rect, list) =>
                {
                    var availableIdentifiers = new List<string>();
                    var availableNames = new List<GUIContent>();

                    // Create a sorted list of upscalers that haven't been added to the fallback chain yet
                    foreach (var upscalerPlugin in _sortedUpscalerPlugins)
                    {
                        string identifier = upscalerPlugin.Identifier;

                        bool found = false;
                        foreach (SerializedProperty element in _upscalerChainProperty)
                        {
                            if (element.stringValue == identifier)
                            {
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                        {
                            availableIdentifiers.Add(identifier);
                            availableNames.Add(new GUIContent(upscalerPlugin.DisplayName));
                        }
                    }

                    void SelectCallback(object userData, string[] options, int selected)
                    {
                        // Add the upscaler to the end of the fallback chain
                        string selectedIdentifier = ((List<string>)userData)[selected];
                        int index = list.count;
                        _upscalerChainProperty.InsertArrayElementAtIndex(index);
                        var newElement = _upscalerChainProperty.GetArrayElementAtIndex(index);
                        newElement.stringValue = selectedIdentifier;
                        serializedObject.ApplyModifiedProperties();
                    }

                    EditorUtility.DisplayCustomMenu(rect, availableNames.ToArray(), availableNames.Count, SelectCallback, availableIdentifiers, false);
                },
            };

            _showAdvancedSettings = EditorPrefs.GetBool(ShowAdvancedKey);
        }

        protected virtual void OnDisable()
        {
        }

        public override void OnInspectorGUI()
        {
            EditorVisuals.GenerateHeader();

            if (DrawValidations())
            {
                // Render pipeline-agnostic warnings
                if (_upscalerChainList.count == 0)
                {
                    EditorGUILayout.HelpBox("No upscalers have been added to the 'Upscaler by Priority' list.\r\nAs long as this list is empty, the TND Upscaler Framework will not perform any visual upscaling.", MessageType.Warning);
                }

                DrawSettings();
            }

            EditorVisuals.GenerateFooter();
        }

        protected virtual bool DrawValidations()
        {
            return true;
        }

        /// <summary>
        /// Generate the actual upscaler settings
        /// </summary>
        private void DrawSettings()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            _upscalerChainList.DoLayoutList();

            int prevQualityIndex = _upscalerQualityProperty.enumValueIndex;
            EditorGUILayout.PropertyField(_upscalerQualityProperty);
            if (_upscalerQualityProperty.enumValueIndex != prevQualityIndex)
            {
                // Disable upscaler component when quality mode is set to Off, enable it otherwise
                serializedObject.FindProperty("m_Enabled").boolValue = _upscalerQualityProperty.enumValueIndex > 0;
            }
            
            EditorGUILayout.PropertyField(_upscalerSharpeningProperty);
            if (_upscalerSharpeningProperty.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_upscalerSharpnessProperty);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            bool newAdvancedSettings = EditorGUILayout.BeginToggleGroup("Show Expert Settings", _showAdvancedSettings);
            if (newAdvancedSettings != _showAdvancedSettings)
            {
                _showAdvancedSettings = newAdvancedSettings;
                EditorPrefs.SetBool(ShowAdvancedKey, newAdvancedSettings);
            }
            
            if (_showAdvancedSettings)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Universal Settings:", EditorStyles.boldLabel);

                EditorGUI.indentLevel++;
                // Auto-generate reactive mask settings
                EditorGUILayout.PropertyField(_upscalerEnableAutoReactiveProperty);
                if (_upscalerEnableAutoReactiveProperty.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_upscalerAutoReactiveSettingsProperty);
                    EditorGUI.indentLevel--;
                }

                // Custom reactive mask settings
                // EditorGUILayout.PropertyField(_upscalerEnableCustomReactiveProperty);
                // if (_upscalerEnableCustomReactiveProperty.boolValue)
                // {
                //     EditorGUI.indentLevel++;
                //     EditorGUILayout.PropertyField(_upscalerCustomReactiveLayerProperty);
                //     EditorGUI.indentLevel--;
                // }

                EditorGUI.indentLevel--;

                DrawAdditionalAdvancedSettings();

                EditorGUILayout.Space();

                if (_sortedUpscalerSettingsEditors.Count > 0)
                {
                    EditorGUILayout.LabelField("Upscaler Specific Settings:", EditorStyles.boldLabel);
                    EditorGUILayout.Space();

                    EditorGUI.indentLevel++;
                    // Per-upscaler advanced settings
                    foreach (var settingsEditorState in _sortedUpscalerSettingsEditors)
                    {
                        bool newFoldoutState = EditorGUILayout.BeginFoldoutHeaderGroup(settingsEditorState.foldOut, settingsEditorState.displayName);
                        if (newFoldoutState != settingsEditorState.foldOut)
                        {
                            settingsEditorState.foldOut = newFoldoutState;
                            EditorPrefs.SetBool(FoldoutStateKey + settingsEditorState.identifier, newFoldoutState);
                        }
                        
                        if (settingsEditorState.foldOut)
                        {
                            settingsEditorState.editor.OnInspectorGUI();
                        }

                        EditorGUILayout.EndFoldoutHeaderGroup();

                    }
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndToggleGroup();

            serializedObject.ApplyModifiedProperties();
        }

        protected virtual void DrawAdditionalAdvancedSettings() { }

        protected void DrawErrorHelpText()
        {
            EditorGUILayout.HelpBox("Something has gone wrong, please contact 'The Naked Dev' for help!", MessageType.Error);
            if (GUILayout.Button("Contact 'The Naked Dev' on Discord"))
            {
                Application.OpenURL("https://discord.gg/r9jkRzaPtC");
            }
        }
    }

    public static class UpscalerControllerEditorExtensions
    {
        public static TEnum GetEnumValue<TEnum>(this SerializedProperty serializedProperty)
            where TEnum: Enum
        {
            Array values = Enum.GetValues(typeof(TEnum));
            int index = serializedProperty.enumValueIndex;
            if (index < 0 || index >= values.Length)
                return default;

            return (TEnum)values.GetValue(index);
        }
        
        public static void SetEnumValue<TEnum>(this SerializedProperty serializedProperty, TEnum enumValue)
            where TEnum: Enum
        {
            serializedProperty.enumValueIndex = Array.IndexOf(Enum.GetValues(typeof(TEnum)), enumValue);
        }
    }
}
