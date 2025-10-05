using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace TND.Upscaling.Framework.HDRP
{
    [CustomEditor(typeof(TNDUpscaler))]
    public class TNDUpscalerEditor : UpscalerControllerEditor
    {
        private Camera _camera;
        private HDAdditionalCameraData _cameraData;
        private HDRenderPipelineAsset _currentRenderPipeline;

        private SerializedObject _serializedCamera;
        private SerializedObject _serializedCameraData;
        private SerializedObject _serializedPipelineAsset;

        protected override void OnEnable()
        {
            base.OnEnable();
            
            if (GraphicsSettings.currentRenderPipeline is not HDRenderPipelineAsset currentRenderPipeline)
                return;

            _currentRenderPipeline = currentRenderPipeline;
            _serializedPipelineAsset = new SerializedObject(_currentRenderPipeline);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
        }

        /// <summary>
        /// Displaying Errors and Warnings for HDRP
        /// </summary>
        protected override bool DrawValidations()
        {
            if (!base.DrawValidations())
                return false;

            if (_currentRenderPipeline == null || target is not TNDUpscaler upscalerScript)
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
                    _cameraData = _camera.GetComponent<HDAdditionalCameraData>();
                }

                if (_cameraData && _serializedCameraData == null)
                {
                    _serializedCameraData = new SerializedObject(_cameraData);
                }

                if (_serializedCamera == null || _serializedCameraData == null)
                {
                    return false;
                }
                
                _serializedPipelineAsset.Update();

                SerializedProperty serializedPipelineSettings = _serializedPipelineAsset.FindProperty("m_RenderPipelineSettings");
                SerializedProperty serializedDynamicResolution = serializedPipelineSettings.FindPropertyRelative(nameof(RenderPipelineSettings.dynamicResolutionSettings));
          
                bool changedSetting = false;

                // ERRORS

                // Missing DLSS
#if UNITY_2023_1_OR_NEWER
                if (!UnityEditor.PackageManager.PackageInfo.IsPackageRegistered("com.unity.modules.nvidia"))
#else
                var registeredPackages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
                if (!registeredPackages.Any(pi => pi.name == "com.unity.modules.nvidia"))
#endif
                {
                    EditorGUILayout.HelpBox("The Nvidia DLSS package has not been installed, TND Upscaling requires this package to be installed!", MessageType.Error);
                    if (GUILayout.Button("Install Nvidia DLSS package"))
                    {
                        UnityEditor.PackageManager.Client.Add("com.unity.modules.nvidia");
                        PipelineDefines.AddDefine("ENABLE_NVIDIA");
                        PipelineDefines.AddDefine("ENABLE_NVIDIA_MODULE");
                    }

                    hasError = true;
                }

                if (!CheckCorrectHDRPSettings(_currentRenderPipeline.currentPlatformRenderPipelineSettings))
                {
                    EditorGUILayout.HelpBox("Camera & HDRP Asset settings are incorrect, TND Upscaling requires specific settings to inject other upscalers.", MessageType.Error);
                    hasError = true;
                    if (GUILayout.Button("Fix Settings"))
                    {
                        SetCorrectHDRPSettings(serializedPipelineSettings, serializedDynamicResolution);
                        changedSetting = true;
                    }
                }

                // WARNINGS

                // Enable motion vectors
                SerializedProperty serializedMotionVectors = serializedPipelineSettings.FindPropertyRelative(nameof(RenderPipelineSettings.supportMotionVectors));
                if (!serializedMotionVectors.boolValue)
                {
                    EditorGUILayout.HelpBox("Motion Vectors are disabled in the HDRP settings. This will cause noticeable smearing and blurring artifacts.", MessageType.Warning);
                    if (GUILayout.Button("Enable Motion Vectors"))
                    {
                        serializedMotionVectors.boolValue = true;
                        changedSetting = true;
                    }
                }

                // Set correct upsample filter
                SerializedProperty serializedUpsampleFilter = serializedDynamicResolution.FindPropertyRelative(nameof(GlobalDynamicResolutionSettings.upsampleFilter));
                if (serializedUpsampleFilter.GetEnumValue<DynamicResUpscaleFilter>() != DynamicResUpscaleFilter.CatmullRom)
                {
                    EditorGUILayout.HelpBox("Dynamic Resolution Setting: 'Default Upscale Filter' is not set to Catmull Rom, this is highly recommended for the best visual quality.", MessageType.Warning);
                    if (GUILayout.Button("Set to 'Catmull Rom'"))
                    {
                        serializedUpsampleFilter.SetEnumValue(DynamicResUpscaleFilter.CatmullRom);
                        changedSetting = true;
                    }
                }

                // Set correct Dynamic Resolution Type
                SerializedProperty serializedDynResType = serializedDynamicResolution.FindPropertyRelative(nameof(GlobalDynamicResolutionSettings.dynResType));
                if (serializedDynResType.GetEnumValue<DynamicResolutionType>() == DynamicResolutionType.Hardware)
                {
                    EditorGUILayout.HelpBox("Set 'Dynamic Resolution Type' to 'Software' for improved compatibility.", MessageType.Warning);
                    if (GUILayout.Button("Switch to 'Software'"))
                    {
                        serializedDynResType.SetEnumValue(DynamicResolutionType.Software);
                        changedSetting = true;
                    }
                }

                // Warning: Disable Anti-Aliasing
                if (_cameraData.antialiasing != HDAdditionalCameraData.AntialiasingMode.None)
                {
                    EditorGUILayout.HelpBox("Disable 'Anti-Aliasing' for the best visual results.", MessageType.Warning);
                    if (GUILayout.Button("Disable 'Anti-Aliasing'"))
                    {
                        _serializedCameraData.Update();
                        _serializedCameraData.FindProperty(nameof(HDAdditionalCameraData.antialiasing)).SetEnumValue(HDAdditionalCameraData.AntialiasingMode.None);
                        _serializedCameraData.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_serializedCameraData.targetObject);
                    }
                }

                // Mipmap Bias
                SerializedProperty serializedMipBias = serializedDynamicResolution.FindPropertyRelative(nameof(GlobalDynamicResolutionSettings.useMipBias));
                if (!serializedMipBias.boolValue)
                {
                    EditorGUILayout.HelpBox("Dynamic Resolution Setting: 'Use Mipmap Bias' is disabled, enabling this is highly recommended for the best visual quality.", MessageType.Warning);
                    if (GUILayout.Button("Enable 'Mipmap Bias'"))
                    {
                        serializedMipBias.boolValue = true;
                        changedSetting = true;
                    }
                }

                if (changedSetting)
                {
                    _serializedPipelineAsset.ApplyModifiedProperties();
                    EditorUtility.SetDirty(_serializedPipelineAsset.targetObject);
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

        /// <summary>
        /// Check whether all HDRP Settings are correct!
        /// </summary>
        private bool CheckCorrectHDRPSettings(in RenderPipelineSettings currentPlatformRenderPipelineSettings)
        {
            if (!RuntimeValidations.ValidateRenderPipelineSettings(currentPlatformRenderPipelineSettings))
                return false;

            if (!RuntimeValidations.ValidateCameraSettings(_camera, _cameraData, target as UpscalerController))
                return false;

            return true;
        }

        /// <summary>
        /// Set all correct HDRP settings
        /// </summary>
        private void SetCorrectHDRPSettings(SerializedProperty serializedPipelineSettings, SerializedProperty serializedDynamicResolution)
        {
            // Enable DLSS
            _serializedCamera.Update();
            _serializedCameraData.Update();
            _serializedCameraData.FindProperty(nameof(HDAdditionalCameraData.allowDeepLearningSuperSampling)).boolValue = true;
            _serializedCameraData.FindProperty(nameof(HDAdditionalCameraData.deepLearningSuperSamplingUseCustomQualitySettings)).boolValue = false;
            _serializedCameraData.FindProperty(nameof(HDAdditionalCameraData.deepLearningSuperSamplingUseCustomAttributes)).boolValue = false;
            _serializedCameraData.FindProperty(nameof(HDAdditionalCameraData.deepLearningSuperSamplingUseOptimalSettings)).boolValue = false;

#if UNITY_2023_2_OR_NEWER
            SerializedProperty serializedList = serializedDynamicResolution.FindPropertyRelative(nameof(GlobalDynamicResolutionSettings.advancedUpscalersByPriority));
            
            // See if DLSS already exists in the priority list
            int dlssIndex = -1;
            for (int index = 0; index < serializedList.arraySize; ++index)
            {
                if (serializedList.GetArrayElementAtIndex(index).GetEnumValue<AdvancedUpscalers>() == AdvancedUpscalers.DLSS)
                {
                    dlssIndex = index;
                    break;
                }
            }

            if (dlssIndex >= 0)
            {
                // Move DLSS to the top of the priority list
                serializedList.MoveArrayElement(dlssIndex, 0);
            }
            else
            {
                // Add DLSS as the top entry in the list
                serializedList.InsertArrayElementAtIndex(0);
                serializedList.GetArrayElementAtIndex(0).SetEnumValue(AdvancedUpscalers.DLSS);
            }
#else
            serializedDynamicResolution.FindPropertyRelative(nameof(GlobalDynamicResolutionSettings.enableDLSS)).boolValue = true;
#endif

            // Set DLSS Mode to Maximum Quality
            serializedDynamicResolution.FindPropertyRelative(nameof(GlobalDynamicResolutionSettings.DLSSPerfQualitySetting)).intValue = 2;

            // Disable DLSS optimal settings
            serializedDynamicResolution.FindPropertyRelative(nameof(GlobalDynamicResolutionSettings.DLSSUseOptimalSettings)).boolValue = false;

            // Enable Dynamic Resolution
            serializedDynamicResolution.FindPropertyRelative(nameof(GlobalDynamicResolutionSettings.enabled)).boolValue = true;
            _serializedCameraData.FindProperty(nameof(HDAdditionalCameraData.allowDynamicResolution)).boolValue = true;

            // Disable force resolution
            serializedDynamicResolution.FindPropertyRelative(nameof(GlobalDynamicResolutionSettings.forceResolution)).boolValue = false;

            // Set min & max percentage correctly
            serializedDynamicResolution.FindPropertyRelative(nameof(GlobalDynamicResolutionSettings.minPercentage)).floatValue = 33;
            serializedDynamicResolution.FindPropertyRelative(nameof(GlobalDynamicResolutionSettings.maxPercentage)).floatValue = 100;

            // Custom Pass, allows opaque-only copy pass
            serializedPipelineSettings.FindPropertyRelative(nameof(RenderPipelineSettings.supportCustomPass)).boolValue = true;
            
            // Disable multi-sample anti-aliasing
            serializedPipelineSettings.FindPropertyRelative(nameof(RenderPipelineSettings.msaaSampleCount)).SetEnumValue(MSAASamples.None);
            _serializedCamera.FindProperty("m_AllowMSAA").boolValue = false;

            _serializedCameraData.ApplyModifiedProperties();
            EditorUtility.SetDirty(_serializedCameraData.targetObject);
            
            _serializedCamera.ApplyModifiedProperties();
            EditorUtility.SetDirty(_serializedCamera.targetObject);
        }
    }
}
