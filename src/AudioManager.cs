using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Diagnostics;
using ManagedBass;
using ManagedBass.Fx;

namespace FpvDroneMod
{
    internal static class AudioManager
    {
        // Все значения в центах (100 центов = 1 полутон, 1200 центов = 1 октава).
        // Оба MP3 записаны на частоте ~297 Hz.
        // -830 центов от базы = ~170 Hz = холостые обороты дрона на земле.
        // +1070 центов от базы = максимальные обороты на скорости TMax.
        // При старте мотор "раскручивается" — начинаем с -1050 центов.

        private const float PITCH_IDLE_CENTS  = -830f;   // питч при speed=0
        private const float PITCH_MAX_CENTS   = +1070f;  // питч при speed=TMax
        private const float PITCH_START_CENTS = -1050f;  // питч в момент включения
        private const float PITCH_RANGE       = PITCH_MAX_CENTS - PITCH_IDLE_CENTS; // = 1900f

        // drone_start.mp3 = 4.206 сек
        private const float START_TOTAL       = 4.206f;
        private const float START_XFADE_BEGIN = 3.206f;  // начало кроссфейда (за 1 сек до конца)
        private const float START_XFADE_DUR   = 1.0f;    // длительность кроссфейда start→fly

        // drone_fly.mp3 = 94.198 сек
        private const float FLY_TOTAL         = 94.198f;
        private const float FLY_XFADE_BEGIN   = 92.198f; // начало кроссфейда (за 2 сек до конца)
        private const float FLY_XFADE_DUR     = 2.0f;    // длительность кроссфейда fly→fly_new

        // Раскрутка звука при старте
        private const float SPOOL_FADE_DUR    = 0.45f;   // за сколько секунд громкость 0→1 при старте

        // Затухание при краше / остановке
        private const float CRASH_FADE_MS     = 300f;    // мс
        private const float STOP_FADE_MS      = 500f;    // мс
        private const float CRASH_FREE_DELAY  = 0.35f;   // сек до вызова FreeStream после краша
        private const float STOP_FREE_DELAY   = 0.55f;   // сек до вызова FreeStream после стопа

        private struct AudioStream
        {
            public int    DecoderHandle; // BASS_STREAM_DECODE хэндл
            public int    FxHandle;      // BassFx.TempoCreate хэндл
            public float  BaseFreq;      // нативная частота файла (Гц)
            public bool   Active;
            public GCHandle PinnedData;  // удерживает byte[] от GC пока BASS читает память
        }

        private static bool   _initialized        = false;

        // Три слота: стартовый звук + два слота для кроссфейда fly→fly
        private static AudioStream _start;   // drone_start.mp3
        private static AudioStream _flyA;    // текущий drone_fly.mp3
        private static AudioStream _flyB;    // входящий при кроссфейде

        // Таймеры (реальное время, сек)
        private static float  _startElapsed       = 0f;
        private static float  _flyElapsed         = 0f;

        // Флаги состояния
        private static bool   _startPlaying       = false;
        private static bool   _startXfadeDone     = false; // start→fly переход выполнен
        private static bool   _flyXfadeScheduled  = false; // fly→fly переход запланирован

        // Громкости (0..1)
        private static float  _volStart           = 0f;
        private static float  _volFlyA            = 0f;
        private static float  _volFlyB            = 0f;

        // Затухание при краше/стопе
        private static bool   _fading             = false;
        private static float  _fadeElapsed        = 0f;
        private static float  _fadeDelay          = 0f;   // сколько ждать до FreeStream

        // Реальное время
        private static readonly Stopwatch _clock  = new Stopwatch();
        private static double _lastClockSec       = 0.0;

        public static void Init()
        {
            try
            {
                ExtractAndLoadNative();
                if (!Bass.Init(-1, 44100, DeviceInitFlags.Default))
                {
                    Log.Error("AudioManager: Bass.Init failed: " + Bass.LastError);
                    return;
                }
                Bass.Configure(Configuration.UpdatePeriod, 5);
                Bass.Configure(Configuration.UpdateThreads, 1);
                _clock.Start();
                _lastClockSec = 0.0;
                _initialized  = true;
                Log.Info("AudioManager: initialized OK");
            }
            catch (Exception ex)
            {
                Log.Error("AudioManager: Init exception", ex);
            }
        }

        public static void OnLaunch()
        {
            if (!_initialized) return;
            try
            {
                // Сброс всего
                FreeAll();
                _startElapsed      = 0f;
                _flyElapsed        = 0f;
                _startPlaying      = false;
                _startXfadeDone    = false;
                _flyXfadeScheduled = false;
                _volStart          = 0f;
                _volFlyA           = 0f;
                _volFlyB           = 0f;
                _fading            = false;
                _fadeElapsed       = 0f;
                _lastClockSec      = _clock.Elapsed.TotalSeconds;

                // Загрузить drone_start.mp3
                _start = CreateStreamFromEmbedded("drone_start.mp3");
                if (!_start.Active)
                {
                    Log.Error("AudioManager: failed to load drone_start.mp3");
                    return;
                }

                // Начальный питч = PITCH_START_CENTS (мотор ещё не раскрутился)
                SetPitchCents(ref _start, PITCH_START_CENTS);

                // Начальная громкость = 0 (нарастает во время раскрутки)
                Bass.ChannelSetAttribute(_start.FxHandle, ChannelAttribute.Volume, 0f);

                // Запустить
                Bass.ChannelPlay(_start.FxHandle, false);
                _startPlaying = true;

                Log.Info("AudioManager: OnLaunch OK, drone_start playing");
            }
            catch (Exception ex)
            {
                Log.Error("AudioManager: OnLaunch exception", ex);
            }
        }

        public static void Update(DroneState s)
        {
            if (!_initialized || (!_startPlaying && !_flyA.Active)) return;
            try
            {
                // ── 1. Реальное dt ──────────────────────────────────────────────
                double nowSec = _clock.Elapsed.TotalSeconds;
                float dt      = (float)(nowSec - _lastClockSec);
                _lastClockSec = nowSec;
                if (dt < 0f) dt = 0f;
                if (dt > 0.05f) dt = 0.05f;  // clamp против спайков

                // ── 2. Обработка затухания при краше/стопе ──────────────────────
                if (_fading)
                {
                    _fadeElapsed += dt;
                    if (_fadeElapsed >= _fadeDelay)
                    {
                        FreeAll();
                        _fading = false;
                    }
                    // Во время затухания питч не обновляем — звук уже тухнет
                    return;
                }

                // ── 3. Инкремент таймеров ────────────────────────────────────────
                if (_startPlaying) _startElapsed += dt;
                if (_flyA.Active) _flyElapsed += dt;

                // ── 4. Питч-движок ───────────────────────────────────────────────
                // Вычислить целевой питч на основе текущей скорости.
                // Квадратичная кривая: основной прирост питча после 60% скорости —
                // создаёт эффект "форсажа" при разгоне.
                float speed = s.V.Length();
                float speedRatio = Clamp01(speed / Config.TMax);
                float curve = speedRatio * speedRatio;
                float targetCents = PITCH_IDLE_CENTS + curve * PITCH_RANGE;

                // Во время раскрутки дрона (spool) интерполируем питч стартового
                // стрима от PITCH_START_CENTS до targetCents.
                // s.SpoolElapsed и s.Spooling выставляются в Main.OnTick.
                if (s.Spooling && _start.Active)
                {
                    float spoolT = Clamp01(s.SpoolElapsed / SPOOL_FADE_DUR);
                    float spoolPitch = PITCH_START_CENTS + spoolT * (targetCents - PITCH_START_CENTS);

                    // Громкость нарастает 0→1 за SPOOL_FADE_DUR секунд
                    _volStart = spoolT;
                    Bass.ChannelSetAttribute(_start.FxHandle, ChannelAttribute.Volume, _volStart);

                    // Питч стартового стрима — по кривой раскрутки
                    SetPitchCents(ref _start, spoolPitch);
                }
                else
                {
                    // После раскрутки — нормальный режим: питч по текущей скорости
                    if (_start.Active) SetPitchCents(ref _start, targetCents);
                }

                // Fly-стримы всегда получают питч по скорости
                if (_flyA.Active) SetPitchCents(ref _flyA, targetCents);
                if (_flyB.Active) SetPitchCents(ref _flyB, targetCents);

                // ── 5. Кроссфейд drone_start → drone_fly ────────────────────────
                if (_startPlaying && !_startXfadeDone)
                {
                    // Шаг 5а: загрузить _flyA в момент начала кроссфейда
                    if (_startElapsed >= START_XFADE_BEGIN && !_flyA.Active)
                    {
                        _flyA = CreateStreamFromEmbedded("drone_fly.mp3");
                        if (!_flyA.Active)
                        {
                            Log.Error("AudioManager: failed to load drone_fly.mp3 for crossfade");
                        }
                        else
                        {
                            SetPitchCents(ref _flyA, targetCents);
                            Bass.ChannelSetAttribute(_flyA.FxHandle, ChannelAttribute.Volume, 0f);
                            Bass.ChannelPlay(_flyA.FxHandle, false);
                            _flyElapsed = 0f;
                            _volFlyA = 0f;
                        }
                    }

                    // Шаг 5б: анимировать кроссфейд
                    if (_startElapsed >= START_XFADE_BEGIN && _flyA.Active)
                    {
                        float xT = Clamp01((_startElapsed - START_XFADE_BEGIN) / START_XFADE_DUR);

                        // drone_start тухнет 1→0
                        _volStart = 1f - xT;
                        Bass.ChannelSetAttribute(_start.FxHandle, ChannelAttribute.Volume, _volStart);

                        // drone_fly нарастает 0→1
                        _volFlyA = xT;
                        Bass.ChannelSetAttribute(_flyA.FxHandle, ChannelAttribute.Volume, _volFlyA);

                        // Кроссфейд завершён
                        if (xT >= 1f)
                        {
                            FreeStream(ref _start);
                            _startPlaying = false;
                            _startXfadeDone = true;
                            _volFlyA = 1f;
                            Bass.ChannelSetAttribute(_flyA.FxHandle, ChannelAttribute.Volume, 1f);
                            Log.Info("AudioManager: start→fly crossfade complete");
                        }
                    }
                }

                // ── 6. Зацикливание drone_fly → новый drone_fly ──────────────────
                if (_flyA.Active && !_flyXfadeScheduled && _flyElapsed >= FLY_XFADE_BEGIN)
                {
                    _flyXfadeScheduled = true;

                    // Загрузить новую копию drone_fly
                    _flyB = CreateStreamFromEmbedded("drone_fly.mp3");
                    if (!_flyB.Active)
                    {
                        Log.Error("AudioManager: failed to load drone_fly.mp3 for loop crossfade");
                        _flyXfadeScheduled = false;
                    }
                    else
                    {
                        SetPitchCents(ref _flyB, targetCents);
                        Bass.ChannelSetAttribute(_flyB.FxHandle, ChannelAttribute.Volume, 0f);
                        Bass.ChannelPlay(_flyB.FxHandle, false);
                        _volFlyB = 0f;
                        Log.Info("AudioManager: fly loop crossfade started");
                    }
                }

                // Анимировать кроссфейд fly→fly
                if (_flyXfadeScheduled && _flyA.Active && _flyB.Active)
                {
                    float xT = Clamp01((_flyElapsed - FLY_XFADE_BEGIN) / FLY_XFADE_DUR);

                    _volFlyA = 1f - xT;
                    Bass.ChannelSetAttribute(_flyA.FxHandle, ChannelAttribute.Volume, _volFlyA);

                    _volFlyB = xT;
                    Bass.ChannelSetAttribute(_flyB.FxHandle, ChannelAttribute.Volume, _volFlyB);

                    if (xT >= 1f)
                    {
                        // Старый flyA → в мусор, flyB становится flyA
                        FreeStream(ref _flyA);
                        _flyA = _flyB;
                        _flyB = default;
                        _volFlyA = 1f;
                        _volFlyB = 0f;
                        _flyElapsed = 0f;       // сброс таймера для следующего цикла
                        _flyXfadeScheduled = false;
                        Bass.ChannelSetAttribute(_flyA.FxHandle, ChannelAttribute.Volume, 1f);
                        Log.Info("AudioManager: fly loop crossfade complete");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("AudioManager: Update exception", ex);
            }
        }

        public static void OnCrash()
        {
            if (!_initialized) return;
            try
            {
                // Быстрое затухание за 300 мс
                SlideVolumeToZero(CRASH_FADE_MS);
                _fading = true;
                _fadeElapsed = 0f;
                _fadeDelay = CRASH_FREE_DELAY;

                // Сброс флагов — новые события не обрабатываем
                _startPlaying = false;
                _startXfadeDone = false;
                _flyXfadeScheduled = false;

                Log.Info("AudioManager: OnCrash, fading out");
            }
            catch (Exception ex)
            {
                Log.Error("AudioManager: OnCrash exception", ex);
            }
        }

        public static void OnStop()
        {
            if (!_initialized) return;
            try
            {
                // Плавное затухание за 500 мс
                SlideVolumeToZero(STOP_FADE_MS);
                _fading = true;
                _fadeElapsed = 0f;
                _fadeDelay = STOP_FREE_DELAY;

                _startPlaying = false;
                _startXfadeDone = false;
                _flyXfadeScheduled = false;

                Log.Info("AudioManager: OnStop, fading out");
            }
            catch (Exception ex)
            {
                Log.Error("AudioManager: OnStop exception", ex);
            }
        }

        // Применить затухание ко всем активным стримам через BASS Slide
        private static void SlideVolumeToZero(float durationMs)
        {
            int ms = (int)durationMs;
            if (_start.Active)
                Bass.ChannelSlideAttribute(_start.FxHandle, ChannelAttribute.Volume, 0f, ms);
            if (_flyA.Active)
                Bass.ChannelSlideAttribute(_flyA.FxHandle, ChannelAttribute.Volume, 0f, ms);
            if (_flyB.Active)
                Bass.ChannelSlideAttribute(_flyB.FxHandle, ChannelAttribute.Volume, 0f, ms);
        }

        // Освободить все стримы
        private static void FreeAll()
        {
            FreeStream(ref _start);
            FreeStream(ref _flyA);
            FreeStream(ref _flyB);
        }

        // Освободить один стрим
        private static void FreeStream(ref AudioStream stream)
        {
            if (!stream.Active) return;
            try
            {
                Bass.ChannelStop(stream.FxHandle);
                Bass.StreamFree(stream.FxHandle);
                Bass.StreamFree(stream.DecoderHandle);
                if (stream.PinnedData.IsAllocated)
                    stream.PinnedData.Free();
            }
            catch (Exception ex)
            {
                Log.Error("AudioManager: FreeStream exception", ex);
            }
            stream = default;
        }

        // Перевод центов → полутона → применить к стриму.
        // ВАЖНО: ChannelAttribute.Pitch принимает значение в ПОЛУТОНАХ.
        // ВАЖНО: вызывать только если stream.Active == true.
        // ВАЖНО: темп (скорость воспроизведения) НЕ ТРОГАЕМ — всегда 0.
        private static void SetPitchCents(ref AudioStream stream, float cents)
        {
            if (!stream.Active) return;
            float semitones = cents / 100f;
            Bass.ChannelSetAttribute(stream.FxHandle, ChannelAttribute.Pitch, semitones);
        }

        // Создать стрим из embedded-ресурса (MP3 из памяти, без файла на диске)
        private static AudioStream CreateStreamFromEmbedded(string resourceName)
        {
            byte[] data = ReadEmbeddedResource(resourceName);
            if (data == null)
            {
                Log.Error("AudioManager: embedded resource not found: " + resourceName);
                return default;
            }

            // Закрепляем массив в памяти — GC не должен его перемещать
            // пока BASS держит указатель на этот буфер
            GCHandle pin = GCHandle.Alloc(data, GCHandleType.Pinned);
            IntPtr ptr = pin.AddrOfPinnedObject();

            int dec = Bass.CreateStream(ptr, 0, data.Length,
                                        BassFlags.Decode | BassFlags.Float);
            if (dec == 0)
            {
                pin.Free();
                Log.Error($"AudioManager: Bass.CreateStream failed for {resourceName}: {Bass.LastError}");
                return default;
            }

            int fx = BassFx.TempoCreate(dec, BassFlags.Default);
            if (fx == 0)
            {
                pin.Free();
                Bass.StreamFree(dec);
                Log.Error($"AudioManager: BassFx.TempoCreate failed for {resourceName}: {Bass.LastError}");
                return default;
            }

            Bass.ChannelGetInfo(fx, out ChannelInfo info);

            // Темп = 0% (скорость воспроизведения не меняется, только питч)
            Bass.ChannelSetAttribute(fx, ChannelAttribute.Tempo, 0f);

            // Быстрый алгоритм питч-шифтинга — меньше CPU на игровой машине
            Bass.ChannelSetAttribute(fx, ChannelAttribute.TempoUseQuickAlgorithm, 1f);

            return new AudioStream
            {
                DecoderHandle = dec,
                FxHandle = fx,
                BaseFreq = info.Frequency,
                PinnedData = pin,
                Active = true
            };
        }

        // Прочитать embedded-ресурс по имени (LogicalName из .csproj)
        private static byte[] ReadEmbeddedResource(string name)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream s = asm.GetManifestResourceStream(name))
            {
                if (s == null) return null;
                byte[] buf = new byte[s.Length];
                s.Read(buf, 0, buf.Length);
                return buf;
            }
        }

        // Извлечь нативные DLL во временную папку и загрузить через Bass.Load
        private static void ExtractAndLoadNative()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "FpvDroneMod");
            Directory.CreateDirectory(tempDir);

            foreach (string dllName in new[] { "bass.dll", "bass_fx.dll" })
            {
                string destPath = Path.Combine(tempDir, dllName);
                if (!File.Exists(destPath))
                {
                    byte[] data = ReadEmbeddedResource(dllName);
                    if (data == null)
                        throw new Exception($"AudioManager: embedded native dll not found: {dllName}");
                    File.WriteAllBytes(destPath, data);
                }
            }

            // Загрузить нативные dll до Bass.Init()
            Bass.Load(Path.Combine(tempDir, "bass.dll"));
            BassFx.Load(Path.Combine(tempDir, "bass_fx.dll"));
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
