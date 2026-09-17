using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine.Profiling;
using Unity.Profiling;
using UnityEngine;
using System.Runtime.InteropServices;
namespace SsalMuk.Unity.Diagnostics
{
    public sealed class MetricsCollector : IDisposable
    {
        private readonly List<double> model = new List<double>(), views = new List<double>(), frames = new List<double>();
        private readonly Stopwatch wall = Stopwatch.StartNew();
        private ProfilerRecorder allocationCounter = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
        private int lastFrame = -1;
        private long allocated, peakManaged, peakUnity, peakWorkingSet;
        public long TickCount => model.Count;
        public void Tick(Action action)
        {
            long start = Stopwatch.GetTimestamp();
            action();
            model.Add((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
        }
        public void Render(Action action)
        {
            long start = Stopwatch.GetTimestamp();
            action();
            views.Add((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
        }
        public void Frame(double seconds)
        {
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (!allocationCounter.Valid) throw new InvalidOperationException("GC allocation counter is unavailable.");
            allocated += allocationCounter.LastValue;
            if (seconds > 0 && !double.IsInfinity(seconds)) frames.Add(seconds * 1000);
        }
        public void Memory()
        {
            peakManaged = Math.Max(peakManaged, Profiler.GetMonoUsedSizeLong());
            peakUnity = Math.Max(peakUnity, Profiler.GetTotalAllocatedMemoryLong());
            if (!GetProcessMemoryInfo(GetCurrentProcess(), out var memory, (uint)Marshal.SizeOf<ProcessMemory>()))
                throw new InvalidOperationException("Windows process memory counter is unavailable.");
            peakWorkingSet = Math.Max(peakWorkingSet, checked((long)memory.WorkingSetSize.ToUInt64()));
        }
        public MetricSummary Summary()
        {
            Memory();
            return new MetricSummary { tickCount = model.Count, renderCount = views.Count, allocatedBytes = allocated,
                peakManagedBytes = peakManaged, peakUnityBytes = peakUnity, peakWorkingSetBytes = peakWorkingSet,
                modelMeanMs = Mean(model), modelP95Ms = Percentile(model, .95), modelMaxMs = Percentile(model, 1),
                viewMeanMs = Mean(views), viewP95Ms = Percentile(views, .95), frameWallP95Ms = Percentile(frames, .95), wallSeconds = wall.Elapsed.TotalSeconds,
                allocationSource = "Unity GC Allocated In Frame; whole rendered frames, including Editor and diagnostics",
                memorySource = "Unity Mono used / Unity allocated / Windows GetProcessMemoryInfo working set" };
        }
        private static double Mean(List<double> values) { double sum = 0; foreach (double x in values) sum += x; return values.Count == 0 ? 0 : sum / values.Count; }
        private static double Percentile(List<double> values, double p) { if (values.Count == 0) return 0; var copy = values.ToArray(); Array.Sort(copy); return copy[Math.Min(copy.Length - 1, (int)Math.Ceiling(copy.Length * p) - 1)]; }
        public void Dispose() { allocationCounter.Dispose(); }
        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("psapi.dll", SetLastError = true)] private static extern bool GetProcessMemoryInfo(IntPtr process, out ProcessMemory counters, uint size);
        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessMemory
        {
            public uint Size, PageFaultCount;
            public UIntPtr PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage,
                QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage;
        }
    }
}
