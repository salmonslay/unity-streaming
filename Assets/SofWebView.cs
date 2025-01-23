using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class SofWebView : MonoBehaviour
{
    private WebViewObject _webViewObject;

    IEnumerator Start()
    {
        _webViewObject = (new GameObject("_webViewObject")).AddComponent<WebViewObject>();
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        _webViewObject.canvas = GameObject.Find("Canvas");
#endif
        _webViewObject.Init(
            cb: msg => { Debug.Log(string.Format("CallFromJS[{0}]", msg)); },
            err: msg => { Debug.Log(string.Format("CallOnError[{0}]", msg)); },
            httpErr: msg => { Debug.Log(string.Format("CallOnHttpError[{0}]", msg)); },
            started: msg => { Debug.Log(string.Format("CallOnStarted[{0}]", msg)); },
            hooked: msg => { Debug.Log(string.Format("CallOnHooked[{0}]", msg)); },
            cookies: msg => { Debug.Log(string.Format("CallOnCookies[{0}]", msg)); },
            ld: msg =>
            {
                Debug.Log(string.Format("CallOnLoaded[{0}]", msg));
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_IOS
                // NOTE: the following js definition is required only for UIWebView; if
                // enabledWKWebView is true and runtime has WKWebView, Unity.call is defined
                // directly by the native plugin.
#if true
                var js = @"
                    if (!(window.webkit && window.webkit.messageHandlers)) {
                        window.Unity = {
                            call: function(msg) {
                                window.location = 'unity:' + msg;
                            }
                        };
                    }
                ";
#else
                // NOTE: depending on the situation, you might prefer this 'iframe' approach.
                // cf. https://github.com/gree/unity-webview/issues/189
                var js = @"
                    if (!(window.webkit && window.webkit.messageHandlers)) {
                        window.Unity = {
                            call: function(msg) {
                                var iframe = document.createElement('IFRAME');
                                iframe.setAttribute('src', 'unity:' + msg);
                                document.documentElement.appendChild(iframe);
                                iframe.parentNode.removeChild(iframe);
                                iframe = null;
                            }
                        };
                    }
                ";
#endif
#elif UNITY_WEBPLAYER || UNITY_WEBGL
                var js = @"
                    window.Unity = {
                        call:function(msg) {
                            parent.unityWebView.sendMessage('_webViewObject', msg);
                        }
                    };
                ";
#else
                var js = "";
#endif
                _webViewObject.EvaluateJS(js + @"Unity.call('ua=' + navigator.userAgent)");
            },
            transparent: true
            //zoom: true,
            //ua: "custom user agent string",
            //radius: 0,  // rounded corner radius in pixel
            //// android
            //androidForceDarkMode: 0,  // 0: follow system setting, 1: force dark off, 2: force dark on
            //// ios
            //enableWKWebView: true,
            //wkContentMode: 0,  // 0: recommended, 1: mobile, 2: desktop
            //wkAllowsLinkPreview: true,
            //// editor
            //separated: false
        );

        // cf. https://github.com/gree/unity-webview/issues/1094#issuecomment-2358718029
        while (!_webViewObject.IsInitialized())
        {
            yield return null;
        }

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        _webViewObject.bitmapRefreshCycle = 1;
        _webViewObject.devicePixelRatio = 1;  // 1 or 2
#endif
        // cf. https://github.com/gree/unity-webview/pull/512
        // Added alertDialogEnabled flag to enable/disable alert/confirm/prompt dialogs. by KojiNakamaru · Pull Request #512 · gree/unity-webview
        //_webViewObject.SetAlertDialogEnabled(false);

        // cf. https://github.com/gree/unity-webview/pull/728
        //_webViewObject.SetCameraAccess(true);
        //_webViewObject.SetMicrophoneAccess(true);

        // cf. https://github.com/gree/unity-webview/pull/550
        // introduced SetURLPattern(..., hookPattern). by KojiNakamaru · Pull Request #550 · gree/unity-webview
        //_webViewObject.SetURLPattern("", "^https://.*youtube.com", "^https://.*google.com");

        // cf. https://github.com/gree/unity-webview/pull/570
        // Add BASIC authentication feature (Android and iOS with WKWebView only) by takeh1k0 · Pull Request #570 · gree/unity-webview
        //_webViewObject.SetBasicAuthInfo("id", "password");

        //_webViewObject.SetScrollbarsVisibility(true);

        _webViewObject.SetMargins(0, 0, 0, 0);
        _webViewObject.SetTextZoom(100); // android only. cf. https://stackoverflow.com/questions/21647641/android-webview-set-font-size-system-default/47017410#47017410
        //_webViewObject.SetMixedContentMode(2);  // android only. 0: MIXED_CONTENT_ALWAYS_ALLOW, 1: MIXED_CONTENT_NEVER_ALLOW, 2: MIXED_CONTENT_COMPATIBILITY_MODE
        _webViewObject.SetVisibility(false);

        yield break;
    }

    public void Load(string url)
    {
        StartCoroutine(LoadURL(url));
        _webViewObject.SetVisibility(true);

        // remove all buttons
        var buttons = FindObjectsOfType<UnityEngine.UI.Button>();
        foreach (var button in buttons)
        {
            Destroy(button.gameObject);
        }
    }

    public void Hide()
    {
        StartCoroutine(LoadURL("about:blank"));
        _webViewObject.SetVisibility(false);
    }

    private IEnumerator LoadURL(string Url)
    {
#if !UNITY_WEBPLAYER && !UNITY_WEBGL
        if (Url.StartsWith("http"))
        {
            _webViewObject.LoadURL(Url.Replace(" ", "%20"));
        }
        else
        {
            var exts = new string[]
            {
                ".jpg",
                ".js",
                ".html", // should be last
            };
            foreach (var ext in exts)
            {
                var url = Url.Replace(".html", ext);
                var src = System.IO.Path.Combine(Application.streamingAssetsPath, url);
                var dst = System.IO.Path.Combine(Application.temporaryCachePath, url);
                byte[] result = null;
                if (src.Contains("://"))
                {
                    // for Android
#if UNITY_2018_4_OR_NEWER
                    // NOTE: a more complete code that utilizes UnityWebRequest can be found in https://github.com/gree/unity-webview/commit/2a07e82f760a8495aa3a77a23453f384869caba7#diff-4379160fa4c2a287f414c07eb10ee36d
                    UnityWebRequest unityWebRequest = UnityWebRequest.Get(src);
                    yield return unityWebRequest.SendWebRequest();
                    result = unityWebRequest.downloadHandler.data;
#else
                    var www = new WWW(src);
                    yield return www;
                    result = www.bytes;
#endif
                }
                else
                {
                    result = System.IO.File.ReadAllBytes(src);
                }

                System.IO.File.WriteAllBytes(dst, result);
                if (ext == ".html")
                {
                    _webViewObject.LoadURL("file://" + dst.Replace(" ", "%20"));
                    break;
                }
            }
        }
#else
        if (Url.StartsWith("http")) {
            _webViewObject.LoadURL(Url.Replace(" ", "%20"));
        } else {
            _webViewObject.LoadURL("StreamingAssets/" + Url.Replace(" ", "%20"));
        }
#endif
    }
}