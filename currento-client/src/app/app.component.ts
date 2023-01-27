import { AfterViewInit, Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { HubConnection } from '@microsoft/signalr';
import { IClientMessageBody } from './Models/clientMessageBody';
import { SignalServiceService } from './services/signal-service.service';

import * as kurentoUtils from 'kurento-utils';
import * as kurentoClient from 'kurento-client';
import { ClientMessage } from './Models/clientMessage';
import { MessageType } from './Models/messageType';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit, AfterViewInit {

  private isConnectionSetup = true;
  private webRtcPeer: kurentoUtils.WebRtcPeer | undefined;
  private hubConnection: HubConnection;

  private isCommunicationOn: boolean = false;

  @ViewChild('m_video', { static: false }) input: ElementRef;

  constructor(private signalService: SignalServiceService) { }

  ngAfterViewInit(): void {
    console.log("my video: ", this.input?.nativeElement)
  }

  ngOnInit(): void {
    this.signalService.setup().then(m => {
      console.log(m.message)
      this.isConnectionSetup = true;
      this.hubConnection = m.connection;
      this.messageHandlers(m.connection)
    })
  }

  private messageHandlers(hubConnection: HubConnection) {
    if (!hubConnection) {
      console.log("Unable to get hubconnection")
      return;
    }


    hubConnection.on("Send", (messageBody: IClientMessageBody) => {
      console.log(messageBody)
      const parsedMessage = JSON.parse(messageBody.body);
      switch (messageBody.id) {
        case 'presenterResponse':
          this.presenterResponse(parsedMessage)
          break;
        case 'viewerResponse':
          this.viewerResponse(parsedMessage);
          break;
        case 'stopCommunication':
          this.dispose();
          break;
        case 'iceCandidate':
          console.log('Ice candidate: ', parsedMessage);
          this.webRtcPeer?.addIceCandidate(parsedMessage)
          break;
        default:
          console.log('unrecognised message: ', parsedMessage);
      }
    });

  }

  public presenter() {
    if (!this.isConnectionSetup) {
      console.log("Connection is not set up")
      return;
    }

    var options = {
      localVideo: this.input.nativeElement,
      onicecandidate: (candidate: any) => { this.onIceCandidate(candidate) }
    }

    this.webRtcPeer = kurentoUtils.WebRtcPeer.WebRtcPeerSendonly(options, (error) => {
      if (error) {
        console.log("[presenter] WebRtcPeerSendonly: ", error);
        return;
      }
      this.webRtcPeer?.generateOffer((error: any | undefined, sdp: string) => { this.onOfferPresenter(error, sdp) });
    })
  }

  public viewer() {
    if (!this.isConnectionSetup) {
      console.log("Connection is not set up")
      return;
    }

    var options = {
      remoteVideo: this.input.nativeElement,
      onicecandidate: (candidate: any) => { this.onIceCandidate(candidate); }
    }

    this.webRtcPeer = kurentoUtils.WebRtcPeer.WebRtcPeerRecvonly(options, (error) => {
      if (error) {
        console.log("[viewer] WebRtcPeerSendonly: ", error);
        return;
      }
      this.webRtcPeer?.generateOffer((error: any | undefined, sdp: string) => { this.onOfferViewer(error, sdp) });
    })
  }

  public stop() {
    debugger;
    if (!this.isConnectionSetup || !this.isCommunicationOn) {
      console.log("Unable to stop not initialized connection");
      return;
    }

    var message: ClientMessage = {
      body: "Message to stop connecting"
    };

    this.sendMessage(MessageType.Stop, message)

    this.dispose();

  }

  public presenterResponse(parsedMessage: any) {
    console.log('Presenter response message: ', parsedMessage);
    if (parsedMessage.message !== 'accepted') {
      console.log(parsedMessage.errors);
      this.dispose();
      return;
    }
    else {
      this.webRtcPeer?.processAnswer(parsedMessage.sdpAnswer);
      this.isCommunicationOn = true;
    }
  }

  public viewerResponse(parsedMessage: any) {
    console.log('Viewer response message: ', parsedMessage);
    if (parsedMessage.message !== 'accepted') {
      console.log(parsedMessage.errors);
      this.dispose();
      return;
    }
    else {
      this.webRtcPeer?.processAnswer(parsedMessage.sdpAnswer);
      this.isCommunicationOn = true;
    }
  }

  private onOfferPresenter(error: any, offerSdp: any) {
    if (error) {
      console.log("onOfferPresenter: ", error);
      return;
    }

    console.log("Offer sdp: ", offerSdp);

    const data = {
      sdpOffer: offerSdp
    }
    var message: ClientMessage = {
      body: JSON.stringify(data)
    };

    this.sendMessage(MessageType.Presenter, message)
  }

  private onOfferViewer(error: any, offerSdp: any) {
    if (error) {
      console.log("onOfferViewer: ", error);
      return;
    }

    console.log("Offer sdp: ", offerSdp);

    const data = {
      sdpOffer: offerSdp
    }
    var message: ClientMessage = {
      body: JSON.stringify(data)
    };

    this.sendMessage(MessageType.Viewer, message)
  }

  private onIceCandidate(candidate: any) {
    console.log('local candidate: ', candidate);

    var message: ClientMessage = {
      body: JSON.stringify(candidate)
    };

    this.sendMessage(MessageType.onIceCandidate, message)
  }

  private sendMessage(messageType: MessageType, body: ClientMessage) {
    if (!this.isConnectionSetup) {
      console.log("unable to send message")
      return;
    }

    this.hubConnection.send("Message", messageType, body);
  }

  private dispose() {
    if (this.isConnectionSetup) {
      this.webRtcPeer?.dispose();
      this.isCommunicationOn = false;
      // this.hubConnection.stop().then(_ => {
      //   this.isConnectionSetup = false;
      // });
    }
  }

}
