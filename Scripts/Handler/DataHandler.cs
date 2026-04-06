using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using ViitorCloud.Utility.PopupManager;

public class DataHandler:MonoBehaviour {
    public static UnityEvent<MAPData> OnDataProcessedEvent = new UnityEvent<MAPData>();

    [SerializeField] private MediaManager mediaManager;
    private void OnEnable() {
        APIHandler.OnAPIDataFetchedEvent.AddListener(OnAPIDataFetched);
    }
    private void OnDisable() {
        APIHandler.OnAPIDataFetchedEvent.RemoveListener(OnAPIDataFetched);
    }
    private void OnAPIDataFetched(MAPData dataFromAPI) {
        PopupManager.Instance.ShowLoading();

        if (dataFromAPI != null) {
            DownloadAllMediaAndProceed(dataFromAPI,() => {
                OnDataProcessedEvent.Invoke(dataFromAPI);
                PopupManager.Instance.HideLoading();
            });
        }
    }

    private void DownloadAllMediaAndProceed(MAPData root,Action OnDonwnloadCompleted) {
        List<string> allDownloadUrl = new List<string>();
        root.CollectAllImagePaths(allDownloadUrl);
        mediaManager.AssignDownloadableUrl(allDownloadUrl);
        mediaManager.DownloadMediaFilesAsync(OnDonwnloadCompleted);
    }
}//DataHandler class end.
