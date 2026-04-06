using UnityEngine;
using UnityEngine.Events;
using ViitorCloud.Utility.PopupManager;

public class APIHandler : MonoBehaviour
{
    public static UnityEvent<MAPData> OnAPIDataFetchedEvent = new UnityEvent<MAPData>();

    private void OnEnable()
    {
        LoginHandler.onLoginSuccessEvent += OnLoginSuccess;
        LoginHandler.onLoginFailureEvent += OnLoginFailure;
    }

    private void OnDisable()
    {
        LoginHandler.onLoginSuccessEvent -= OnLoginSuccess;
        LoginHandler.onLoginFailureEvent -= OnLoginFailure;
    }

    private void OnLoginSuccess(LoginResponse response)
    {
        Debug.Log("Login Successful! Token: " + response.access_token);
        FetchDefaultData();
    }

    private void OnLoginFailure(string message)
    {
        PopupManager.Instance.HideLoading();
        Debug.LogError("Login Failed: " + message);
    }
    private void Start() {
        FetchDefaultData();
    }
    /// <summary>
    /// Fetches default collection data after login.
    /// Uses the base URL without extra params (returns all data with pagination).
    /// </summary>
    internal void FetchDefaultData()
    {
        PopupManager.Instance.ShowLoading();
        PopupManager.Instance.SetProgressText("Fetching data...");
        APICall.Instance.RequestData(API.APIGetAllData, OnAPIDataFetchedSuccess, OnAPIDataFetchedFailed);
    }

    private void OnAPIDataFetchedSuccess(MAPData response)
    {
        PopupManager.Instance.HideLoading();

        if (response != null && response.results != null)
        {
            Debug.Log($"[APIHandler] Fetched {response.results.data?.Count ?? 0} artworks.");
            OnAPIDataFetchedEvent?.Invoke(response);
        }
        else
        {
            Debug.LogError("APIHandler: response or response.results is null.");
            PopupManager.Instance.ShowToast("Unable to retrieve data. Please try again later.");
        }
    }

    private void OnAPIDataFetchedFailed(string error)
    {
        PopupManager.Instance.HideLoading();
        PopupManager.Instance.ShowToast("Failed: " + error);
        Debug.LogError("APIHandler Fail: " + error);
    }
}
