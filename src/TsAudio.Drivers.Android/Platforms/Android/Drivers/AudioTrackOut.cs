using Android.Media;

using System.Buffers;
using System.Runtime.InteropServices;

using TsAudio.Wave.WaveFormats;
using TsAudio.Wave.WaveOutputs;
using TsAudio.Wave.WaveProviders;

using Encoding = Android.Media.Encoding;

namespace TsAudio.Drivers.Android.Platforms.Android.Drivers;

/// <summary>
/// Represents an Android wave player implemented using <see cref="AudioTrack"/>.
/// </summary>
public class AudioTrackOut : IWavePlayer
{
    #region Fields

    private IWaveProvider waveProvider;
    private AudioTrack audioTrack;

    private bool disposed;
    private CancellationTokenSource cancellationTokenSource;
    private Task playing;
    #endregion

    #region Properties

    private PlaybackState playbackState;
    public PlaybackState PlaybackState
    {
        get => this.playbackState;
        set
        {
            this.playbackState = value;
            this.PlaybackStateChanged?.Invoke(this, new PlaybackStateEventArgs(value));
        }
    }

    /// <summary>
    /// Gets or sets the volume in % (0.0 to 1.0).
    /// </summary>
    private float volume;
    public float Volume
    {
        get => this.volume;
        set
        {
            this.volume = Math.Clamp(value, 0f, 1f);
            this.audioTrack?.SetVolume(this.volume);
        }
    }

    /// <summary>
    /// Gets or sets the desired latency in milliseconds.
    /// </summary>
    public int DesiredLatency { get; set; }

    /// <summary>
    /// Gets or sets the number of buffers to use.
    /// </summary>
    public int NumberOfBuffers { get; set; }

    /// <summary>
    /// Gets or sets the usage.
    /// </summary>
    public AudioUsageKind Usage { get; set; }

    /// <summary>
    /// Gets or sets the content type.
    /// </summary>
    public AudioContentType ContentType { get; set; }

    /// <summary>
    /// Gets or sets the performance mode.
    /// </summary>
    public AudioTrackPerformanceMode PerformanceMode { get; set; }

    public WaveFormat WaveFormat => this.waveProvider?.WaveFormat;

    #endregion

    #region Events

    /// <summary>
    /// Occurs when the player has stopped.
    /// </summary>
    public event EventHandler<PlaybackStateEventArgs> PlaybackStateChanged;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioTrackOut"/> class.
    /// </summary>
    public AudioTrackOut()
    {
        //Initialize the fields and properties
        this.waveProvider = null;
        this.audioTrack = null;
        this.volume = 1.0f;
        this.disposed = false;
        this.PlaybackState = PlaybackState.Stopped;
        this.DesiredLatency = 500;
        this.NumberOfBuffers = 2;
        this.Usage = AudioUsageKind.Media;
        this.ContentType = AudioContentType.Music;
        this.PerformanceMode = AudioTrackPerformanceMode.LowLatency;
    }

    /// <summary>
    /// Releases the unmanaged resources used by the current instance of the <see cref="AudioTrackOut"/> class.
    /// </summary>
    ~AudioTrackOut()
    {
        //Dispose of this object
        this.Dispose(false);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Initializes the player with the specified wave provider.
    /// </summary>
    /// <param name="waveProvider">The wave provider to be played.</param>
    public void Init(IWaveProvider waveProvider)
    {
        if(waveProvider is null)
        {
            throw new ArgumentNullException(nameof(waveProvider));
        }

        this.waveProvider = waveProvider;

        //Make sure we haven't been disposed
        this.ThrowIfDisposed();

        //Initialize the wave provider
        var encoding = GetEncoding(waveProvider);


        //Determine the channel mask
        var channelMask = GetChannelMask();
        var bufferSize = GetBufferSize(encoding, channelMask);

        var audioAttributes = new AudioAttributes.Builder()
                .SetUsage(this.Usage)
                .SetContentType(this.ContentType)
                .Build();

        var audioFormat = new AudioFormat.Builder()
                .SetEncoding(encoding)
                .SetSampleRate(this.waveProvider.WaveFormat.SampleRate)
                .SetChannelMask(channelMask)
                .Build();

        //Initialize the audio track
        this.audioTrack = new AudioTrack.Builder()
            .SetAudioAttributes(audioAttributes)
            .SetAudioFormat(audioFormat)
            .SetBufferSizeInBytes(bufferSize)
            .SetTransferMode(AudioTrackMode.Stream)
            .SetPerformanceMode(this.PerformanceMode)
            .Build();

        this.audioTrack.SetVolume(this.Volume);
    }

    /// <summary>
    /// Starts the player.
    /// </summary>
    public void Play()
    {
        //Make sure we haven't been disposed
        this.ThrowIfDisposed();

        //Check the player state
        this.ThrowIfNotInitialized();

        if(this.PlaybackState == PlaybackState.Playing)
        {
            return;
        }

        this.audioTrack.Play();

        this.PlaybackState = PlaybackState.Playing;

        if(this.PlaybackState == PlaybackState.Paused)
        {
            return;
        }

        this.RenewCancelationToken();

        this.playing?.Dispose();
        this.playing = Task.Run(this.DoPlaybackWrapper, this.cancellationTokenSource.Token);
    }


    /// <summary>
    /// Pauses the player.
    /// </summary>
    public void Pause()
    {
        //Make sure we haven't been disposed
        this.ThrowIfDisposed();

        //Check the player state
        this.ThrowIfNotInitialized();

        if(this.audioTrack.PlayState == PlayState.Playing)
        {
            this.audioTrack.Pause();
            //Pause the wave player
            this.PlaybackState = PlaybackState.Paused;
        }
    }

    /// <summary>
    /// Stops the player.
    /// </summary>
    public void Stop()
    {
        //Make sure we haven't been disposed
        this.ThrowIfDisposed();

        //Check the player state
        if(this.waveProvider == null)
        {
            return;
        }

        if(this.audioTrack.PlayState != PlayState.Stopped)
        {
            this.audioTrack.Stop();
            this.PlaybackState = PlaybackState.Stopped;

            this.cancellationTokenSource?.Cancel();
        }
    }

    /// <summary>
    /// Releases all resources used by the current instance of the <see cref="AudioTrackOut"/> class.
    /// </summary>
    public void Dispose()
    {
        //Dispose of this object
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Protected Methods
    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="AudioTrackOut"/>, and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        //Clean up any managed and unmanaged resources
        if(!this.disposed)
        {
            if(disposing)
            {
                if(this.PlaybackState != TsAudio.Wave.WaveOutputs.PlaybackState.Stopped)
                {
                    Stop();
                }
                this.audioTrack?.Release();
                this.audioTrack?.Dispose();
            }
            this.disposed = true;
        }
    }

    #endregion

    #region Private Methods

    private async Task DoPlaybackWrapper()
    {
        try
        {
            await this.DoPlayback(this.cancellationTokenSource.Token);
        }
        catch(Exception)
        {

        }
        finally
        {
            this.PlaybackState = PlaybackState.Stopped;
        }
    }

    private async ValueTask DoPlayback(CancellationToken cancellationToken = default)
    {
        if (this.audioTrack is null)
        {
            throw new InvalidOperationException("Must be initialized first.");
        }

        //Initialize the wave buffer
        var waveBufferSize = (this.audioTrack.BufferSizeInFrames + this.NumberOfBuffers - 1) / this.NumberOfBuffers * this.waveProvider.WaveFormat.BlockAlign;
        waveBufferSize = (waveBufferSize + 3) & ~3;

        var buffer = ArrayPool<byte>.Shared.Rent(waveBufferSize);

        //Run the playback loop
        while(this.audioTrack.PlayState != PlayState.Stopped)
        {
            //Fill the wave buffer with new samples
            int bytesRead = await this.waveProvider.ReadAsync(buffer.AsMemory(0, waveBufferSize), cancellationToken);
            if(bytesRead > 0)
            {
                buffer.AsSpan((bytesRead + 3) & ~3).Clear();
                //Write the wave buffer to the audio track
                await this.WriteBuffer(buffer, 0, bytesRead);
            }
            else
            {
                //Stop the audio track
                this.audioTrack.Stop();
                this.PlaybackState = PlaybackState.Stopped;
                break;
            }
        }
        ArrayPool<byte>.Shared.Return(buffer);
        //Flush the audio track
        this.audioTrack.Flush();
    }

    private async ValueTask WriteBuffer(byte[] buffer, int offset, int count)
    {
        //Write the specified wave buffer to the audio track
        if(this.waveProvider.WaveFormat.Encoding == WaveFormatEncoding.Pcm)
        {
            this.audioTrack.Write(buffer, 0, count);
        }
        else if(this.waveProvider.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            var floatBufferSize = buffer.Length / sizeof(float);

            var floatBuffer = ArrayPool<float>.Shared.Rent(floatBufferSize);

            this.FillFloatBuffer(new Span<byte>(buffer, offset, count), floatBuffer);

            await this.audioTrack.WriteAsync(floatBuffer, 0, floatBufferSize, WriteMode.Blocking);

            ArrayPool<float>.Shared.Return(floatBuffer);
        }
    }

    private void FillFloatBuffer(Span<byte> byteSpan, float[] floatBuffer)
    {
        var floatSpan = MemoryMarshal.Cast<byte, float>(byteSpan);

        floatSpan.CopyTo(floatBuffer.AsSpan(0, floatSpan.Length));
    }

    private void ThrowIfNotInitialized()
    {
        //Throw an exception if this object has not been initialized
        if(this.waveProvider is null)
        {
            throw new InvalidOperationException("This wave player instance has not been initialized");
        }
    }

    private void ThrowIfDisposed()
    {
        //Throw an exception if this object has been disposed
        if(this.disposed)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }


    private void RenewCancelationToken()
    {
        this.cancellationTokenSource?.Cancel();
        this.cancellationTokenSource?.Dispose();
        this.cancellationTokenSource = new CancellationTokenSource();
    }

    private int GetBufferSize(Encoding encoding, ChannelOut channelMask)
    {
        //Determine the buffer size
        int bufferSize = this.waveProvider.WaveFormat.ConvertLatencyToByteSize(this.DesiredLatency);

        int minBufferSize = AudioTrack.GetMinBufferSize(this.waveProvider.WaveFormat.SampleRate, channelMask, encoding);

        if(bufferSize < minBufferSize)
        {
            bufferSize = minBufferSize;
        }

        return bufferSize;
    }

    private ChannelOut GetChannelMask()
    {
        return this.waveProvider.WaveFormat.Channels switch
        {
            1 => ChannelOut.Mono,
            2 => ChannelOut.Stereo,
            _ => throw new ArgumentException("Input wave provider must be mono or stereo", nameof(waveProvider))
        };
    }

    private static Encoding GetEncoding(IWaveProvider waveProvider)
    {
        Encoding encoding;
        if(waveProvider.WaveFormat.Encoding == WaveFormatEncoding.Pcm
                    || waveProvider.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            encoding = waveProvider.WaveFormat.BitsPerSample switch
            {
                8 => Encoding.Pcm8bit,
                16 => Encoding.Pcm16bit,
                32 => Encoding.PcmFloat,
                _ => throw new ArgumentException("Input wave provider must be 8-bit, 16-bit, or 32-bit", nameof(waveProvider))
            };
        }
        else
        {
            throw new ArgumentException("Input wave provider must be PCM or IEEE float", nameof(waveProvider));
        }

        return encoding;
    }


    #endregion
}
