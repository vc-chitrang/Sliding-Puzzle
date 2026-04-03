using System.IO;
using UnityEngine;
using UnityEngine.Events;
using ViitorCloud.Utility.PopupManager;

public class APIHandler:MonoBehaviour {
    public static UnityEvent<MAPData> OnAPIDataFetchedEvent = new UnityEvent<MAPData>();

    private void OnEnable() {
        LoginHandler.onLoginSuccessEvent += OnLoginSuccess;
        LoginHandler.onLoginFailureEvent += OnLoginFailure;
    }
    private void OnDisable() {
        LoginHandler.onLoginSuccessEvent -= OnLoginSuccess;
        LoginHandler.onLoginFailureEvent -= OnLoginFailure;
    }
    private void OnLoginSuccess(LoginResponse    response) {
        Debug.Log("Login Successful! Token: " + response.access_token);
        APICall();
    }
    private void OnLoginFailure(string message) {
        Debug.LogError("Login Failed: " + message);
    }
    private void Start() {
        APICall();
    }
    internal void APICall() {
        PopupManager.Instance.ShowLoading();
        PopupManager.Instance.SetProgressText("Fetching data...");
        global::APICall.Instance.RequestData(API.APIGetAllData,OnAPIDataFetchedSuccess,OnAPIDataFetchedFailed);
    }
    private void OnAPIDataFetchedSuccess(MAPData response) {
        PopupManager.Instance.HideLoading();

        if (response != null && response.results != null) {
            OnAPIDataFetchedEvent?.Invoke(response);
        } else {
            Debug.LogError("APIHandler: response or response.results is null.");
            PopupManager.Instance.ShowToast("Unable to retrieve data. Please try again later.");
        }
    }
    private void OnAPIDataFetchedFailed(string error) {
        PopupManager.Instance.ShowToast(error);
        Debug.LogError("Fail: " + error);
    }
}//APIHandler class end.