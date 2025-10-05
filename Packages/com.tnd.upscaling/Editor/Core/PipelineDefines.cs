using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TND.Upscaling.Framework
{
    //Clear all scriptable defines when the framework gets uninstalled
    public class RemoveDefines : AssetPostprocessor
    {
        const string RelativeFilePathWithTypeExtension = "com.tnd.upscaling";

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
                                           string[] movedAssets, string[] movedFromAssetPaths)
        {
            try
            {
                if (deletedAssets != null && deletedAssets.Contains($"Packages/{RelativeFilePathWithTypeExtension}"))
                {
                    PipelineDefines.RemoveDefine("TND_BIRP");
                    PipelineDefines.RemoveDefine("TND_HDRP");
                    PipelineDefines.RemoveDefine("TND_URP");
                    PipelineDefines.RemoveDefine("TND_POST_PROCESSING_STACK_V2");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }

    [InitializeOnLoad]
    public class PipelineDefines
    {
        enum PipelineType
        {
            BIRP,
            URP,
            HDRP
        }

        static PipelineDefines()
        {
            UpdateDefines();
        }

        /// <summary>
        /// Update the unity pipeline defines for URP
        /// </summary>
        private static void UpdateDefines()
        {
            var pipeline = GetPipeline();

            if (pipeline == PipelineType.URP)
            {
                AddDefine("TND_URP");
            }
            else
            {
                RemoveDefine("TND_URP");
            }

            if (pipeline == PipelineType.HDRP)
            {
                AddDefine("TND_HDRP");

                // Ensure the DLSS defines are added when the Nvidia plugin module is installed.
                // This allows the HDRP DLSS upscaler injection method to work across all platforms. 
#if UNITY_2023_1_OR_NEWER
                if (UnityEditor.PackageManager.PackageInfo.IsPackageRegistered("com.unity.modules.nvidia"))
#else
                var registeredPackages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
                if (registeredPackages.Any(pi => pi.name == "com.unity.modules.nvidia"))
#endif
                {
                    List<string> definesList = GetDefines(EditorUserBuildSettings.activeBuildTarget);
                    if (!definesList.Contains("ENABLE_NVIDIA") || !definesList.Contains("ENABLE_NVIDIA_MODULE"))
                    {
                        AddDefine("ENABLE_NVIDIA");
                        AddDefine("ENABLE_NVIDIA_MODULE");
                    }
                }
            }
            else
            {
                RemoveDefine("TND_HDRP");
            }

            if (pipeline == PipelineType.BIRP)
            {
                AddDefine("TND_BIRP");
            }
            else
            {
                RemoveDefine("TND_BIRP");
            }
        }

        private static PipelineType GetPipeline()
        {
            string srpType = string.Empty;
            
#if UNITY_2019_1_OR_NEWER
            if (GraphicsSettings.defaultRenderPipeline != null)
            {
                srpType = GraphicsSettings.defaultRenderPipeline.GetType().ToString();
            }
            else if (QualitySettings.renderPipeline != null)
            {
                srpType = QualitySettings.renderPipeline.GetType().ToString();
            }
#endif

            if (srpType.Contains("HDRenderPipeline"))
            {
                return PipelineType.HDRP;
            }
            
            if (srpType.Contains("UniversalRenderPipeline"))
            {
                return PipelineType.URP;
            }

            // BiRP or some other custom SRP
            return PipelineType.BIRP;
        }

        private static List<string> GetDefines(BuildTarget target)
        {
            BuildTargetGroup buildTargetGroup = BuildPipeline.GetBuildTargetGroup(target);

#if UNITY_2022_1_OR_NEWER
            UnityEditor.Build.NamedBuildTarget namedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
            string defines = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
#else
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);
#endif
            return defines.Split(';').ToList();
        }

        public static void AddDefine(string define)
        {
            foreach (BuildTarget target in GetBuildTargets())
            {
                List<string> definesList = GetDefines(target);

                if (definesList.Contains(define))
                    continue;

                definesList.Add(define);
                string defines = string.Join(";", definesList.ToArray());

                BuildTargetGroup buildTargetGroup = BuildPipeline.GetBuildTargetGroup(target);

#if UNITY_2022_1_OR_NEWER
                UnityEditor.Build.NamedBuildTarget namedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
                PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, defines);
#else
                PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, defines);
#endif
            }
        }

        public static void RemoveDefine(string define)
        {
            foreach (BuildTarget target in GetBuildTargets())
            {
                List<string> definesList = GetDefines(target);

                if (!definesList.Contains(define))
                    continue;

                definesList.Remove(define);
                string defines = string.Join(";", definesList.ToArray());

                BuildTargetGroup buildTargetGroup = BuildPipeline.GetBuildTargetGroup(target);


#if UNITY_2022_1_OR_NEWER
                UnityEditor.Build.NamedBuildTarget namedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
                PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, defines);
#else
                PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, defines);
#endif
            }
        }

        private static IEnumerable<BuildTarget> GetBuildTargets()
        {
            return Enum.GetValues(typeof(BuildTarget))
                .Cast<BuildTarget>()
                .Where(x => (int)x > 1 && !IsObsolete(x));
        }

        static bool IsObsolete(BuildTarget group)
        {
            // GameCoreXboxSeries overlaps with GameCoreScarlett, which is marked as Obsolete
            if (group == BuildTarget.GameCoreXboxSeries)
                return false;
            
            var attrs = typeof(BuildTarget)
                .GetField(group.ToString())
                .GetCustomAttributes(typeof(ObsoleteAttribute), false);

            return attrs.Length > 0;
        }
    }
}
