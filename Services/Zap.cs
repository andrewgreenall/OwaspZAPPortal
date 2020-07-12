using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using OWASPZAPDotNetAPI;
using Microsoft.Extensions.Caching.Memory;
using OwaspZAPPortal.Models;

namespace OwaspZAPPortal.Services
{
    public enum ZapState { Stopped, Starting, Running, Stopping }
    public class Zap : IZap
    {
        private readonly ILogger<Zap> _logger;
        private readonly IMemoryCache _cache;
        private readonly string _filename;
        private readonly string _workingDir;
        public readonly string _urlHost;
        private readonly int _portHost;
        public ZapState _state = ZapState.Stopped;
        public ClientApi _api;
        public IApiResponse _apiResponse;
        private List<string> spideringList = new List<string>();
        public Zap(IConfiguration configuration, ILogger<Zap> logger, IMemoryCache memoryCache)
        {
            _filename = configuration.GetSection("ZAPConfig").GetValue<string>("location");
            _workingDir = configuration.GetSection("ZAPConfig").GetValue<string>("workingDir");
            _urlHost = configuration.GetSection("ZAPConfig").GetValue<string>("daemonHost");
            _portHost = configuration.GetSection("ZAPConfig").GetValue<int>("daemonPort");
            var apiKey = configuration.GetSection("ZAPConfig").GetValue<string>("apikey");
            if (!string.IsNullOrEmpty(_filename))
            {
                var useDaemon = configuration.GetSection("ZAPConfig").GetValue<bool>("useDaemon");
                if (useDaemon)
                    StartDaemon();
                else
                    StartZapUI();
            }
            _api = new ClientApi(_urlHost, _portHost, apiKey);
            _logger = logger;
            _cache = memoryCache;
        }
        public void StartZapUI()
        {
            ProcessStartInfo zapProcess = new ProcessStartInfo();
            zapProcess.FileName = _filename;
            zapProcess.WorkingDirectory = _workingDir;

            Process zap = Process.Start(zapProcess);
            _state = ZapState.Starting;
            if (!CheckIfZAPHasStarted(1))
            {
                _state = ZapState.Stopped;
                throw new Exception($"Unable to contact ZAP after 1 mins");
            }
            _state = ZapState.Running;
        }
        public void StartDaemon()
        {
            ProcessStartInfo zapProcess = new ProcessStartInfo();
            zapProcess.FileName = _filename;
            zapProcess.WorkingDirectory = _workingDir;
            zapProcess.Arguments = $"-daemon -host {_urlHost} -port {_portHost}";

            Process zap = Process.Start(zapProcess);
            _state = ZapState.Starting;
            if (!CheckIfZAPHasStarted(1))
            {
                _state = ZapState.Stopped;
                throw new Exception($"Unable to contact ZAP after 1 mins");
            }
            _state = ZapState.Running;
        }
        public void ShutdownZAP()
        {
            _apiResponse = _api.core.shutdown();
            if (((ApiResponseElement)_apiResponse).Value == "OK")
            {
                _state = ZapState.Stopped;
            }
        }

        public bool CheckIfZAPHasStarted(int minToWait = 1)
        {
            bool result = false;
            WebClient webClient = new WebClient();
            Stopwatch watch = new Stopwatch();
            watch.Start();
            int waitFor = minToWait * 60 * 1000;
            string zapUrlToDownload = $"http://{_urlHost}:{_portHost.ToString()}";

            while (waitFor > watch.ElapsedMilliseconds && result == false)
            {
                try
                {
                    Thread.Sleep(2000);
                    string responseString = webClient.DownloadString(zapUrlToDownload);
                    result = true;
                }
                catch (WebException ex)
                {
                    //not started yet
                    //_logger.LogError(ex.Message);
                    Thread.Sleep(2000);
                }
            }

            return result;
        }
        public string StartSpidering(string _target, string _maxChildren = "", string _recurse = "", string _contextName = "", string _subTreeOnly = "")
        {
            if (Uri.IsWellFormedUriString(_target, UriKind.Absolute))
            {
                if (spideringList.IndexOf(_target) > -1)
                    return "Scanning";
                _logger.LogInformation("Spider: " + _target);
                spideringList.Add(_target);
                _apiResponse = _api.spider.scan(_target, _maxChildren, _recurse, _contextName, _subTreeOnly);
                string scanid = ((ApiResponseElement)_apiResponse).Value;
                return scanid;
            }
            return "";
        }
        public void PollTheSpiderTillCompletion(string scanid)
        {
            int spiderProgress;
            while (true)
            {
                Thread.Sleep(1000);
                spiderProgress = int.Parse(((ApiResponseElement)_api.spider.status(scanid)).Value);
                _logger.LogInformation("Spider progress: {0}%", spiderProgress);
                if (spiderProgress >= 100)
                    break;
            }

            _logger.LogInformation("Spider complete");
            //Thread.Sleep(10000);
        }
        public string StartActiveScanning(string _target, string _recurse = "", string _inScopeOnly = "", string _scanPolicyName = "",
            string _method = "", string _postData = "", string _contextId = "")
        {
            String cachedTarget = "";
            bool justCreated = false;
            // cachedTarget = _cache.Get<string>(CacheObject.lastActiveScanUrl);
            if (!_cache.TryGetValue(CacheObject.lastActiveScanUrl, out cachedTarget))
            {
                // Key not in cache, so get data.
                cachedTarget = _target;

                // Set cache options.
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    // Keep in cache for this time, reset time if accessed.
                    .SetSlidingExpiration(TimeSpan.FromSeconds(30));

                // Save data in cache.
                _cache.Set(CacheObject.lastActiveScanUrl, cachedTarget, cacheEntryOptions);
                justCreated = true;
            }
            if (!justCreated && cachedTarget == _target)
                return "Running";

            _logger.LogInformation("Active Scanner: " + _target);
            _apiResponse = _api.ascan.scan(_target, _recurse, _inScopeOnly, _scanPolicyName, _method, _postData, _contextId);

            string activeScanId = ((ApiResponseElement)_apiResponse).Value;
            return activeScanId;
        }
        public void PollTheActiveScannerTillCompletion(string activeScanId)
        {
            int activeScannerprogress;
            while (true)
            {
                Thread.Sleep(5000);
                activeScannerprogress = int.Parse(((ApiResponseElement)_api.ascan.status(activeScanId)).Value);
                _logger.LogInformation("Active scanner progress: {0}%", activeScannerprogress);
                if (activeScannerprogress >= 100)
                    break;
            }
            _logger.LogInformation("Active scanner complete");
        }
        public string StartAjaxSpidering(string _target, string _inScope = "", string _contextName = "", string _subTreeOnly = "")
        {
            _logger.LogInformation("Ajax Spider: " + _target);
            _apiResponse = _api.ajaxspider.scan(_target, _inScope, _contextName, _subTreeOnly);

            if ("OK" == ((ApiResponseElement)_apiResponse).Value)
                _logger.LogInformation("Ajax Spider started for " + _target);
            return ((ApiResponseElement)_apiResponse).Value;
        }
        public void PollTheAjaxSpiderTillCompletion()
        {
            while (true)
            {
                Thread.Sleep(1000);
                string ajaxSpiderStatusText = string.Empty;
                ajaxSpiderStatusText = Convert.ToString(((ApiResponseElement)_api.ajaxspider.status()).Value);
                if (ajaxSpiderStatusText.IndexOf("running", StringComparison.InvariantCultureIgnoreCase) > -1)
                {
                    _logger.LogInformation("Ajax Spider running");
                }
                else
                    break;
            }

            _logger.LogInformation("Ajax Spider complete");
            Thread.Sleep(10000);
        }
        public byte[] GetHTMLReport()
        {
            return _api.core.htmlreport();
        }
        public byte[] GetXMLReport()
        {
            return _api.core.xmlreport();
        }
        public byte[] GetJSONReport()
        {
            return _api.core.jsonreport();
        }
        public List<Alert> GetAlerts(string _target)
        {
            return _api.GetAlerts(_target, 0, 0, string.Empty);
        }

        public ZapState GetState()
        {
            return _state;
        }
    }
}