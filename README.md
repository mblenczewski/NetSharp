# NOTE: This library is deprecated, and will not receive any updates

# NetSharp

A networking library written in C#, focusing on performance and built on top of SocketAsyncEventArgs.

It is geared towards asynchronous and highly concurrent applications, which aim for maximum bandwidth.

For help with the library, feel free to visit our Discord and ask any questions [here](https://discord.gg/DKQhxuY).

| Service | Status Badge |
| ------- | ------------ |
| Code Quality | [![CodeFactor](https://www.codefactor.io/repository/github/mblenczewski/netsharp/badge/master)](https://www.codefactor.io/repository/github/mblenczewski/netsharp/overview/master) |

## Examples & Benchmarks (WARNING: STALE)
#### NOTE: I'll redo these when i have time, im currently reimplementing the library due to some issues i encountered. They are still up here because i want to know what i need to beat performance-wise, and what benchmarks i included

The library comes with a .NET Core 3.1 console project containing examples and benchmarks for the components of the library. The example project and a few benchmarks can be seen below:

### Example Project

![Example Project 'Main Menu'][example-project]

### Benchmarks

#### Preface

All the benchmarks were performed on a Ryzen 7 1700 CPU @ 3.20GHz. This library is built to be concurrent and to scale well with the number of execution threads available. The per-core clocks are also important though (when are they not?).

During a benchmark 1,000,000 packets are sent per client by default, with a data segment size of 8192 bytes (8 kibibytes). This means that, by default, 8.192 gigabytes of user data are sent one way, and 8.192 are received (in the form of a response packet). The server is set up to simply echo any received packets, so packet processing overhead is virtually non-existent. Your mileage may vary.

The RTT values measured are the very extremes of what you would get. When printing the RTT-per-packet, the maximum value is attained at the start of the connection and the minimum value seems to be sustained for the remainder of the connection. Again, your mileage may vary depending on the speed of your network, your packet loss (if using TCP), and the amount of packet processing you do on the server. The more processing you do, the longer your RTT times will be.

### Datagram Network Reader & Writer Benchmarks

The following is the datagram network listener (UDP) benchmark:

![Datagram Network Reader Benchmark][datagram-network-reader-benchmark]

The following are the synchronous and asynchronous datagram network writer (UDP) benchmarks. Worse performance is expected when compared to the synchronous benchmark, due to the increased overhead of async operations:

![Datagram Network Writer Benchmark][datagram-network-writer-benchmark]

### Stream Network Reader & Writer Benchmarks

The following is the stream network listener (TCP) benchmark:

![Stream Network Reader Benchmark][stream-network-reader-benchmark]

The following are the synchronous and asynchronous stream network writer (TCP) benchmarks. Worse performance is expected when compared to the synchronous benchmark, due to the increased overhead of async operations:

![Stream Network Writer Benchmark][stream-network-writer-benchmark]

[discord-server-badge]: https://img.shields.io/discord/703255900600795196.svg?style=flat-square&logo=discord&color=blue

[example-project]: docs/example-selector.png

[datagram-network-reader-benchmark]: docs/datagram-network-reader-benchmark.png
[datagram-network-writer-benchmark]: docs/datagram-network-writer-benchmark.png

[stream-network-reader-benchmark]: docs/stream-network-reader-benchmark.png
[stream-network-writer-benchmark]: docs/stream-network-writer-benchmark.png

