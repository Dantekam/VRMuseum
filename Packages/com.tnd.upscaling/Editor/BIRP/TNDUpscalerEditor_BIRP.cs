using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_POST_PROCESSING_STACK_V2
using UnityEngine.Rendering.PostProcessing;
#endif

namespace TND.Upscaling.Framework.BIRP
{
    [CustomEditor(typeof(TNDUpscaler))]
    public class TNDUpscalerEditor : UpscalerControllerEditor
    {
        private Camera _camera;
#if UNITY_POST_PROCESSING_STACK_V2
        private PostProcessLayer _postProcessLayer;
#endif
        
        private SerializedObject _serializedCamera;
        private SerializedObject _serializedPostProcessLayer;
        
        private SerializedProperty _upscalerAutoTextureUpdateProperty;
        private SerializedProperty _upscalerUpdateIntervalProperty;
        private SerializedProperty _upscalerAdditionalBiasProperty;

        protected override void OnEnable()
        {
            base.OnEnable();

            _upscalerAutoTextureUpdateProperty = serializedObject.FindProperty(nameof(UpscalerController_BIRP.autoTextureUpdate));
            _upscalerUpdateIntervalProperty = serializedObject.FindProperty(nameof(UpscalerController_BIRP.updateInterval));
            _upscalerAdditionalBiasProperty = serializedObject.FindProperty(nameof(UpscalerController_BIRP.additionalBias));
        }

        /// <summary>
        /// Displaying Errors and Warnings for BIRP
        /// </summary>
        protected override bool DrawValidations()
        {
            if (!base.DrawValidations())
                return false;
            
            if (target is not TNDUpscaler upscalerScript)
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

                if (_serializedCamera == null)
                {
                    return false;
                }

#if UNITY_POST_PROCESSING_STACK_V2
                if (!_postProcessLayer)
                {
                    _postProcessLayer = _camera.GetComponent<PostProcessLayer>();
                    if (_postProcessLayer != null)
                    {
                        _serializedPostProcessLayer = new SerializedObject(_postProcessLayer);
                    }
                }

                // Warning missing Post Processing Layer!
                if (_postProcessLayer == null)
                {
                    EditorGUILayout.HelpBox("TND Upscalers will work out of the box, but for best third-party asset and Post-Processing support we recommend using our upscalers together with our custom Post-Processing Stack v2.\nPlease refer to our documentation for more information.", MessageType.Warning);
                }
                else
                {
    #if TND_POST_PROCESSING_STACK_V2
                    // TND custom Post-Processing stack with integrated upscaling
                    if (_postProcessLayer.antialiasingMode != PostProcessLayer.Antialiasing.AdvancedUpscaling)
                    {
                        EditorGUILayout.HelpBox("Post-Processing Stack v2 currently has not selected 'TND Upscaling', TND Upscalers are now disabled.", MessageType.Warning);
                        if (GUILayout.Button("Set Anti-Aliasing mode to TND Upscaling"))
                        {
                            _serializedPostProcessLayer.Update();
                            _serializedPostProcessLayer.FindProperty(nameof(PostProcessLayer.antialiasingMode)).SetEnumValue(PostProcessLayer.Antialiasing.AdvancedUpscaling);
                            _serializedPostProcessLayer.ApplyModifiedProperties();
                            EditorUtility.SetDirty(_serializedPostProcessLayer.targetObject);
                        }
                    }
    #else
                    EditorGUILayout.HelpBox("TND Upscalers will work out of the box, but for best third-party asset and Post-Processing support we recommend using our upscalers together with our custom Post-Processing Stack v2.\nPlease refer to our documentation for more information.", MessageType.Warning);

                    // Using PPV2, but not our custom one so we are using the OnRenderImage fallback
                    if (_postProcessLayer.antialiasingMode != PostProcessLayer.Antialiasing.None)
                    {
                        EditorGUILayout.HelpBox("Disable 'Anti-Aliasing' for the best visual results.", MessageType.Warning);
                        if (GUILayout.Button("Disable 'Anti-Aliasing'"))
                        {
                            _serializedPostProcessLayer.Update();
                            _serializedPostProcessLayer.FindProperty(nameof(PostProcessLayer.antialiasingMode)).SetEnumValue(PostProcessLayer.Antialiasing.None);
                            _serializedPostProcessLayer.ApplyModifiedProperties();
                            EditorUtility.SetDirty(_serializedPostProcessLayer.targetObject);
                        }
                    }
    #endif
                }
#else 
                // Warning missing PPV2 for best results
                EditorGUILayout.HelpBox("TND Upscalers will work out of the box, but for best third-party asset and Post-Processing support we recommend using our upscalers together with our custom Post-Processing Stack v2.\nPlease refer to our documentation for more information.", MessageType.Warning);
#endif
                
                // Warning: Disable Anti-Aliasing
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
                
                // Warning: Enable Motion Vectors
                if (GraphicsSettings.GetShaderMode(BuiltinShaderType.MotionVectors) == BuiltinShaderMode.Disabled)
                {
                    EditorGUILayout.HelpBox("Motion Vectors are disabled in the Graphics settings. This will cause noticeable smearing and blurring artifacts.", MessageType.Warning);
                    if (GUILayout.Button("Enable Motion Vectors"))
                    {
                        GraphicsSettings.SetShaderMode(BuiltinShaderType.MotionVectors, BuiltinShaderMode.UseBuiltin);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                
                EditorGUILayout.HelpBox("Something has gone wrong, please contact 'The Naked Dev' for help!", MessageType.Error);
                hasError = true;
                if (GUILayout.Button("Contact 'The Naked Dev' on Discord"))
                {
                    Application.OpenURL("https://discord.gg/r9jkRzaPtC");
                }
            }

            return !hasError;
        }

        protected override void DrawAdditionalAdvancedSettings()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Mipmap Settings:", EditorStyles.boldLabel);
            
            EditorGUI.indentLevel++;
            
            EditorGUILayout.PropertyField(_upscalerAutoTextureUpdateProperty);
            if (_upscalerAutoTextureUpdateProperty.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_upscalerUpdateIntervalProperty);
                EditorGUILayout.PropertyField(_upscalerAdditionalBiasProperty);
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
        }
    }
}
