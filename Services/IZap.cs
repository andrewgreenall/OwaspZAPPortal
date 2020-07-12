namespace OwaspZAPPortal.Services
{
    public interface IZap
    {
        void StartZapUI();
        void ShutdownZAP();
        bool CheckIfZAPHasStarted(int minToWait = 1);
        string StartSpidering(string _target, string _maxChildren = "", string _recurse = "", string _contextName = "", string _subTreeOnly = "");
        void PollTheSpiderTillCompletion(string scanid);
        string StartActiveScanning(string _target, string _recurse = "", string _inScopeOnly = "", string _scanPolicyName = "",
            string _method = "", string _postData = "", string _contextId = "");
        void PollTheActiveScannerTillCompletion(string activeScanId);
        string StartAjaxSpidering(string _target, string _inScope = "", string _contextName = "", string _subTreeOnly = "");
        void PollTheAjaxSpiderTillCompletion();
        byte[] GetHTMLReport();
        byte[] GetXMLReport();
        byte[] GetJSONReport();
        ZapState GetState();
    }
}