using System.Collections.Generic;
using Il2CppInterop.Runtime;
using InnerNet;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Onyx;

public sealed class OnyxLobbyClones : MonoBehaviour
{
    private const float PickRadius = 0.5f;
    private const float CloneZ = -0.5f;
    private const float ShadowDelay = 0.35f;
    private const float DriftSpeed = 0.6f;
    private const float DriftMaxR = 7f;

    private sealed class Clone
    {
        public GameObject Go;
        public bool Shadow;
        public bool Static;
        public Vector2 Vel;
        public float DirTimer;
        public SpriteRenderer[] Rends;
        public Vector3[] Pose;
    }

    internal static OnyxLobbyClones Instance;

    private readonly List<Clone> _clones = new List<Clone>();
    private readonly List<Clone> _regular = new List<Clone>();
    private readonly Queue<GameObject> _pool = new Queue<GameObject>();
    private readonly List<(float t, Vector3 p)> _trail = new List<(float, Vector3)>(256);
    private Clone _drag;
    private Vector3 _dragOff;
    private Clone _shadow;
    private bool _wasLobby;

    public void Awake() => Instance = this;

    public void Update()
    {
        bool inLobby = LobbyBehaviour.Instance != null || OnyxFakeMap.Active;
        if (_wasLobby && !inLobby) ClearAll();
        _wasLobby = inLobby;

        RecordTrail();
        ManageShadow();

        if (!Active()) return;

        Vector3 world = MouseWorld();
        LeftClick(world);
        RightClick(world);
        Drag(world);

        Guard(Time.deltaTime);
        Drift(Time.deltaTime);
    }

    public void LateUpdate()
    {
        for (int i = 0; i < _clones.Count; i++)
        {
            Clone e = _clones[i];
            if (e.Go == null || e.Rends == null) continue;
            for (int j = 0; j < e.Rends.Length; j++)
            {
                SpriteRenderer sr = e.Rends[j];
                if (sr != null) sr.transform.localPosition = e.Pose[j];
            }
        }
    }

    private static bool Active()
    {
        return OnyxConfig.LobbyCloneMode.Value
            && (LobbyBehaviour.Instance != null || OnyxFakeMap.Active)
            && PlayerControl.LocalPlayer != null
            && !OnyxMenu.Opened;
    }

    private void LeftClick(Vector3 world)
    {
        if (!Input.GetMouseButtonDown(0)) return;

        Clone hit = Nearest(world);
        if (hit != null && !hit.Shadow)
        {
            _drag = hit;
            _dragOff = hit.Go.transform.position - world;
            return;
        }

        int count = OnyxConfig.LobbyCloneSpawnCount.Value;
        for (int i = 0; i < count; i++)
        {
            if (_regular.Count >= OnyxConfig.LobbyCloneMax.Value) break;
            Vector3 p = world;
            if (count > 1) { Vector2 o = Random.insideUnitCircle * 0.6f; p += new Vector3(o.x, o.y, 0f); }
            Clone e = Spawn(p, false);
            if (e != null) RandomDrift(e);
        }
    }

    private void RightClick(Vector3 world)
    {
        if (!Input.GetMouseButtonDown(1)) return;
        Clone hit = Nearest(world);
        if (hit != null && !hit.Shadow) Remove(hit);
    }

    private void Drag(Vector3 world)
    {
        if (_drag == null) return;
        if (Input.GetMouseButton(0))
        {
            if (_drag.Go != null) _drag.Go.transform.position = world + _dragOff;
        }
        else _drag = null;
    }

    private void Guard(float dt)
    {
        if (!OnyxConfig.LobbyCloneGuard.Value || PlayerControl.LocalPlayer == null || _regular.Count == 0) return;
        Vector3 c = PlayerControl.LocalPlayer.transform.position;
        float r = OnyxConfig.LobbyCloneGuardRadius.Value;
        for (int i = 0; i < _regular.Count; i++)
        {
            if (_regular[i].Go == null || _regular[i].Static) continue;
            float ang = i * (Mathf.PI * 2f / _regular.Count) - Mathf.PI * 0.5f;
            Vector3 target = c + new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, 0f);
            target.z = CloneZ;
            _regular[i].Go.transform.position = Vector3.Lerp(_regular[i].Go.transform.position, target, dt * 6f);
        }
    }

    private void Drift(float dt)
    {
        if (!OnyxConfig.LobbyCloneDrift.Value || OnyxConfig.LobbyCloneGuard.Value || PlayerControl.LocalPlayer == null) return;
        Vector3 c = PlayerControl.LocalPlayer.transform.position;
        foreach (Clone e in _regular)
        {
            if (e.Go == null || e.Static) continue;
            e.DirTimer -= dt;
            if (e.DirTimer <= 0f) RandomDrift(e);
            Vector3 p = e.Go.transform.position;
            p.x += e.Vel.x * dt;
            p.y += e.Vel.y * dt;
            Vector2 diff = new Vector2(p.x - c.x, p.y - c.y);
            if (diff.sqrMagnitude > DriftMaxR * DriftMaxR)
                e.Vel = new Vector2(c.x - p.x, c.y - p.y).normalized * DriftSpeed;
            p.z = CloneZ;
            e.Go.transform.position = p;
        }
    }

    private static void RandomDrift(Clone e)
    {
        float a = Random.Range(0f, Mathf.PI * 2f);
        e.Vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * DriftSpeed;
        e.DirTimer = Random.Range(1.5f, 4f);
    }

    private void RecordTrail()
    {
        if (!OnyxConfig.LobbyCloneShadow.Value || PlayerControl.LocalPlayer == null) return;
        _trail.Add((Time.time, PlayerControl.LocalPlayer.transform.position));
        float cutoff = Time.time - ShadowDelay - 0.5f;
        while (_trail.Count > 0 && _trail[0].t < cutoff) _trail.RemoveAt(0);
    }

    private void ManageShadow()
    {
        bool want = OnyxConfig.LobbyCloneShadow.Value;
        bool can = (LobbyBehaviour.Instance != null || OnyxFakeMap.Active) && PlayerControl.LocalPlayer != null;
        if (!want || !can)
        {
            if (_shadow != null) { Remove(_shadow); _shadow = null; }
            return;
        }

        if (_shadow == null || _shadow.Go == null)
            _shadow = Spawn(PlayerControl.LocalPlayer.transform.position, true);
        if (_shadow == null || _shadow.Go == null || _trail.Count == 0) return;

        float target = Time.time - ShadowDelay;
        Vector3 pos = PlayerControl.LocalPlayer.transform.position;
        for (int i = 0; i < _trail.Count; i++)
        {
            if (_trail[i].t <= target) pos = _trail[i].p;
            else break;
        }
        pos.z = CloneZ;
        _shadow.Go.transform.position = pos;
    }

    // ── формации ──
    internal void BuildFormation(int idx)
    {
        if (PlayerControl.LocalPlayer == null || _regular.Count == 0) return;
        Vector3 c = PlayerControl.LocalPlayer.transform.position;
        int total = _regular.Count;
        int copies = Mathf.Clamp(OnyxConfig.LobbyCloneFormationCopies.Value, 1, 5);
        int per = Mathf.Max(1, Mathf.CeilToInt(total / (float)copies));
        for (int i = 0; i < _regular.Count; i++)
        {
            Clone e = _regular[i];
            if (e.Go == null) continue;
            e.Static = true;
            int ring = Mathf.Min(copies - 1, i / per);
            Vector3 off = FormationPos(idx, i % per, per, c) - c;
            Vector3 p = c + off * (1f + ring * 0.55f);
            p.z = CloneZ;
            e.Go.transform.position = p;
        }
    }

    internal void BuildText(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || PlayerControl.LocalPlayer == null) return;
        text = text.ToUpperInvariant();

        float px = 0.38f;
        float need = OnyxCloneFont.GetTextWidth(text, px);
        if (need > 15f) px = px * 15f / need;

        List<Vector3> offsets = OnyxCloneFont.GetPositions(text, px);
        if (offsets.Count == 0) return;

        ClearRegular();
        Vector3 c = PlayerControl.LocalPlayer.transform.position;
        for (int i = 0; i < offsets.Count && i < 300; i++)
        {
            Clone e = Spawn(new Vector3(c.x + offsets[i].x, c.y + offsets[i].y, CloneZ), false);
            if (e != null) e.Static = true;
        }
    }

    internal static Vector3 FormationPos(int idx, int i, int total, Vector3 c)
    {
        float t;
        switch (idx)
        {
            case 1:
            {
                float r = Mathf.Max(1.4f, total * 0.28f);
                float a = i * (Mathf.PI * 2f / total) - Mathf.PI * 0.5f;
                return new Vector3(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r, 0f);
            }
            case 2:
            {
                float baseR = Mathf.Max(1.4f, total * 0.22f);
                float a = (i % 3) * (Mathf.PI * 2f / 3f) - Mathf.PI * 0.5f;
                float r = baseR + (i / 3) * 0.5f;
                return new Vector3(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r, 0f);
            }
            case 3:
            {
                float outer = Mathf.Max(1.8f, total * 0.22f);
                int slot = i % 10;
                float r = slot % 2 == 0 ? outer : outer * 0.42f;
                float a = slot * (Mathf.PI / 5f) - Mathf.PI * 0.5f;
                return new Vector3(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r, 0f);
            }
            case 4:
            {
                float tt = i * (Mathf.PI * 2f / total) - Mathf.PI;
                float x = 16f * Mathf.Pow(Mathf.Sin(tt), 3f);
                float y = 13f * Mathf.Cos(tt) - 5f * Mathf.Cos(2f * tt) - 2f * Mathf.Cos(3f * tt) - Mathf.Cos(4f * tt);
                return new Vector3(c.x + x * 0.12f, c.y + y * 0.12f, 0f);
            }
            case 5:
            {
                float r = Mathf.Max(1.4f, total * 0.28f);
                int side = (int)((float)i / total * 4f);
                float tt = (float)i / total * 4f - side;
                float a = side * Mathf.PI * 0.5f - Mathf.PI * 0.25f;
                float na = a + Mathf.PI * 0.5f;
                float px = Mathf.Lerp(Mathf.Cos(a), Mathf.Cos(na), tt) * r;
                float py = Mathf.Lerp(Mathf.Sin(a), Mathf.Sin(na), tt) * r;
                return new Vector3(c.x + px, c.y + py, 0f);
            }
            case 6:
            {
                float maxR = Mathf.Max(2f, total * 0.3f);
                t = (float)i / Mathf.Max(1, total - 1);
                float a = t * Mathf.PI * 2f * 2.5f;
                return new Vector3(c.x + Mathf.Cos(a) * t * maxR, c.y + Mathf.Sin(a) * t * maxR, 0f);
            }
            case 7:
            {
                float len = Mathf.Max(1.2f, total * 0.25f);
                int arm = i % 4;
                t = ((i / 4) + 1f) / Mathf.Max(1f, total / 4f);
                float x = arm == 1 ? len * t : arm == 3 ? -len * t : 0f;
                float y = arm == 0 ? len * t : arm == 2 ? -len * t : 0f;
                return new Vector3(c.x + x, c.y + y, 0f);
            }
            case 8:
            {
                float width = Mathf.Max(3f, total * 0.5f);
                t = total > 1 ? (float)i / (total - 1) : 0.5f;
                return new Vector3(c.x + (t - 0.5f) * width, c.y + Mathf.Sin(t * Mathf.PI * 3f) * 0.9f, 0f);
            }
            case 9:
            {
                t = total > 1 ? (float)i / (total - 1) : 0.5f;
                float len = Mathf.Max(4f, total * 0.4f);
                float x = (t - 0.5f) * len;
                float y = Mathf.Sin(t * Mathf.PI * 4f) * 1.7f * (1f - 0.35f * t);
                return new Vector3(c.x + x, c.y + y, 0f);
            }
            case 10:
            {
                int visorN = Mathf.Clamp(total / 6, 3, 12);
                if (i < visorN)
                {
                    float va = (float)i / visorN * Mathf.PI * 2f;
                    return new Vector3(c.x + 0.5f + Mathf.Cos(va) * 0.55f, c.y + 0.85f + Mathf.Sin(va) * 0.34f, 0f);
                }
                int bi = i - visorN, bn = Mathf.Max(1, total - visorN);
                float a = (float)bi / bn * Mathf.PI * 2f - Mathf.PI * 0.5f;
                return new Vector3(c.x + Mathf.Cos(a) * 1.3f, c.y + Mathf.Sin(a) * 2.2f, 0f);
            }
            case 11:
            {
                float s = Mathf.Max(2f, total * 0.24f);
                float tt = (float)i / total * Mathf.PI * 2f;
                float d = 1f + Mathf.Sin(tt) * Mathf.Sin(tt);
                return new Vector3(c.x + Mathf.Cos(tt) / d * s, c.y + Mathf.Sin(tt) * Mathf.Cos(tt) / d * s, 0f);
            }
            case 12:
            {
                float len = Mathf.Max(3f, total * 0.32f);
                int headN = Mathf.Clamp(total / 3, 4, 60);
                if (i < headN)
                {
                    float h = (float)i / headN;
                    float hx = ((i % 2 == 0) ? -1f : 1f) * h * len * 0.4f;
                    return new Vector3(c.x + hx, c.y + len * 0.5f - h * len * 0.45f, 0f);
                }
                int si = i - headN, sn = Mathf.Max(1, total - headN);
                return new Vector3(c.x, c.y + len * 0.5f - (float)si / sn * len, 0f);
            }
            case 13:
            {
                float width = Mathf.Max(3f, total * 0.38f);
                t = total > 1 ? (float)i / (total - 1) : 0.5f;
                float zy = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3f)) * 1.5f;
                return new Vector3(c.x + (t - 0.5f) * width, c.y + zy - 0.4f, 0f);
            }
            case 14:
            {
                float hh = Mathf.Max(3.5f, total * 0.34f);
                t = total > 1 ? (float)i / (total - 1) : 0.5f;
                float y = (0.5f - t) * hh;
                float x = t < 0.4f ? Mathf.Lerp(0.5f, -0.6f, t / 0.4f)
                        : t < 0.5f ? Mathf.Lerp(-0.6f, 0.4f, (t - 0.4f) / 0.1f)
                        : Mathf.Lerp(0.4f, -0.7f, (t - 0.5f) / 0.5f);
                return new Vector3(c.x + x, c.y + y, 0f);
            }
            case 15:
            {
                float s = Mathf.Max(2f, total * 0.26f);
                float th = (float)i / total * Mathf.PI * 2f;
                float r = (0.55f + 0.45f * Mathf.Cos(6f * th)) * s;
                return new Vector3(c.x + Mathf.Cos(th) * r, c.y + Mathf.Sin(th) * r, 0f);
            }
            case 16:
            {
                float r = Mathf.Max(1.6f, total * 0.26f);
                int outN = Mathf.Max(1, Mathf.RoundToInt(total * 0.6f));
                if (i < outN)
                {
                    float a = Mathf.Lerp(-Mathf.PI * 0.72f, Mathf.PI * 0.72f, (float)i / outN);
                    return new Vector3(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r, 0f);
                }
                int j = i - outN, jn = Mathf.Max(1, total - outN);
                float a2 = Mathf.Lerp(Mathf.PI * 0.72f, -Mathf.PI * 0.72f, (float)j / jn);
                return new Vector3(c.x + 0.55f + Mathf.Cos(a2) * r * 0.6f, c.y + Mathf.Sin(a2) * r * 0.6f, 0f);
            }
            case 17:
            {
                float s = Mathf.Max(2.2f, total * 0.3f);
                float th = (float)i / total * Mathf.PI * 2f;
                float r = Mathf.Abs(Mathf.Sin(2f * th)) * s;
                return new Vector3(c.x + Mathf.Cos(th) * r, c.y + Mathf.Sin(th) * r, 0f);
            }
            case 18:
            {
                float hh = Mathf.Max(3f, total * 0.3f);
                int trunkN = Mathf.Clamp(total / 8, 2, 12);
                if (i < trunkN)
                {
                    float tt = (float)i / trunkN;
                    return new Vector3(c.x + ((i % 2 == 0) ? -0.25f : 0.25f), c.y - hh * 0.5f - tt * 0.6f, 0f);
                }
                int j = i - trunkN, jn = Mathf.Max(1, total - trunkN);
                float t2 = (float)j / jn;
                float halfW = t2 * hh * 0.5f;
                return new Vector3(c.x + ((j % 2 == 0) ? -1f : 1f) * halfW, c.y + hh * 0.5f - t2 * hh, 0f);
            }
            default:
                return new Vector3(c.x + (i - (total - 1) * 0.5f) * 0.75f, c.y, 0f);
        }
    }

    // ── spawn / pool ──
    private Clone Spawn(Vector3 pos, bool shadow)
    {
        PlayerControl src = PlayerControl.LocalPlayer;
        if (src == null) return null;

        GameObject go = Acquire(src);
        if (go == null) return null;

        int idx = _clones.Count;
        Reinit(go, src, idx * 10);
        pos.z = CloneZ;
        go.transform.position = pos;
        go.transform.localScale = src.gameObject.transform.localScale * OnyxConfig.LobbyCloneScale.Value;
        go.SetActive(true);

        Clone e = new Clone { Go = go, Shadow = shadow };
        CapturePose(e, go);
        _clones.Add(e);
        if (!shadow) _regular.Add(e);
        return e;
    }

    private static void CapturePose(Clone e, GameObject go)
    {
        SpriteRenderer[] rends = go.GetComponentsInChildren<SpriteRenderer>(true);
        var pose = new Vector3[rends.Length];
        for (int i = 0; i < rends.Length; i++)
            if (rends[i] != null) pose[i] = rends[i].transform.localPosition;
        e.Rends = rends;
        e.Pose = pose;
    }

    private GameObject Acquire(PlayerControl src)
    {
        while (_pool.Count > 0)
        {
            GameObject g = _pool.Dequeue();
            if (g != null) return g;
        }
        return MakeClone(src);
    }

    private static GameObject MakeClone(PlayerControl src)
    {
        PlayerControl realLocal = PlayerControl.LocalPlayer;
        GameObject go = Object.Instantiate(src.gameObject);
        go.name = "OnyxLobbyClone";

        try
        {
            foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string tn;
                try { tn = mb.GetIl2CppType().Name; } catch { tn = string.Empty; }
                if (tn == "CosmeticsLayer" || tn == "HatParent" || tn == "VisorLayer" || tn == "SkinLayer"
                    || tn == "SpriteAnim" || tn == "SpriteAnimNodes" || tn == "SpriteAnimNodeSync"
                    || tn == "PlayerAnimations") continue;
                mb.enabled = false;
            }
        }
        catch { }

        try
        {
            PlayerControl pc = go.GetComponent<PlayerControl>();
            if (pc != null && PlayerControl.AllPlayerControls != null) PlayerControl.AllPlayerControls.Remove(pc);
            if (realLocal != null && PlayerControl.LocalPlayer != realLocal) PlayerControl.LocalPlayer = realLocal;
            if (pc != null && pc.cosmetics != null && pc.cosmetics.nameText != null)
                ((Component)pc.cosmetics.nameText).gameObject.SetActive(false);
        }
        catch { }

        try
        {
            foreach (Component comp in go.GetComponentsInChildren<Component>(true))
            {
                switch (comp)
                {
                    case InnerNetObject net: net.NetId = uint.MaxValue; break;
                    case Collider2D col: col.enabled = false; break;
                    case AudioSource au: au.enabled = false; break;
                    case Rigidbody2D rb: rb.isKinematic = true; break;
                }
            }
        }
        catch { }

        return go;
    }

    private static void Reinit(GameObject go, PlayerControl src, int off)
    {
        try
        {
            PlayerControl pc = go.GetComponent<PlayerControl>();
            if (pc != null && pc.cosmetics != null && src.Data != null && src.Data.DefaultOutfit != null)
            {
                NetworkedPlayerInfo.PlayerOutfit o = src.Data.DefaultOutfit;
                int over = OnyxConfig.LobbyCloneColorId.Value;
                int color = over >= 0 ? over : o.ColorId;
                pc.cosmetics.SetHat(o.HatId, color);
                pc.cosmetics.SetSkin(o.SkinId, color);
                pc.cosmetics.SetVisor(o.VisorId, color);
                pc.cosmetics.SetColor(color);
            }
        }
        catch { }

        CopySprites(src.gameObject, go, off);
    }

    private static void CopySprites(GameObject src, GameObject clone, int off)
    {
        try
        {
            SpriteRenderer[] s = src.GetComponentsInChildren<SpriteRenderer>(true);
            SpriteRenderer[] d = clone.GetComponentsInChildren<SpriteRenderer>(true);
            var map = new Dictionary<string, SpriteRenderer>(s.Length);
            Transform sroot = src.transform;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == null) continue;
                string key = PathOf(s[i].transform, sroot);
                if (!map.ContainsKey(key)) map[key] = s[i];
            }
            Transform droot = clone.transform;
            for (int i = 0; i < d.Length; i++)
            {
                if (d[i] == null) continue;
                if (!map.TryGetValue(PathOf(d[i].transform, droot), out SpriteRenderer sr) || sr == null) continue;
                d[i].sprite = sr.sprite;
                d[i].color = sr.color;
                d[i].flipX = sr.flipX;
                d[i].flipY = sr.flipY;
                d[i].sortingOrder = sr.sortingOrder + off;
                Transform st = sr.transform;
                Transform dt = d[i].transform;
                dt.localPosition = st.localPosition;
                dt.localRotation = st.localRotation;
                dt.localScale = st.localScale;
            }
        }
        catch { }
    }

    private static string PathOf(Transform t, Transform root)
    {
        string p = t.name;
        Transform cur = t.parent;
        while (cur != null && cur != root)
        {
            p = cur.name + "/" + p;
            cur = cur.parent;
        }
        return p;
    }

    private void Remove(Clone e)
    {
        Pool(e.Go);
        _clones.Remove(e);
        if (!e.Shadow) _regular.Remove(e);
        if (_drag == e) _drag = null;
        if (_shadow == e) _shadow = null;
    }

    private void ClearRegular()
    {
        for (int i = _regular.Count - 1; i >= 0; i--)
        {
            Pool(_regular[i].Go);
            _clones.Remove(_regular[i]);
        }
        _regular.Clear();
        _drag = null;
    }

    internal void ClearAll()
    {
        foreach (Clone e in _clones) Pool(e.Go);
        _clones.Clear();
        _regular.Clear();
        _trail.Clear();
        _drag = null;
        _shadow = null;
    }

    private void Pool(GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);
        if (_pool.Count < 80) _pool.Enqueue(go);
    }

    private Clone Nearest(Vector3 world)
    {
        Clone best = null;
        float bestD = PickRadius;
        for (int i = 0; i < _clones.Count; i++)
        {
            Clone e = _clones[i];
            if (e.Go == null) continue;
            float d = Vector2.Distance(e.Go.transform.position, world);
            if (d < bestD) { bestD = d; best = e; }
        }
        return best;
    }

    private static Vector3 MouseWorld()
    {
        if (Camera.main == null) return Vector3.zero;
        Vector3 v = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        v.z = 0f;
        return v;
    }

    public void OnDestroy()
    {
        foreach (Clone e in _clones) if (e.Go != null) e.Go.SetActive(false);
        _clones.Clear();
        _regular.Clear();
    }
}
