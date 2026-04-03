using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using ViitorCloud.Utility.PopupManager;

public class DataHandler:MonoBehaviour {
    public static UnityEvent<MAPData> OnDataProcessedEvent = new UnityEvent<MAPData>();
    [SerializeField]
    private string FileNameWithoutExtension = "FileName";
    private string FilePath { get { return Path.Combine(Application.persistentDataPath,FileNameWithExtension); } }
    private string FileNameWithExtension { get { return $"{FileNameWithoutExtension}{Extension.json}"; } }

    [SerializeField] private MediaManager mediaManager;
    private void OnEnable() {
        APIHandler.OnAPIDataFetchedEvent.AddListener(OnAPIDataFetched);
    }
    private void OnDisable() {
        APIHandler.OnAPIDataFetchedEvent.RemoveListener(OnAPIDataFetched);
    }
    private void OnAPIDataFetched(MAPData dataFromAPI) {
        PopupManager.Instance.ShowLoading();
        if (File.Exists(FilePath)) {
            try {
                string json = File.ReadAllText(FilePath);
                MAPData localFileData = JsonUtility.FromJson<MAPData>(json);

                if (localFileData != null) {
                    DownloadAllMediaAndProceed(localFileData,OnMediaFileDownloaded(localFileData));
                }
            } catch (Exception e) {
                Debug.LogError("ERROR: loading local file, falling back to API: " + e.Message);
            }
            return;
        }

        if (dataFromAPI != null) {
            DownloadAllMediaAndProceed(dataFromAPI,() => {
                SaveToCache(dataFromAPI,FilePath);
                OnDataProcessedEvent.Invoke(dataFromAPI);
                PopupManager.Instance.HideLoading();
            });
        }
    }

    private Action OnMediaFileDownloaded(MAPData localFileData) {
        return () => {
            OnContinueWithTheGivenData(localFileData);
        };
    }

    private void OnContinueWithTheGivenData(MAPData cachedData) {
        OnDataProcessedEvent.Invoke(cachedData);
        PopupManager.Instance.HideLoading();
    }

    private void DownloadAllMediaAndProceed(MAPData root,Action OnDonwnloadCompleted) {
        List<string> allDownloadUrl = new List<string>();
        root.CollectAllImagePaths(allDownloadUrl);
        mediaManager.AssignDownloadableUrl(allDownloadUrl);
        mediaManager.DownloadMediaFilesAsync(OnDonwnloadCompleted);
    }
    private void SaveToCache(MAPData rootData,string path) {
        try {
            string json = JsonUtility.ToJson(rootData,true);
            File.WriteAllText(path,json);
            Debug.Log("Data saved to: " + path);
        } catch (Exception e) {
            Debug.LogError("Failed to save data to cache: " + e.Message);
        }
    }
    internal void ClearLocalStoredData() {
        if (File.Exists(FilePath)) {
            File.Delete(FilePath);
            PopupManager.Instance.ShowToast("Local data cleared successfully.");
        }
    }
}//DataHandler class end.

