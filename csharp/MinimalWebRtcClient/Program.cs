try
{
    var options = ClientOptions.Parse(args);
    using var shutdown = new CancellationTokenSource();

    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        shutdown.Cancel();
    };

    var attempt = 0;
    do
    {
        attempt++;
        if (options.Repeat)
        {
            AppLog.Write("repeat", $"Starting C# listener attempt #{attempt}.");
        }

        using var attemptTimeout = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);
        attemptTimeout.CancelAfter(options.Timeout);

        try
        {
            using var client = CreateReceiver(options);
            await client.RunAsync(attemptTimeout.Token);
        }
        catch (OperationCanceledException) when (!shutdown.IsCancellationRequested)
        {
            AppLog.Write("timeout", "WebRTC negotiation/observation did not complete before the configured timeout.");
            if (!options.Repeat)
            {
                Environment.ExitCode = 2;
                return;
            }
        }
        catch (Exception ex) when (options.Repeat && !shutdown.IsCancellationRequested)
        {
            AppLog.Write("fatal", ex.ToString());
        }

        if (!options.Repeat)
        {
            break;
        }

        AppLog.Write("repeat", $"Waiting {options.RetryDelay.TotalSeconds:N0}s before reconnecting.");
        await Task.Delay(options.RetryDelay, shutdown.Token);
    }
    while (!shutdown.IsCancellationRequested);

    Environment.ExitCode = 0;
}
catch (OperationCanceledException)
{
    AppLog.Write("timeout", "WebRTC negotiation/observation did not complete before the configured timeout.");
    Environment.ExitCode = 2;
}
catch (ArgumentException ex)
{
    AppLog.Write("usage", ex.Message);
    Console.WriteLine(ClientOptions.Usage);
    Environment.ExitCode = 2;
}
catch (Exception ex)
{
    AppLog.Write("fatal", ex.ToString());
    Environment.ExitCode = 1;
}

static GstWebRtcReceiver.Core.GstWebRtcReceiver CreateReceiver(ClientOptions options)
{
    var receiver = new GstWebRtcReceiver.Core.GstWebRtcReceiver(new GstWebRtcReceiver.Core.GstWebRtcReceiverConfig(
        options.SignallingUri,
        options.ObserveRtp,
        options.VerboseFrames));

    receiver.OnLog += entry => AppLog.Write(entry.Step, entry.Message);
    receiver.OnConnected += () => AppLog.Write("event.connected", "Receiver core reported negotiated remote video track + ICE connected.");
    receiver.OnIceStateChanged += state => AppLog.Write("event.ice", state.ToString());
    receiver.OnDtlsStateChanged += state =>
        AppLog.Write("event.dtls", $"pc={state.PeerConnectionState}; complete={state.IsNegotiationComplete}; srtpEnc={state.IsSrtpEncoderActive}; srtpDec={state.IsSrtpDecoderActive}.");
    receiver.OnRemoteTrackNegotiated += track =>
        AppLog.Write("event.track", $"Remote video negotiated; SSRC={track.Ssrc}; fingerprint={track.RemoteDtlsFingerprintAlgorithm} {track.RemoteDtlsFingerprintValue}.");
    receiver.OnRtpPacket += packet =>
    {
        if (packet.PacketCount == 1 || packet.PacketCount % 100 == 0)
        {
            AppLog.Write("event.rtp", $"count={packet.PacketCount}; pt={packet.Packet.Header.PayloadType}; ssrc={packet.Packet.Header.SyncSource}; remote={packet.RemoteEndpoint}.");
        }
    };
    receiver.OnSessionEnded += session =>
        AppLog.Write("event.session", $"sessionId={session.SessionId}; remote={session.IsRemoteInitiated}; reason={session.Reason}.");
    receiver.OnError += ex => AppLog.Write("event.error", ex.Message);

    return receiver;
}

sealed record ClientOptions(
    Uri SignallingUri,
    TimeSpan Timeout,
    TimeSpan ObserveRtp,
    bool VerboseFrames,
    bool Repeat,
    TimeSpan RetryDelay)
{
    public const string Usage = """
        Usage:
          dotnet run --project csharp/MinimalWebRtcClient/MinimalWebRtcClient.csproj -- [options]

        Options:
          --signalling <uri>     Signalling URI. Default: ws://localhost:8443
          --timeout <seconds>   Total client timeout. Default: 60
          --observe <seconds>   RTP observation window after ICE connected. Default: 15
          --verbose-frames      Log every decoded video frame.
          --repeat              Keep reconnecting after each session, like a persistent listener.
          --retry-delay <sec>   Delay between repeated listener sessions. Default: 3
        """;

    public static ClientOptions Parse(string[] args)
    {
        var uri = new Uri("ws://localhost:8443");
        var timeout = TimeSpan.FromSeconds(60);
        var observeRtp = TimeSpan.FromSeconds(15);
        var verboseFrames = false;
        var repeat = false;
        var retryDelay = TimeSpan.FromSeconds(3);

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--signalling":
                case "--signaling":
                    uri = new Uri(ReadValue(args, ref i));
                    break;
                case "--timeout":
                    timeout = TimeSpan.FromSeconds(ParsePositiveInt(ReadValue(args, ref i), "--timeout"));
                    break;
                case "--observe":
                    observeRtp = TimeSpan.FromSeconds(ParsePositiveInt(ReadValue(args, ref i), "--observe"));
                    break;
                case "--verbose-frames":
                    verboseFrames = true;
                    break;
                case "--repeat":
                    repeat = true;
                    break;
                case "--retry-delay":
                    retryDelay = TimeSpan.FromSeconds(ParsePositiveInt(ReadValue(args, ref i), "--retry-delay"));
                    break;
                case "--help":
                case "-h":
                    Console.WriteLine(Usage);
                    Environment.Exit(0);
                    return new ClientOptions(uri, timeout, observeRtp, verboseFrames, repeat, retryDelay);
                default:
                    throw new ArgumentException($"Unknown argument '{args[i]}'. Use --help for supported options.");
            }
        }

        if (timeout <= observeRtp)
        {
            throw new ArgumentException("--timeout must be greater than --observe so negotiation has time to complete before RTP observation starts.");
        }

        return new ClientOptions(uri, timeout, observeRtp, verboseFrames, repeat, retryDelay);
    }

    private static string ReadValue(string[] args, ref int index)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Missing value for '{args[index]}'.");
        }

        index++;
        return args[index];
    }

    private static int ParsePositiveInt(string value, string optionName)
    {
        if (!int.TryParse(value, out var parsed) || parsed <= 0)
        {
            throw new ArgumentException($"{optionName} must be a positive integer number of seconds.");
        }

        return parsed;
    }
}

static class AppLog
{
    public static void Write(string step, string message) =>
        Console.WriteLine($"[{DateTimeOffset.Now:HH:mm:ss.fff}] {step,-18} {message}");
}
