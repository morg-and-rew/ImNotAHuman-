using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Очищает цель вывода VideoPlayer, чтобы при смене клипа не мелькал последний кадр предыдущего ролика.
/// </summary>
public static class VideoRenderTextureUtil
{
    public static void ClearVideoTargetIfRenderTexture(VideoPlayer videoPlayer)
    {
        if (videoPlayer == null) return;
        if (videoPlayer.renderMode != VideoRenderMode.RenderTexture) return;
        ClearRenderTexture(videoPlayer.targetTexture);
    }

    public static void ClearRenderTexture(RenderTexture rt)
    {
        if (rt == null) return;
        Graphics.Blit(Texture2D.blackTexture, rt);
    }
}
