using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TND.Upscaling.Framework.URP
{
    [CustomEditor(typeof(TNDUpscaler))]
    public class TNDUpscalerEditor : UpscalerControllerEditor
    {
        private Camera _camera;
        private UniversalAdditionalCameraData _cameraData;
        private UniversalRenderPipelineAsset _activeRenderPipelineAsset;

        private SerializedObject _serializedCamera;
        private SerializedObject _serializedCameraData;
        private SerializedObject _serializedPipelineAsset;
        private SerializedObject _serializedRendererData;
        private SerializedProperty _serializedRendererFeatures;
        private SerializedProperty _serializedRendererFeatureMap;

        protected override void OnEnable()
        {
            base.OnEnable();
            
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset activeRenderPipelineAsset)
                return;

            _activeRenderPipelineAsset = activeRenderPipelineAsset;
            _serializedPipelineAsset = new SerializedObject(_activeRenderPipelineAsset);
            _serializedRendererData = new SerializedObject(UpscalingHelpers.GetRendererData(_activeRenderPipelineAsset));
            _serializedRendererFeatures = _serializedRendererData.FindProperty("m_RendererFeatures");
            _serializedRendererFeatureMap = _serializedRendererData.FindProperty("m_RendererFeatureMap");
        }

        protected override void OnDisable()
        {
            base.OnDisable();
        }

        /// <summary>
        /// Displaying Errors and Warnings for URP
        /// </summary>
        protected override bool DrawValidations()
        {
            if (!base.DrawValidations())
                return false;
            
            if (_activeRenderPipelineAsset == null || target is not TNDUpscaler upscalerScript)
                return false;

            bool hasError = false;
            try
            {
                if (!_camera)
                {
                    _camera = upscalerScript.GetComponent<Camera>();
                }

                if (_camera && _serializedCamera == null)
                {
                    _serializedCamera = new SerializedObject(_camera);
                }

                if (!_cameraData)
                {
                    _cameraData = _camera.GetUniversalAdditionalCameraData();
                }

                if (_cameraData && _serializedCameraData == null)
                {
                    _serializedCameraData = new SerializedObject(_cameraData);
                }
                
                if (_serializedCamera == null || _serializedCameraData == null)
                {
                    return false;
                }
                
                _serializedRendererData.Update();

                // Error: Add Required RendererFeature, or Remove it if there's more than one
                int rendererFeatureCount = 0;
                bool renderFeatureEnabled = true;
                for (int i = 0; i < _serializedRendererFeatures.arraySize; ++i)
                {
                    if (_serializedRendererFeatures.GetArrayElementAtIndex(i).objectReferenceValue is UpscalingRendererFeature)
                    {
                        UpscalingRendererFeature upscalingRenderFeature = _serializedRendererFeatures.GetArrayElementAtIndex(i).objectReferenceValue as UpscalingRendererFeature;
                        renderFeatureEnabled = upscalingRenderFeature.isActive;
                        rendererFeatureCount++;
                    }

                }

                if (rendererFeatureCount == 0)
                {
                    EditorGUILayout.HelpBox("TND Upscaling Renderer Feature is missing on this Camera's Renderer!", MessageType.Error);
                    hasError = true;
                    if (GUILayout.Button("Add Renderer Feature"))
                    {
                        AddRendererFeature<UpscalingRendererFeature>(); 
                    }
                }
                else if (rendererFeatureCount > 1)
                {
                    EditorGUILayout.HelpBox("More than one TND Upscaling Renderer Feature has been added to this Camera's Renderer!", MessageType.Error);
                    hasError = true;
                    if (GUILayout.Button("Remove Additional Renderer Feature(s)"))
                    {
                        for (int i = _serializedRendererFeatures.arraySize - 1; i >= 0 && rendererFeatureCount > 1; i--)
                        {
                            if (_serializedRendererFeatures.GetArrayElementAtIndex(i).objectReferenceValue is UpscalingRendererFeature)
                            {
                                RemoveRendererFeature(i);
                                rendererFeatureCount--;
                            }
                        }
                    } 
                }
                else if (!renderFeatureEnabled)
                {
                    EditorGUILayout.HelpBox("TND Upscaling Renderer Feature is disabled!", MessageType.Error);
                    hasError = true;

                    if (GUILayout.Button("Enable Renderer Feature"))
                    {
                        for (int i = 0; i < _serializedRendererFeatures.arraySize; ++i)
                        {
                            if (_serializedRendererFeatures.GetArrayElementAtIndex(i).objectReferenceValue is UpscalingRendererFeature)
                            {
                                UpscalingRendererFeature upscalingRenderFeature = _serializedRendererFeatures.GetArrayElementAtIndex(i).objectReferenceValue as UpscalingRendererFeature;
                                upscalingRenderFeature.SetActive(true);
                            }
                        }
                    }
                }

                // Error: Disable legacy Multi-Sample Anti-Aliasing
                if (_camera.allowMSAA)
                {
                    EditorGUILayout.HelpBox("Disable 'Allow MSAA' to prevent compatibility issues.", MessageType.Error);
                    hasError = true;
                    if (GUILayout.Button("Disable 'Allow MSAA'"))
                    {
                        _serializedCamera.Update();
                        _serializedCamera.FindProperty("m_AllowMSAA").boolValue = false;
                        _serializedCamera.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_serializedCamera.targetObject);
                    }
                }

                // Camera Stacking Warning
                var cameraStack = _cameraData.cameraStack;
                if (_cameraData.renderType == CameraRenderType.Overlay || (cameraStack != null && cameraStack.Count > 0))
                {
                    EditorGUILayout.HelpBox("Unity currently doesn't support stacked cameras for upscaling, we're still investigating ways to work around this limitation.", MessageType.Warning);
                }
                
                // Upscaling Filter should be set to Point (or Linear) 
                if (_activeRenderPipelineAsset.upscalingFilter != UpscalingFilterSelection.Auto)
                {
                    EditorGUILayout.HelpBox("Upscaling Filter should be set to Automatic for the best compatibility.", MessageType.Warning);
                    if (GUILayout.Button("Set to 'Automatic'"))
                    {
                        _serializedPipelineAsset.Update();
                        _serializedPipelineAsset.FindProperty("m_UpscalingFilter").SetEnumValue(UpscalingFilterSelection.Auto);
                        _serializedPipelineAsset.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_serializedPipelineAsset.targetObject);
                    }
                }

                // Warning: Disable Anti-Aliasing
                if (_cameraData.antialiasing != AntialiasingMode.None)
                {
                    EditorGUILayout.HelpBox("Disable 'Anti-Aliasing' for the best visual results.", MessageType.Warning);
                    if (GUILayout.Button("Disable 'Anti-Aliasing'"))
                    {
                        _serializedCameraData.Update();
                        _serializedCameraData.FindProperty("m_Antialiasing").SetEnumValue(AntialiasingMode.None);
                        _serializedCameraData.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_serializedCameraData.targetObject);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                DrawErrorHelpText();
            }

            return !hasError;
        }

        private void AddRendererFeature<TComponent>()
            where TComponent : ScriptableObject
        {
            _serializedRendererData.Update();

            ScriptableObject component = CreateInstance<TComponent>();
            component.name = $"{typeof(TComponent)}";
            Undo.RegisterCreatedObjectUndo(component, "Add Renderer Feature");

            // Store this new effect as a sub-asset so we can reference it safely afterward
            // Only when we're not dealing with an instantiated asset
            if (EditorUtility.IsPersistent(_serializedRendererData.targetObject))
            {
                AssetDatabase.AddObjectToAsset(component, _serializedRendererData.targetObject);
            }
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(component, out _, out long localId);

            // Grow the list first, then add - that's how serialized lists work in Unity
            _serializedRendererFeatures.arraySize++;
            SerializedProperty componentProp = _serializedRendererFeatures.GetArrayElementAtIndex(_serializedRendererFeatures.arraySize - 1);
            componentProp.objectReferenceValue = component;

            // Update GUID Map
            _serializedRendererFeatureMap.arraySize++;
            SerializedProperty guidProp = _serializedRendererFeatureMap.GetArrayElementAtIndex(_serializedRendererFeatureMap.arraySize - 1);
            guidProp.longValue = localId;
            _serializedRendererData.ApplyModifiedProperties();

            // Force save / refresh
            if (EditorUtility.IsPersistent(_serializedRendererData.targetObject))
            {
                EditorUtility.SetDirty(_serializedRendererData.targetObject);
            }
            _serializedRendererData.ApplyModifiedProperties();
        }

        private void RemoveRendererFeature(int arrayIndex)
        {
            SerializedProperty property = _serializedRendererFeatures.GetArrayElementAtIndex(arrayIndex);
            UnityEngine.Object component = property.objectReferenceValue;
            property.objectReferenceValue = null;

            Undo.SetCurrentGroupName(component == null ? "Remove Renderer Feature" : $"Remove {component.name}");

            // remove the array index itself from the list
            _serializedRendererFeatures.DeleteArrayElementAtIndex(arrayIndex);
            _serializedRendererFeatureMap.DeleteArrayElementAtIndex(arrayIndex);
            _serializedRendererData.ApplyModifiedProperties();

            // Destroy the setting object after ApplyModifiedProperties(). If we do it before, redo
            // actions will be in the wrong order and the reference to the setting object in the
            // list will be lost.
            if (component != null)
            {
                Undo.DestroyObjectImmediate(component);

                ScriptableRendererFeature feature = component as ScriptableRendererFeature;
                feature?.Dispose();
            }

            // Force save / refresh
            EditorUtility.SetDirty(_serializedRendererData.targetObject);
        }
    }
}
