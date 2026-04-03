public class API {
    public static string APIDevelopmentBaseURL = "https://srcapi.cumulus.co.in/";
    public static string APIProductionBaseURL = "https://srcapi.cumulus.co.in/";

    public static string APILogin = APIBaseURL + "oauth/token";
    public static string APIGetAllData = APIBaseURL + "api/web_hook/v1/artwork?key=41204aed-89d7-4a45-9455-976ac475a8ab";

    public static string APIBaseURL {
        get {
            switch (APICall.Instance.serverType) {
                case Server.Live:
                return APIProductionBaseURL;
                case Server.Development:
                return APIDevelopmentBaseURL;
            }

            return APIProductionBaseURL;
        }
    }

    public enum Server {
        Live,
        Development,
    }
}
