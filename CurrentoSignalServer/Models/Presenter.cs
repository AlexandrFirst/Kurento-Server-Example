using Kurento.NET;

namespace CurrentoSignalServer.Models
{
    public class Presenter: User
    {
        public MediaPipeline MediaPipeline { get; set; }
        public WebRtcEndpoint WebRtcEndpoiont { get; set; }
    }
}
