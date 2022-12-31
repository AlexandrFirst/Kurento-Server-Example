using CurrentoSignalServer.Hub;
using CurrentoSignalServer.Models;
using CurrentoSignalServer.Models.RequestModels;
using Kurento.NET;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace CurrentoSignalServer.Services
{
    public class TranslationStateManager
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<TranslationStateManager> logger;
        private static KurentoClient kurentoClient;

        private static Presenter presenter { get; set; }
        private static List<Viewer> viewers { get; set; }
        private static ConcurrentDictionary<string, ConcurrentQueue<Candidate>> candidateQueue { get; set; }

        string ws_uri = "ws://localhost:8888/kurento";

        string file_uri = "file:///tmp/recorder_demo.webm";

        private Object _lock = new Object();

        private IHubContext<SignalHub, ISignalHubClient> signalHubInstance
        {
            get
            {
                return this.serviceProvider.GetRequiredService<IHubContext<SignalHub, ISignalHubClient>>();
            }
        }

        public TranslationStateManager(IServiceProvider serviceProvider, ILogger<TranslationStateManager> _logger)
        {
            viewers = new List<Viewer>();
            candidateQueue = new ConcurrentDictionary<string, ConcurrentQueue<Candidate>>();

            this.serviceProvider = serviceProvider;
            logger = _logger;
        }

        public async Task Stop(string sessionId)
        {
            logger.LogDebug($"Stopping user translation with connection id: {sessionId} ");

            if (presenter != null && presenter.SessionId == sessionId)
            {
                foreach (var i in viewers)
                {
                    await signalHubInstance.Clients.Client(i.SessionId).Send(new ClientMessageBody()
                    {
                        Id = "stopCommunication"
                    });
                }
                await presenter.MediaPipeline.ReleaseAsync();
                presenter = null;
                viewers.Clear();
            }
            else if (viewers.Any(x => x.SessionId == sessionId))
            {
                var viewer = viewers.First(x => x.SessionId == sessionId);
                await viewer.WebRtcEndpoint.ReleaseAsync();
                viewers.Remove(viewer);
            }

            clearCandidateQueue(sessionId);

            if (viewers.Count < 1 && presenter == null)
            {
                kurentoClient = null;
            }
        }

        public async Task<PresenterResponse> StartPresenter(string connectionId, string sdpOffer)
        {
            logger.LogDebug($"Starting presenter with connection id: {connectionId}");

            clearCandidateQueue(connectionId);

            if (presenter != null)
            {
                await Stop(connectionId);
                return new PresenterResponse()
                {
                    IsSuccess = false,
                    Errors = new List<string>() { "Another user is currently acting as presenter. Try again later ..." }
                };
            }

            if(!candidateQueue.ContainsKey(connectionId))
                candidateQueue.TryAdd(connectionId, new ConcurrentQueue<Candidate>());


            presenter = new Presenter()
            {
                SessionId = connectionId,
                MediaPipeline = null,
                WebRtcEndpoiont = null
            };

            kurentoClient = getKurentoClient();

            Task.Run(async() => 
            {
                while (true)
                {
                    var response = await kurentoClient.SendAsync("ping", new
                    {
                        interval = 1000
                    });
                    var res = response.Result.GetValue("value");
                    Console.WriteLine("Result of ping: " + res + " " + DateTime.Now);
                    Thread.Sleep(500);
                }
            });

            if (kurentoClient == null)
            {
                await Stop(connectionId);
                return new PresenterResponse() { IsSuccess = false, Errors = new List<string>() { "Error while creating kurento client" } };
            }

            var mediaPipeline = await kurentoClient.CreateAsync(new MediaPipeline());

            presenter.MediaPipeline = mediaPipeline;

            var webRtcEndPoint = await kurentoClient.CreateAsync(new WebRtcEndpoint(mediaPipeline, recvonly: false, sendonly: true, useDataChannels: false));
            presenter.WebRtcEndpoiont = webRtcEndPoint;

            await webRtcEndPoint.SetMinOutputBitrateAsync(30);
            await webRtcEndPoint.SetMaxOutputBitrateAsync(100);

            await webRtcEndPoint.SetMinVideoSendBandwidthAsync(30);
            await webRtcEndPoint.SetMaxVideoSendBandwidthAsync(100);


            while (candidateQueue[connectionId].TryDequeue(out var candidate))
            {
                await webRtcEndPoint.AddIceCandidateAsync(candidate.IceCandidate);
            }

            webRtcEndPoint.IceCandidateFound += (IceCandidateFoundEventArgs obj) =>
            {
                var cadidate = obj.candidate;
                signalHubInstance.Clients.Client(connectionId).Send(new ClientMessageBody()
                {
                    Id = "iceCandidate",
                    Body = JsonConvert.SerializeObject(cadidate)
                });
            };

            var sdpAnswer = await webRtcEndPoint.ProcessOfferAsync(sdpOffer);

            RecorderEndpoint recorderEndpoint = await kurentoClient.CreateAsync(new RecorderEndpoint(mediaPipeline, file_uri));
            await webRtcEndPoint.ConnectAsync(recorderEndpoint);

            recorderEndpoint.Recording += (RecordingEventArgs obj) =>
            {
                Console.WriteLine(obj.timestamp);
            };

           // await recorderEndpoint.RecordAsync();

            return new PresenterResponse() { IsSuccess = true, SdpAnswer = sdpAnswer, Endpoint = webRtcEndPoint };

        }

        public async Task<ViewerResponse> StartViewer(string connectionId, string sdpOffer)
        {
            clearCandidateQueue(connectionId);
            if (presenter == null)
            {
                await Stop(connectionId);
                return new ViewerResponse()
                {
                    IsSuccess = false,
                    Errors = new List<string>() { "No presenter yeat available..." }
                };
            }

            var webRtcEndPoint = await kurentoClient.CreateAsync(new WebRtcEndpoint(presenter.MediaPipeline, recvonly: true, sendonly:false, useDataChannels: false));

            await webRtcEndPoint.SetMinVideoRecvBandwidthAsync(30);
            await webRtcEndPoint.SetMaxVideoRecvBandwidthAsync(100);


            if (!candidateQueue.ContainsKey(connectionId))
                candidateQueue.TryAdd(connectionId, new ConcurrentQueue<Candidate>());

            var viewer = new Viewer()
            {
                SessionId = connectionId,
                WebRtcEndpoint = webRtcEndPoint,
            };

            viewers.Add(viewer);

            while (candidateQueue[connectionId].TryDequeue(out var candidate))
            {
                await webRtcEndPoint.AddIceCandidateAsync(candidate.IceCandidate);
            }

            webRtcEndPoint.IceCandidateFound += (IceCandidateFoundEventArgs obj) =>
            {
                var cadidate = obj.candidate;
                signalHubInstance.Clients.Client(connectionId).Send(new ClientMessageBody()
                {
                    Id = "iceCandidate",
                    Body = JsonConvert.SerializeObject(cadidate)
                });
            };

            webRtcEndPoint.DataChannelClose += (DataChannelCloseEventArgs obj) =>
            {
                logger.LogError($"Data channel close with client id: {connectionId} happend; {obj.ToString()} ");
            };

            webRtcEndPoint.ConnectionStateChanged += (ConnectionStateChangedEventArgs obj) =>
            {
                logger.LogError($"ConnectionStateChanged with client id: {connectionId} happend; {obj.ToString()} ");
            };


            webRtcEndPoint.Error += (ErrorEventArgs args) => {
                logger.LogError($"Error with client id: {connectionId} happend; {args.description} {args.errorCode} {args.type}");
            };

            webRtcEndPoint.MediaSessionTerminated += (MediaSessionTerminatedEventArgs args) => 
            {
                logger.LogError($"MediaSessionTerminated with client id: {connectionId} happend; {args.type}");
            };

            webRtcEndPoint.OnDataChannelClosed += (OnDataChannelClosedEventArgs args) => 
            {
                logger.LogError($"OnDataChannelClosed with client id: {connectionId} happend; {args.type}; channel id: {args.channelId}");
            };

            webRtcEndPoint.ElementDisconnected += (ElementDisconnectedEventArgs obj) =>
            {
                logger.LogError($"Disconnected with client id: {connectionId} happend; {obj.ToString()} ");
            };

            var sdpAnswer = await webRtcEndPoint.ProcessOfferAsync(sdpOffer);

            await presenter.WebRtcEndpoiont.ConnectAsync(webRtcEndPoint); 

            //await webRtcEndPoint.GatherCandidatesAsync();

            return new ViewerResponse() { IsSuccess = true, SdpAnswer = sdpAnswer, Endpoint = webRtcEndPoint };

        }


        public void OnIceCandidate(string connectionId, IceCandidate _candidate)
        {
            if (presenter != null &&
                presenter.SessionId == connectionId &&
                presenter.WebRtcEndpoiont != null)
            {
                logger.LogDebug("Sending presenter candidate");
                presenter.WebRtcEndpoiont.AddIceCandidateAsync(_candidate);
            }
            else if (viewers.Any(x => x.SessionId == connectionId) &&
                viewers.First(x => x.SessionId == connectionId).WebRtcEndpoint != null)
            {
                logger.LogDebug("Sending viewer candidate");
                var viewer = viewers.First(x => x.SessionId == connectionId);
                viewer.WebRtcEndpoint.AddIceCandidateAsync(_candidate);
            }
            else
            {
                logger.LogDebug("Queueing candidate");

                var connectionExists = candidateQueue.ContainsKey(connectionId);
                if (!connectionExists)
                    candidateQueue.TryAdd(connectionId, new ConcurrentQueue<Candidate>());

                candidateQueue[connectionId].Enqueue(new Candidate()
                {
                    IceCandidate = _candidate,
                    SessionId = connectionId,
                });
            }
        }

        private KurentoClient getKurentoClient()
        {
            if (kurentoClient != null)
            {
                return kurentoClient;
            }

            try
            {
                var _kurentoClient = new KurentoClient(ws_uri, logger) { };
                return _kurentoClient;
            }
            catch (Exception ex)
            {
                logger.LogError($"Could not find media server at address {ws_uri}; error: {ex.Message}");
                return null;
            }

        }

        private void clearCandidateQueue(string connectionId)
        {
            logger.LogDebug("Clearing candidates queue");
            candidateQueue.TryRemove(connectionId, out var value);
            //if (candidateQueue.Tr)
            //{
            //    candidateQueue.Clear();
            //}
        }

    }
}
