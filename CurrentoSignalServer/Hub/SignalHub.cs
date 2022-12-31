using CurrentoSignalServer.Models;
using CurrentoSignalServer.Models.RequestModels;
using CurrentoSignalServer.Services;
using Kurento.NET;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace CurrentoSignalServer.Hub
{
    public class SignalHub : Hub<ISignalHubClient>
    {
        private readonly TranslationStateManager translationStateManager;
        private readonly ILogger<SignalHub> signalRLogging;

        public SignalHub(TranslationStateManager translationStateManager, ILogger<SignalHub> signalRLogging)
        {
            this.translationStateManager = translationStateManager;
            this.signalRLogging = signalRLogging;
        }

        public async Task Error(ServerMessageBody messageBody)
        {
            signalRLogging.LogError($"Error: (session id: {Context.ConnectionId}) {messageBody.Body}");
            await translationStateManager.Stop(Context.ConnectionId);
        }

        public async Task Close()
        {
            signalRLogging.LogError($"Session id: {Context.ConnectionId} closed");
            await translationStateManager.Stop(Context.ConnectionId);
        }

        public async Task Message(MessageType messageType, ServerMessageBody messageBody)
        {
            switch (messageType)
            {
                case MessageType.Presenter:
                    var presentorRequest = messageBody.GetMessageBody<PresenterRequest>();

                    var presenterResponse = await translationStateManager.StartPresenter(Context.ConnectionId, presentorRequest.SdpOffer);
                    if (!presenterResponse.IsSuccess)
                    {

                        await Clients.Client(Context.ConnectionId).Send(new ClientMessageBody()
                        {
                            Id = "presenterResponse",
                            Body = JsonConvert.SerializeObject(new { message = "rejected", errors = presenterResponse.Errors })
                        });
                    }
                    else
                    {
                        await Clients.Client(Context.ConnectionId).Send(new ClientMessageBody()
                        {
                            Id = "presenterResponse",
                            Body = JsonConvert.SerializeObject(new { message = "accepted", sdpAnswer = presenterResponse.SdpAnswer })
                        });
                        await presenterResponse.Endpoint.GatherCandidatesAsync();
                    }
                    break;
                case MessageType.Viewer:
                    var viewerRequest = messageBody.GetMessageBody<ViewerRequest>();
                    var viewerResponse = await translationStateManager.StartViewer(Context.ConnectionId, viewerRequest.SdpOffer);

                    if (!viewerResponse.IsSuccess)
                    {

                        await Clients.Client(Context.ConnectionId).Send(new ClientMessageBody()
                        {
                            Id = "viewerResponse",
                            Body = JsonConvert.SerializeObject(new { message = "rejected", errors = viewerResponse.Errors })
                        });
                    }
                    else
                    {
                        await Clients.Client(Context.ConnectionId).Send(new ClientMessageBody()
                        {
                            Id = "viewerResponse",
                            Body = JsonConvert.SerializeObject(new { message = "accepted", sdpAnswer = viewerResponse.SdpAnswer })
                        });
                        await viewerResponse.Endpoint.GatherCandidatesAsync();
                    }
                    break;
                case MessageType.Stop:
                    await translationStateManager.Stop(Context.ConnectionId);
                    break;
                case MessageType.onIceCandidate:
                    var onIceCandidateRequest = messageBody.GetMessageBody<IceCandidate>();
                    translationStateManager.OnIceCandidate(Context.ConnectionId, onIceCandidateRequest);
                    break;
                default:
                    await Clients.Client(Context.ConnectionId).Send(new ClientMessageBody()
                    {
                        Id = "error",
                        Body = JsonConvert.SerializeObject(new { message = $"invalid message {messageBody.Body}" })
                    });
                    break;
            }
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var connectionId = Context.ConnectionId;
            signalRLogging.LogError($"{connectionId} is closing");
            await translationStateManager.Stop(connectionId);
        }
    }
}
