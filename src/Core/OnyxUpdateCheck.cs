using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace Onyx;

public sealed class OnyxUpdateCheck : MonoBehaviour
{
    private const string LatestPage = "https://github.com/Kawas-set1/OnyxMenu/releases/latest";
    private const string Api = "https://api.github.com/repos/Kawas-set1/OnyxMenu/releases/latest";
    private const string ReleasesUrl = "https://github.com/Kawas-set1/OnyxMenu/releases";

    private volatile bool _done;
    private volatile bool _newer;
    private volatile string _latest;
    private bool _started;
    private bool _shown;
    private float _at;

    public void Start() => _at = Time.unscaledTime + 10f;

    public void Update()
    {
        if (_shown) return;

        if (!_started)
        {
            if (Time.unscaledTime < _at) return;
            _started = true;
            ThreadPool.QueueUserWorkItem(_ => Fetch());
            return;
        }

        if (!_done) return;
        _shown = true;
        if (!_newer || string.IsNullOrEmpty(_latest)) return;

        OnyxToast.Push(
            OnyxText.T("Доступно обновление", "Update available"),
            OnyxText.T("Вышла v", "Version v") + _latest + OnyxText.T(" — обнови мод, ссылка скопирована.", " is out — update, link copied."),
            9f, OnyxNotifyKind.Success);
        try { GUIUtility.systemCopyBuffer = ReleasesUrl; } catch { }
    }

    [HideFromIl2Cpp]
    private void Fetch()
    {
        try
        {
            string tag = TagFromRedirect();

            if (string.IsNullOrEmpty(tag))
            {
                try
                {
                    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
                    http.DefaultRequestHeaders.Add("User-Agent", "OnyxMenu");
                    tag = Grab(http.GetStringAsync(Api).GetAwaiter().GetResult(), "\"tag_name\"");
                }
                catch (Exception e) { OnyxPlugin.Logger?.LogWarning((object)("[Update] api: " + e.Message)); }
            }

            if (!string.IsNullOrEmpty(tag))
            {
                _latest = tag.TrimStart('v', 'V');
                _newer = Newer(_latest, OnyxPlugin.PluginVersion);
            }
        }
        catch (Exception e) { OnyxPlugin.Logger?.LogWarning((object)("[Update] error: " + e.Message)); }
        finally { _done = true; }
    }

    [HideFromIl2Cpp]
    private static string TagFromRedirect()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            http.DefaultRequestHeaders.Add("User-Agent", "OnyxMenu");
            var resp = http.GetAsync(LatestPage, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
            string url = resp.RequestMessage != null && resp.RequestMessage.RequestUri != null ? resp.RequestMessage.RequestUri.ToString() : null;
            if (!string.IsNullOrEmpty(url))
            {
                int i = url.LastIndexOf("/tag/", StringComparison.Ordinal);
                if (i >= 0) return url.Substring(i + 5);
            }
        }
        catch (Exception e) { OnyxPlugin.Logger?.LogWarning((object)("[Update] redirect: " + e.Message + " | " + (e.InnerException != null ? e.InnerException.Message : "-"))); }
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
            foreach (char c in p[i]) if (char.IsDigit(c)) sb.Append(c);
            int.TryParse(sb.ToString(), out r[i]);
        }
        return r;
    }
}
