using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Velin.Services
{
    // Centraliza música de fondo y sonido de click, pudiendo sonar los dos A LA VEZ.
    // (System.Media.SoundPlayer no sirve para esto: usa la API PlaySound de Windows,
    // que sólo deja reproducir un sonido por vez en todo el proceso — por eso antes
    // el click cortaba la música. Acá usamos un mezclador real de NAudio.)
    public static class SoundManager
    {
        public static bool MusicEnabled = true;
        public static bool ClickEnabled = true;

        private static WaveOutEvent? _device;
        private static MixingSampleProvider? _mixer;
        private static string _musicPath = "";
        private static string _clickPath = "";

        private static AudioFileReader? _musicReader;
        private static ISampleProvider? _musicInput;

        private static readonly Dictionary<ISampleProvider, AudioFileReader> _activeClicks = new();

        public static void Init()
        {
            _musicPath = Path.Combine(AppContext.BaseDirectory, "Assets", "velin_bgmusic.wav");
            _clickPath = Path.Combine(AppContext.BaseDirectory, "Assets", "click.wav");

            try
            {
                // Todos nuestros .wav están generados a 22050 Hz mono; el mezclador usa
                // ese mismo formato para que no haga falta re-muestrear nada.
                _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(22050, 1))
                {
                    ReadFully = true // sigue "sonando" silencio cuando no hay nada, así el device no se detiene
                };
                _mixer.MixerInputEnded += OnMixerInputEnded;

                _device = new WaveOutEvent();
                _device.Init(_mixer);
                _device.Play();
            }
            catch
            {
                // Si el dispositivo de audio falla (poco común), el launcher sigue andando mudo.
                _device = null;
                _mixer = null;
            }
        }

        private static void OnMixerInputEnded(object? sender, SampleProviderEventArgs e)
        {
            lock (_activeClicks)
            {
                if (_activeClicks.TryGetValue(e.SampleProvider, out var reader))
                {
                    _activeClicks.Remove(e.SampleProvider);
                    try { reader.Dispose(); } catch { }
                }
            }
        }

        public static void StartMusic()
        {
            if (!MusicEnabled || _mixer == null) return;
            StopMusic(); // por si ya había una instancia sonando
            try
            {
                if (!File.Exists(_musicPath)) return;
                _musicReader = new AudioFileReader(_musicPath);
                _musicInput = new LoopingSampleProvider(_musicReader);
                _mixer.AddMixerInput(_musicInput);
            }
            catch { _musicReader = null; _musicInput = null; }
        }

        public static void StopMusic()
        {
            try
            {
                if (_musicInput != null && _mixer != null) _mixer.RemoveMixerInput(_musicInput);
                _musicReader?.Dispose();
            }
            catch { }
            finally { _musicReader = null; _musicInput = null; }
        }

        public static void SetMusicEnabled(bool enabled)
        {
            MusicEnabled = enabled;
            if (enabled) StartMusic(); else StopMusic();
        }

        public static void SetClickEnabled(bool enabled) => ClickEnabled = enabled;

        public static void PlayClick()
        {
            if (!ClickEnabled || _mixer == null) return;
            try
            {
                if (!File.Exists(_clickPath)) return;
                var reader = new AudioFileReader(_clickPath);
                var sp = reader.ToSampleProvider();
                lock (_activeClicks) _activeClicks[sp] = reader;
                _mixer.AddMixerInput(sp);
            }
            catch { }
        }

        // Filtro global: reproduce el click en CUALQUIER clic izquierdo del mouse dentro
        // de una ventana de Velin, no sólo en botones.
        private const int WM_LBUTTONDOWN = 0x0201;

        private class ClickSoundFilter : IMessageFilter
        {
            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg == WM_LBUTTONDOWN) PlayClick();
                return false; // nunca bloqueamos el mensaje, sólo escuchamos
            }
        }

        public static void InstallGlobalClickSound() => Application.AddMessageFilter(new ClickSoundFilter());
    }

    // Envuelve un AudioFileReader para que, al llegar al final, vuelva al principio
    // en vez de terminar — así la música de fondo suena en loop sin cortes.
    internal class LoopingSampleProvider : ISampleProvider
    {
        private readonly AudioFileReader _reader;
        public LoopingSampleProvider(AudioFileReader reader) => _reader = reader;
        public WaveFormat WaveFormat => _reader.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = _reader.Read(buffer, offset + totalRead, count - totalRead);
                if (read == 0)
                {
                    if (_reader.Position == 0) break; // archivo vacío: evita loop infinito
                    _reader.Position = 0;
                    continue;
                }
                totalRead += read;
            }
            return totalRead;
        }
    }
}
