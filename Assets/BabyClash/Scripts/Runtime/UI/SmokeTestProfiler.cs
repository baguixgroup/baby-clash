using System;
using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;

namespace BabyClash
{
    /// <summary>
    /// Performance investigation helper for <see cref="CommandLineSmokeTest"/> (<c>-profileMarkers</c>, development builds
    /// only): records the player-loop, script, UI, physics and rendering profiler markers and reports the most
    /// expensive ones per frame.
    /// </summary>
    public sealed class SmokeTestProfiler : IDisposable
    {
        static readonly string[] Prefixes =
        {
            "Initialization.", "EarlyUpdate.", "FixedUpdate.", "PreUpdate.", "Update.", "PreLateUpdate.", "PostLateUpdate.",
            "Gfx.", "Canvas.", "UGUI.", "Physics.", "ParticleSystem.", "Camera.", "Culling", "RenderGraph",
            "UniversalRenderPipeline", "Inl_", "ScriptableRenderer", "GC.", "BehaviourUpdate", "LateBehaviourUpdate",
            "FixedBehaviourUpdate", "CoroutinesDelayedCalls", "Layout", "Text.", "TextRendering", "Font.", "Skinning",
            "MeshSkinning", "Animation", "Director", "Audio", "Input", "Render", "Shadows", "Shader", "CreateGPUProgram",
        };

        readonly List<ProfilerRecorder> recorders = new();
        readonly List<string> names = new();
        readonly HashSet<string> known = new();
        readonly List<ProfilerRecorderHandle> handles = new();
        double[] sums = Array.Empty<double>();
        int frames;

        public int MarkerCount => recorders.Count;

        /// <summary>Starts recording every interesting marker registered since the last call.</summary>
        public void AddNewMarkers()
        {
            handles.Clear();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (ProfilerRecorderHandle handle in handles)
            {
                ProfilerRecorderDescription description = ProfilerRecorderHandle.GetDescription(handle);
                if (description.UnitType != ProfilerMarkerDataUnit.TimeNanoseconds || !IsInteresting(description.Name)
                    || !known.Add(description.Name))
                    continue;
                recorders.Add(ProfilerRecorder.StartNew(description.Category, description.Name, 1,
                    ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.WrapAroundWhenCapacityReached));
                names.Add(description.Name);
            }
            Array.Resize(ref sums, recorders.Count);
        }

        /// <summary>The most expensive markers of the last frame only (for hitches).</summary>
        public string TopOfLastFrame(int count)
        {
            var order = new int[recorders.Count];
            var values = new long[recorders.Count];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
                values[i] = recorders[i].LastValue;
            }
            Array.Sort(order, (a, b) => values[b].CompareTo(values[a]));
            var text = new StringBuilder();
            for (int i = 0; i < count && i < order.Length; i++)
                text.Append($"\n    {values[order[i]] / 1e6:0.0} ms  {names[order[i]]}");
            return text.ToString();
        }

        static bool IsInteresting(string name)
        {
            if (name.Contains("[Invoke]") || name.Contains("()"))
                return true;
            foreach (string prefix in Prefixes)
            {
                if (name.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>Call once per frame; accumulates the previous frame's values.</summary>
        public void Sample()
        {
            frames++;
            for (int i = 0; i < recorders.Count; i++)
                sums[i] += recorders[i].LastValue;
        }

        /// <summary>The most expensive markers since the last report, in ms per frame, then resets.</summary>
        public string Report(int count)
        {
            if (frames == 0 || recorders.Count == 0)
                return "no profiler markers recorded (development build needed)";
            var order = new int[recorders.Count];
            for (int i = 0; i < order.Length; i++)
                order[i] = i;
            Array.Sort(order, (a, b) => sums[b].CompareTo(sums[a]));
            var text = new StringBuilder();
            for (int i = 0; i < count && i < order.Length; i++)
            {
                int index = order[i];
                text.Append($"\n    {sums[index] / frames / 1e6:0.00} ms  {names[index]}");
            }
            Array.Clear(sums, 0, sums.Length);
            frames = 0;
            return text.ToString();
        }

        public void Dispose()
        {
            foreach (ProfilerRecorder recorder in recorders)
                recorder.Dispose();
            recorders.Clear();
        }
    }
}
