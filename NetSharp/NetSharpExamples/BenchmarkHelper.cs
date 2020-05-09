using System;
using System.Diagnostics;

namespace NetSharpExamples
{
    public class BenchmarkHelper
    {
        private readonly Stopwatch stopwatch = new Stopwatch();
        private long lastTicksSnapshot = 0, lastMsSnapshot = 0;
        private long minRttMs = int.MaxValue, maxRttMs = int.MinValue;
        private long minRttTicks = int.MaxValue, maxRttTicks = int.MinValue;

        public long RttMs
        {
            get { return stopwatch.ElapsedMilliseconds; }
        }

        public long RttTicks
        {
            get { return stopwatch.ElapsedTicks; }
        }

        public double CalcBandwidth(long sentPacketCount, long packetSize)
        {
            long millis = stopwatch.ElapsedMilliseconds;
            double megabytes = sentPacketCount * packetSize / 1_000_000.0;
            double bandwidth = megabytes / (millis / 1000.0);

            return bandwidth;
        }

        public void PrintBandwidthStats(int clientId, long sentPacketCount, long packetSize)
        {
            long millis = stopwatch.ElapsedMilliseconds;
            double megabytes = sentPacketCount * packetSize / 1_000_000.0;
            double bandwidth = megabytes / (millis / 1000.0);

            lock (typeof(Console))
            {
                Console.WriteLine($"[Client {clientId}] Sent {sentPacketCount} packets (of size {packetSize} bytes; {megabytes / 1000} gigabytes [one-way]) in {millis} milliseconds");
                Console.WriteLine($"[Client {clientId}] Approximate bandwidth: {bandwidth:F3} MBps");
            }
        }

        public void PrintRttStats(int clientId)
        {
            lock (typeof(Console))
            {
                Console.WriteLine($"[Client {clientId}] Min RTT: {minRttTicks} ticks, {minRttMs} ms");
                Console.WriteLine($"[Client {clientId}] Max RTT: {maxRttTicks} ticks, {maxRttMs} ms");
            }
        }

        public void ResetStopwatch()
        {
            lastTicksSnapshot = 0;
            lastMsSnapshot = 0;

            stopwatch.Reset();
        }

        public void SnapshotRttStats()
        {
            long elapsedTicksSnapshot = stopwatch.ElapsedTicks, elapsedMsSnapshot = stopwatch.ElapsedMilliseconds;

            minRttTicks = elapsedTicksSnapshot - lastTicksSnapshot < minRttTicks
                ? elapsedTicksSnapshot - lastTicksSnapshot
                : minRttTicks;

            minRttMs = elapsedMsSnapshot - lastMsSnapshot < minRttMs
                ? elapsedMsSnapshot - lastMsSnapshot
                : minRttMs;

            maxRttTicks = elapsedTicksSnapshot - lastTicksSnapshot > maxRttTicks
                ? elapsedTicksSnapshot - lastTicksSnapshot
                : maxRttTicks;

            maxRttMs = elapsedMsSnapshot - lastMsSnapshot > maxRttMs
                ? elapsedMsSnapshot - lastMsSnapshot
                : maxRttMs;

            lastTicksSnapshot = elapsedTicksSnapshot;
            lastMsSnapshot = elapsedMsSnapshot;
        }

        public void StartStopwatch()
        {
            stopwatch.Start();
        }

        public void StopStopwatch()
        {
            stopwatch.Stop();
        }
    }
}