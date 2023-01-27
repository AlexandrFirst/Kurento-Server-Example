import { Injectable } from '@angular/core';
import { HubConnection } from '@microsoft/signalr';
import { HubConnectionBuilder } from '@microsoft/signalr/dist/esm/HubConnectionBuilder';
import { LogLevel } from '@microsoft/signalr/dist/esm/ILogger';
import { HttpTransportType } from '@microsoft/signalr/dist/esm/ITransport';

@Injectable({
  providedIn: 'root'
})
export class SignalServiceService {

  private hubConnection: HubConnection | undefined;
  private isConnectionEstablished: boolean = false;

  constructor() {
    
  }

  public setup(): Promise<any> {
    return new Promise((res, rej) => {
      this.hubConnection = new HubConnectionBuilder()
        .configureLogging(LogLevel.Information)
        .withAutomaticReconnect()
        .withUrl("http://localhost:5000/signal", {
          transport: HttpTransportType.WebSockets,
          skipNegotiation: true
        })
        .build()

      this.hubConnection.start().then(_ => {
        console.log("Connection with server established")
        this.isConnectionEstablished = true;
      }).catch(err => {
        console.error("Error while establishing the connection")
      })

      this.hubConnection.onclose(e => {
        console.log("connection is closing", e)
      })

      this.hubConnection.onreconnecting(e => {
        console.log("connection is reconnectiong", e)
      })

      this.hubConnection.onreconnecting(e => {
        console.log("connection is reconnected", e)
      })

      res({
        message: "Setup of connextion done",
        connection: this.hubConnection
      })
    });
  }
}
