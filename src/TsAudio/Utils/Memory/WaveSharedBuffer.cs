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
    public int numberOfBytes;

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

    /// <summary>
    /// Gets the float buffer.
    /// </summary>
    /// <value>The float buffer.</value>
    public float[] FloatBuffer => this.floatBuffer;

    /// <summary>
    /// Gets the short buffer.
    /// </summary>
    /// <value>The short buffer.</value>
    public short[] ShortBuffer => this.shortBuffer;

    /// <summary>
    /// Gets the int buffer.
    /// </summary>
    /// <value>The int buffer.</value>
    public int[] IntBuffer => this.intBuffer;

    public WaveSharedBuffer(byte[] buffer)
    {
        this.byteBuffer = buffer;
    }

    /// <summary>
    /// Clears the associated buffer.
    /// </summary>
    public void Clear()
    {
        Array.Clear(byteBuffer, 0, byteBuffer.Length);
    }

    /// <summary>
    /// Copy this WaveBuffer to a destination buffer up to ByteBufferCount bytes.
    /// </summary>
    public void Copy(Array destinationArray)
    {
        Array.Copy(byteBuffer, destinationArray, numberOfBytes);
    }


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
    /// Performs an implicit conversion from <see cref="NAudio.Wave.WaveBuffer"/> to <see cref="float"/>.
    /// </summary>
    /// <param name="waveBuffer">The wave buffer.</param>
    /// <returns>The result of the conversion.</returns>
    public static implicit operator float[](WaveSharedBuffer waveBuffer)
    {
        return waveBuffer.FloatBuffer;
    }

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
    /// Performs an implicit conversion from <see cref="NAudio.Wave.WaveBuffer"/> to <see cref="short"/>.
    /// </summary>
    /// <param name="waveBuffer">The wave buffer.</param>
    /// <returns>The result of the conversion.</returns>
    public static implicit operator short[](WaveSharedBuffer waveBuffer)
    {
        return waveBuffer.ShortBuffer;
    }
}
