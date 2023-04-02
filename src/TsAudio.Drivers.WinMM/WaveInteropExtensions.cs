using System;
using System.Runtime.InteropServices;
using System.Threading;

using TsAudio.Drivers.WinMM.MmeInterop;

namespace TsAudio.Drivers.WinMM;
public static class WaveInteropExtensions
{
    public static void WaveOutWrite(IntPtr hWaveOut, WaveHeader lpWaveOutHdr, object locker)
    {
        MmResult result;

        lock(locker)
        {
            result = WaveInterop.waveOutWrite(hWaveOut, lpWaveOutHdr, Marshal.SizeOf(lpWaveOutHdr));
        }

        MmException.Try(result, nameof(WaveInterop.waveOutWrite));
    }

    public static void WaveOutPrepareHeader(IntPtr hWaveOut, WaveHeader lpWaveOutHdr, object locker)
    {
        MmResult result;

        lock(locker)
        {
            result = WaveInterop.waveOutPrepareHeader(hWaveOut, lpWaveOutHdr, Marshal.SizeOf(lpWaveOutHdr));
        }

        MmException.Try(result, nameof(WaveInterop.waveOutPrepareHeader));
    }

    public static void WaveInPrepareHeader(IntPtr hWaveOut, WaveHeader lpWaveOutHdr, object locker)
    {
        MmResult result;

        lock(locker)
        {
            result = WaveInterop.waveInPrepareHeader(hWaveOut, lpWaveOutHdr, Marshal.SizeOf(lpWaveOutHdr));
        }

        MmException.Try(result, nameof(WaveInterop.waveInPrepareHeader));
    }

    public static void WaveOutUnprepareHeader(IntPtr hWaveOut, WaveHeader lpWaveOutHdr, object locker)
    {
        MmResult result;

        lock(locker)
        {
            result = WaveInterop.waveOutUnprepareHeader(hWaveOut, lpWaveOutHdr, Marshal.SizeOf(lpWaveOutHdr));
        }

        MmException.Try(result, nameof(WaveInterop.waveOutUnprepareHeader));
    }

    public static void WaveInUnprepareHeader(IntPtr hWaveOut, WaveHeader lpWaveOutHdr, object locker)
    {
        MmResult result;

        lock(locker)
        {
            result = WaveInterop.waveInUnprepareHeader(hWaveOut, lpWaveOutHdr, Marshal.SizeOf(lpWaveOutHdr));
        }

        MmException.Try(result, nameof(WaveInterop.waveInUnprepareHeader));
    }

    public static void WaveInAddBuffer(IntPtr hWaveOut, WaveHeader lpWaveOutHdr, object locker)
    {
        MmResult result;

        lock(locker)
        {
            result = WaveInterop.waveInAddBuffer(hWaveOut, lpWaveOutHdr, Marshal.SizeOf(lpWaveOutHdr));
        }

        MmException.Try(result, nameof(WaveInterop.waveInAddBuffer));
    }

    public static void WaveOutReset(IntPtr hWaveOut, object locker)
    {
        MmResult result;

        lock(locker)
        {
            result = WaveInterop.waveOutReset(hWaveOut);
        }

        MmException.Try(result, nameof(WaveInterop.waveOutReset));
    }

    public static void WaveOutRestart(IntPtr hWaveOut, object locker)
    {
        MmResult result;

        lock(locker)
        {
            result = WaveInterop.waveOutRestart(hWaveOut);
        }

        MmException.Try(result, nameof(WaveInterop.waveOutRestart));
    }

    public static void WaveOutPause(IntPtr hWaveOut, object locker)
    {
        MmResult result;

        lock(locker)
        {
            result = WaveInterop.waveOutPause(hWaveOut);
        }

        MmException.Try(result, nameof(WaveInterop.waveOutPause));
    }

    public static void WaveOutGetPosition(IntPtr hWaveOut, out MmTime mmTime, object locker)
    {
        MmResult result;

        // request results in bytes, TODO: perhaps make this a little more flexible and support the other types?
        mmTime = new MmTime();
        mmTime.wType = MmTime.TIME_BYTES;

        lock(locker)
        {
            result = WaveInterop.waveOutGetPosition(hWaveOut, ref mmTime, Marshal.SizeOf(mmTime));
        }

        MmException.Try(result, nameof(WaveInterop.waveOutGetPosition));

        if(mmTime.wType != MmTime.TIME_BYTES)
        {
            throw new Exception(string.Format("{3}: wType -> Expected {0}, Received {1}", MmTime.TIME_BYTES, mmTime.wType, nameof(WaveInterop.waveOutGetPosition)));
        }
    }

    public static void WaveInGetPosition(IntPtr hWaveOut, out MmTime mmTime, object locker)
    {
        MmResult result;

        mmTime = new MmTime();
        mmTime.wType = MmTime.TIME_BYTES; // request results in bytes, TODO: perhaps make this a little more flexible and support the other types?

        lock(locker)
        {
            result = WaveInterop.waveInGetPosition(hWaveOut, out mmTime, Marshal.SizeOf(mmTime));
        }

        MmException.Try(result, nameof(WaveInterop.waveInGetPosition));

        if(mmTime.wType != MmTime.TIME_BYTES)
        {
            throw new Exception(string.Format("waveInGetPosition: wType -> Expected {0}, Received {1}", MmTime.TIME_BYTES, mmTime.wType));
        }

    }

    public static void WaveOutGetVolume(IntPtr hWaveOut, out int dwVolume, object locker)
    {
        MmResult result;

        lock(locker)
        {
            result = WaveInterop.waveOutGetVolume(hWaveOut, out dwVolume);
        }

        MmException.Try(result, nameof(WaveInterop.waveOutGetVolume));
    }

    public static void WaveOutSetVolume(IntPtr hWaveOut, int dwVolume, object locker)
    {
        MmResult result;

        lock(locker)
        {
            result = WaveInterop.waveOutSetVolume(hWaveOut, dwVolume);
        }

        MmException.Try(result, nameof(WaveInterop.waveOutSetVolume));
    }

    public static void WaveInStart(IntPtr hWaveIn, object locker)
    {
        MmResult result;

        lock(locker)
        {
            result = WaveInterop.waveInStart(hWaveIn);
        }

        MmException.Try(result, nameof(WaveInterop.waveInStart));
    }

    public static void WaveInStop(IntPtr hWaveIn, object locker)
    {
        MmResult result;

        lock(locker)
        {
            result = WaveInterop.waveInStop(hWaveIn);
        }

        MmException.Try(result, nameof(WaveInterop.waveInStop));
    }

    public static void WaveInReset(IntPtr hWaveIn, object locker)
    {
        MmResult result;

        lock(locker)
        {
            result = WaveInterop.waveInReset(hWaveIn);
        }

        MmException.Try(result, nameof(WaveInterop.waveInReset));
    }

    public static void WaveInGetDevCaps(IntPtr deviceID, out WaveInCapabilities waveInCaps)
    {
        waveInCaps = new WaveInCapabilities();

        var result = WaveInterop.waveInGetDevCaps(deviceID, out waveInCaps, Marshal.SizeOf(waveInCaps));

        MmException.Try(result, nameof(WaveInterop.waveInGetDevCaps));
    }

    public static void Invoke<TEventArgs>(this EventHandler<TEventArgs> handler, object sender, TEventArgs e, SynchronizationContext synchronizationContext = null)
    {
        var handler2 = handler;
        if (synchronizationContext is null)
        {
            handler2?.Invoke(sender, e);
        }
        else
        {
            synchronizationContext.Post(state => handler2?.Invoke(sender, e), null);
        }
    }
}
