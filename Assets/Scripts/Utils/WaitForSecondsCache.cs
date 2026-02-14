using System.Collections.Generic;
using UnityEngine;

public static class WaitForSecondsCache
{
    private static readonly Dictionary<int, WaitForSeconds> _cache = new Dictionary<int, WaitForSeconds>(16);

    public static WaitForSeconds Get(float seconds)
    {
        int key = Mathf.RoundToInt(seconds * 10f);
        if (key < 1) key = 1;

        if (!_cache.TryGetValue(key, out var wfs))
        {
            wfs = new WaitForSeconds(key * 0.1f);
            _cache[key] = wfs;
        }
        return wfs;
    }

    public static readonly WaitForSeconds HalfSecond = new WaitForSeconds(0.5f);
    public static readonly WaitForSeconds OneSecond = new WaitForSeconds(1f);
    public static readonly WaitForSeconds TwoSeconds = new WaitForSeconds(2f);
    public static readonly WaitForSeconds ThreeSeconds = new WaitForSeconds(3f);
    public static readonly WaitForSeconds FiveSeconds = new WaitForSeconds(5f);
}
