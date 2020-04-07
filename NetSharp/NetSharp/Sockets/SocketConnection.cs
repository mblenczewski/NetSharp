using System;
using System.Net;
using System.Net.Sockets;

namespace NetSharp.Sockets
{
    public abstract class SocketConnection : IDisposable
    {
        protected readonly Socket connection;

        protected SocketConnection(in AddressFamily connectionAddressFamily, in SocketType connectionSocketType, in ProtocolType connectionProtocolType)
        {
            connection = new Socket(connectionAddressFamily, connectionSocketType, connectionProtocolType);
        }

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