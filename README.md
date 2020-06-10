# NetSharp

A (somewhat) performant network library for TCP and UDP written over raw sockets and SocketAsyncEventArgs in C#, geared towards asynchronous and concurrent applications.

For help with the library, feel free to visit our Discord and ask your questions [here](https://discord.gg/DKQhxuY).

| Service | Status Badge |
| ------- | ------------ |
| Discord | [![Discord Server][discord-server-badge]](https://discord.gg/DKQhxuY) |

## License

This library is under the GNU General Public License v3.0. What does this mean for you?

You can use this library for: 'Commercial Use', 'Private Use', and 'Patent Use'. You can make any modifications you want and you can distribute it as-is or with your modifications.

When using this library in your project you must: 'Disclose the source' (a link to this GitHub is fine), 'Include a copy of the GNU GPL v3.0 with your project' (copy LICENSE to your projects root dir, named NetSharp-LICENSE), and 'State changes made' (documented somewhere accessible by users of your project). Should you modify this library in any way, the modified library must be released under the GNU GPL v3.0 license as well. Note that your own project doesn't have to be released under the GNU GPL v3.0, only your edited version of this library does.

No warranty is provided, and contributors are not liable to you (the user of the library) for damages incurred through the use of this library. See LICENSE; 'Terms and Conditions' points 15 and 16 ('Disclaimer of Warranty' and 'Limitation of Liability respectively') for more information.

## Examples & Benchmarks

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
