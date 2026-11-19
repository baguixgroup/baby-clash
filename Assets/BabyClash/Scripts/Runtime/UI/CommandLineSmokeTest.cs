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
            foreach (BabyController baby in roundManager.Babies)
            {
                baby.HitReceived += (_, report) => hits++;
                baby.ThrewItem += (_, item) => throws++;
                baby.Shoved += (_, victim) => shoves++;
            }
            roundManager.PhaseChanged += phase => Log($"t={elapsed:0.0}s round {roundManager.RoundNumber}: {phase}");
            roundManager.MatchEnded += winner =>
            {
                Log($"Match over. Winner: {(winner != null ? winner.DisplayName : "none")}. Throws {throws}, hits {hits}, shoves {shoves}");
                finishing = true;
                nextShot = elapsed + 1.5f;
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
                gameFlow.StartMatch(false, CpuDifficulty.Hard);
                foreach (BabyInputHandler input in FindObjectsByType<BabyInputHandler>())
                    input.enabled = false;
                foreach (BabyBrain brain in FindObjectsByType<BabyBrain>())
                    brain.enabled = true;
                Log("CPU vs CPU match started.");
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
