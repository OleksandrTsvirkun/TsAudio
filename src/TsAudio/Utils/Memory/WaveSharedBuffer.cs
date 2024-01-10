using System;
using System.Runtime.InteropServices;

namespace TsAudio.Utils.Memory;

[StructLayout(LayoutKind.Explicit, Pack = 2)]
public class WaveSharedBuffer
{
    /// <summary>
    /// Number of Bytes
    /// </summary>
    [FieldOffset(0)]
    private int numberOfBytes;

    [FieldOffset(8)]
    private byte[] byteBuffer;

    [FieldOffset(8)]
    private float[] floatBuffer;

    [FieldOffset(8)]
    private short[] shortBuffer;

    [FieldOffset(8)]
    private int[] intBuffer;

    /// <summary>
    /// Gets the byte buffer.
    /// </summary>
    /// <value>The byte buffer.</value>
    public byte[] ByteBuffer => this.byteBuffer;

    public int ByteBufferLength => this.numberOfBytes;

    /// <summary>
    /// Performs an implicit conversion from <see cref="NAudio.Wave.WaveBuffer"/> to <see cref="byte"/>.
    /// </summary>
    /// <param name="waveBuffer">The wave buffer.</param>
    /// <returns>The result of the conversion.</returns>
    public static implicit operator byte[](WaveSharedBuffer waveBuffer)
    {
        return waveBuffer.ByteBuffer;
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="NAudio.Wave.WaveBuffer"/> to <see cref="byte"/>.
    /// </summary>
    /// <param name="waveBuffer">The wave buffer.</param>
    /// <returns>The result of the conversion.</returns>
    public static implicit operator Span<byte>(WaveSharedBuffer waveBuffer)
    {
        return waveBuffer.ByteBuffer.AsSpan(0, waveBuffer.IntBufferLength);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="NAudio.Wave.WaveBuffer"/> to <see cref="byte"/>.
    /// </summary>
    /// <param name="waveBuffer">The wave buffer.</param>
    /// <returns>The result of the conversion.</returns>
    public static implicit operator Memory<byte>(WaveSharedBuffer waveBuffer)
    {
        return waveBuffer.ByteBuffer.AsMemory(0, waveBuffer.IntBufferLength);
    }

    /// <summary>
    /// Gets the float buffer.
    /// </summary>
    /// <value>The float buffer.</value>
    public float[] FloatBuffer => this.floatBuffer;

    public int FloatBufferLength => this.numberOfBytes / sizeof(float);

    /// <summary>
    /// Performs an implicit conversion from <see cref="NAudio.Wave.WaveBuffer"/> to <see cref="float"/>.
    /// </summary>
    /// <param name="waveBuffer">The wave buffer.</param>
    /// <returns>The result of the conversion.</returns>
    public static implicit operator float[](WaveSharedBuffer waveBuffer)
    {
        return waveBuffer.FloatBuffer;
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="NAudio.Wave.WaveBuffer"/> to <see cref="float"/>.
    /// </summary>
    /// <param name="waveBuffer">The wave buffer.</param>
    /// <returns>The result of the conversion.</returns>
    public static implicit operator Span<float>(WaveSharedBuffer waveBuffer)
    {
        return waveBuffer.FloatBuffer.AsSpan(0, waveBuffer.FloatBufferLength);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="NAudio.Wave.WaveBuffer"/> to <see cref="float"/>.
    /// </summary>
    /// <param name="waveBuffer">The wave buffer.</param>
    /// <returns>The result of the conversion.</returns>
    public static implicit operator Memory<float>(WaveSharedBuffer waveBuffer)
    {
        return waveBuffer.FloatBuffer.AsMemory(0, waveBuffer.FloatBufferLength);
    }

    /// <summary>
    /// Gets the short buffer.
    /// </summary>
    /// <value>The short buffer.</value>
    public short[] ShortBuffer => this.shortBuffer;

    public int ShortBufferLength => this.numberOfBytes / sizeof(short);

    /// <summary>
    /// Performs an implicit conversion from <see cref="NAudio.Wave.WaveBuffer"/> to <see cref="short"/>.
    /// </summary>
    /// <param name="waveBuffer">The wave buffer.</param>
    /// <returns>The result of the conversion.</returns>
    public static implicit operator short[](WaveSharedBuffer waveBuffer)
    {
        return waveBuffer.ShortBuffer;
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="NAudio.Wave.WaveBuffer"/> to <see cref="short"/>.
    /// </summary>
    /// <param name="waveBuffer">The wave buffer.</param>
    /// <returns>The result of the conversion.</returns>
    public static implicit operator Span<short>(WaveSharedBuffer waveBuffer)
    {
        return waveBuffer.ShortBuffer.AsSpan(0, waveBuffer.ShortBufferLength);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="NAudio.Wave.WaveBuffer"/> to <see cref="short"/>.
    /// </summary>
    /// <param name="waveBuffer">The wave buffer.</param>
    /// <returns>The result of the conversion.</returns>
    public static implicit operator Memory<short>(WaveSharedBuffer waveBuffer)
    {
        return waveBuffer.ShortBuffer.AsMemory(0, waveBuffer.ShortBufferLength);
    }

    /// <summary>
    /// Gets the int buffer.
    /// </summary>
    /// <value>The int buffer.</value>
    public int[] IntBuffer => this.intBuffer;

    public int IntBufferLength => this.numberOfBytes / sizeof(int);

    /// <summary>
    /// Performs an implicit conversion from <see cref="NAudio.Wave.WaveBuffer"/> to <see cref="int"/>.
    /// </summary>
    /// <param name="waveBuffer">The wave buffer.</param>
    /// <returns>The result of the conversion.</returns>
    public static implicit operator int[](WaveSharedBuffer waveBuffer)
    {
        return waveBuffer.IntBuffer;
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="NAudio.Wave.WaveBuffer"/> to <see cref="int"/>.
    /// </summary>
    /// <param name="waveBuffer">The wave buffer.</param>
    /// <returns>The result of the conversion.</returns>
    public static implicit operator Span<int>(WaveSharedBuffer waveBuffer)
    {
        return waveBuffer.IntBuffer.AsSpan(0, waveBuffer.IntBufferLength);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="NAudio.Wave.WaveBuffer"/> to <see cref="int"/>.
    /// </summary>
    /// <param name="waveBuffer">The wave buffer.</param>
    /// <returns>The result of the conversion.</returns>
    public static implicit operator Memory<int>(WaveSharedBuffer waveBuffer)
    {
        return waveBuffer.IntBuffer.AsMemory(0, waveBuffer.IntBufferLength);
    }

    public WaveSharedBuffer(int length)
    {
        this.numberOfBytes = length;
        this.byteBuffer = new byte[length];
    }

    /// <summary>
    /// Clears the associated buffer.
    /// </summary>
    public void Clear()
    {
        Array.Clear(this.byteBuffer, 0, this.byteBuffer.Length);
    }

    /// <summary>
    /// Copy this WaveBuffer to a destination buffer up to ByteBufferCount bytes.
    /// </summary>
    public void Copy(Array destinationArray)
    {
        Array.Copy(this.byteBuffer, destinationArray, this.numberOfBytes);
    }
}
