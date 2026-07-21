using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace Onyx;

internal enum UpState { Idle, Checking, Found, Loading, Done, Fail }

public sealed class OnyxUpdateCheck : MonoBehaviour
{
    private const string LatestPage = "https://github.com/Kawas-set1/OnyxMenu/releases/latest";
    private const string Api = "https://api.github.com/repos/Kawas-set1/OnyxMenu/releases/latest";
    private const string ReleasesUrl = "https://github.com/Kawas-set1/OnyxMenu/releases";

    private static readonly HttpClient Http = Make();

    internal static UpState State { get; private set; } = UpState.Idle;
    internal static string Latest { get; private set; } = "";
    internal static string Err { get; private set; } = "";
    private static string _url = "";
    private static string _asset = "";

    private Task<string> _check;
    private Task<byte[]> _load;
    private bool _started;
    private bool _shown;
    private float _at;

    internal static OnyxUpdateCheck Instance { get; private set; }
    public void Awake() => Instance = this;
    public void Start() => _at = Time.unscaledTime + 10f;

    private static HttpClient Make()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        h.DefaultRequestHeaders.Add("User-Agent", "OnyxMenu");
        return h;
    }

    public void Update()
    {
        if (!_started && Time.unscaledTime >= _at)
        {
            _started = true;
            BeginCheck();
        }

        if (_check != null && _check.IsCompleted)
        {
            Task<string> t = _check;
            _check = null;
            if (t.IsFaulted || t.IsCanceled) Fail(ErrOf(t));
            else Apply(t.Result);
        }

        if (_load != null && _load.IsCompleted)
        {
            Task<byte[]> t = _load;
            _load = null;
            if (t.IsFaulted || t.IsCanceled)
            {
                Fail(ErrOf(t));
                OnyxToast.Push(OnyxText.T("Обновление", "Update"), OnyxText.T("Не скачалось: ", "Download failed: ") + Err, 6f, OnyxNotifyKind.Danger);
            }
            else Install(t.Result);
        }

        if (!_shown && State == UpState.Found)
        {
            _shown = true;
            OnyxToast.Push(OnyxText.T("Доступно обновление", "Update available"),
                OnyxText.T("Вышла v", "Version v") + Latest + OnyxText.T(" — качается из меню.", " — download it from the menu."),
                9f, OnyxNotifyKind.Success);
        }
    }

    internal static void Recheck()
    {
        if (Instance == null || State == UpState.Checking || State == UpState.Loading) return;
        Instance._shown = false;
        Instance.BeginCheck();
    }

    private void BeginCheck()
    {
        Err = "";
        State = UpState.Checking;
        try { _check = Task.Run(() => Fetch()); }
        catch (Exception e) { Fail(e.Message); _check = null; }
    }

    internal static void Download()
    {
        if (Instance == null || State != UpState.Found || Instance._load != null) return;
        if (string.IsNullOrWhiteSpace(_url))
        {
            try { GUIUtility.systemCopyBuffer = ReleasesUrl; } catch { }
            try { Application.OpenURL(ReleasesUrl); } catch { }
            return;
        }

        Err = "";
        State = UpState.Loading;
        try { Instance._load = Http.GetByteArrayAsync(_url); }
        catch (Exception e) { Fail(e.Message); Instance._load = null; }
    }

    private static void Fail(string e)
    {
        Err = e ?? "";
        State = UpState.Fail;
    }

    private static string ErrOf(Task t)
    {
        Exception e = t.Exception != null ? t.Exception.GetBaseException() : null;
        if (e == null) return t.IsCanceled ? "canceled" : "unknown";
        return e.GetType().Name + ": " + e.Message;
    }

    [HideFromIl2Cpp]
    private static void Apply(string json)
    {
        try
        {
            string tag = Grab(json, "\"tag_name\"");
            if (string.IsNullOrEmpty(tag)) { State = UpState.Idle; return; }

            Latest = tag.TrimStart('v', 'V').Trim();
            PickAsset(json);
            State = Newer(Latest, OnyxPlugin.PluginVersion) ? UpState.Found : UpState.Idle;
        }
        catch (Exception e) { Fail(e.Message); }
    }

    [HideFromIl2Cpp]
    private static void PickAsset(string json)
    {
        _url = "";
        _asset = "";
        int at = json.IndexOf("\"assets\"", StringComparison.Ordinal);
        if (at < 0) return;

        while (true)
        {
            int n = json.IndexOf("\"name\"", at, StringComparison.Ordinal);
            if (n < 0) return;
            string name = Grab(json.Substring(n), "\"name\"");
            if (Ours(name))
            {
                int u = json.IndexOf("\"browser_download_url\"", n, StringComparison.Ordinal);
                if (u >= 0)
                {
                    string url = Grab(json.Substring(u), "\"browser_download_url\"");
                    if (!string.IsNullOrEmpty(url)) { _url = url; _asset = name; return; }
                }
            }
            at = n + 6;
        }
    }

    private static bool Ours(string name)
        => !string.IsNullOrWhiteSpace(name)
        && name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
        && name.IndexOf("Onyx", StringComparison.OrdinalIgnoreCase) >= 0;

    [HideFromIl2Cpp]
    private static void Install(byte[] data)
    {
        if (data == null || data.Length < 1024) { Fail("пустой файл"); return; }
        try
        {
            string cur = Assembly.GetExecutingAssembly().Location;
            string dir = Path.GetDirectoryName(cur);
            string dst = Path.Combine(dir, string.IsNullOrEmpty(_asset) ? Path.GetFileName(cur) : _asset);
            string tmp = dst + ".new";
            string bak = cur + ".bak";

            File.WriteAllBytes(tmp, data);
            if (File.Exists(bak)) File.Delete(bak);
            File.Move(cur, bak);
            if (File.Exists(dst)) File.Delete(dst);
            File.Move(tmp, dst);

            State = UpState.Done;
            OnyxToast.Push(OnyxText.T("Обновление", "Update"), OnyxText.T("Установлено. Перезапусти игру.", "Installed. Restart the game."), 9f, OnyxNotifyKind.Success);
        }
        catch (Exception e)
        {
            Fail(e.GetType().Name + ": " + e.Message);
            OnyxToast.Push(OnyxText.T("Обновление", "Update"), OnyxText.T("Не установилось: ", "Install failed: ") + Err, 6f, OnyxNotifyKind.Danger);
        }
    }

    internal static void Restart()
    {
        try { Application.Quit(); } catch { }
    }

    [HideFromIl2Cpp]
    private static string Fetch()
    {
        try { return Http.GetStringAsync(Api).GetAwaiter().GetResult(); }
        catch
        {
            string tag = TagFromRedirect();
            if (string.IsNullOrEmpty(tag)) throw;
            return "{\"tag_name\":\"" + tag + "\",\"assets\":[]}";
        }
    }

    [HideFromIl2Cpp]
    private static string TagFromRedirect()
    {
        try
        {
            var resp = Http.GetAsync(LatestPage, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
            string url = resp.RequestMessage != null && resp.RequestMessage.RequestUri != null ? resp.RequestMessage.RequestUri.ToString() : null;
            if (!string.IsNullOrEmpty(url))
            {
                int i = url.LastIndexOf("/tag/", StringComparison.Ordinal);
                if (i >= 0) return url.Substring(i + 5);
            }
        }
        catch (Exception e) { OnyxPlugin.Logger?.LogWarning((object)("[Update] redirect: " + e.Message)); }
        return null;
    }

    [HideFromIl2Cpp]
    private static string Grab(string json, string key)
    {
        int i = json.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return null;
        i = json.IndexOf(':', i);
        if (i < 0) return null;
        int q1 = json.IndexOf('"', i + 1);
        if (q1 < 0) return null;
        int q2 = json.IndexOf('"', q1 + 1);
        return q2 < 0 ? null : json.Substring(q1 + 1, q2 - q1 - 1);
    }

    [HideFromIl2Cpp]
    private static bool Newer(string latest, string current)
    {
        try
        {
            int[] a = Parts(latest), b = Parts(current);
            for (int i = 0; i < 3; i++)
            {
                if (a[i] > b[i]) return true;
                if (a[i] < b[i]) return false;
            }
        }
        catch { }
        return false;
    }

    [HideFromIl2Cpp]
    private static int[] Parts(string v)
    {
        var r = new int[3];
        string[] p = v.Trim().Split('.', '-', '+');
        for (int i = 0; i < 3 && i < p.Length; i++)
        {
            var sb = new StringBuilder();
            foreach (char c in p[i])
            {
                if (!char.IsDigit(c)) break;
                sb.Append(c);
            }
            int.TryParse(sb.ToString(), out r[i]);
        }
        return r;
    }
}
