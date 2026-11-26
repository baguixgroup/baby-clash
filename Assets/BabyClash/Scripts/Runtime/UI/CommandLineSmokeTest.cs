using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BabyClash
{
    /// <summary>
    /// QA hook for builds: launch the game with <c>-smoketest &lt;folder&gt;</c> and it plays a CPU-vs-CPU match on its own,
    /// saves screenshots and a short log to the folder, then quits. Web builds use ?smoketest=1 and log to the console.
    /// </summary>
    public sealed class CommandLineSmokeTest : MonoBehaviour
    {
        const string Argument = "-smoketest";

        [SerializeField] GameFlow gameFlow;
        [SerializeField] RoundManager roundManager;

        [Tooltip("Real-time seconds between screenshots.")]
        [SerializeField] float screenshotInterval = 6f;

        [Tooltip("The run ends after this many real-time seconds, or when the match ends.")]
        [SerializeField] float maxDuration = 150f;

        [Tooltip("Frames longer than this (real-time seconds) are logged as hitches.")]
        [SerializeField] float hitchThreshold = 0.25f;

        [Tooltip("Real-time seconds between performance summaries in the log.")]
        [SerializeField] float perfInterval = 10f;

        readonly FrameTiming[] timing = new FrameTiming[1];
        string folder;
        float startTime;
        float lastFrameTime;
        float elapsed;
        float nextShot = 3f;
        float nextPerf;
        float readyTime = -1f;
        float worstFrame;
        int frames;
        int lastGcCount;
        bool shotLastFrame;
        // Performance window: frame count, slowest frame, and CPU/GPU time sums from FrameTimingManager.
        int windowFrames;
        int windowTimed;
        float windowStart;
        float windowTime;
        float windowWorst;
        double windowCpuMain;
        double windowCpuRender;
        double windowGpu;
        int windowGcStart;
        bool perfExperiment;
        SmokeTestProfiler profiler;
        int perfStep;
        string perfStepName = "baseline";
        int savedMsaa;
        float savedScale;
        float savedShadowDistance;
        int shotIndex;
        int hits;
        int throws;
        int shoves;
        int giggles;
        PowerUpSpawner powerUps;
        bool matchStarted;
        bool finishing;

        /// <summary>Scene wiring (editor tooling).</summary>
        public void Configure(GameFlow flow, RoundManager round)
        {
            gameFlow = flow;
            roundManager = round;
        }

        void Awake()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out Uri url) ||
                Array.IndexOf(url.Query.TrimStart('?').Split('&'), "smoketest=1") < 0)
            {
                enabled = false;
                return;
            }
#else
            folder = ReadFolderArgument();
            if (folder == null)
            {
                enabled = false;
                return;
            }
            Directory.CreateDirectory(folder);
#endif
            // The run must not stall when the window is behind a terminal.
            Application.runInBackground = true;
            PlayerProgress.UseThrowawayProfile(2000);
            startTime = lastFrameTime = windowStart = Time.realtimeSinceStartup;
            nextPerf = perfInterval;
            lastGcCount = windowGcStart = GC.CollectionCount(0);
            perfExperiment = Array.IndexOf(Environment.GetCommandLineArgs(), "-perfTest") >= 0;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-profileMarkers") >= 0)
            {
                profiler = new SmokeTestProfiler();
                profiler.AddNewMarkers();
            }
            Log($"Smoke test started. Unity {Application.unityVersion}, {Screen.width}x{Screen.height}, " +
                $"{SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), {SystemInfo.processorType}, " +
                $"quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}, vSync {QualitySettings.vSyncCount}");
        }

        void Start()
        {
            if (!enabled)
                return;
            foreach (BabyController baby in roundManager.AllBabies)
            {
                baby.HitReceived += (_, report) => hits++;
                baby.ThrewItem += (_, item) => throws++;
                baby.Shoved += (_, victim) => shoves++;
            }
            roundManager.PhaseChanged += phase => Log($"t={elapsed:0.0}s round {roundManager.RoundNumber}: {phase}");
            roundManager.MatchEnded += winner =>
            {
                string scores = "";
                foreach (BabyController baby in roundManager.Babies)
                    scores += $" {baby.DisplayName}={(roundManager.UsesScore ? roundManager.GetScore(baby) : roundManager.GetWins(baby)):0.0}";
                Log($"Match over. Winner: {(winner != null ? winner.DisplayName : "none")}. Throws {throws}, hits {hits}, shoves {shoves}, giggles {giggles}.{scores}");
                finishing = true;
                nextShot = elapsed + 4.5f;
            };
        }

        void Update()
        {
            float now = Time.realtimeSinceStartup;
            float frameTime = now - lastFrameTime;
            lastFrameTime = now;
            elapsed = now - startTime;
            frames++;
            worstFrame = Mathf.Max(worstFrame, frameTime);
            int gcCount = GC.CollectionCount(0);
            // The frame that saved a screenshot is the test's own cost: logged, but kept out of the performance numbers.
            bool screenshotFrame = shotLastFrame;
            if (frameTime > hitchThreshold && !screenshotFrame)
            {
                string cause = gcCount != lastGcCount ? " GC" : "";
                string markers = profiler != null ? profiler.TopOfLastFrame(6) : "";
                Log($"t={elapsed:0.0}s hitch: {frameTime * 1000f:0} ms frame ({roundManager.Phase}){cause}{markers}");
            }
            lastGcCount = gcCount;
            shotLastFrame = false;
            TrackPerformance(frameTime, now, gcCount, screenshotFrame);

            if (readyTime < 0f && gameFlow.IsReady)
            {
                readyTime = elapsed;
                Log($"t={elapsed:0.0}s loading done, title screen up");
            }
            if (!matchStarted && readyTime >= 0f && elapsed > readyTime + 2f)
            {
                // Leave the title screen for a real (non-endless) CPU vs CPU match.
                matchStarted = true;
                if (Argument2("-screens") != null)
                {
                    StartCoroutine(TourScreens());
                    return;
                }
                if (Argument2("-touch") != null)
                {
                    StartCoroutine(TouchTest());
                    return;
                }
                MatchSetup setup = RequestedSetup();
                gameFlow.StartMatch(setup);
                // Human slots are played by brains, so the whole rules/rewards path runs.
                foreach (BabyController baby in roundManager.Babies)
                {
                    baby.GetComponent<BabyInputHandler>().enabled = false;
                    var brain = baby.GetComponent<BabyBrain>();
                    if (setup.IsHuman(baby.PlayerIndex))
                        brain.Difficulty = Argument2("-weak") != null ? CpuDifficulty.Easy : CpuDifficulty.Hard;
                    brain.enabled = true;
                    baby.Giggled += (_, joker) => giggles++;
                }
                powerUps = FindAnyObjectByType<PowerUpSpawner>();
                if (powerUps != null)
                    powerUps.Used += (baby, kind) => Log($"t={elapsed:0.0}s {baby.DisplayName} used {kind}");
                Log($"Match started: {setup.Mode} in {setup.Room}, {setup.Count(false)} babies{(setup.Teams ? ", teams" : "")}{(setup.Level != null ? ", level " + setup.Level.Id : "")}.");
            }

            if (elapsed >= nextShot)
            {
                nextShot = elapsed + screenshotInterval;
                if (folder != null)
                {
                    string path = Path.Combine(folder, $"shot_{shotIndex++:00}.png");
                    ScreenCapture.CaptureScreenshot(path);
                    shotLastFrame = true;
                }
                if (finishing)
                {
                    Log($"Done. {frames} frames in {elapsed:0.0}s: average {frames / elapsed:0} fps, worst frame {worstFrame * 1000f:0} ms.");
                    if (folder != null)
                        Invoke(nameof(Quit), 1f);
                    enabled = false;
                }
            }

            if (elapsed > maxDuration && !finishing)
            {
                Log($"Time limit reached. Throws {throws}, hits {hits}, shoves {shoves}");
                finishing = true;
            }
        }

        static string Argument2(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, name);
            return at < 0 ? null : at + 1 < args.Length ? args[at + 1] : "";
        }

        /// <summary>The match asked for with -mode / -room / -players / -teams / -level, or a Hard duel.</summary>
        static MatchSetup RequestedSetup()
        {
            string level = Argument2("-level");
            if (level != null && BabyClash.Core.LevelCatalog.Find(level) != null)
                return MatchSetup.ForLevel(BabyClash.Core.LevelCatalog.Find(level));
            var setup = new MatchSetup { Slots = new[] { SlotDriver.Human, SlotDriver.CpuHard, SlotDriver.Off, SlotDriver.Off } };
            if (Enum.TryParse(Argument2("-mode") ?? "", out BabyClash.Core.GameMode mode))
                setup.Mode = mode;
            if (Enum.TryParse(Argument2("-room") ?? "", out BabyClash.Core.RoomKind room))
                setup.Room = room;
            if (int.TryParse(Argument2("-players") ?? "", out int players))
            {
                for (int i = 1; i < setup.Slots.Length; i++)
                    setup.Slots[i] = i < players ? SlotDriver.CpuNormal : SlotDriver.Off;
            }
            setup.Teams = Argument2("-teams") != null;
            return setup;
        }

        System.Collections.IEnumerator TourScreens()
        {
            foreach (string button in new[] { "Party", "Story", "ChallengeList", "Wardrobe" })
            {
                yield return Photograph("title_" + button, 0.1f);
                Press(button);
                yield return Photograph(button, 1.6f);
                if (button == "Wardrobe")
                {
                    Press("Item8");
                    yield return Photograph("wardrobe_crown", 0.6f);
                    Press("FACES");
                    Press("Item4");
                    yield return Photograph("wardrobe_shades", 0.6f);
                    Press("OUTFITS");
                    Press("Item6");
                    Press("Slot");
                    Press("HATS");
                    Press("Item4");
                    yield return Photograph("wardrobe_baby2_bunny", 1.4f);
                }
                Press("Back");
                yield return new WaitForSecondsRealtime(0.3f);
            }
            Log("Screens done.");
            Invoke(nameof(Quit), 1f);
        }

        /// <summary>A simulated finger drives baby 1: drag to walk right, tap to throw, tap to dash.</summary>
        System.Collections.IEnumerator TouchTest()
        {
            var screen = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Touchscreen>();
            gameFlow.StartMatch(MatchSetup.Duel(true, CpuDifficulty.Easy));
            BabyController baby = roundManager.AllBabies[0];
            roundManager.AllBabies[1].GetComponent<BabyBrain>().enabled = false;
            int thrown = 0, dashed = 0;
            baby.ThrewItem += (_, item) => thrown++;
            baby.Dashed += _ => dashed++;
            while (roundManager.Phase != BabyClash.Core.RoundPhase.Playing)
                yield return null;
            Vector3 start = baby.transform.position;
            var origin = new Vector2(Screen.width * 0.2f, Screen.height * 0.3f);
            Finger(screen, 1, UnityEngine.InputSystem.TouchPhase.Began, origin);
            yield return null;
            yield return null;
            Finger(screen, 1, UnityEngine.InputSystem.TouchPhase.Moved, origin + new Vector2(Screen.height * 0.12f, 0f));
            yield return Photograph("touch_walk", 0.7f);
            Log($"Touch: walked {baby.transform.position.x - start.x:0.0} m right");
            Finger(screen, 1, UnityEngine.InputSystem.TouchPhase.Ended, origin);
            yield return new WaitForSeconds(0.3f);
            yield return Tap(screen, 2, new Vector2(Screen.width * 0.88f, Screen.height * 0.22f));
            yield return new WaitForSeconds(0.6f);
            yield return Tap(screen, 3, new Vector2(Screen.width * 0.75f, Screen.height * 0.14f));
            yield return new WaitForSeconds(0.5f);
            Log($"Touch: throws {thrown}, dashes {dashed}");
            Invoke(nameof(Quit), 0.5f);
        }

        static System.Collections.IEnumerator Tap(UnityEngine.InputSystem.Touchscreen screen, int id, Vector2 position)
        {
            Finger(screen, id, UnityEngine.InputSystem.TouchPhase.Began, position);
            yield return null;
            yield return null;
            Finger(screen, id, UnityEngine.InputSystem.TouchPhase.Ended, position);
        }

        static void Finger(UnityEngine.InputSystem.Touchscreen screen, int id, UnityEngine.InputSystem.TouchPhase phase, Vector2 position) =>
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(screen, new UnityEngine.InputSystem.LowLevel.TouchState { touchId = id, phase = phase, position = position });

        System.Collections.IEnumerator Photograph(string shot, float wait)
        {
            yield return new WaitForSecondsRealtime(wait);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "screen_" + shot + ".png"));
            yield return new WaitForSecondsRealtime(0.3f);
        }

        void Press(string buttonName)
        {
            foreach (UnityEngine.UI.Button button in FindObjectsByType<UnityEngine.UI.Button>())
            {
                if (button.name != buttonName || !button.isActiveAndEnabled)
                    continue;
                button.onClick.Invoke();
                return;
            }
            Log("No active button " + buttonName);
        }

        void TrackPerformance(float frameTime, float now, int gcCount, bool screenshotFrame)
        {
            FrameTimingManager.CaptureFrameTimings();
            if (!screenshotFrame)
            {
                windowFrames++;
                windowTime += frameTime;
                windowWorst = Mathf.Max(windowWorst, frameTime);
                if (FrameTimingManager.GetLatestTimings(1, timing) == 1)
                {
                    windowTimed++;
                    windowCpuMain += timing[0].cpuMainThreadFrameTime;
                    windowCpuRender += timing[0].cpuRenderThreadFrameTime;
                    windowGpu += timing[0].gpuFrameTime;
                }
                profiler?.Sample();
            }
            if (elapsed < nextPerf || windowFrames == 0)
                return;

            float seconds = windowTime;
            string split = windowTimed > 0
                ? $"CPU main {windowCpuMain / windowTimed:0.0} ms, render thread {windowCpuRender / windowTimed:0.0} ms, GPU {windowGpu / windowTimed:0.0} ms"
                : "no frame timing data";
            Log($"t={elapsed:0.0}s perf [{perfStepName}]: {windowFrames / seconds:0.0} fps (avg {seconds * 1000f / windowFrames:0.0} ms, worst {windowWorst * 1000f:0} ms), " +
                $"{split}, {gcCount - windowGcStart} GCs");
            if (perfExperiment)
                perfStepName = ApplyPerfStep(++perfStep);
            if (profiler != null)
            {
                Log($"  top markers (ms per frame):{profiler.Report(15)}");
                // Scripts and systems register their markers on first use; pick up the new ones.
                profiler.AddNewMarkers();
            }
            nextPerf = elapsed + perfInterval;
            windowStart = now;
            windowTime = 0f;
            windowFrames = windowTimed = 0;
            windowWorst = 0f;
            windowCpuMain = windowCpuRender = windowGpu = 0;
            windowGcStart = gcCount;
        }

        /// <summary>
        /// With <c>-perfTest</c>, each performance window switches off one more rendering cost, then restores everything,
        /// so one run shows what each one costs on this machine.
        /// </summary>
        string ApplyPerfStep(int step)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
                return "no URP";
            if (step == 1)
            {
                savedMsaa = pipeline.msaaSampleCount;
                savedScale = pipeline.renderScale;
                savedShadowDistance = pipeline.shadowDistance;
            }
            switch (step)
            {
                case 1: return "baseline";
                case 2: SetAmbientOcclusion(pipeline, false); return "no SSAO";
                case 3: pipeline.msaaSampleCount = 1; return "no SSAO, no MSAA";
                case 4: pipeline.renderScale = 0.5f; return "no SSAO, no MSAA, half res";
                case 5: pipeline.shadowDistance = 0f; return "no SSAO, no MSAA, half res, no shadows";
                case 6:
                    SetAmbientOcclusion(pipeline, true);
                    pipeline.msaaSampleCount = savedMsaa;
                    pipeline.renderScale = savedScale;
                    pipeline.shadowDistance = savedShadowDistance;
                    return "baseline again";
                default: return "baseline again";
            }
        }

        static void SetAmbientOcclusion(UniversalRenderPipelineAsset pipeline, bool active)
        {
            foreach (ScriptableRendererData data in pipeline.rendererDataList)
            {
                if (data == null)
                    continue;
                foreach (ScriptableRendererFeature feature in data.rendererFeatures)
                {
                    if (feature is ScreenSpaceAmbientOcclusion)
                        feature.SetActive(active);
                }
            }
        }

        void OnApplicationFocus(bool focus)
        {
            if (enabled)
                Log($"t={elapsed:0.0}s window focus: {focus}");
        }

        void OnDestroy() => profiler?.Dispose();

        void Quit() => Application.Quit();

        void Log(string line)
        {
            Debug.Log("[SmokeTest] " + line);
            if (folder != null)
                File.AppendAllText(Path.Combine(folder, "smoketest.log"), line + Environment.NewLine);
        }

        static string ReadFolderArgument()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == Argument)
                    return args[i + 1];
            }
            return null;
        }
    }
}
