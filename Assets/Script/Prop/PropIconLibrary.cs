using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PropIconEntry
{
    public string propId;
    public Sprite iconSprite;
}

[DisallowMultipleComponent]
public class PropIconLibrary : MonoBehaviour
{
    [Header("道具ID到图标的映射表")]
    public List<PropIconEntry> entries = new List<PropIconEntry>();

    private Dictionary<string, Sprite> _map;

    void Awake()
    {
        _map = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries)
        {
            if (!string.IsNullOrEmpty(e.propId) && e.iconSprite != null)
                _map[e.propId] = e.iconSprite;
        }
    }

    public Sprite GetIcon(string propId)
    {
        if (string.IsNullOrEmpty(propId)) return null;
        return _map != null && _map.TryGetValue(propId, out var s) ? s : null;
    }
}