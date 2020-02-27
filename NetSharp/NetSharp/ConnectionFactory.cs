using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using NetSharp.Logging;

namespace NetSharp
{
    /// <summary>
    /// Provides methods to construct and configure <see cref="Connection"/> instances.
    /// </summary>
    public static class ConnectionFactory
    {
        public static Connection Init(EndPoint localEndPoint, AddressFamily connectionAddressFamily,
            SocketType connectionSocketType, ProtocolType connectionProtocolType, TimeSpan timeout, int objectPoolSize = 10,
            bool preallocateBuffers = false, Stream? loggingStream = default, LogLevel minimumLoggedSeverity = LogLevel.Info)
        {
            Connection connection = new Connection(connectionAddressFamily, connectionSocketType,
                connectionProtocolType, objectPoolSize, preallocateBuffers, loggingStream, minimumLoggedSeverity);

            connection.TryBind(localEndPoint, timeout);

            return connection;
        }

        public static Connection InitTcp(EndPoint remoteEndPoint, int objectPoolSize = 10, bool preallocateBuffers = false,
            Stream? loggingStream = default, LogLevel minimumLoggedSeverity = LogLevel.Info)
            => Init(remoteEndPoint, AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp, TimeSpan.FromSeconds(1),
                objectPoolSize, preallocateBuffers, loggingStream, minimumLoggedSeverity);

        public static Connection InitUdp(EndPoint remoteEndPoint, int objectPoolSize = 10, bool preallocateBuffers = false,
            Stream? loggingStream = default, LogLevel minimumLoggedSeverity = LogLevel.Info)
            => Init(remoteEndPoint, AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp, TimeSpan.FromSeconds(1),
                objectPoolSize, preallocateBuffers, loggingStream, minimumLoggedSeverity);
    }
}