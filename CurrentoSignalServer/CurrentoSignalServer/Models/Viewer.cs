using Kurento.NET;

namespace CurrentoSignalServer.Models
{
    public class Viewer: User
    {
        public WebRtcEndpoint WebRtcEndpoint { get; set; }
    }
}
