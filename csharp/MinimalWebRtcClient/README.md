# Minimal WebRTC C# Consumer

This is a minimal .NET 8 console client for the GStreamer WebRTC signalling server used by `webrtcsink`.

It does not use Unity and does not render video. The current smoke test verifies:

- WebSocket connection to `ws://localhost:8443`
- browser-compatible signalling flow
- SDP offer/answer exchange
- ICE candidate exchange
- remote video track negotiation
- ICE connected state
- 15-second post-ICE RTP observation before session shutdown
- first video RTP packet payload type, SSRC, and packet count logging
- non-zero exit if ICE connects but no video RTP arrives during the observation window

The client uses `SIPSorcery` for the WebRTC peer connection.

The signalling flow intentionally mirrors the GStreamer JS browser client from `gstwebrtc-api`: register as
`listener`, request the producer list, start a consumer session, answer the producer SDP offer, exchange ICE, and send
`endSession` when the session closes.

## Prerequisites

Keep the compose stack and minimal producer running:

```bash
docker compose ps
docker compose exec gst-env bash -lc 'ps -ef | grep "[g]st-launch-1.0"'
```

Expected producer:

```bash
gst-launch-1.0 videotestsrc pattern=18 ! webrtcsink signaller::uri=ws://localhost:8443 congestion-control=0
```

## Run

From the repository root:

```bash
dotnet run --project csharp/MinimalWebRtcClient/MinimalWebRtcClient.csproj -- --timeout 45
```

Optional signalling URI:

```bash
dotnet run --project csharp/MinimalWebRtcClient/MinimalWebRtcClient.csproj -- --signalling ws://localhost:8443 --timeout 45
```

Short smoke test:

```bash
dotnet run --project csharp/MinimalWebRtcClient/MinimalWebRtcClient.csproj -- --timeout 30 --observe 5
```

Debug every decoded frame only when needed:

```bash
dotnet run --project csharp/MinimalWebRtcClient/MinimalWebRtcClient.csproj -- --verbose-frames
```

Persistent listener mode, closest to the old browser client lifecycle:

```bash
dotnet run --project csharp/MinimalWebRtcClient/MinimalWebRtcClient.csproj -- --repeat
```

## Docker Compose

The default compose stack runs the C# listener instead of the old JS browser client:

```bash
docker compose up -d --build
docker compose logs -f csharp-client
```

Default WebRTC path:

```text
gst-producer -> rs-signalling -> csharp-client
```

The old JS client is still available explicitly:

```bash
docker compose --profile js up -d --build js-client
```

Exit codes:

- `0` - signalling, SDP, ICE, remote video track negotiation, and the post-ICE RTP observation window completed
- `1` - fatal client error
- `2` - timeout before negotiation/observation reached the expected state

## Browser To C# Mapping

The GStreamer browser client performs the flow below. The C# client mirrors it directly.

1. Server sends:

```json
{"type":"welcome","peerId":"..."}
```

2. Browser registers as listener. C# sends the same role and logs it as consumer/listener:

```json
{"type":"setPeerStatus","roles":["listener"],"meta":null}
```

3. Once the server confirms the listener role, C# requests producers:

```json
{"type":"list"}
```

4. C# selects the first listed producer and starts a consumer session:

```json
{"type":"startSession","peerId":"<producer-id>"}
```

5. Server returns:

```json
{"type":"sessionStarted","peerId":"<producer-id>","sessionId":"<session-id>"}
```

6. Producer sends an SDP offer through signalling:

```json
{"type":"peer","sessionId":"<session-id>","sdp":{"type":"offer","sdp":"..."}}
```

7. C# sets the remote description, creates an SDP answer, sets the local description, and sends:

```json
{"type":"peer","sessionId":"<session-id>","sdp":{"type":"answer","sdp":"..."}}
```

8. ICE candidates are exchanged as browser-compatible peer messages:

```json
{"type":"peer","sessionId":"<session-id>","ice":{"candidate":"candidate:...","sdpMLineIndex":0}}
```

## RTP Observation

The smoke test no longer exits immediately when the remote video track is negotiated and ICE reaches `connected`. It keeps the session alive for at least 15 seconds after ICE connected so `webrtcsink` has time to send media.

During that window the client logs:

- first video RTP packet arrival
- RTP payload type
- RTP SSRC
- packet count every 5 seconds and at 100-packet intervals

By default the client does not log every decoded video frame, because that makes smoke-test output hard to read. Use `--verbose-frames` when per-frame diagnostics are needed.

The client sends `endSession` only after the 15-second observation window completes or the explicit `--timeout` cancellation is reached.
