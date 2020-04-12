using NetSharp.Packets;
using NetSharp.Utils;

using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;

namespace NetSharp.Sockets
{
    //TODO document class
    public abstract class SocketConnection : IDisposable
    {
        protected readonly Socket connection;

        protected readonly ArrayPool<byte> BufferPool;

        protected readonly SlimObjectPool<SocketAsyncEventArgs> TransmissionArgsPool;

        protected SocketConnection(in AddressFamily connectionAddressFamily, in SocketType connectionSocketType, in ProtocolType connectionProtocolType)
        {
            connection = new Socket(connectionAddressFamily, connectionSocketType, connectionProtocolType);

            BufferPool = ArrayPool<byte>.Create(NetworkPacket.TotalSize, 1000);

            TransmissionArgsPool = new SlimObjectPool<SocketAsyncEventArgs>(CreateTransmissionArgs, ResetTransmissionArgs, DestroyTransmissionArgs, CanTransmissionArgsBeReused);
        }

        protected abstract SocketAsyncEventArgs CreateTransmissionArgs();

        protected abstract void ResetTransmissionArgs(SocketAsyncEventArgs args);

        protected abstract bool CanTransmissionArgsBeReused(in SocketAsyncEventArgs args);

        protected abstract void DestroyTransmissionArgs(SocketAsyncEventArgs remoteConnectionArgs);

        protected abstract void HandleIoCompleted(object sender, SocketAsyncEventArgs args);

        public void Bind(in EndPoint localEndPoint)
        {
            connection.Bind(localEndPoint);
        }

        public void Shutdown(SocketShutdown how)
        {
            try
            {
                connection.Shutdown(how);
            }
            catch (SocketException) { }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            connection.Close();
            connection.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}