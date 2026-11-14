using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace IdolShowdownAccess
{
    /// <summary>
    /// Eigener kleiner Software-Mixer über die Windows-waveOut-Schnittstelle.
    /// Das Spiel nutzt Wwise und hat Unitys eigenes Audio abgeschaltet, deshalb
    /// erzeugen wir die Orientierungstöne selbst (Stereo, 44,1 kHz).
    /// </summary>
    internal static class Audio
    {
        private const int SampleRate = 44100;
        private const int FramesPerBuffer = 768;   // ca. 17 ms
        private const int BufferCount = 4;
        private const int WHDR_DONE = 0x1;

        private struct WAVEFORMATEX
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        private struct WAVEHDR
        {
            public IntPtr lpData;
            public uint dwBufferLength;
            public uint dwBytesRecorded;
            public IntPtr dwUser;
            public uint dwFlags;
            public uint dwLoops;
            public IntPtr lpNext;
            public IntPtr reserved;
        }

        [DllImport("winmm.dll")] private static extern int waveOutOpen(out IntPtr hWaveOut, IntPtr uDeviceID, ref WAVEFORMATEX lpFormat, IntPtr dwCallback, IntPtr dwInstance, uint dwFlags);
        [DllImport("winmm.dll")] private static extern int waveOutPrepareHeader(IntPtr hWaveOut, IntPtr lpWaveOutHdr, uint uSize);
        [DllImport("winmm.dll")] private static extern int waveOutUnprepareHeader(IntPtr hWaveOut, IntPtr lpWaveOutHdr, uint uSize);
        [DllImport("winmm.dll")] private static extern int waveOutWrite(IntPtr hWaveOut, IntPtr lpWaveOutHdr, uint uSize);
        [DllImport("winmm.dll")] private static extern int waveOutReset(IntPtr hWaveOut);
        [DllImport("winmm.dll")] private static extern int waveOutClose(IntPtr hWaveOut);

        private static IntPtr hWave = IntPtr.Zero;
        private static IntPtr[] headers;
        private static IntPtr[] datas;
        private static Thread thread;
        private static volatile bool running;
        private static readonly object voiceLock = new object();
        private static readonly List<Voice> voices = new List<Voice>();
        private static int flagsOffset;
        private static uint hdrSize;

        public static volatile float MasterVolume = 0.8f;
        public static bool Ready => running;

        public static void Init()
        {
            try
            {
                var fmt = new WAVEFORMATEX
                {
                    wFormatTag = 1,
                    nChannels = 2,
                    nSamplesPerSec = SampleRate,
                    wBitsPerSample = 16,
                    nBlockAlign = 4,
                    nAvgBytesPerSec = SampleRate * 4,
                    cbSize = 0
                };
                int r = waveOutOpen(out hWave, new IntPtr(0xFFFFFFFFL), ref fmt, IntPtr.Zero, IntPtr.Zero, 0);
                if (r != 0)
                {
                    r = waveOutOpen(out hWave, IntPtr.Zero, ref fmt, IntPtr.Zero, IntPtr.Zero, 0);
                    if (r != 0) { Plugin.Log.LogError("waveOutOpen fehlgeschlagen: " + r); return; }
                }
                hdrSize = (uint)Marshal.SizeOf(typeof(WAVEHDR));
                flagsOffset = (int)Marshal.OffsetOf(typeof(WAVEHDR), "dwFlags");
                headers = new IntPtr[BufferCount];
                datas = new IntPtr[BufferCount];
                int bytes = FramesPerBuffer * 4;
                for (int i = 0; i < BufferCount; i++)
                {
                    datas[i] = Marshal.AllocHGlobal(bytes);
                    var h = new WAVEHDR { lpData = datas[i], dwBufferLength = (uint)bytes };
                    headers[i] = Marshal.AllocHGlobal((int)hdrSize);
                    Marshal.StructureToPtr(h, headers[i], false);
                    waveOutPrepareHeader(hWave, headers[i], hdrSize);
                    // als "fertig" markieren, damit der Thread sofort befüllt
                    Marshal.WriteInt32(headers[i], flagsOffset, Marshal.ReadInt32(headers[i], flagsOffset) | WHDR_DONE);
                }
                running = true;
                thread = new Thread(Loop) { IsBackground = true, Name = "ISAccessAudio", Priority = ThreadPriority.AboveNormal };
                thread.Start();
                Plugin.Log.LogInfo("Audio-Engine gestartet.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Audio-Init fehlgeschlagen: " + e);
                running = false;
            }
        }

        public static void Shutdown()
        {
            if (!running) return;
            running = false;
            try
            {
                thread?.Join(200);
                waveOutReset(hWave);
                for (int i = 0; i < BufferCount; i++)
                {
                    waveOutUnprepareHeader(hWave, headers[i], hdrSize);
                    Marshal.FreeHGlobal(headers[i]);
                    Marshal.FreeHGlobal(datas[i]);
                }
                waveOutClose(hWave);
            }
            catch { }
        }

        private static void Loop()
        {
            var mix = new float[FramesPerBuffer * 2];
            var pcm = new short[FramesPerBuffer * 2];
            while (running)
            {
                bool wrote = false;
                for (int i = 0; i < BufferCount && running; i++)
                {
                    int flags = Marshal.ReadInt32(headers[i], flagsOffset);
                    if ((flags & WHDR_DONE) == 0) continue;
                    Array.Clear(mix, 0, mix.Length);
                    lock (voiceLock)
                    {
                        for (int v = voices.Count - 1; v >= 0; v--)
                        {
                            if (!voices[v].Render(mix, FramesPerBuffer)) voices.RemoveAt(v);
                        }
                    }
                    float mv = MasterVolume;
                    for (int s = 0; s < mix.Length; s++)
                    {
                        float x = mix[s] * mv;
                        // weiches Clipping
                        if (x > 1f) x = 1f; else if (x < -1f) x = -1f;
                        pcm[s] = (short)(x * 30000f);
                    }
                    Marshal.Copy(pcm, 0, datas[i], pcm.Length);
                    Marshal.WriteInt32(headers[i], flagsOffset, flags & ~WHDR_DONE);
                    waveOutWrite(hWave, headers[i], hdrSize);
                    wrote = true;
                }
                if (!wrote) Thread.Sleep(2);
            }
        }

        private static void Add(Voice v)
        {
            if (!running) return;
            lock (voiceLock)
            {
                if (voices.Count > 48) voices.RemoveAt(0);
                voices.Add(v);
            }
        }

        /// <summary>Klingt gerade ein Mod-Ton? (für "Ducking bei Tönen")</summary>
        public static bool Busy { get { lock (voiceLock) return voices.Count > 0; } }

        public static void StopAll()
        {
            lock (voiceLock) voices.Clear();
        }

        // ---------------- Klangbausteine ----------------

        public enum Wave { Sine, Triangle, Square, Soft, Noise }

        /// <summary>Kurzer Klang mit Hüllkurve, optional mit Tonhöhen- und Panoramaverlauf.
        /// Bei Wave.Noise ist freq die Grenzfrequenz des Tiefpasses (Rauschen, "Wusch"/"Klopfen").</summary>
        public static void Tone(float freq, float durationSec, float pan, float volume, Wave wave = Wave.Sine, float freqEnd = -1f, float delaySec = 0f, float attack = 0.004f, float panEnd = float.NaN)
        {
            Add(new ToneVoice
            {
                f0 = freq,
                f1 = freqEnd > 0 ? freqEnd : freq,
                len = Math.Max(1, (int)(durationSec * SampleRate)),
                delay = (int)(delaySec * SampleRate),
                pan0 = Clamp(pan, -1f, 1f),
                pan1 = float.IsNaN(panEnd) ? Clamp(pan, -1f, 1f) : Clamp(panEnd, -1f, 1f),
                vol = volume,
                wave = wave,
                attack = Math.Max(1, (int)(attack * SampleRate))
            });
        }

        private static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v);

        private abstract class Voice
        {
            /// <returns>false, wenn die Stimme fertig ist.</returns>
            public abstract bool Render(float[] buf, int frames);
        }

        private sealed class ToneVoice : Voice
        {
            public float f0, f1, pan0, pan1, vol;
            public int len, delay, attack, pos;
            public Wave wave;
            private double phase;
            private float lp, lp2;
            private uint rng = 2463534242u;

            public override bool Render(float[] buf, int frames)
            {
                for (int i = 0; i < frames; i++)
                {
                    if (delay > 0) { delay--; continue; }
                    if (pos >= len) return false;
                    float t = (float)pos / len;
                    float f = f0 + (f1 - f0) * t;
                    float s;
                    if (wave == Wave.Noise)
                    {
                        rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                        float white = (rng / 4294967296f) * 2f - 1f;
                        float a = Clamp((float)(2 * Math.PI * f / SampleRate), 0.001f, 1f);
                        lp += a * (white - lp);
                        lp2 += a * (lp - lp2);
                        s = lp2 * Math.Min(8f, 1.3f / (float)Math.Sqrt(a)); // Lautheit unabhängig von der Filterfrequenz
                    }
                    else
                    {
                        phase += f / SampleRate;
                        if (phase > 1.0) phase -= Math.Floor(phase);
                        switch (wave)
                        {
                            case Wave.Triangle: s = (float)(4.0 * Math.Abs(phase - 0.5) - 1.0); break;
                            case Wave.Square: s = phase < 0.5 ? 0.6f : -0.6f; break;
                            case Wave.Soft: s = (float)(Math.Sin(2 * Math.PI * phase) * 0.8 + Math.Sin(4 * Math.PI * phase) * 0.35); break;
                            default: s = (float)Math.Sin(2 * Math.PI * phase); break;
                        }
                    }
                    float env;
                    if (pos < attack) env = (float)pos / attack;
                    else
                    {
                        float rel = (float)(pos - attack) / Math.Max(1, len - attack);
                        env = (1f - rel) * (1f - rel);
                    }
                    s *= env * vol;
                    // Gleichleistungs-Panorama (mit optionalem Verlauf)
                    float pan = pan0 + (pan1 - pan0) * t;
                    double angle = (pan + 1.0) * Math.PI / 4.0;
                    buf[i * 2] += s * (float)Math.Cos(angle) * 1.2f;
                    buf[i * 2 + 1] += s * (float)Math.Sin(angle) * 1.2f;
                    pos++;
                }
                return pos < len || delay > 0;
            }
        }

        // ---------------- Fertige Signale ----------------
        // Jede Signalart hat einen eigenen Klangcharakter, nicht nur eine andere Tonhöhe.

        public static void MenuOpen() { Tone(660, 0.07f, 0, 0.35f, Wave.Triangle); Tone(990, 0.09f, 0, 0.35f, Wave.Triangle, -1, 0.07f); }
        public static void MenuClose() { Tone(990, 0.07f, 0, 0.35f, Wave.Triangle); Tone(660, 0.09f, 0, 0.35f, Wave.Triangle, -1, 0.07f); }
        public static void Click() { Tone(1200, 0.025f, 0, 0.25f, Wave.Triangle); }
        public static void Edge() { Tone(220, 0.06f, 0, 0.3f, Wave.Square); }

        /// <summary>Gegner am Boden: runder, kurzer "Plopp" (Glockenton).</summary>
        public static void EnemyGround(float pan) { Tone(620, 0.07f, pan, 0.5f, Wave.Soft, 560); }

        /// <summary>Gegner in der Luft: heller, nach oben gleitender "Zwitscher".</summary>
        public static void EnemyAir(float pan) { Tone(900, 0.09f, pan, 0.42f, Wave.Triangle, 1700); }

        /// <summary>Gegner direkt bei dir: kurzes, raues Schnarren in der Mitte.</summary>
        public static void EnemyClose() { Tone(180, 0.07f, 0, 0.45f, Wave.Square, 160); }

        /// <summary>Seitenwechsel: Rausch-"Wusch", der von der alten zur neuen Seite wandert.</summary>
        public static void SideSwitch(int newSide)
        {
            Tone(500, 0.32f, -newSide * 0.9f, 0.55f, Wave.Noise, 5000, 0f, 0.05f, newSide * 0.9f);
        }

        /// <summary>Wand/Ecke: zweimal dumpfes Klopfen auf der Seite der Wand.</summary>
        public static void WallKnock(float pan)
        {
            for (int i = 0; i < 2; i++)
            {
                Tone(350, 0.05f, pan, 0.8f, Wave.Noise, 200, i * 0.12f, 0.002f);
                Tone(95, 0.06f, pan, 0.5f, Wave.Sine, 70, i * 0.12f, 0.002f);
            }
        }

        /// <summary>Unverwundbar beginnt: helles, glitzerndes Aufsteigen (zwei Glockentöne).</summary>
        public static void InvulStart()
        {
            Tone(1320, 0.10f, 0, 0.35f, Wave.Sine, 1320, 0f, 0.003f);
            Tone(1980, 0.16f, 0, 0.35f, Wave.Sine, 2100, 0.07f, 0.003f);
        }

        /// <summary>Unverwundbarkeit endet: kurzer, leiser Ton abwärts.</summary>
        public static void InvulEnd() { Tone(1500, 0.08f, 0, 0.22f, Wave.Sine, 1000); }

        /// <summary>Super-Rüstung aktiv: metallisches "Kling".</summary>
        public static void ArmorStart()
        {
            Tone(880, 0.12f, 0, 0.3f, Wave.Square, 860);
            Tone(1310, 0.12f, 0, 0.2f, Wave.Square, 1290);
        }

        /// <summary>Viel Gesundheit auf einmal verloren: tiefer, abfallender Einschlag mit Rauschen.</summary>
        public static void BigHit()
        {
            Tone(420, 0.35f, 0, 0.6f, Wave.Soft, 70, 0f, 0.003f);
            Tone(2500, 0.2f, 0, 0.45f, Wave.Noise, 300, 0f, 0.002f);
        }

        /// <summary>Geschoss des Gegners: zwei harte, kurze Rechtecktöne von der Seite des Werfers.</summary>
        public static void ProjectileWarn(float pan)
        {
            Tone(1400, 0.045f, pan, 0.4f, Wave.Square, 1250, 0f, 0.002f);
            Tone(1400, 0.045f, pan, 0.4f, Wave.Square, 1250, 0.08f, 0.002f);
        }

        /// <summary>Herzschlag (lub-dub), mittig.</summary>
        public static void Heartbeat(float volume)
        {
            Tone(85, 0.11f, 0, volume, Wave.Soft, 60, 0f, 0.006f);
            Tone(70, 0.12f, 0, volume * 0.85f, Wave.Soft, 50, 0.16f, 0.006f);
        }
    }
}
