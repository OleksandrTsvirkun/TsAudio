using System;
using System.Collections.Generic;
using System.Text;

namespace TsAudio.Drivers.WinMM.MmeInterop
{
    [Flags]
    public enum WaveInOutOpenFlags
    {
        /// <summary>
        /// CALLBACK_NULL
        /// No callback
        /// </summary>
        CallbackNull = 0,
        /// <summary>
        /// CALLBACK_FUNCTION
        /// dwCallback is a FARPROC 
        /// </summary>
        CallbackFunction = 0x30000,
        /// <summary>
        /// CALLBACK_EVENT
        /// dwCallback is an EVENT handle 
        /// </summary>
        CallbackEvent = 0x50000,
        /// <summary>
        /// CALLBACK_WINDOW
        /// dwCallback is a HWND 
        /// </summary>
        CallbackWindow = 0x10000,
        /// <summary>
        /// CALLBACK_THREAD
        /// callback is a thread ID 
        /// </summary>
        CallbackThread = 0x20000,
    }
}
