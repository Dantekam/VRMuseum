using System.Collections.Generic;
using UnityEngine;

#if TND_BIRP || TND_URP || TND_HDRP
using TND.Upscaling.Framework;

public class ExampleScript : MonoBehaviour
{
    private TNDUpscaler _upscalerReference;

    void Start()
    {
        //Getting the reference to the TND Upscaler component
        _upscalerReference = GetComponent<TNDUpscaler>();

        //Getting the list of currently supported Upscalers
        List<UpscalerName> supportedUpscalers = TNDUpscaler.GetSupported();
        Debug.Log("Supported upscalers: " + string.Join(", ", supportedUpscalers));

        //Setting the upscaling quality level to Balanced
        _upscalerReference.SetQuality(UpscalerQuality.Balanced);
    }
}
#endif
