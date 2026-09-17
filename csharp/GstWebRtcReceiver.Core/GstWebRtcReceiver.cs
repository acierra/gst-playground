using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;

namespace GstWebRtcReceiver.Core;

public sealed class GstWebRtcReceiver : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan PacketCountLogInterval = TimeSpan.FromSeconds(5);

    private readonly GstWebRtcReceiverConfig _config;
    private readonly ClientWebSocket _webSocket = new();
    private readonly TaskCompletionSource<bool> _negotiated = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<RTCIceCandidateInit> _pendingRemoteIce = [];
    private readonly List<string> _pendingLocalIceJson = [];

    private RTCPeerConnection? _peerConnection;
    private string? _clientPeerId;
    private string? _producerPeerId;
    private string? _sessionId;
    private bool _isReady;
    private bool _sessionRequested;
    private bool _remoteDescriptionSet;
    private bool _answerSent;
    private bool _remoteVideoTrackSeen;
    private bool _iceConnected;
    private bool _connectedRaised;
    private DateTimeOffset? _iceConnectedAt;
    private int _videoPacketCount;

    public GstWebRtcReceiver(GstWebRtcReceiverConfig config)
    {
        _config = config;
    }

    public event Action<ReceiverLogMessage>? OnLog;
    public event Action? OnConnected;
    public event Action<RTCIceConnectionState>? OnIceStateChanged;
    public event Action<DtlsStateChangedEvent>? OnDtlsStateChanged;
    public event Action<RemoteTrackNegotiatedEvent>? OnRemoteTrackNegotiated;
    public event Action<RtpPacketReceivedEvent>? OnRtpPacket;
    public event Action<SessionEndedEvent>? OnSessionEnded;
    public event Action<Exception>? OnError;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            Log("config", $"Signalling={_config.SignallingUri}; observe={_config.ObserveRtp.TotalSeconds:N0}s; verboseFrames={_config.VerboseFrames}.");
            Log("ws.connect", $"Connecting to {_config.SignallingUri}.");
            await _webSocket.ConnectAsync(_config.SignallingUri, cancellationToken);
            Log("ws.connected", "Connected to signalling server.");

            var receiveTask = ReceiveLoopAsync(cancellationToken);
            var completed = await Task.WhenAny(_negotiated.Task, receiveTask);

            if (completed == receiveTask)
            {
                await receiveTask;
                throw new WebSocketException("Signalling loop ended before negotiation reached remote video track + ICE connected.");
            }

            Log("webrtc.ready", $"Remote video track negotiated and ICE connected; video RTP packets observed so far: {_videoPacketCount}.");
            await ObserveRtpAsync(receiveTask, cancellationToken);
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
            throw;
        }
    }

    private async Task ObserveRtpAsync(Task receiveTask, CancellationToken cancellationToken)
    {
        var iceConnectedAt = _iceConnectedAt ?? DateTimeOffset.Now;
        var observationEndsAt = iceConnectedAt + _config.ObserveRtp;
        var lastLogAt = DateTimeOffset.Now;

        Log("rtp.observe", $"Keeping session alive until {observationEndsAt:HH:mm:ss.fff} to observe video RTP.");

        while (DateTimeOffset.Now < observationEndsAt)
        {
            var now = DateTimeOffset.Now;
            var nextLogAt = lastLogAt + PacketCountLogInterval;
            var nextWakeAt = nextLogAt < observationEndsAt ? nextLogAt : observationEndsAt;
            var delay = nextWakeAt - now;

            if (delay <= TimeSpan.Zero)
            {
                LogVideoPacketCount(iceConnectedAt);
                lastLogAt = now;
                continue;
            }

            var delayTask = Task.Delay(delay, cancellationToken);
            var completed = await Task.WhenAny(delayTask, receiveTask);

            if (completed == receiveTask)
            {
                await receiveTask;
                throw new WebSocketException("Signalling loop ended while observing video RTP.");
            }

            await delayTask;
            LogVideoPacketCount(iceConnectedAt);
            lastLogAt = DateTimeOffset.Now;
        }

        Log("rtp.observe", $"Observation complete after {_config.ObserveRtp.TotalSeconds:N0}s post-ICE; video RTP packets observed: {_videoPacketCount}.");
        if (_videoPacketCount == 0)
        {
            throw new InvalidOperationException("WebRTC reached ICE connected, but no video RTP packets arrived during the observation window.");
        }
    }

    private void LogVideoPacketCount(DateTimeOffset iceConnectedAt)
    {
        var elapsed = DateTimeOffset.Now - iceConnectedAt;
        Log("rtp.video", $"Post-ICE {elapsed.TotalSeconds:N1}s packet count: {_videoPacketCount}.");
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 1024];

        while (!cancellationToken.IsCancellationRequested &&
               _webSocket.State == WebSocketState.Open)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult result;

            do
            {
                result = await _webSocket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Log("ws.closed", $"Server closed the socket: {_webSocket.CloseStatus} {_webSocket.CloseStatusDescription}");
                    return;
                }

                message.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            var json = Encoding.UTF8.GetString(message.ToArray());
            Log("signal.recv", json);

            using var document = JsonDocument.Parse(json);
            await HandleMessageAsync(document.RootElement, cancellationToken);
        }
    }

    private async Task HandleMessageAsync(JsonElement msg, CancellationToken cancellationToken)
    {
        var type = msg.GetOptionalString("type");

        switch (type)
        {
            case "welcome":
                _clientPeerId = msg.GetOptionalString("peerId");
                Log("signal.welcome", $"Client peer id: {_clientPeerId}.");
                await SendAsync(new
                {
                    type = "setPeerStatus",
                    roles = new[] { "listener" },
                    meta = (object?)null
                }, cancellationToken);
                Log("signal.register", "Registered as consumer/listener.");
                break;

            case "peerStatusChanged":
                await HandlePeerStatusChangedAsync(msg, cancellationToken);
                break;

            case "list":
                HandleProducerList(msg);
                await TryStartSessionAsync(cancellationToken);
                break;

            case "sessionStarted":
                _sessionId = msg.GetOptionalString("sessionId");
                Log("session.started", $"Producer peer id: {msg.GetOptionalString("peerId")}; session id: {_sessionId}.");
                break;

            case "peer":
                await HandlePeerMessageAsync(msg, cancellationToken);
                break;

            case "error":
                throw new InvalidOperationException($"Signalling server error: {msg.GetOptionalString("details")}");

            case "endSession":
                var remoteSessionId = msg.GetOptionalString("sessionId");
                Log("session.ended", $"Session ended by peer: {remoteSessionId}.");
                OnSessionEnded?.Invoke(new SessionEndedEvent(remoteSessionId, true, "peer endSession"));
                break;

            default:
                Log("signal.unknown", $"Ignoring message type '{type}'.");
                break;
        }
    }

    private async Task HandlePeerStatusChangedAsync(JsonElement msg, CancellationToken cancellationToken)
    {
        var peerId = msg.GetOptionalString("peerId");
        var roles = msg.TryGetProperty("roles", out var rolesElement)
            ? rolesElement.EnumerateArray().Select(role => role.GetString()).Where(role => role != null).ToHashSet()
            : [];

        if (peerId == _clientPeerId)
        {
            if (!_isReady && roles.Contains("listener"))
            {
                _isReady = true;
                Log("signal.ready", "Listener role confirmed; requesting producer list.");
                await SendAsync(new { type = "list" }, cancellationToken);
            }

            return;
        }

        if (roles.Contains("producer"))
        {
            _producerPeerId ??= peerId;
            Log("producer.added", $"Producer available: {_producerPeerId}.");
            await TryStartSessionAsync(cancellationToken);
        }
    }

    private void HandleProducerList(JsonElement msg)
    {
        if (!msg.TryGetProperty("producers", out var producers) || producers.ValueKind != JsonValueKind.Array)
        {
            Log("producer.list", "No producers array in list response.");
            return;
        }

        foreach (var producer in producers.EnumerateArray())
        {
            var id = producer.GetOptionalString("id") ?? producer.GetOptionalString("peerId");
            if (!string.IsNullOrWhiteSpace(id) && id != _clientPeerId)
            {
                _producerPeerId ??= id;
                Log("producer.list", $"Selected producer: {_producerPeerId}.");
                return;
            }
        }

        Log("producer.list", "No remote producer currently listed; waiting for peerStatusChanged.");
    }

    private async Task TryStartSessionAsync(CancellationToken cancellationToken)
    {
        if (!_isReady || _sessionRequested || string.IsNullOrWhiteSpace(_producerPeerId))
        {
            return;
        }

        _sessionRequested = true;
        await SendAsync(new
        {
            type = "startSession",
            peerId = _producerPeerId
        }, cancellationToken);
        Log("session.start", $"Requested consumer session with producer {_producerPeerId}.");
    }

    private async Task HandlePeerMessageAsync(JsonElement msg, CancellationToken cancellationToken)
    {
        if (_sessionId == null)
        {
            _sessionId = msg.GetOptionalString("sessionId");
        }

        EnsurePeerConnection(cancellationToken);

        if (msg.TryGetProperty("sdp", out var sdp))
        {
            await HandleRemoteSdpAsync(sdp, cancellationToken);
        }
        else if (msg.TryGetProperty("ice", out var ice))
        {
            HandleRemoteIce(ice);
        }
        else
        {
            Log("peer.empty", "Peer message had neither sdp nor ice.");
        }
    }

    private void EnsurePeerConnection(CancellationToken cancellationToken)
    {
        if (_peerConnection != null)
        {
            return;
        }

        _peerConnection = new RTCPeerConnection(new RTCConfiguration
        {
            X_UseRsaForDtlsCertificate = true
        });
        Log("dtls.config", "Using RSA DTLS certificate to test OpenSSL cipher-suite compatibility.");
        Log("dtls.local", $"Local DTLS fingerprint: {_peerConnection.DtlsCertificateFingerprint}; signature algorithm: {_peerConnection.DtlsCertificateSignatureAlgorithm}.");
        _peerConnection.addTrack(new MediaStreamTrack(
            new List<VideoFormat>
            {
                new(VideoCodecsEnum.VP8, 96, 90000, null),
                new(VideoCodecsEnum.H264, 97, 90000, "packetization-mode=1;profile-level-id=42c015;level-asymmetry-allowed=1")
            },
            MediaStreamStatusEnum.RecvOnly));

        _peerConnection.onicecandidate += candidate =>
        {
            if (candidate == null || string.IsNullOrWhiteSpace(_sessionId))
            {
                return;
            }

            Log("ice.local", candidate.candidate);
            var iceJson = candidate.toJSON();
            if (!_answerSent)
            {
                _pendingLocalIceJson.Add(iceJson);
                Log("ice.local", "Queued local ICE until SDP answer is sent.");
                return;
            }

            _ = SendRawAsync($$"""{"type":"peer","sessionId":"{{_sessionId}}","ice":{{iceJson}}}""", cancellationToken);
        };

        _peerConnection.onicecandidateerror += (candidate, error) =>
            Log("ice.error", $"{candidate?.candidate}: {error}");

        _peerConnection.onicegatheringstatechange += state =>
            Log("ice.gathering", state.ToString());

        _peerConnection.oniceconnectionstatechange += state =>
        {
            Log("ice.connection", state.ToString());
            OnIceStateChanged?.Invoke(state);
            if (state == RTCIceConnectionState.connected)
            {
                _iceConnected = true;
                _iceConnectedAt ??= DateTimeOffset.Now;
                CompleteIfNegotiated();
            }
        };

        _peerConnection.onconnectionstatechange += state =>
        {
            Log("pc.state", state.ToString());
            var dtlsState = new DtlsStateChangedEvent(
                state,
                _peerConnection.IsDtlsNegotiationComplete,
                IsSrtpActive("SrtpEncoder"),
                IsSrtpActive("SrtpDecoder"));
            Log("dtls.state", $"Negotiation complete: {dtlsState.IsNegotiationComplete}; SRTP encoder active: {dtlsState.IsSrtpEncoderActive}; SRTP decoder active: {dtlsState.IsSrtpDecoderActive}.");
            OnDtlsStateChanged?.Invoke(dtlsState);
        };

        _peerConnection.OnVideoFormatsNegotiated += formats =>
            Log("track.video", "Negotiated remote video formats: " + string.Join(", ", formats.Select(format => format.Codec)));

        if (_config.VerboseFrames)
        {
            _peerConnection.OnVideoFrameReceived += (remote, timestamp, frame, format) =>
                Log("video.frame", $"{frame.Length} bytes from {remote}, ts={timestamp}, codec={format.Codec}.");
        }

        _peerConnection.OnRtpPacketReceived += (remote, media, packet) =>
        {
            if (media != SDPMediaTypesEnum.video)
            {
                return;
            }

            var count = Interlocked.Increment(ref _videoPacketCount);
            if (count == 1)
            {
                Log("rtp.video", $"First video RTP packet from {remote}; payload type={packet.Header.PayloadType}; SSRC={packet.Header.SyncSource}.");
                Log("srtp.ready", "First RTP packet received after DTLS/SRTP setup.");
            }
            else if (count % 100 == 0)
            {
                Log("rtp.video", $"Received {count} video RTP packets; latest payload type={packet.Header.PayloadType}; SSRC={packet.Header.SyncSource}.");
            }

            OnRtpPacket?.Invoke(new RtpPacketReceivedEvent(remote?.ToString(), media, packet, count));
        };

        Log("pc.create", "Created RTCPeerConnection.");
        Log("track.local", "Added recv-only video capabilities: VP8, H264.");
    }

    private async Task HandleRemoteSdpAsync(JsonElement sdp, CancellationToken cancellationToken)
    {
        var typeText = sdp.GetRequiredString("type");
        var sdpText = sdp.GetRequiredString("sdp");
        var type = Enum.Parse<RTCSdpType>(typeText, ignoreCase: true);

        Log("sdp.remote", $"Received {typeText} with {sdpText.Length} chars.");

        var result = _peerConnection!.setRemoteDescription(new RTCSessionDescriptionInit
        {
            type = type,
            sdp = sdpText
        });

        Log("sdp.remote", $"setRemoteDescription result: {result}.");
        _remoteDescriptionSet = true;

        if (_peerConnection.VideoRemoteTrack != null)
        {
            Log("track.video", $"Remote video track present; SSRC={_peerConnection.VideoRemoteTrack.Ssrc}.");
            Log("dtls.remote", $"Remote DTLS fingerprint: {_peerConnection.RemotePeerDtlsFingerprint?.algorithm} {_peerConnection.RemotePeerDtlsFingerprint?.value}.");
            _remoteVideoTrackSeen = true;
            OnRemoteTrackNegotiated?.Invoke(new RemoteTrackNegotiatedEvent(
                _peerConnection.VideoRemoteTrack.Ssrc,
                _peerConnection.RemotePeerDtlsFingerprint?.algorithm,
                _peerConnection.RemotePeerDtlsFingerprint?.value));
            CompleteIfNegotiated();
        }

        foreach (var candidate in _pendingRemoteIce)
        {
            _peerConnection.addIceCandidate(candidate);
            Log("ice.remote", $"Applied queued remote ICE: {candidate.candidate}");
        }
        _pendingRemoteIce.Clear();

        var answer = _peerConnection.createAnswer(null);
        answer.sdp = answer.sdp.Replace("UDP/TLS/RTP/SAVP ", "UDP/TLS/RTP/SAVPF ", StringComparison.Ordinal);
        answer.sdp = answer.sdp.Replace("a=rtcp-fb:96 transport-cc\r\n", "a=rtcp-fb:96 nack\r\na=rtcp-fb:96 nack pli\r\na=rtcp-fb:96 ccm fir\r\na=rtcp-fb:96 transport-cc\r\n", StringComparison.Ordinal);
        answer.sdp = answer.sdp.Replace("a=rtcp-fb:97 transport-cc\r\n", "a=rtcp-fb:97 nack\r\na=rtcp-fb:97 nack pli\r\na=rtcp-fb:97 ccm fir\r\na=rtcp-fb:97 transport-cc\r\n", StringComparison.Ordinal);
        answer.sdp = answer.sdp.Replace("a=rtcp-mux\r\n", "a=rtcp-mux\r\na=rtcp-mux-only\r\na=rtcp-rsize\r\n", StringComparison.Ordinal);
        answer.sdp = answer.sdp.Replace("a=end-of-candidates\r\n", "", StringComparison.Ordinal);
        Log("sdp.local", $"Created answer with {answer.sdp.Length} chars.");

        await _peerConnection.setLocalDescription(answer);
        Log("sdp.local", "setLocalDescription completed.");

        await SendRawAsync($$"""{"type":"peer","sessionId":"{{_sessionId}}","sdp":{{answer.toJSON()}}}""", cancellationToken);
        _answerSent = true;
        Log("sdp.answer", "Sent SDP answer to producer.");

        foreach (var iceJson in _pendingLocalIceJson)
        {
            await SendRawAsync($$"""{"type":"peer","sessionId":"{{_sessionId}}","ice":{{iceJson}}}""", cancellationToken);
            Log("ice.local", "Sent queued local ICE.");
        }
        _pendingLocalIceJson.Clear();
    }

    private void HandleRemoteIce(JsonElement ice)
    {
        var candidate = new RTCIceCandidateInit
        {
            candidate = ice.GetRequiredString("candidate"),
            sdpMid = ice.GetOptionalString("sdpMid"),
            usernameFragment = ice.GetOptionalString("usernameFragment")
        };

        if (string.IsNullOrWhiteSpace(candidate.candidate))
        {
            Log("ice.remote", "Ignoring empty end-of-candidates marker.");
            return;
        }

        if (ice.TryGetProperty("sdpMLineIndex", out var index) && index.ValueKind == JsonValueKind.Number)
        {
            candidate.sdpMLineIndex = index.GetUInt16();
        }

        if (!_remoteDescriptionSet)
        {
            _pendingRemoteIce.Add(candidate);
            Log("ice.remote", $"Queued remote ICE until SDP is set: {candidate.candidate}");
            return;
        }

        _peerConnection!.addIceCandidate(candidate);
        Log("ice.remote", $"Added remote ICE: {candidate.candidate}");
    }

    private void CompleteIfNegotiated()
    {
        if (_remoteVideoTrackSeen && _iceConnected)
        {
            if (!_connectedRaised)
            {
                _connectedRaised = true;
                OnConnected?.Invoke();
            }

            _negotiated.TrySetResult(true);
        }
    }

    private bool IsSrtpActive(string propertyName)
    {
        var dtlsHandle = typeof(RTCPeerConnection).GetField("_dtlsHandle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_peerConnection);
        return dtlsHandle?.GetType().GetProperty(propertyName)?.GetValue(dtlsHandle) != null;
    }

    private Task SendAsync<T>(T payload, CancellationToken cancellationToken) =>
        SendRawAsync(JsonSerializer.Serialize(payload, JsonOptions), cancellationToken);

    private async Task SendRawAsync(string json, CancellationToken cancellationToken)
    {
        Log("signal.send", json);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    private void Log(string step, string message) =>
        OnLog?.Invoke(new ReceiverLogMessage(DateTimeOffset.Now, step, message));

    public void Dispose()
    {
        if (!string.IsNullOrWhiteSpace(_sessionId) && _webSocket.State == WebSocketState.Open)
        {
            try
            {
                SendRawAsync($$"""{"type":"endSession","sessionId":"{{_sessionId}}"}""", CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                OnSessionEnded?.Invoke(new SessionEndedEvent(_sessionId, false, "local dispose"));
            }
            catch
            {
                // Best-effort shutdown only.
            }
        }

        _peerConnection?.close();
        _peerConnection?.Dispose();
        _webSocket.Dispose();
    }
}

public sealed record GstWebRtcReceiverConfig(
    Uri SignallingUri,
    TimeSpan ObserveRtp,
    bool VerboseFrames = false);

public sealed record ReceiverLogMessage(
    DateTimeOffset Timestamp,
    string Step,
    string Message);

public sealed record DtlsStateChangedEvent(
    RTCPeerConnectionState PeerConnectionState,
    bool IsNegotiationComplete,
    bool IsSrtpEncoderActive,
    bool IsSrtpDecoderActive);

public sealed record RemoteTrackNegotiatedEvent(
    uint Ssrc,
    string? RemoteDtlsFingerprintAlgorithm,
    string? RemoteDtlsFingerprintValue);

public sealed record RtpPacketReceivedEvent(
    string? RemoteEndpoint,
    SDPMediaTypesEnum MediaType,
    RTPPacket Packet,
    int PacketCount);

public sealed record SessionEndedEvent(
    string? SessionId,
    bool IsRemoteInitiated,
    string Reason);

internal static class JsonExtensions
{
    public static string? GetOptionalString(this JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;

    public static string GetRequiredString(this JsonElement element, string propertyName) =>
        element.GetOptionalString(propertyName)
        ?? throw new InvalidOperationException($"Missing required JSON string property '{propertyName}'.");
}
