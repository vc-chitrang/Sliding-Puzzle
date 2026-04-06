using UnityEngine.Events;
using ViitorCloud.API;
using static API;

public class APICall:PersistentLazySingleton<APICall> {
    public Server serverType;
    public void RequestData(string form,UnityAction<MAPData> callbackOnSuccess,UnityAction<string> callbackOnFail) {
        ServerCommunication.Instance.SendRequestGet(form,
            callbackOnSuccess,callbackOnFail);
    }
    public void RequestLogin(string form,string url,UnityAction<LoginResponse> callbackOnSuccess,UnityAction<string> callbackOnFail) {
        ServerCommunication.Instance.SendRequestPost(form,url,
            callbackOnSuccess,callbackOnFail);
    }
}//APICall class end.
