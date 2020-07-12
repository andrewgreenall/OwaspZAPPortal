namespace OwaspZAPPortal.Models
{
    public class ZAPConfig
    {
        public string location { get; set; }
        public string workingDir { get; set; }
        public string daemonHost { get; set; }
        public int daemonPort { get; set; }
        public bool useDaemon { get; set; }
    }
}