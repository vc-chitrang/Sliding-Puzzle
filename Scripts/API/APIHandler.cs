using UnityEngine;
using UnityEngine.Events;
using ViitorCloud.Utility.PopupManager;

public class APIHandler : MonoBehaviour
{
    public static UnityEvent<MAPData> OnAPIDataFetchedEvent = new UnityEvent<MAPData>();

    /// <summary>
    /// The most recent successful API response.
    /// Allows screens that become active after the event has already fired
    /// (e.g. Launch_Screen) to retrieve the data without waiting for the next fetch.
    /// </summary>
    public static MAPData LastFetchedData { get; private set; }

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
            LastFetchedData = response;
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
