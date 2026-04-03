using System;
using System.IO;
using UnityEngine.Networking;
using System.Threading.Tasks;

public class DownloadUtility {
    public static async Task DownloadAssetAsync(string url,
        string path,
        Action<string> OnDownloadComplete = null,
        Action<string> OnDownloadFail = null,
        Action<float> progress = null) {
        string ext = GetFileNameWithoutSpace(url);
        string filePath = Path.Combine(path,ext);
        if (File.Exists(filePath)) {
            OnDownloadComplete?.Invoke(filePath);
            return;
        }

        using (UnityWebRequest request = UnityWebRequest.Get(url)) {
            UnityWebRequestAsyncOperation asyncOp = request.SendWebRequest();

            while (!asyncOp.isDone) {
                progress?.Invoke(asyncOp.progress);
                await Task.Delay(100);
            }

            if (request.result != UnityWebRequest.Result.Success) {
                OnDownloadFail?.Invoke(request.error);
            } else {
                string savePath = Path.Combine(path,GetFileNameWithoutSpace(url));
                File.WriteAllBytes(savePath,request.downloadHandler.data);
                OnDownloadComplete?.Invoke(savePath);
            }
        }
    }

    private static string GetFileNameWithoutSpace(string url) {
        return Path.GetFileName(url).Replace("%20"," ");
    }
}//DownloadUtility class end
