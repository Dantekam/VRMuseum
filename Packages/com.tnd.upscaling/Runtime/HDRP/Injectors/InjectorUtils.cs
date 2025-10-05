using System;
using System.Reflection;

namespace UnityEngine.Rendering.HighDefinition
{
    public static class InjectorUtils
    {
        private static readonly FieldInfo OwnerCameraRefField = typeof(DynamicResolutionHandler).GetField("m_OwnerCameraWeakRef", BindingFlags.Instance | BindingFlags.NonPublic);

        public static Camera FindCurrentCamera()
        {
            Camera camera = null;

            // We can't query HDRP itself for a direct reference to the calling HDCamera, because that would cause cyclic asmdef references between HDRP and this package.
            // But we *can* reference the Core SRP package and ask the dynamic resolution system which camera is currently active.
            // Luckily the current active camera is always cached here before postprocessing and upscaling on said camera, so we can assume that this is the correct camera.
            if (OwnerCameraRefField?.GetValue(DynamicResolutionHandler.instance) is WeakReference ownerCameraRef)
            {
                if (ownerCameraRef.IsAlive && ownerCameraRef.Target is Camera ownerCamera)
                    camera = ownerCamera;
            }

            return camera;
        }
    }
}
