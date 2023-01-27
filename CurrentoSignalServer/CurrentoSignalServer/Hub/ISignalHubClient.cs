using CurrentoSignalServer.Models;
using System.Threading.Tasks;

namespace CurrentoSignalServer.Hub
{
    public interface ISignalHubClient
    {
        public Task Send(ClientMessageBody clientMessageBody);
    }
}
