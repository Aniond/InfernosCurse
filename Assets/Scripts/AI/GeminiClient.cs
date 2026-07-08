using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Minimal runtime Gemini client (gemini-2.5-flash). Key comes from the
// gitignored Assets/Scripts/Config/GeminiConfig.json ({"apiKey":"..."}) —
// David's local machine for now; a settings-menu BYO-key field ships later.
// Available == false -> callers MUST fall back to their classic-mode path
// (dual-mode product rule: the game is complete without AI).
public static class GeminiClient
{
    [Serializable] class Cfg { public string apiKey; }

    static string _key;
    static bool _probed;

    public static bool Available
    {
        get
        {
            if (!_probed)
            {
                _probed = true;
                try
                {
                    string p = System.IO.Path.Combine(Application.dataPath, "Scripts/Config/GeminiConfig.json");
                    if (System.IO.File.Exists(p))
                        _key = JsonUtility.FromJson<Cfg>(System.IO.File.ReadAllText(p))?.apiKey;
                }
                catch { }
            }
            return !string.IsNullOrEmpty(_key);
        }
    }

    // Fire a prompt; onDone(text) with the model's reply, onFail(reason) on
    // any error. Run via StartCoroutine from any MonoBehaviour.
    public static IEnumerator Generate(string prompt, Action<string> onDone, Action<string> onFail)
    {
        if (!Available) { onFail?.Invoke("no api key"); yield break; }

        string url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=" + _key;
        string body = "{\"contents\":[{\"parts\":[{\"text\":\"" + Escape(prompt) + "\"}]}]}";

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 20;
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onFail?.Invoke(req.error);
            yield break;
        }
        string text = ExtractText(req.downloadHandler.text);
        if (string.IsNullOrEmpty(text)) onFail?.Invoke("empty response");
        else onDone?.Invoke(text);
    }

    static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");

    // Pull the first candidates[].content.parts[].text without a JSON lib.
    static string ExtractText(string json)
    {
        int i = json.IndexOf("\"text\":", StringComparison.Ordinal);
        if (i < 0) return null;
        int start = json.IndexOf('"', i + 7) + 1;
        var sb = new StringBuilder();
        for (int c = start; c < json.Length; c++)
        {
            if (json[c] == '\\' && c + 1 < json.Length)
            {
                char n = json[c + 1];
                sb.Append(n == 'n' ? '\n' : n);
                c++;
                continue;
            }
            if (json[c] == '"') break;
            sb.Append(json[c]);
        }
        return sb.ToString();
    }
}
