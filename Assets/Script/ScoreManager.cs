using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void RecountScores(out int p1, out int p2)
    {
        p1 = p2 = 0;

        foreach (var mb in FindObjectsOfType<MonoBehaviour>())
        {
            var type = mb.GetType();
            var fIsMember = type.GetField("isTowerMember");
            if (fIsMember == null) continue;

            bool isMember = (bool)fIsMember.GetValue(mb);
            if (!isMember) continue;

            int owner = GetOwnerId(mb.gameObject);
            if (owner == 1) p1++;
            else if (owner == 2) p2++;
        }
    }

    int GetOwnerId(GameObject go)
    {
        foreach (var c in go.GetComponents<MonoBehaviour>())
        {
            var f = c.GetType().GetField("ownerPlayerId");
            if (f != null)
            {
                object val = f.GetValue(c);
                if (val is int id && (id == 1 || id == 2)) return id;
            }
        }
        if (go.name.Contains("P1")) return 1;
        if (go.name.Contains("P2")) return 2;
        return 0;
    }
}