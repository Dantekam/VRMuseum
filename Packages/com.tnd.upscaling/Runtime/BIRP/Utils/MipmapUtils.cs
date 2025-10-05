using UnityEngine;

namespace TND.Upscaling.Framework.BIRP
{
    /// <summary>
    /// Utilities for applying mipmap bias to textures in Unity built-in render pipeline.
    /// BiRP does not have the concept of a global mipmap sampling bias, which necessitates applying this bias to each texture individually.
    /// This class contains methods to facilitate that, as well as tools to periodically check and update mipmap bias on all textures in the scene.
    /// </summary>
    public static class MipmapUtils
    {
        public static float CalculateMipmapBias(float renderWidth, float displayWidth, float additionalBias = 0.0f)
        {
            return Mathf.Min(Mathf.Log(renderWidth / displayWidth, 2f) + additionalBias, 0.0f);
        }

        /// <summary>
        /// Updates a single texture, where the mipmap bias is calculated by the provided render width, display width and override.
        /// Should be called when an object is instantiated, or when the ScaleFactor is changed.
        /// </summary>
        public static void MipmapSingleTexture(Texture texture, float renderWidth, float displayWidth, float additionalBias = 0.0f)
        {
            MipmapSingleTexture(texture, CalculateMipmapBias(renderWidth, displayWidth, additionalBias));
        }

        /// <summary>
        /// Updates a single texture to the set mipmap bias.
        /// Should be called when an object is instantiated, or when the ScaleFactor is changed.
        /// </summary>
        public static void MipmapSingleTexture(Texture texture, float mipmapBias)
        {
            texture.mipMapBias = mipmapBias;
        }

        /// <summary>
        /// Updates all textures currently loaded, where the MipMap bias is calculated by the provided render width, display width and override.
        /// Should be called when a lot of new textures are loaded, or when the ScaleFactor is changed.
        /// </summary>
        public static void MipmapAllTextures(float renderWidth, float displayWidth, float additionalBias = 0.0f)
        {
            MipmapAllTextures(CalculateMipmapBias(renderWidth, displayWidth, additionalBias));
        }

        /// <summary>
        /// Updates all textures currently loaded to the set MipMap Bias.
        /// Should be called when a lot of new textures are loaded, or when the ScaleFactor is changed.
        /// </summary>
        public static void MipmapAllTextures(float mipmapBias)
        {
            Texture[] allTextures = Resources.FindObjectsOfTypeAll<Texture>();
            for (int i = 0; i < allTextures.Length; i++)
            {
                allTextures[i].mipMapBias = mipmapBias;
            }
        }

        /// <summary>
        /// Resets all currently loaded textures to the default mipmap bias. 
        /// </summary>
        public static void ResetAllMipmaps(ref float prevMipmapBias)
        {
            if (prevMipmapBias == 0f)
                return;
            
            prevMipmapBias = 0f;
            
            Texture[] allTextures = Resources.FindObjectsOfTypeAll<Texture>();
            for (int i = 0; i < allTextures.Length; i++)
            {
                allTextures[i].mipMapBias = 0;
            }
        }

        public static void AutoUpdateMipmaps(float renderWidth, float displayWidth, float additionalBias, float updateInterval, ref float prevMipmapBias, ref float mipmapTimer, ref ulong prevMemoryUse)
        {
            mipmapTimer -= Time.deltaTime;
            if (mipmapTimer < 0)
            {
                mipmapTimer = updateInterval;

                float mipMapBias = CalculateMipmapBias(renderWidth, displayWidth, additionalBias);

                if (prevMemoryUse != Texture.currentTextureMemory || !Mathf.Approximately(prevMipmapBias, mipMapBias))
                {
                    prevMipmapBias = mipMapBias;
                    prevMemoryUse = Texture.currentTextureMemory;
                    MipmapAllTextures(mipMapBias);
                }
            }
        }
    }
}
