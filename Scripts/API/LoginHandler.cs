using System;

using UnityEngine;
using UnityEngine.Events;

using ViitorCloud.Utility.PopupManager;

using static UnityEngine.Audio.ProcessorInstance;

[Serializable]
public class LoginData {
    public string grant_type;
    public string client_id;
    public string client_secret;
    public string username;
    public string password;
    public string scope;
}

[Serializable]
public class LoginResponse {
    public string token_type;
    public int expires_in;
    public string access_token;
}

public class LoginHandler:MonoBehaviour {
    [SerializeField]
    private LoginData loginData;
    public static UnityAction<LoginResponse> onLoginSuccessEvent;
    public static UnityAction<string> onLoginFailureEvent;
    
    [SerializeField]
    [TextArea(5,15)]
    public string _token;

    private void Start() {
        //Login();
        ViitorCloud.API.ServerCommunication.ViitorCloudToken = _token;
    }
    
    private void Login() {
        string formData = JsonUtility.ToJson(loginData);
        APICall.Instance.RequestLogin(formData,API.APILogin,OnLoginSuccess,OnLoginFailed);
    }

    private void OnLoginSuccess(LoginResponse response) {
        if (response != null && !string.IsNullOrEmpty(response.access_token)) {
            ViitorCloud.API.ServerCommunication.ViitorCloudToken = response.access_token;
            onLoginSuccessEvent?.Invoke(response);
        } else {
            PopupManager.Instance.ShowToast("Something went wrong!");
            onLoginFailureEvent?.Invoke("Something went wrong!");
        }
    }

    private void OnLoginFailed(string error) {
        PopupManager.Instance.HideLoading();
        PopupManager.Instance.ShowToast("Login Failed");
        onLoginFailureEvent?.Invoke(error);
    }
}//LoginHandler class end
