using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEngine;
using TND.Upscaling.Framework.HDRP;

public class HDRPPackageInjector : AssetPostprocessor, IPreprocessBuildWithReport
{
    private const string AsmDefExtension = ".asmdef";
    private const string HdrpAsmDefName = "Unity.RenderPipelines.HighDefinition.Runtime";
    private const string HdrpPackageName = "com.unity.render-pipelines.high-definition";
    private static readonly Dictionary<string, string> UpscalerAsmDefs = new()
    {
        { "GUID:039a05db797535d47bc605c30639a749", "TND.Upscaling.Framework.Runtime.Core" }, 
        { "GUID:295bcf0a3b5b6b1438fc61cb0489f285", "TND.Upscaling.Framework.Runtime.Injectors" }, 
        { "GUID:f65f98cd822cbe74aafbe7d5474b33ac", "TND.Upscaling.HDRP.NVModule" },
    };
    private static readonly string[] ExpectedPackagePaths =
    {
        "Library/PackageCache/",  // Standard packages from Unity
        "Packages/",              // Custom source forks
    };

    public int callbackOrder => 0;

    static HDRPPackageInjector()
    {
        Events.registeredPackages += OnRegisteredPackages;
        AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
    }

    /// <summary>
    /// Callback on editor startup and after a script compile/domain reload.
    /// </summary>
#if UNITY_EDITOR
    [InitializeOnLoadMethod]
#endif
    private static void OnInitialize()
    {
        ScanPackagePaths();
    }

    /// <summary>
    /// IPreprocessBuild callback, check HDRP assembly definition before a standalone build starts.
    /// </summary>
    public void OnPreprocessBuild(BuildReport report)
    {
        ScanPackagePaths();
    }
    
    /// <summary>
    /// AssetPostprocessor callback, modify HDRP assembly definition before it's imported into the asset database.
    /// </summary>
    private void OnPreprocessAsset()
    {
        string fileName = Path.GetFileNameWithoutExtension(assetPath);
        string extension = Path.GetExtension(assetPath);

        if (extension == AsmDefExtension && fileName.Contains(HdrpAsmDefName, StringComparison.InvariantCultureIgnoreCase))
        {
            if (InjectReferences(assetPath))
            {
                Debug.Log($"Modified HDRP assembly definition on import: {assetPath}");
            }
        }
    }
    
    /// <summary>
    /// Package manager callback, check HDRP assembly definition after importing other packages.
    /// </summary>
    private static void OnRegisteredPackages(PackageRegistrationEventArgs obj)
    {
        ScanPackagePaths();
    }
    
    /// <summary>
    /// After reloading domain, check if the assembly reference injection actually stuck or if a force reimport is necessary.
    /// </summary>
    private static void OnAfterAssemblyReload()
    {
        switch (RuntimeValidations.ValidateInjection())
        {
            case RuntimeValidations.InjectionStatus.NvidiaModuleNotInstalled:
                // NVIDIA module and related scripting defines have to be installed first
                return;
            case RuntimeValidations.InjectionStatus.DlssPassUsesTndPackage:
                // DLSSPass is using injected TND Upscaler classes, all good
                return;
            case RuntimeValidations.InjectionStatus.DlssPassUsesNvidiaModule:
            {
                if (!TryFindRuntimePackage(out string asmDefPath) || !CheckReferencesInjected(asmDefPath))
                {
                    // Assemblies haven't been injected yet, the other event callbacks will have to take care of that first
                    return;
                }

                // If DLSSPass is still using classes from the NVIDIA module, we know we need to give Unity one more gentle push to fully reimport and recompile the HDRP assembly definition.
                EditorApplication.delayCall += () =>
                {
                    Debug.Log($"Forcing reimport of HDRP assembly definition at path: {asmDefPath}");
                    AssetDatabase.ImportAsset(NormalizeAssemblyPath(asmDefPath), ImportAssetOptions.ForceUpdate | ImportAssetOptions.DontDownloadFromCacheServer);
                };
                
                break;
            }
        }
    }

    /// <summary>
    /// Scan expected package install locations for the HDRP package, check and reimport the assembly definition if necessary.
    /// </summary>
    private static void ScanPackagePaths()
    {
        if (TryFindRuntimePackage(out string asmDefPath) && InjectReferences(asmDefPath))
        {
            Debug.Log($"Modified HDRP assembly definition at path: {asmDefPath}");
            AssetDatabase.ImportAsset(NormalizeAssemblyPath(asmDefPath), ImportAssetOptions.ForceUpdate | ImportAssetOptions.DontDownloadFromCacheServer);
            AssetDatabase.Refresh();
        }
    }

    /// <summary>
    /// Attempt to find the location of the main HDRP runtime assembly definition file. 
    /// </summary>
    private static bool TryFindRuntimePackage(out string asmDefPath)
    {
        foreach (string expectedPath in ExpectedPackagePaths)
        {
            string[] directories = Directory.GetDirectories(expectedPath, HdrpPackageName + "*");
            if (directories.Length == 0)
                continue;

            foreach (string directory in directories)
            {
                string filePath = Path.Combine(directory, "Runtime", HdrpAsmDefName + AsmDefExtension);
                if (File.Exists(filePath))
                {
                    asmDefPath = filePath;
                    return true;
                }
            }
        }

        asmDefPath = null;
        return false;
    }

    /// <summary>
    /// Unity internally sees all package contents to be located relative to /Packages, even if the actual files are in a different location (e.g. Library/PackageCache).
    /// This means that if we try to force a reimport on an assembly definition's actual file location, Unity might not recognize it as a valid asset.
    /// This method transforms filenames inside the HDRP package to be in this normalized form expected by Unity's asset database.
    /// </summary>
    private static string NormalizeAssemblyPath(string asmDefPath)
    {
        return Regex.Replace(asmDefPath, @"^(.*)high-definition(@[a-z0-9]+)?[\\\/]Runtime[\\\/]", "Packages/com.unity.render-pipelines.high-definition/Runtime/");
    }

    /// <summary>
    /// Check whether the required TND assembly references are already present in the given assembly definition.
    /// </summary>
    private static bool CheckReferencesInjected(string asmDefPath)
    {
        if (!ParseAssemblyDefinition(asmDefPath, out _, out JArray references))
        {
            return false;
        }
            
        foreach ((string asmDefGuid, string asmDefName) in UpscalerAsmDefs)
        {
            if (!ReferenceExists(references, asmDefGuid, asmDefName, out _))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Check the given assembly definition for TND assembly references and add them if they're not present.
    /// </summary>
    private static bool InjectReferences(string asmDefPath)
    {
        bool changed = false;
        
        if (!ParseAssemblyDefinition(asmDefPath, out JObject jObject, out JArray references))
        {
            return false;
        }
        
        foreach ((string asmDefGuid, string asmDefName) in UpscalerAsmDefs)
        {
            changed = EnsureReference(references, asmDefGuid, asmDefName) || changed;
        }

        if (changed)
        {
            File.WriteAllText(asmDefPath, jObject.ToString(Formatting.Indented));
        }

        return changed;
    }
    
    private static bool ParseAssemblyDefinition(string asmDefPath, out JObject jObject, out JArray references)
    {
        jObject = JObject.Parse(File.ReadAllText(asmDefPath));
        if (!jObject.TryGetValue("references", out JToken jToken) || jToken is not JArray refs)
        {
            references = null;
            return false;
        }

        references = refs;
        return true;
    }

    private static bool EnsureReference(JArray references, string asmDefGuid, string asmDefName)
    {
        if (ReferenceExists(references, asmDefGuid, asmDefName, out bool usingGuidReferences))
        {
            // Reference already in place, no need to modify
            return false;
        }

        references.Add(usingGuidReferences ? new JValue(asmDefGuid) : new JValue(asmDefName));
        return true;
    }

    private static bool ReferenceExists(JArray references, string asmDefGuid, string asmDefName, out bool usingGuidReferences)
    {
        usingGuidReferences = false;
        
        foreach (var reference in references)
        {
            if (reference.Type != JTokenType.String)
                continue;
            
            string refValue = (string)reference;
            
            // Check if it looks like this asmdef has "Use GUIDs" enabled
            if (refValue != null && refValue.StartsWith("GUID:", StringComparison.InvariantCulture))
            {
                usingGuidReferences = true;
            }
            
            if (refValue == asmDefGuid || refValue == asmDefName)
            {
                // Requested assembly reference was found
                return true;
            }
        }

        return false;
    }
}
