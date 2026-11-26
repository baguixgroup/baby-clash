using UnityEngine;
using UnityEngine.UI;

namespace BabyClash
{
    /// <summary>
    /// The UI is laid out for 16:9. On squarer screens (iPads are 4:3) it scales to the width, so nothing is squeezed
    /// sideways and the extra room goes above and below; on wider screens (phones) it scales to the height.
    /// </summary>
    public sealed class AspectMatch : MonoBehaviour
    {
        const float DesignAspect = 16f / 9f;
        int width;
        int height;

        void Update()
        {
            if (Screen.width == width && Screen.height == height)
                return;
            width = Screen.width;
            height = Screen.height;
            float match = height > 0 && (float)width / height < DesignAspect ? 0f : 1f;
            foreach (CanvasScaler scaler in FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include))
            {
                if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                    scaler.matchWidthOrHeight = match;
            }
        }
    }
}
