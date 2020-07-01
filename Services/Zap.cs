using System;
using System.Diagnostics;
using System.Net;
using System.Threading;

namespace OwaspZAPPortal.Services
{
    public class ZAP
    {
        private readonly string filename;
        private readonly string workingDir;
        public readonly string urlHost;
        private readonly int portHost;

        public ZAP(string _filename, string workingDirectory, string host, int port)
        {
            filename = _filename;
            workingDir = workingDirectory;
            urlHost = host;
            portHost = port;
        }
        public void StartZapUI()
        {
            ProcessStartInfo zapProcess = new ProcessStartInfo();
            zapProcess.FileName = filename;
            zapProcess.WorkingDirectory = workingDir;

            Process zap = Process.Start(zapProcess);
        }



        public bool CheckIfZAPHasStarted(int minToWait = 1)
        {
            bool result = false;
            WebClient webClient = new WebClient();
            Stopwatch watch = new Stopwatch();
            watch.Start();
            int waitFor = minToWait * 60 * 1000;
            string zapUrlToDownload = $"http://{urlHost}:{portHost.ToString()}";

            while(waitFor > watch.ElapsedMilliseconds && result == false)
            {
                try
                {
                    string responseString = webClient.DownloadString(zapUrlToDownload);
                    result = true;
                }
                catch (WebException ex)
                {
                    //not started yet
                    Thread.Sleep(2000);
                }
            }

            //throw new Exception($"Unable to contact ZAP after {minToWait} mins");
            return result;
        }
    }
}