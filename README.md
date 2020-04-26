# NetSharp
A (somewhat) performant network library for TCP and UDP written over raw sockets and SocketAsyncEventArgs in C#, geared towards asynchronous and concurrent applications.

For help with the library, feel free to visit our Discord and ask your questions [here](https://discord.gg/DKQhxuY) (raw link: https://discord.gg/DKQhxuY).

| Service | Status Badge |
| ------- | ------------ |
| Discord | [![Discord Server][discord-server-badge]](https://discord.gg/DKQhxuY) |

## License
This library is under the GNU General Public License v3.0. What does this mean for you?

You can use this library for: 'Commercial Use', 'Private Use', and 'Patent Use'. You can make any modifications you want and you can distribute it as-is or with your modifications.

When using this library in your project you must: 'Disclose the source' (a link to this GitHub is fine), 'Include a copy of the GNU GPL v3.0 with your project' (copy LICENSE to your projects root dir, named NetSharp-LICENSE), and 'State changes made' (documented somewhere accessible by users of your project). Should you modify this library in any way, the modified library must be released under the GNU GPL v3.0 license as well. Note that your own project doesn't have to be released under the GNU GPL v3.0, only your edited version of this library does.

No warranty is provided, and contributors are not liable to you (the user of the library) for damages incurred through the use of this library. See LICENSE; 'Terms and Conditions' points 15 and 16 ('Disclaimer of Warranty' and 'Limitation of Liability respectively') for more information.

# Examples & Benchmarks
The library comes with a .NET Core 3.1 console project containing examples and benchmarks for the components of the library. The example project and a few benchmarks can be seen below:

## Example Project
![Example Project 'Main Menu'][example-project]

## Benchmarks

### Preface
All the benchmarks were performed on a Ryzen 7 1700 CPU @ 3.20GHz. This library is built to be concurrent and to scale well with the number of execution threads available. The per-core clocks are also important though (when are they not?).

During a benchmark 1,000,000 packets are sent per client by default, with a data segment size of 8192 bytes (8 kibibytes). This means that, by default, 8.192 gigabytes of user data are sent one way, and 8.192 are received (in the form of a response packet). The server is set up to simply echo any received packets, so packet processing overhead is virtually non-existent. Your mileage may vary.

The RTT values measured are the very extremes of what you would get. When printing the RTT-per-packet, the maximum value is attained at the start of the connection and the minimum value seems to be sustained for the remainder of the connection. Again, your mileage may vary depending on the speed of your network, your packet loss (if using TCP), and the amount of packet processing you do on the server.

### UDP Socket Server & Socket Client
The following is the UDP socket server benchmark:
![UDP Socket Server Benchmark][udp-server-benchmark]

The following is the synchronous UDP socket client benchmark:
![Synchronous UDP Socket Client Benchmark][udp-client-sync-benchmark]

The following is the asynchronous UDP socket client benchmark. Worse performance is expected when compared to the synchronous benchmark, due to the increased overhead of async operations:
![Asynchronous UDP Socket Client Benchmark][udp-client-async-benchmark]

The following is the TCP socket server benchmark:
![TCP Socket Server Benchmark][tcp-server-benchmark]

The following is the synchronous TCP socket client benchmark:
![Synchronous TCP Socket Client Benchmark][tcp-client-sync-benchmark]

The following is the asynchronous TCP socket client benchmark. Worse performance is expected when compared to the synchronous benchmark, due to the increased overhead of async operations:
![Asynchronous TCP Socket Client Benchmark][tcp-client-async-benchmark]

[discord-server-badge]: https://img.shields.io/discord/703255900600795196.svg?style=flat-square&logo=discord&color=blue

[example-project]: ./docs/netsharp-example-selector.png

[udp-server-benchmark]: ./docs/netsharp-udp-server-benchmark.png
[udp-client-sync-benchmark]: ./docs/netsharp-udp-client-sync-benchmark.png
[udp-client-async-benchmark]: ./docs/netsharp-udp-client-async-benchmark.png

[tcp-server-benchmark]: ./docs/netsharp-tcp-server-benchmark.png
[tcp-client-sync-benchmark]: ./docs/netsharp-tcp-client-sync-benchmark.png
[tcp-client-async-benchmark]: ./docs/netsharp-tcp-client-async-benchmark.png
