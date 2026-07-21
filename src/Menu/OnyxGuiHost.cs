using UnityEngine;

namespace Onyx;

public sealed class OnyxGuiHost : MonoBehaviour
{
    private OnyxMenu _menu;
    private OnyxLobby _lobby;
    private OnyxTracers _tracers;
    private OnyxRadar _radar;
    private OnyxLobbyBar _bar;
    private OnyxMusicPlayer _music;
    private OnyxRadial _radial;
    private OnyxEventLog _log;
    private OnyxChatWindow _chat;
    private OnyxToast _toast;
    private bool _bound;

    private void Bind()
    {
        if (_bound) return;
        _bound = true;
        _menu = GetComponent<OnyxMenu>();
        _lobby = GetComponent<OnyxLobby>();
        _tracers = GetComponent<OnyxTracers>();
        _radar = GetComponent<OnyxRadar>();
        _bar = GetComponent<OnyxLobbyBar>();
        _music = GetComponent<OnyxMusicPlayer>();
        _radial = GetComponent<OnyxRadial>();
        _log = GetComponent<OnyxEventLog>();
        _chat = GetComponent<OnyxChatWindow>();
        _toast = GetComponent<OnyxToast>();
    }

    public void OnGUI()
    {
        Bind();
        Matrix4x4 m = GUI.matrix;
        Color c = GUI.color;

        if (_menu != null) { try { _menu.DrawGui(); } catch { } GUI.matrix = m; GUI.color = c; }
        if (_lobby != null) { try { _lobby.DrawGui(); } catch { } GUI.matrix = m; GUI.color = c; }
        if (_tracers != null) { try { _tracers.DrawGui(); } catch { } GUI.matrix = m; GUI.color = c; }
        if (_radar != null) { try { _radar.DrawGui(); } catch { } GUI.matrix = m; GUI.color = c; }
        if (_bar != null) { try { _bar.DrawGui(); } catch { } GUI.matrix = m; GUI.color = c; }
        if (_music != null) { try { _music.DrawGui(); } catch { } GUI.matrix = m; GUI.color = c; }
        if (_radial != null) { try { _radial.DrawGui(); } catch { } GUI.matrix = m; GUI.color = c; }
        if (_log != null) { try { _log.DrawGui(); } catch { } GUI.matrix = m; GUI.color = c; }
        if (_chat != null) { try { _chat.DrawGui(); } catch { } GUI.matrix = m; GUI.color = c; }
        if (_toast != null) { try { _toast.DrawGui(); } catch { } GUI.matrix = m; GUI.color = c; }
    }
}
