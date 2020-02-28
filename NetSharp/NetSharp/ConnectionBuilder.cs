using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using NetSharp.Logging;
using NetSharp.Packets;
using NetSharp.Pipelines;

namespace NetSharp
{
    /// <summary>
    /// Allows for configuring and subsequently building a <see cref="Connection"/> instance.
    /// </summary>
    public sealed class ConnectionBuilder
    {
        private static readonly LoggingSettings DefaultLoggingSettings = new LoggingSettings(Stream.Null, LogLevel.Warn);
        private static readonly PoolingSettings DefaultPoolingSettings = new PoolingSettings(10, false);

        private readonly List<Func<Memory<byte>, Memory<byte>>> incomingPipelineStages =
            new List<Func<Memory<byte>, Memory<byte>>>();

        private readonly List<Func<Memory<byte>, Memory<byte>>> outgoingPipelineStages =
            new List<Func<Memory<byte>, Memory<byte>>>();

        private LoggingSettings? loggingSettings;
        private PoolingSettings? poolingSettings;
        private SocketSettings? socketSettings;

        /// <summary>
        /// The number of stages in the currently configured incoming packet pipeline.
        /// </summary>
        public int IncomingPacketPipelineStageCount
        {
            get { return incomingPipelineStages.Count; }
        }

        /// <summary>
        /// The number of stages in the currently configured outgoing packet pipeline.
        /// </summary>
        public int OutgoingPacketPipelineStageCount
        {
            get { return outgoingPipelineStages.Count; }
        }

        /// <summary>
        /// Returns a new <see cref="Connection"/> instance with the current configuration.
        /// </summary>
        /// <returns>The configured <see cref="Connection"/> instance.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <see cref="WithSocket"/> has not been called.
        /// </exception>
        public Connection Build()
        {
            if (socketSettings == null)
            {
                throw new ArgumentNullException(nameof(socketSettings), $"{nameof(WithSocket)} has not been called.");
            }

            PacketPipelineBuilder<Memory<byte>, Memory<byte>, NetworkPacket> incomingPipelineBuilder =
                new PacketPipelineBuilder<Memory<byte>, Memory<byte>, NetworkPacket>();

            incomingPipelineBuilder.WithInputStage(memory => memory);
            foreach (Func<Memory<byte>, Memory<byte>> stage in incomingPipelineStages)
            {
                incomingPipelineBuilder = incomingPipelineBuilder.WithIntermediateStage(stage);
            }

            incomingPipelineBuilder.WithOutputStage(NetworkPacket.Deserialise);

            PacketPipelineBuilder<NetworkPacket, Memory<byte>, Memory<byte>> outgoingPipelineBuilder =
                new PacketPipelineBuilder<NetworkPacket, Memory<byte>, Memory<byte>>();

            outgoingPipelineBuilder.WithInputStage(NetworkPacket.Serialise);
            foreach (Func<Memory<byte>, Memory<byte>> stage in outgoingPipelineStages)
            {
                outgoingPipelineBuilder = outgoingPipelineBuilder.WithIntermediateStage(stage);
            }

            outgoingPipelineBuilder.WithOutputStage(memory => memory);

            Connection connection = new Connection(
                socketSettings.Value.AddressFamily,
                socketSettings.Value.SocketType,
                socketSettings.Value.ProtocolType,
                incomingPipelineBuilder.Build(),
                outgoingPipelineBuilder.Build(),
                poolingSettings?.ObjectPoolSize ?? DefaultPoolingSettings.ObjectPoolSize,
                poolingSettings?.PreallocateBuffers ?? DefaultPoolingSettings.PreallocateBuffers,
                loggingSettings?.LoggingStream ?? DefaultLoggingSettings.LoggingStream,
                loggingSettings?.MinimumLevel ?? DefaultLoggingSettings.MinimumLevel);

            return connection;
        }

        /// <summary>
        /// Adds an extra pipeline stage to the currently configured incoming packet pipeline, at the given index.
        /// </summary>
        /// <param name="transform">
        /// The transformation that should be applied when a packet passes through the pipeline.
        /// </param>
        /// <param name="index">The position in the pipeline at which to place the transform.</param>
        /// <returns>The builder instance for further configuration.</returns>
        public ConnectionBuilder WithIncomingPipelineStage(in Func<Memory<byte>, Memory<byte>> transform, int index)
        {
            incomingPipelineStages.Insert(index, transform);
            return this;
        }

        /// <summary>
        /// Sets the logging settings for the currently configured connection.
        /// </summary>
        /// <param name="settings">The logging settings to use.</param>
        /// <returns>The builder instance for further configuration.</returns>
        public ConnectionBuilder WithLogging(LoggingSettings settings)
        {
            loggingSettings = settings;
            return this;
        }

        /// <summary>
        /// Adds an extra pipeline stage to the currently configured outgoing packet pipeline, at the given index.
        /// </summary>
        /// <param name="transform">
        /// The transformation that should be applied when a packet passes through the pipeline.
        /// </param>
        /// <param name="index">The position in the pipeline at which to place the transform.</param>
        /// <returns>The builder instance for further configuration.</returns>
        public ConnectionBuilder WithOutgoingPipelineStage(in Func<Memory<byte>, Memory<byte>> transform, int index)
        {
            outgoingPipelineStages.Insert(index, transform);
            return this;
        }

        /// <summary>
        /// Sets the pooling settings for the currently configured connection.
        /// </summary>
        /// <param name="settings">The pooling settings to use.</param>
        /// <returns>The builder instance for further configuration.</returns>
        public ConnectionBuilder WithPooling(PoolingSettings settings)
        {
            poolingSettings = settings;
            return this;
        }

        /// <summary>
        /// Sets the socket settings for the currently configured connection.
        /// </summary>
        /// <param name="settings">The socket settings to use.</param>
        /// <returns>The builder instance for further configuration.</returns>
        public ConnectionBuilder WithSocket(SocketSettings settings)
        {
            socketSettings = settings;
            return this;
        }

        /// <summary>
        /// Holds settings for configuring a connection's logging.
        /// </summary>
        public readonly struct LoggingSettings
        {
            /// <summary>
            /// The stream to which messages will be logged.
            /// </summary>
            public readonly Stream LoggingStream;

            /// <summary>
            /// The minimum severity that a log message must have to be recorded.
            /// </summary>
            public readonly LogLevel MinimumLevel;

            /// <summary>
            /// Initialises a new instance of the <see cref="LoggingStream"/> struct.
            /// </summary>
            /// <param name="stream">The stream to which messages will be logged..</param>
            /// <param name="minimumLevel">The minimum severity that a log message must have to be recorded.</param>
            public LoggingSettings(Stream stream, LogLevel minimumLevel)
            {
                LoggingStream = stream;
                MinimumLevel = minimumLevel;
            }
        }

        /// <summary>
        /// Holds settings for configuring a connection's buffer pooling.
        /// </summary>
        public readonly struct PoolingSettings
        {
            /// <summary>
            /// The number of objects that will be held in the object pools.
            /// </summary>
            public readonly int ObjectPoolSize;

            /// <summary>
            /// Whether the buffers for receiving messages should be preallocated.
            /// </summary>
            public readonly bool PreallocateBuffers;

            /// <summary>
            /// Initialises a new instance of the <see cref="PoolingSettings"/> struct.
            /// </summary>
            /// <param name="poolSize">The number of objects that will be held in the object pools.</param>
            /// <param name="preallocateBuffers">Whether the buffers for receiving messages should be preallocated.</param>
            public PoolingSettings(int poolSize, bool preallocateBuffers)
            {
                ObjectPoolSize = poolSize;
                PreallocateBuffers = preallocateBuffers;
            }
        }

        /// <summary>
        /// Holds settings fo configuring a connection's underlying socket.
        /// </summary>
        public readonly struct SocketSettings
        {
            /// <summary>
            /// The address family for the socket underlying the connection.
            /// </summary>
            public readonly AddressFamily AddressFamily;

            /// <summary>
            /// The socket type for the socket underlying the connection.
            /// </summary>
            public readonly ProtocolType ProtocolType;

            /// <summary>
            /// The protocol type for the socket underlying the connection.
            /// </summary>
            public readonly SocketType SocketType;

            /// <summary>
            /// Initialises a new instance of the <see cref="SocketSettings"/> struct.
            /// </summary>
            /// <param name="addressFamily">The address family for the underlying socket.</param>
            /// <param name="socketType">The socket type for the underlying socket.</param>
            /// <param name="protocolType">The protocol type for the underlying socket.</param>
            public SocketSettings(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
            {
                AddressFamily = addressFamily;
                SocketType = socketType;
                ProtocolType = protocolType;
            }
        }
    }
}