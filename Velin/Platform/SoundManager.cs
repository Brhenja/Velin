using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Velin.Services
{
    // Sonido de click. Si hacés click varias veces seguidas los sonidos se SUPERPONEN (se mezclan con un mezclador de NAudio);
    // System.Media.SoundPlayer no sirve para esto porque usa PlaySound, que sólo deja sonar un sonido por vez.
    // El dispositivo de audio se abre únicamente si el click está activado, y el sonido se carga una sola vez en memoria (pesa 2 KB).
    public static class SoundManager
    {
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int MaxVoices = 16;      // tope de clicks sonando a la vez (más no se nota y sólo gasta CPU)
        private const float VoiceGain = 0.8f;  // un poco menos de volumen por click para que varios juntos no distorsionen

        private static bool _clickEnabled;
        private static bool _filterInstalled;
        private static WaveOutEvent? _device;
        private static MixingSampleProvider? _mixer;
        private static float[]? _clickSamples;
        private static int _activeVoices;

        public static bool ClickEnabled => _clickEnabled;

        // Activa o desactiva el sonido de click (se puede llamar en cualquier momento, desde el hilo de la interfaz).
        public static void SetClickEnabled(bool enabled)
        {
            _clickEnabled = enabled;
            if (enabled)
            {
                if (_device == null) OpenDevice();
                if (!_filterInstalled) { Application.AddMessageFilter(new ClickSoundFilter()); _filterInstalled = true; }
            }
            else CloseDevice();   // sin click no hace falta tener el audio abierto
        }

        private static void OpenDevice()
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "Assets", "click.wav");
                if (!File.Exists(path)) return;

                using var reader = new AudioFileReader(path);
                var samples = new List<float>();
                var buffer = new float[4096];
                int read;
                while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                    for (int i = 0; i < read; i++) samples.Add(buffer[i] * VoiceGain);
                if (samples.Count == 0) return;

                var mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(reader.WaveFormat.SampleRate, reader.WaveFormat.Channels))
                {
                    ReadFully = true   // sigue "sonando" silencio cuando no hay nada, así el dispositivo no se detiene
                };
                mixer.MixerInputEnded += (s, e) => Interlocked.Decrement(ref _activeVoices);

                // Latencia baja: el click tiene que sonar casi en el momento en que lo tocás.
                var device = new WaveOutEvent { DesiredLatency = 80, NumberOfBuffers = 3 };
                device.Init(mixer);
                device.Play();

                _clickSamples = samples.ToArray();
                _mixer = mixer;
                _device = device;
            }
            catch
            {
                CloseDevice();   // si el audio falla (poco común), el launcher sigue andando sin sonido
            }
        }

        private static void CloseDevice()
        {
            try { _device?.Stop(); _device?.Dispose(); } catch { }
            _device = null;
            _mixer = null;
            _clickSamples = null;
            _activeVoices = 0;
        }

        public static void PlayClick()
        {
            var mixer = _mixer;
            var samples = _clickSamples;
            if (!_clickEnabled || mixer == null || samples == null) return;
            if (Volatile.Read(ref _activeVoices) >= MaxVoices) return;

            Interlocked.Increment(ref _activeVoices);
            try { mixer.AddMixerInput(new ClickVoice(samples, mixer.WaveFormat)); }
            catch { Interlocked.Decrement(ref _activeVoices); }
        }

        // Un click sonando: lee del mismo arreglo de muestras (compartido) desde el principio y termina solo.
        private sealed class ClickVoice : ISampleProvider
        {
            private readonly float[] _data;
            private int _position;

            public ClickVoice(float[] data, WaveFormat format)
            {
                _data = data;
                WaveFormat = format;
            }

            public WaveFormat WaveFormat { get; }

            public int Read(float[] buffer, int offset, int count)
            {
                int n = Math.Min(count, _data.Length - _position);
                if (n <= 0) return 0;
                Array.Copy(_data, _position, buffer, offset, n);
                _position += n;
                return n;
            }
        }

        private class ClickSoundFilter : IMessageFilter
        {
            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg == WM_LBUTTONDOWN) PlayClick();
                return false; // nunca bloqueamos el mensaje, sólo escuchamos
            }
        }
    }
}
