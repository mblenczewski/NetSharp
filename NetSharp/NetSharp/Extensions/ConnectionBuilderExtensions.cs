using System;
using System.IO;
using System.Net.Sockets;
using NetSharp.Logging;

namespace NetSharp.Extensions
{
    /// <summary>
    /// Provides additional methods and functionality to the <see cref="ConnectionBuilder"/> class.
    /// </summary>
    public static class ConnectionBuilderExtensions
    {
        public static ConnectionBuilder AppendIncomingPipelineStage(this ConnectionBuilder instance,
            in Func<Memory<byte>, Memory<byte>> transform)
            => instance.WithIncomingPipelineStage(transform, instance.IncomingPacketPipelineStageCount);

        public static ConnectionBuilder AppendOutgoingPipelineStage(this ConnectionBuilder instance,
            in Func<Memory<byte>, Memory<byte>> transform)
            => instance.WithOutgoingPipelineStage(transform, instance.OutgoingPacketPipelineStageCount);

        public static ConnectionBuilder WithLogging(this ConnectionBuilder instance,
                            Stream loggingStream, LogLevel minimumLogLevel)
            => instance.WithLogging(new ConnectionBuilder.LoggingSettings(loggingStream, minimumLogLevel));

        public static ConnectionBuilder WithPooling(this ConnectionBuilder instance,
            int poolSize, bool preallocateBuffers)
            => instance.WithPooling(new ConnectionBuilder.PoolingSettings(poolSize, preallocateBuffers));
    }
}