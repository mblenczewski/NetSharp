using System;
using System.Diagnostics;

namespace NetSharpExamples
{
    public class BenchmarkHelper
    {
        private readonly Stopwatch rttStopwatch = new Stopwatch();
        private readonly Stopwatch bandwidthStopwatch = new Stopwatch();

        private long minRttTicks = int.MaxValue, maxRttTicks = int.MinValue;
        private long minRttMs = int.MaxValue, maxRttMs = int.MinValue;

        public void StartRttStopwatch()
        {
            rttStopwatch.Start();
        }

        public void StopRttStopwatch()
        {
            rttStopwatch.Stop();
        }

        public void ResetRttStopwatch()
        {
            rttStopwatch.Reset();
        }

        public void UpdateRttStats(int clientId)
        {
            minRttTicks = rttStopwatch.ElapsedTicks < minRttTicks
                ? rttStopwatch.ElapsedTicks
                : minRttTicks;

            minRttMs = rttStopwatch.ElapsedMilliseconds < minRttMs
                ? rttStopwatch.ElapsedMilliseconds
                : minRttMs;

            maxRttTicks = rttStopwatch.ElapsedTicks > maxRttTicks
                ? rttStopwatch.ElapsedTicks
                : maxRttTicks;

            maxRttMs = rttStopwatch.ElapsedMilliseconds > maxRttMs
                ? rttStopwatch.ElapsedMilliseconds
                : maxRttMs;
        }

        public long RttTicks
        {
            get { return rttStopwatch.ElapsedTicks; }
        }

        public long RttMs
        {
            get { return rttStopwatch.ElapsedMilliseconds; }
        }

        public void PrintRttStats(int clientId)
        {
            lock (typeof(Console))
            {
                Console.WriteLine($"[Client {clientId}] Min RTT: {minRttTicks} ticks, {minRttMs} ms");
                Console.WriteLine($"[Client {clientId}] Max RTT: {maxRttTicks} ticks, {maxRttMs} ms");
            }
        }

        public void StartBandwidthStopwatch()
        {
            bandwidthStopwatch.Start();
        }

        public void StopBandwidthStopwatch()
        {
            bandwidthStopwatch.Stop();
        }

        public void ResetBandwidthStopwatch()
        {
            bandwidthStopwatch.Reset();
        }

        public void PrintBandwidthStats(int clientId, long sentPacketCount, long packetSize)
        {
            long millis = bandwidthStopwatch.ElapsedMilliseconds;
            double megabytes = sentPacketCount * packetSize / 1_000_000.0;
            double bandwidth = megabytes / (millis / 1000.0);

            lock (typeof(Console))
            {
                Console.WriteLine($"[Client {clientId}] Sent {sentPacketCount} packets (of size {packetSize}) in {millis} milliseconds");
                Console.WriteLine($"[Client {clientId}] Approximate bandwidth: {bandwidth:F3} MBps");
            }
        }

        public double CalcBandwidth(long sentPacketCount, long packetSize)
        {
            long millis = bandwidthStopwatch.ElapsedMilliseconds;
            double megabytes = sentPacketCount * packetSize / 1_000_000.0;
            double bandwidth = megabytes / (millis / 1000.0);

            return bandwidth;
        }
    }
}