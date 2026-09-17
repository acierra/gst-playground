# gst-playground — Unity WebRTC receiver fork

This fork contains changes to the C# WebRTC client from `gst-playground` for use inside Unity.

## Main changes

The original project provides a GStreamer WebRTC test environment
with a browser-based JavaScript client.

This fork adds a C# receiving path for Unity integration.

### `csharp/MinimalWebRtcClient/`

C# console application used to test connection to the existing
GStreamer WebRTC infrastructure without Unity.

### `csharp/GstWebRtcReceiver.Core/`

Reusable C# WebRTC receiver extracted from the console prototype.

It is used by the Unity project to:

- connect to `rs-signalling`;
- perform WebRTC negotiation;
- receive the video stream;
- expose received video data to Unity.
## Current test architecture

```mermaid
flowchart LR
    GST["GStreamer<br/>webrtcsink"]
    SIG["rs-signalling"]

    subgraph UNITY["Unity application"]
        CORE["GstWebRtcReceiver.Core<br/>C# WebRTC receiver"]
    end

    GST -->|"WebRTC video stream"| CORE

    GST -. "signalling" .-> SIG
    CORE -. "signalling" .-> SIG
```

`rs-signalling` is used only to establish the WebRTC connection.

The video stream itself is transferred from the GStreamer WebRTC sender to `GstWebRtcReceiver.Core`.

## Main modified directories

### `csharp/GstWebRtcReceiver.Core/`

Reusable C# WebRTC receiver.

Responsibilities:

* connection to `rs-signalling`;
* SDP and ICE handling;
* WebRTC connection setup;
* RTP video reception;
* exposing received video data through C# events/callbacks.

### `csharp/MinimalWebRtcClient/`

Console smoke test for `GstWebRtcReceiver.Core`.

It allows the receiver to be tested independently from Unity.

## Relation to the Unity project

The Unity project references `GstWebRtcReceiver.Core` and uses it as its WebRTC receiver.

At the current stage, `gst-playground` is used as a test environment for the GStreamer/WebRTC side of the system.
