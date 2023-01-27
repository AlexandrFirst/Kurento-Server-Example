
# Kurento Server Example 


This project provides an example of how to create a video conferencing application with a recording function using the Kurento Media Server.


## Deployment

1) Run Kurento Media Server using docker option

```bat
  docker run --rm ^
    -p 8888:8888/tcp ^
    -p 5000-5050:5000-5050/udp ^
    -e KMS_MIN_PORT=5000 ^
    -e KMS_MAX_PORT=5050 ^
    -e KMS_ICE_TCP=1 ^
    kurento/kurento-media-server:6.18.0
```
2) Run the signal server (web API application). SignalR is used for signalling
```
  cd '.\CurrentoSignalServer\CurrentoSignalServer\'
  dotnet run
```
3) Run the client application. It is written in Angular 15
```
  cd '.\currento-client\'
  npm i
  ng serve
```


## Helpful Links

 - [Kurento documentation](https://doc-kurento.readthedocs.io/en/latest/index.html)
 - [Kurento source code](https://github.com/Kurento/kurento-media-server)
 - [Kurento Community](https://groups.google.com/g/kurento)

 ## Nuget packages
The [Kurento.NET](https://www.nuget.org/packages/Kurento.NET/) API package is used to communicate with the Kurento Media Server.
