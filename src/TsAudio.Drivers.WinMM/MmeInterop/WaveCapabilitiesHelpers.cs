﻿using Microsoft.Win32;

using System;

namespace TsAudio.Drivers.WinMM.MmeInterop
{
    public static class WaveCapabilitiesHelpers
    {
        public static readonly Guid MicrosoftDefaultManufacturerId = new Guid("d5a47fa8-6d98-11d1-a21a-00a0c9223196");
        public static readonly Guid DefaultWaveOutGuid = new Guid("E36DC310-6D9A-11D1-A21A-00A0C9223196");
        public static readonly Guid DefaultWaveInGuid = new Guid("E36DC311-6D9A-11D1-A21A-00A0C9223196");

        /// <summary>
        /// The device name from the registry if supported
        /// </summary>
        public static string GetNameFromGuid(Guid guid)
        {
            // n.b it seems many audio drivers just return the default values, which won't be in the registry
            string name = null;
            using(var namesKey = Registry.LocalMachine.OpenSubKey(@"System\CurrentControlSet\Control\MediaCategories"))
            using(var nameKey = namesKey.OpenSubKey(guid.ToString("B")))
            {
                if(nameKey != null)
                    name = nameKey.GetValue("Name") as string;
            }
            return name;
        }

    }
}
