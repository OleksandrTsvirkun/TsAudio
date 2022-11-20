using System;
using System.Collections.Generic;
using System.Text;

namespace TsAudio.Drivers.WinMM.MmeInterop
{
	public enum WaveMessage
	{
		/// <summary>
		/// WIM_OPEN
		/// </summary>
		WaveInOpen = 0x3BE,
		/// <summary>
		/// WIM_CLOSE
		/// </summary>
		WaveInClose = 0x3BF,
		/// <summary>
		/// WIM_DATA
		/// </summary>
		WaveInData = 0x3C0,

		/// <summary>
		/// WOM_CLOSE
		/// </summary>
		WaveOutClose = 0x3BC,
		/// <summary>
		/// WOM_DONE
		/// </summary>
		WaveOutDone = 0x3BD,
		/// <summary>
		/// WOM_OPEN
		/// </summary>
		WaveOutOpen = 0x3BB
	}
}
