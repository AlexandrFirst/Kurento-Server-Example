using Kurento.NET;
using System.Collections.Generic;

namespace CurrentoSignalServer.Models.RequestModels
{
    public class PresenterResponse
    {
        public bool IsSuccess { get; set; }
        public string SdpAnswer { get; set; }
        public List<string> Errors { get; set; }
        public WebRtcEndpoint Endpoint { get; set; }
    }
}
