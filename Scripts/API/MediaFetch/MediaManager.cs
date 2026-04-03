using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using ViitorCloud.Utility.PopupManager;

public interface IContainMediaFile {
    public void CollectAllImagePaths(List<string> downloadableUrl);
}
public struct Extension {
    public const string mp4 = ".mp4";
    public const string jpg = ".jpg";
    public const string jpeg = ".jpeg";
    public const string png = ".png";
    public const string json = ".json";
}

[Serializable]
public class MediaList {
    public Dictionary<string,MediaFile> list;

    public MediaList() {
        list = new Dictionary<string,MediaFile>();
    }

    public MediaFile GetMedia(string fileName) {
        MediaFile media = null;
        list.TryGetValue(fileName,out media);
        return media;
    }
}

[Serializable]
public class MediaFile {
    public string fileName;
    public int id;
    public string fileType;
    public string url;

    public bool IsVideoFile() {
        return fileType == Extension.mp4;
    }

    public bool IsImageFile() {
        return fileType == Extension.jpg;
    }
} //MediaInformation class end

[System.Serializable]
public class ImagesListData {
    public List<string> listOfImages;
}

public class MediaManager:MonoBehaviour {
    private List<Task> downloadTasks = new List<Task>();
    public MediaList _mediaList = new MediaList();
    [SerializeField] private List<string> assetURLList;
    private Dictionary<string,Sprite> _spriteCache = new Dictionary<string,Sprite>();

    internal void AssignDownloadableUrl(List<string> downloadUrl) {
        assetURLList.Clear();
        downloadUrl = downloadUrl.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        assetURLList.AddRange(downloadUrl);
    }

    public async void DownloadMediaFilesAsync(Action onDownloadCompleted = null) {
        if (assetURLList == null || assetURLList.Count == 0) {
            onDownloadCompleted?.Invoke();
            return;
        }

        downloadTasks.Clear();
        _mediaList.list.Clear();

        for (int i = 0;i < assetURLList.Count;i++) {
            downloadTasks.Add(DownloadUtility.DownloadAssetAsync(assetURLList[i],
                GetDirectoryPath(),
                OnDownloadComplete,
                OnDownloadFail,
                DownloadingProgress));
        }

        await Task.WhenAll(downloadTasks);
        for (int i = 0;i < assetURLList.Count;i++) {
            DownloadSingleMediaFileAsync(assetURLList[i],null);
        }

        AllMediaDownloaded();
        onDownloadCompleted?.Invoke();
    }

    private string GetDirectoryPath() {
        string path = Path.Combine(Application.persistentDataPath,"MediaFiles");

        if (!Directory.Exists(path)) {
            Directory.CreateDirectory(path);
        }

        return path;
    }

    private void OnDownloadComplete(string filePath) {
        MediaFile media = new MediaFile() {
            fileName = Path.GetFileName(filePath),
            fileType = Path.GetExtension(filePath),
            url = filePath,
        };

        _mediaList.list[media.fileName] = media;
    }

    private void OnDownloadFail(string errorMessage) {
        Debug.LogError($"Error OnDownloadFail: {errorMessage}");
        PopupManager.Instance.HideLoading();
    }
    private void DownloadingProgress(float progress) {
        ShowLoadingOnUI(_mediaList.list.Count,assetURLList.Count,progress);
    }
    private void AllMediaDownloaded() {
        //Debug.Log("AllMediaDownloaded: Loading: 100%");
        ShowLoadingOnUI(assetURLList.Count,assetURLList.Count,1f);
    }
    public async void DownloadSingleMediaFileAsync(string assetURL,Action<Sprite> onDownloadCompleted = null) {
        if (string.IsNullOrEmpty(assetURL)) {
            Debug.LogError("Asset URL is null or empty");
            onDownloadCompleted?.Invoke(null);
            return;
        }

        if (_spriteCache.ContainsKey(assetURL)) {
            onDownloadCompleted?.Invoke(_spriteCache[assetURL]);
            return;
        }

        // 2. Prepare Lists
        downloadTasks.Clear();
        //_mediaList.list.Clear();

        // 3. Start Download Task using the provided DownloadUtility
        Task downloadTask = DownloadUtility.DownloadAssetAsync(
            assetURL,
            GetDirectoryPath(),
            OnDownloadComplete, // This populates _mediaList
            OnDownloadFail,
            DownloadingProgress
        );
        downloadTasks.Add(downloadTask);

        // 4. Await Completion
        await Task.WhenAll(downloadTasks);

        // 5. Cleanup UI
        AllMediaDownloaded();

        // 6. Load Sprite from Disk
        Sprite resultSprite = null;

        // Replicate DownloadUtility's naming logic to find the file key
        string fileName = Path.GetFileName(assetURL).Replace("%20"," ");

        if (_mediaList.list.ContainsKey(fileName)) {
            string filePath = _mediaList.list[fileName].url;

            if (File.Exists(filePath)) {
                // Read bytes and create texture
                byte[] fileData = File.ReadAllBytes(filePath);
                Texture2D texture = new Texture2D(2,2);

                // LoadImage auto-resizes the texture dimensions
                if (texture.LoadImage(fileData)) {
                    resultSprite = Sprite.Create(
                        texture,
                        new Rect(0,0,texture.width,texture.height),
                        new Vector2(0.5f,0.5f)
                    );
                }
            } else {
                Debug.LogError($"File not found at path: {filePath}");
            }
        } else {
            Debug.LogError($"File {fileName} not found in _mediaList after download.");
            PopupManager.Instance.HideLoading();
        }

        _spriteCache[assetURL] = resultSprite;
        // 7. Invoke Callback
        onDownloadCompleted?.Invoke(resultSprite);
    }
    internal void ClearLocalStoredData() {
        // 1. Clear the in-memory dictionary references
        _mediaList.list.Clear();

        // 2. Get the path where images are stored
        string path = GetDirectoryPath();

        // 3. Check if directory exists and delete it
        if (Directory.Exists(path)) {
            try {
                // The 'true' parameter performs a recursive delete 
                // (removes the folder AND all files inside it)
                Directory.Delete(path,true);
                Debug.Log($"<color=red>Successfully cleared local data at:</color> {path}");
            } catch (Exception e) {
                Debug.LogError($"Failed to clear local data: {e.Message}");
            }
        } else {
            Debug.LogWarning("ClearLocalStoredData: Directory did not exist, nothing to delete.");
        }
    }
    internal void LoadSprite(string url,Action<Sprite> onLoaded) {
        Debug.Log($"LoadSprite called with URL: {url}");
        DownloadSingleMediaFileAsync(url,onLoaded);
    }

    #region VIDEO
    public async void DownloadSingleVideoFileAsync(string assetURL,Action<string> onDownloadCompleted = null) {
        if (string.IsNullOrEmpty(assetURL)) {
            Debug.LogError("Video Asset URL is null or empty");
            onDownloadCompleted?.Invoke(null);
            return;
        }
        string localFilePath = string.Empty;
        string fileName = Path.GetFileName(assetURL).Replace("%20"," ");

        if (_mediaList.list.ContainsKey(fileName)) {
            localFilePath = _mediaList.list[fileName].url;

            // Verify physical existence
            if (string.IsNullOrEmpty(localFilePath)) {
                if (File.Exists(localFilePath)) {
                    onDownloadCompleted?.Invoke(localFilePath);
                } else {
                    Debug.LogError($"Video file record exists but file is missing at: {localFilePath}");
                    localFilePath = string.Empty;
                }
                return;
            }
        }
        // 1. Prepare
        downloadTasks.Clear();

        // 2. Start Download Task 
        // Uses existing logic to save to Application.persistentDataPath/Images
        Task downloadTask = DownloadUtility.DownloadAssetAsync(
            assetURL,
            GetDirectoryPath(),
            OnDownloadComplete,
            OnDownloadFail,
            DownloadingProgress
        );
        downloadTasks.Add(downloadTask);

        // 3. Await Completion
        await Task.WhenAll(downloadTasks);

        // 4. Cleanup UI (Hide loading via PopupManager)
        AllMediaDownloaded();

        // 5. Retrieve Local Path
        fileName = Path.GetFileName(assetURL).Replace("%20"," ");

        if (_mediaList.list.ContainsKey(fileName)) {
            localFilePath = _mediaList.list[fileName].url;

            // Verify physical existence
            if (!File.Exists(localFilePath)) {
                Debug.LogError($"Video file record exists but file is missing at: {localFilePath}");
                localFilePath = null;
            }
        } else {
            Debug.LogError($"Video File {fileName} not found in _mediaList after download.");
        }

        // 6. Return the local path
        onDownloadCompleted?.Invoke(localFilePath);
    }
    #endregion
    private void ShowLoadingOnUI(int downloadedCount,int totalCount,float progress) {
        string progressText = $"Loading...\n{downloadedCount}/{totalCount} ({progress:P2})";
        PopupManager.Instance.SetProgressText(progressText);
    }
} //MediaManager class end.