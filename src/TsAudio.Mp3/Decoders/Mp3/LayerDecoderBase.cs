﻿using System;
using System.Collections.Generic;
using System.Numerics;

namespace TsAudio.Decoders.Mp3
{
    abstract class LayerDecoderBase
    {
        protected const int SBLIMIT = 32;

        private const float INV_SQRT_2 = 7.071067811865474617150084668537e-01f;

        #region Tables

        private static float[] DEWINDOW_TABLE = {
                                         0.000000000f, -0.000015259f, -0.000015259f, -0.000015259f,
                                        -0.000015259f, -0.000015259f, -0.000015259f, -0.000030518f,
                                        -0.000030518f, -0.000030518f, -0.000030518f, -0.000045776f,
                                        -0.000045776f, -0.000061035f, -0.000061035f, -0.000076294f,
                                        -0.000076294f, -0.000091553f, -0.000106812f, -0.000106812f,
                                        -0.000122070f, -0.000137329f, -0.000152588f, -0.000167847f,
                                        -0.000198364f, -0.000213623f, -0.000244141f, -0.000259399f,
                                        -0.000289917f, -0.000320435f, -0.000366211f, -0.000396729f,
                                        -0.000442505f, -0.000473022f, -0.000534058f, -0.000579834f,
                                        -0.000625610f, -0.000686646f, -0.000747681f, -0.000808716f,
                                        -0.000885010f, -0.000961304f, -0.001037598f, -0.001113892f,
                                        -0.001205444f, -0.001296997f, -0.001388550f, -0.001480103f,
                                        -0.001586914f, -0.001693726f, -0.001785278f, -0.001907349f,
                                        -0.002014160f, -0.002120972f, -0.002243042f, -0.002349854f,
                                        -0.002456665f, -0.002578735f, -0.002685547f, -0.002792358f,
                                        -0.002899170f, -0.002990723f, -0.003082275f, -0.003173828f,
                                         0.003250122f,  0.003326416f,  0.003387451f,  0.003433228f,
                                         0.003463745f,  0.003479004f,  0.003479004f,  0.003463745f,
                                         0.003417969f,  0.003372192f,  0.003280640f,  0.003173828f,
                                         0.003051758f,  0.002883911f,  0.002700806f,  0.002487183f,
                                         0.002227783f,  0.001937866f,  0.001617432f,  0.001266479f,
                                         0.000869751f,  0.000442505f, -0.000030518f, -0.000549316f,
                                        -0.001098633f, -0.001693726f, -0.002334595f, -0.003005981f,
                                        -0.003723145f, -0.004486084f, -0.005294800f, -0.006118774f,
                                        -0.007003784f, -0.007919312f, -0.008865356f, -0.009841919f,
                                        -0.010848999f, -0.011886597f, -0.012939453f, -0.014022827f,
                                        -0.015121460f, -0.016235352f, -0.017349243f, -0.018463135f,
                                        -0.019577026f, -0.020690918f, -0.021789551f, -0.022857666f,
                                        -0.023910522f, -0.024932861f, -0.025909424f, -0.026840210f,
                                        -0.027725220f, -0.028533936f, -0.029281616f, -0.029937744f,
                                        -0.030532837f, -0.031005859f, -0.031387329f, -0.031661987f,
                                        -0.031814575f, -0.031845093f, -0.031738281f, -0.031478882f,
                                         0.031082153f,  0.030517578f,  0.029785156f,  0.028884888f,
                                         0.027801514f,  0.026535034f,  0.025085449f,  0.023422241f,
                                         0.021575928f,  0.019531250f,  0.017257690f,  0.014801025f,
                                         0.012115479f,  0.009231567f,  0.006134033f,  0.002822876f,
                                        -0.000686646f, -0.004394531f, -0.008316040f, -0.012420654f,
                                        -0.016708374f, -0.021179199f, -0.025817871f, -0.030609131f,
                                        -0.035552979f, -0.040634155f, -0.045837402f, -0.051132202f,
                                        -0.056533813f, -0.061996460f, -0.067520142f, -0.073059082f,
                                        -0.078628540f, -0.084182739f, -0.089706421f, -0.095169067f,
                                        -0.100540161f, -0.105819702f, -0.110946655f, -0.115921021f,
                                        -0.120697021f, -0.125259399f, -0.129562378f, -0.133590698f,
                                        -0.137298584f, -0.140670776f, -0.143676758f, -0.146255493f,
                                        -0.148422241f, -0.150115967f, -0.151306152f, -0.151962280f,
                                        -0.152069092f, -0.151596069f, -0.150497437f, -0.148773193f,
                                        -0.146362305f, -0.143264771f, -0.139450073f, -0.134887695f,
                                        -0.129577637f, -0.123474121f, -0.116577148f, -0.108856201f,
                                         0.100311279f,  0.090927124f,  0.080688477f,  0.069595337f,
                                         0.057617187f,  0.044784546f,  0.031082153f,  0.016510010f,
                                         0.001068115f, -0.015228271f, -0.032379150f, -0.050354004f,
                                        -0.069168091f, -0.088775635f, -0.109161377f, -0.130310059f,
                                        -0.152206421f, -0.174789429f, -0.198059082f, -0.221984863f,
                                        -0.246505737f, -0.271591187f, -0.297210693f, -0.323318481f,
                                        -0.349868774f, -0.376800537f, -0.404083252f, -0.431655884f,
                                        -0.459472656f, -0.487472534f, -0.515609741f, -0.543823242f,
                                        -0.572036743f, -0.600219727f, -0.628295898f, -0.656219482f,
                                        -0.683914185f, -0.711318970f, -0.738372803f, -0.765029907f,
                                        -0.791213989f, -0.816864014f, -0.841949463f, -0.866363525f,
                                        -0.890090942f, -0.913055420f, -0.935195923f, -0.956481934f,
                                        -0.976852417f, -0.996246338f, -1.014617920f, -1.031936646f,
                                        -1.048156738f, -1.063217163f, -1.077117920f, -1.089782715f,
                                        -1.101211548f, -1.111373901f, -1.120223999f, -1.127746582f,
                                        -1.133926392f, -1.138763428f, -1.142211914f, -1.144287109f,
                                         1.144989014f,  1.144287109f,  1.142211914f,  1.138763428f,
                                         1.133926392f,  1.127746582f,  1.120223999f,  1.111373901f,
                                         1.101211548f,  1.089782715f,  1.077117920f,  1.063217163f,
                                         1.048156738f,  1.031936646f,  1.014617920f,  0.996246338f,
                                         0.976852417f,  0.956481934f,  0.935195923f,  0.913055420f,
                                         0.890090942f,  0.866363525f,  0.841949463f,  0.816864014f,
                                         0.791213989f,  0.765029907f,  0.738372803f,  0.711318970f,
                                         0.683914185f,  0.656219482f,  0.628295898f,  0.600219727f,
                                         0.572036743f,  0.543823242f,  0.515609741f,  0.487472534f,
                                         0.459472656f,  0.431655884f,  0.404083252f,  0.376800537f,
                                         0.349868774f,  0.323318481f,  0.297210693f,  0.271591187f,
                                         0.246505737f,  0.221984863f,  0.198059082f,  0.174789429f,
                                         0.152206421f,  0.130310059f,  0.109161377f,  0.088775635f,
                                         0.069168091f,  0.050354004f,  0.032379150f,  0.015228271f,
                                        -0.001068115f, -0.016510010f, -0.031082153f, -0.044784546f,
                                        -0.057617187f, -0.069595337f, -0.080688477f, -0.090927124f,
                                         0.100311279f,  0.108856201f,  0.116577148f,  0.123474121f,
                                         0.129577637f,  0.134887695f,  0.139450073f,  0.143264771f,
                                         0.146362305f,  0.148773193f,  0.150497437f,  0.151596069f,
                                         0.152069092f,  0.151962280f,  0.151306152f,  0.150115967f,
                                         0.148422241f,  0.146255493f,  0.143676758f,  0.140670776f,
                                         0.137298584f,  0.133590698f,  0.129562378f,  0.125259399f,
                                         0.120697021f,  0.115921021f,  0.110946655f,  0.105819702f,
                                         0.100540161f,  0.095169067f,  0.089706421f,  0.084182739f,
                                         0.078628540f,  0.073059082f,  0.067520142f,  0.061996460f,
                                         0.056533813f,  0.051132202f,  0.045837402f,  0.040634155f,
                                         0.035552979f,  0.030609131f,  0.025817871f,  0.021179199f,
                                         0.016708374f,  0.012420654f,  0.008316040f,  0.004394531f,
                                         0.000686646f, -0.002822876f, -0.006134033f, -0.009231567f,
                                        -0.012115479f, -0.014801025f, -0.017257690f, -0.019531250f,
                                        -0.021575928f, -0.023422241f, -0.025085449f, -0.026535034f,
                                        -0.027801514f, -0.028884888f, -0.029785156f, -0.030517578f,
                                         0.031082153f,  0.031478882f,  0.031738281f,  0.031845093f,
                                         0.031814575f,  0.031661987f,  0.031387329f,  0.031005859f,
                                         0.030532837f,  0.029937744f,  0.029281616f,  0.028533936f,
                                         0.027725220f,  0.026840210f,  0.025909424f,  0.024932861f,
                                         0.023910522f,  0.022857666f,  0.021789551f,  0.020690918f,
                                         0.019577026f,  0.018463135f,  0.017349243f,  0.016235352f,
                                         0.015121460f,  0.014022827f,  0.012939453f,  0.011886597f,
                                         0.010848999f,  0.009841919f,  0.008865356f,  0.007919312f,
                                         0.007003784f,  0.006118774f,  0.005294800f,  0.004486084f,
                                         0.003723145f,  0.003005981f,  0.002334595f,  0.001693726f,
                                         0.001098633f,  0.000549316f,  0.000030518f, -0.000442505f,
                                        -0.000869751f, -0.001266479f, -0.001617432f, -0.001937866f,
                                        -0.002227783f, -0.002487183f, -0.002700806f, -0.002883911f,
                                        -0.003051758f, -0.003173828f, -0.003280640f, -0.003372192f,
                                        -0.003417969f, -0.003463745f, -0.003479004f, -0.003479004f,
                                        -0.003463745f, -0.003433228f, -0.003387451f, -0.003326416f,
                                         0.003250122f,  0.003173828f,  0.003082275f,  0.002990723f,
                                         0.002899170f,  0.002792358f,  0.002685547f,  0.002578735f,
                                         0.002456665f,  0.002349854f,  0.002243042f,  0.002120972f,
                                         0.002014160f,  0.001907349f,  0.001785278f,  0.001693726f,
                                         0.001586914f,  0.001480103f,  0.001388550f,  0.001296997f,
                                         0.001205444f,  0.001113892f,  0.001037598f,  0.000961304f,
                                         0.000885010f,  0.000808716f,  0.000747681f,  0.000686646f,
                                         0.000625610f,  0.000579834f,  0.000534058f,  0.000473022f,
                                         0.000442505f,  0.000396729f,  0.000366211f,  0.000320435f,
                                         0.000289917f,  0.000259399f,  0.000244141f,  0.000213623f,
                                         0.000198364f,  0.000167847f,  0.000152588f,  0.000137329f,
                                         0.000122070f,  0.000106812f,  0.000106812f,  0.000091553f,
                                         0.000076294f,  0.000076294f,  0.000061035f,  0.000061035f,
                                         0.000045776f,  0.000045776f,  0.000030518f,  0.000030518f,
                                         0.000030518f,  0.000030518f,  0.000015259f,  0.000015259f,
                                         0.000015259f,  0.000015259f,  0.000015259f,  0.000015259f
                                        };

        private static float[] SYNTH_COS64_TABLE = {
                                           5.0060299823519627260e-01f, 5.0241928618815567820e-01f, 5.0547095989754364798e-01f, 5.0979557910415917998e-01f,
                                           5.1544730992262455249e-01f, 5.2249861493968885462e-01f, 5.3104259108978413284e-01f, 5.4119610014619701222e-01f,
                                           5.5310389603444454210e-01f, 5.6694403481635768927e-01f, 5.8293496820613388554e-01f, 6.0134488693504528634e-01f,
                                           6.2250412303566482475e-01f, 6.4682178335999007679e-01f, 6.7480834145500567800e-01f, 7.0710678118654746172e-01f,
                                           7.4453627100229857749e-01f, 7.8815462345125020249e-01f, 8.3934964541552681272e-01f, 8.9997622313641556513e-01f,
                                           9.7256823786196078263e-01f, 1.0606776859903470633e+00f, 1.1694399334328846596e+00f, 1.3065629648763763537e+00f,
                                           1.4841646163141661852e+00f, 1.7224470982383341955e+00f, 2.0577810099534108446e+00f, 2.5629154477415054814e+00f,
                                           3.4076084184687189804e+00f, 5.1011486186891552563e+00f, 1.0190008123548032870e+01f
                                           };

        #endregion

        private List<float[]> synBuffer = new List<float[]>(2);
        private List<int> bufferOffset = new List<int>(2);

        private float[] _eq;

        internal StereoMode StereoMode { get; set; }

        internal LayerDecoderBase()
        {
            this.StereoMode = StereoMode.Both;
        }

        internal abstract int DecodeFrame(IMpegFrame frame, Span<float> ch0, Span<float> ch1);

        internal void SetEQ(float[] eq)
        {
            if(eq == null || eq.Length == 32)
            {
                _eq = eq;
            }
        }

        internal virtual void ResetForSeek()
        {
            synBuffer.Clear();
            bufferOffset.Clear();
        }

        protected void InversePolyPhase(int channel, in Span<float> data)
        {
            Span<float> ippuv = stackalloc float[512];
            Span<float> synBuf;

            int k;

            this.GetBufAndOffset(channel, out synBuf, out k);

            if(_eq != null)
            {
                if(Vector.IsHardwareAccelerated)
                {
                    var dataVec = new Vector<float>(data.Slice(0, 32));
                    var eqVec = new Vector<float>(_eq.AsSpan(0, 32));
                    dataVec *= eqVec;
                    dataVec.CopyTo(data);
                }
                else
                {
                    for(int i = 0; i < 32; i++)
                    {
                        data[i] *= _eq[i];
                    }
                }

            }

            this.DCT32(in data, in synBuf, k);

            this.BuildUVec(in ippuv, in synBuf, k);

            this.DewindowOutput(in ippuv, in data);
        }

        private void GetBufAndOffset(int channel, out Span<float> synBuf, out int k)
        {
            while(this.synBuffer.Count <= channel)
            {
                this.synBuffer.Add(new float[1024]);
            }

            while(this.bufferOffset.Count <= channel)
            {
                this.bufferOffset.Add(0);
            }

            synBuf = this.synBuffer[channel];
            k = this.bufferOffset[channel];

            k = k - 32 & 511;
            this.bufferOffset[channel] = k;
        }

        private void DCT32(in Span<float> _in, in Span<float> _out, int k)
        {
            int i;
            Span<float> ei32 = stackalloc float[16];
            Span<float> eo32 = stackalloc float[16];
            Span<float> oi32 = stackalloc float[16];
            Span<float> oo32 = stackalloc float[16];

            for(i = 0; i < 16; i++)
            {
                ei32[i] = _in[i] + _in[31 - i];
                oi32[i] = (_in[i] - _in[31 - i]) * SYNTH_COS64_TABLE[2 * i];
            }

            DCT16(ei32, eo32);
            DCT16(oi32, oo32);

            for(i = 0; i < 15; i++)
            {
                _out[2 * i + k] = eo32[i];
                _out[2 * i + 1 + k] = oo32[i] + oo32[i + 1];
            }
            _out[30 + k] = eo32[15];
            _out[31 + k] = oo32[15];
        }


        private void DCT16(in Span<float> _in, in Span<float> _out)
        {
            Span<float> ei16 = stackalloc float[8];
            Span<float> eo16 = stackalloc float[8];
            Span<float> oi16 = stackalloc float[8];
            Span<float> oo16 = stackalloc float[8];

            float a, b;

            for(int i = 0, k = 1; i < 8; ++i, k += 4)
            {
                a = _in[i];
                b = _in[15 - i];
                ei16[i] = a + b;
                oi16[i] = (a - b) * SYNTH_COS64_TABLE[k];
            }

            DCT8(ei16, eo16);
            DCT8(oi16, oo16);

            for(int i = 0, j = 0; i < 13;)
            {
                _out[i++] = eo16[j];
                _out[i++] = oo16[j++] + oo16[j];
            }

            _out[14] = eo16[7];
            _out[15] = oo16[7];
        }

        private void DCT8(in Span<float> _in, in Span<float> _out)
        {
            Span<float> ei8 = stackalloc float[4];
            Span<float> tmp8 = stackalloc float[6];
            Span<float> oi8 = stackalloc float[4];
            Span<float> oo8 = stackalloc float[4];

            /* Even indices */
            ei8[0] = _in[0] + _in[7];
            ei8[1] = _in[3] + _in[4];
            ei8[2] = _in[1] + _in[6];
            ei8[3] = _in[2] + _in[5];

            tmp8[0] = ei8[0] + ei8[1];
            tmp8[1] = ei8[2] + ei8[3];
            tmp8[2] = (ei8[0] - ei8[1]) * SYNTH_COS64_TABLE[7];
            tmp8[3] = (ei8[2] - ei8[3]) * SYNTH_COS64_TABLE[23];
            tmp8[4] = (float)((tmp8[2] - tmp8[3]) * INV_SQRT_2);

            _out[0] = tmp8[0] + tmp8[1];
            _out[2] = tmp8[2] + tmp8[3] + tmp8[4];
            _out[4] = (float)((tmp8[0] - tmp8[1]) * INV_SQRT_2);
            _out[6] = tmp8[4];

            /* Odd indices */
            oi8[0] = (_in[0] - _in[7]) * SYNTH_COS64_TABLE[3];
            oi8[1] = (_in[1] - _in[6]) * SYNTH_COS64_TABLE[11];
            oi8[2] = (_in[2] - _in[5]) * SYNTH_COS64_TABLE[19];
            oi8[3] = (_in[3] - _in[4]) * SYNTH_COS64_TABLE[27];

            tmp8[0] = oi8[0] + oi8[3];
            tmp8[1] = oi8[1] + oi8[2];
            tmp8[2] = (oi8[0] - oi8[3]) * SYNTH_COS64_TABLE[7];
            tmp8[3] = (oi8[1] - oi8[2]) * SYNTH_COS64_TABLE[23];
            tmp8[4] = tmp8[2] + tmp8[3];
            tmp8[5] = (float)((tmp8[2] - tmp8[3]) * INV_SQRT_2);

            oo8[0] = tmp8[0] + tmp8[1];
            oo8[1] = tmp8[4] + tmp8[5];
            oo8[2] = (float)((tmp8[0] - tmp8[1]) * INV_SQRT_2);
            oo8[3] = tmp8[5];

            _out[1] = oo8[0] + oo8[1];
            _out[3] = oo8[1] + oo8[2];
            _out[5] = oo8[2] + oo8[3];
            _out[7] = oo8[3];
        }

        private void BuildUVec(in Span<float> u_vec, in Span<float> cur_synbuf, int k)
        {
            int i, j, uvp = 0;

            for(j = 0; j < 8; j++)
            {
                for(i = 0; i < 16; i++)
                {
                    /* Copy first 32 elements */
                    u_vec[uvp + i] = cur_synbuf[k + i + 16];
                    u_vec[uvp + i + 17] = -cur_synbuf[k + 31 - i];
                }

                /* k wraps at the synthesis buffer boundary  */
                k = k + 32 & 511;

                for(i = 0; i < 16; i++)
                {
                    /* Copy next 32 elements */
                    u_vec[uvp + i + 32] = -cur_synbuf[k + 16 - i];
                    u_vec[uvp + i + 48] = -cur_synbuf[k + i];
                }
                u_vec[uvp + 16] = 0;

                /* k wraps at the synthesis buffer boundary  */
                k = k + 32 & 511;
                uvp += 64;
            }
        }

        private void DewindowOutput(in Span<float> u_vec, in Span<float> samples)
        {
            if(Vector.IsHardwareAccelerated)
            {
                for(int i = 0; i < 512; i+= 4)
                {
                    var uVec = new Vector4(u_vec.Slice(i, 4));
                    var deVec = new Vector4(DEWINDOW_TABLE.AsSpan(i, 4));
                    uVec *= deVec;
                    uVec.CopyTo(u_vec.Slice(i));
                }
            }
            else
            {
                for(int i = 0; i < 512; i++)
                {
                    u_vec[i] *= DEWINDOW_TABLE[i];
                }
            }

            for(int i = 0; i < 32; i++)
            {
                float sum = u_vec[i];

                for(int j = 1; j < 16; ++j)
                {
                    sum += u_vec[i + (j << 5)];
                }

                u_vec[i] = sum;
            }

            //u_vec.Slice(0, 32).CopyTo(samples);

            for(int i = 0; i < 32; i++)
            {
                samples[i] = u_vec[i];
            }
        }
    }
}
