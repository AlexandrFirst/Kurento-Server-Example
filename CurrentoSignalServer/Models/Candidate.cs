using Kurento.NET;

namespace CurrentoSignalServer.Models
{
    public class Candidate: User
    {
        public IceCandidate IceCandidate { get; set; }
    }
}
