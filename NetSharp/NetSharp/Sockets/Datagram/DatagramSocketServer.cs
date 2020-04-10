using NetSharp.Utils;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;
using NetSharp.Deprecated;
using NetworkPacket = NetSharp.Packets.NetworkPacket;

namespace NetSharp.Sockets.Datagram
{
    public class DatagramSocketServer : SocketServer
    {
        private static readonly EndPoint AnyRemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        private readonly ConcurrentDictionary<EndPoint, RemoteDatagramClientToken> connectedClientTokens;

        private readonly ObjectPool<SocketAsyncEventArgs> ArgsPool;

        private readonly struct RemoteDatagramClientToken
        {
            private readonly Channel<NetworkPacket> PacketChannel;

            public readonly ChannelReader<NetworkPacket> PacketReader;

            public readonly ChannelWriter<NetworkPacket> PacketWriter;

            public RemoteDatagramClientToken(in Channel<NetworkPacket> packetChannel)
            {
                PacketChannel = packetChannel;
                PacketReader = packetChannel.Reader;
                PacketWriter = packetChannel.Writer;
            }
        }

        public DatagramSocketServer(in AddressFamily connectionAddressFamily, in ProtocolType connectionProtocolType)
            : base(in connectionAddressFamily, SocketType.Dgram, in connectionProtocolType)
        {
            connectedClientTokens = new ConcurrentDictionary<EndPoint, RemoteDatagramClientToken>();

            SocketAsyncEventArgs CreateArgs()
            {
                SocketAsyncEventArgs args = new SocketAsyncEventArgs();

                args.Completed += HandleIoCompleted;

                return args;
            }

            static void ResetArgs(SocketAsyncEventArgs args)
            {

            }

            void DestroyArgs(SocketAsyncEventArgs args)
            {
                args.Completed -= HandleIoCompleted;

                args.Dispose();
            }

            static bool ReBufferArgsPredicate(in SocketAsyncEventArgs args)
            {
                return true;
            }

            ArgsPool = new ObjectPool<SocketAsyncEventArgs>(CreateArgs, ResetArgs, DestroyArgs, ReBufferArgsPredicate);
        }

        protected override SocketAsyncEventArgs GenerateConnectionArgs(EndPoint remoteEndPoint)
        {
            SocketAsyncEventArgs connectionArgs = new SocketAsyncEventArgs { RemoteEndPoint = remoteEndPoint };

            connectionArgs.Completed += SocketAsyncOperations.HandleIoCompleted;

            return connectionArgs;
        }

        protected override void DestroyConnectionArgs(SocketAsyncEventArgs remoteConnectionArgs)
        {
            remoteConnectionArgs.Completed -= SocketAsyncOperations.HandleIoCompleted;

            remoteConnectionArgs.Dispose();
        }

        protected override async Task HandleClient(SocketAsyncEventArgs clientArgs, CancellationToken cancellationToken = default)
        {
            EndPoint clientEndPoint = clientArgs.RemoteEndPoint;
            RemoteDatagramClientToken clientToken = connectedClientTokens[clientEndPoint];

            byte[] responseBuffer = new byte[NetworkPacket.TotalSize];
            Memory<byte> responseBufferMemory = new Memory<byte>(responseBuffer);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    NetworkPacket request = await clientToken.PacketReader.ReadAsync(cancellationToken);

                    // TODO implement actual request handling, besides just an echo
                    NetworkPacket response = request;

                    NetworkPacket.Serialise(response, responseBufferMemory);

                    TransmissionResult sendResult =
                        await SocketAsyncOperations
                            .SendToAsync(clientArgs, connection, clientEndPoint, SocketFlags.None, responseBufferMemory,
                                cancellationToken);

#if DEBUG
                    lock (typeof(Console))
                    {
                        Console.WriteLine($"[Server] Sent {sendResult.Count} bytes to {sendResult.RemoteEndPoint}");
                        Console.WriteLine($"[Server] >>>> {Encoding.UTF8.GetString(sendResult.Buffer.Span)}");
                    }
#endif
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Client task for {clientArgs.RemoteEndPoint} cancelled!");
            }
            finally
            {
                DestroyConnectionArgs(clientArgs);
            }
        }

        private readonly struct ClientRequest
        {
            public readonly NetworkPacket RequestPacket;

            public readonly EndPoint ClientEndPoint;

            public readonly CancellationToken CancellationToken;

            public ClientRequest(in NetworkPacket requestPacket, in EndPoint clientEndPoint, in CancellationToken cancellationToken)
            {
                RequestPacket = requestPacket;

                ClientEndPoint = clientEndPoint;

                CancellationToken = cancellationToken;
            }
        }

        private async Task HandleClientRequest(ClientRequest clientRequest)
        {
            SocketAsyncEventArgs clientArgs = TransmissionArgsPool.Get();

            byte[] responseBuffer = BufferPool.Rent(NetworkPacket.TotalSize);
            Memory<byte> responseBufferMemory = new Memory<byte>(responseBuffer);

            NetworkPacket request = clientRequest.RequestPacket;
            EndPoint remoteEndPoint = clientRequest.ClientEndPoint;
            CancellationToken cancellationToken = clientRequest.CancellationToken;

            // TODO implement actual request handling, besides just an echo
            NetworkPacket response = request;

            NetworkPacket.Serialise(response, responseBufferMemory);

            TransmissionResult sendResult =
                await SocketAsyncOperations
                    .SendToAsync(clientArgs, connection, remoteEndPoint, SocketFlags.None, responseBufferMemory, cancellationToken)
                    .ConfigureAwait(false);

#if DEBUG
                    lock (typeof(Console))
                    {
                        Console.WriteLine($"[Server] Sent {sendResult.Count} bytes to {sendResult.RemoteEndPoint}");
                        Console.WriteLine($"[Server] >>>> {Encoding.UTF8.GetString(sendResult.Buffer.Span)}");
                    }
#endif

            BufferPool.Return(responseBuffer, true);

            TransmissionArgsPool.Return(clientArgs);
        }

        private readonly struct ClientPacket
        {
            public readonly SocketAsyncEventArgs RentedArgs;

            public readonly byte[] RentedBuffer;

            public readonly Memory<byte> RentedBufferMemory;

            public readonly EndPoint RemoteEndPoint;

            public ClientPacket(in SocketAsyncEventArgs rentedArgs, in byte[] rentedBuffer, in Memory<byte> rentedBufferMemory, in EndPoint remoteEndPoint)
            {
                RentedArgs = rentedArgs;

                RentedBuffer = rentedBuffer;

                RentedBufferMemory = rentedBufferMemory;

                RemoteEndPoint = remoteEndPoint;
            }
        }

        private readonly struct SocketOperationToken
        {
            public readonly byte[] RentedBuffer;

            public readonly Memory<byte> RentedBufferMemory;

            public SocketOperationToken(in byte[] rentedBuffer, in Memory<byte> rentedBufferMemory)
            {
                RentedBuffer = rentedBuffer;

                RentedBufferMemory = rentedBufferMemory;
            }
        }

        private void HandleIoCompleted(object sender, SocketAsyncEventArgs args)
        {
            switch (args.LastOperation)
            {
                case SocketAsyncOperation.SendTo:
                    CompleteSendTo(args);
                    break;

                case SocketAsyncOperation.ReceiveFrom:
                    CompleteReceiveFrom(args);
                    break;

                default:
                    throw new NotSupportedException($"{nameof(HandleIoCompleted)} doesn't support {args.LastOperation}");
            }
        }

        private void DoSendTo(SocketAsyncEventArgs sendArgs)
        {
            bool completesAsync = connection.SendToAsync(sendArgs);

            if (!completesAsync)
            {
                CompleteSendTo(sendArgs);
            }
        }

        private void CompleteSendTo(SocketAsyncEventArgs sendArgs)
        {
            SocketOperationToken sendToken = (SocketOperationToken) sendArgs.UserToken;

            TransmissionResult sendResult = new TransmissionResult(sendArgs);

#if DEBUG
                    lock (typeof(Console))
                    {
                        Console.WriteLine($"[Server] Sent {sendResult.Count} bytes to {sendResult.RemoteEndPoint}");
                        Console.WriteLine($"[Server] >>>> {Encoding.UTF8.GetString(sendResult.Buffer.Span)}");
                    }
#endif

            BufferPool.Return(sendToken.RentedBuffer, true);

            ArgsPool.Return(sendArgs);
        }

        private void DoReceiveFrom(EndPoint remoteEndPoint)
        {
            SocketAsyncEventArgs args = ArgsPool.Rent();

            byte[] receiveBuffer = BufferPool.Rent(NetworkPacket.TotalSize);
            Memory<byte> receiveBufferMemory = new Memory<byte>(receiveBuffer);

            args.SetBuffer(receiveBufferMemory);
            args.RemoteEndPoint = remoteEndPoint;
            args.UserToken = new SocketOperationToken(in receiveBuffer, in receiveBufferMemory);

            bool completesAsync = connection.ReceiveFromAsync(args);

            if (!completesAsync)
            {
                CompleteReceiveFrom(args);
            }
        }

        private void CompleteReceiveFrom(SocketAsyncEventArgs receiveArgs)
        {
            DoReceiveFrom(AnyRemoteEndPoint); // start a new receive from operation immediately, to not drop any packets

            SocketOperationToken receiveToken = (SocketOperationToken)receiveArgs.UserToken;

            TransmissionResult receiveResult = new TransmissionResult(receiveArgs);

#if DEBUG
                lock (typeof(Console))
                {
                    Console.WriteLine($"[Server] Received {receiveResult.Count} bytes from {receiveResult.RemoteEndPoint}");
                    Console.WriteLine($"[Server] <<<< {Encoding.UTF8.GetString(receiveResult.Buffer.Span)}");
                }
#endif

            NetworkPacket request = NetworkPacket.Deserialise(receiveArgs.MemoryBuffer);

            NetworkPacket response = request;

            SocketAsyncEventArgs sendArgs = ArgsPool.Rent();

            byte[] sendBuffer = BufferPool.Rent(NetworkPacket.TotalSize);
            Memory<byte> sendBufferMemory = new Memory<byte>(sendBuffer);

            NetworkPacket.Serialise(response, sendBufferMemory);

            sendArgs.SetBuffer(sendBufferMemory);
            sendArgs.RemoteEndPoint = receiveResult.RemoteEndPoint;
            sendArgs.UserToken = new SocketOperationToken(in sendBuffer, in sendBufferMemory);

            DoSendTo(sendArgs);

            BufferPool.Return(receiveToken.RentedBuffer, true);

            ArgsPool.Return(receiveArgs);
        }

        public override Task RunAsync(CancellationToken cancellationToken = default)
        {
            for (int i = 0; i < 10; i++)
            {
                DoReceiveFrom(AnyRemoteEndPoint);
            }

            return cancellationToken.WaitHandle.WaitOneAsync();
            /*
            UnboundedChannelOptions requestChannelOptions = new UnboundedChannelOptions()
            {
                AllowSynchronousContinuations = true,
                SingleReader = true,
                SingleWriter = true,
            };
            Channel<ClientPacket> requestChannel = Channel.CreateUnbounded<ClientPacket>(requestChannelOptions);

            UnboundedChannelOptions responseChannelOptions = new UnboundedChannelOptions()
            {
                AllowSynchronousContinuations = true,
                SingleReader = true,
                SingleWriter = true,
            };
            Channel<ClientPacket> responseChannel = Channel.CreateUnbounded<ClientPacket>(responseChannelOptions);

            async Task ReceivePacketTask()
            {
                EndPoint anyRemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

                while (!cancellationToken.IsCancellationRequested)
                {
                    SocketAsyncEventArgs transmissionArgs = TransmissionArgsPool.Get();

                    byte[] requestBuffer = BufferPool.Rent(NetworkPacket.TotalSize);
                    Memory<byte> requestBufferMemory = new Memory<byte>(requestBuffer);

                    TransmissionResult receiveResult =
                        await SocketAsyncOperations
                            .ReceiveFromAsync(transmissionArgs, connection, anyRemoteEndPoint, SocketFlags.None, requestBufferMemory, cancellationToken)
                            .ConfigureAwait(false);

#if DEBUG
                lock (typeof(Console))
                {
                    Console.WriteLine($"[Server] Received {receiveResult.Count} bytes from {receiveResult.RemoteEndPoint}");
                    Console.WriteLine($"[Server] <<<< {Encoding.UTF8.GetString(receiveResult.Buffer.Span)}");
                }
#endif
                    ClientPacket request = new ClientPacket(in transmissionArgs, in requestBuffer, in requestBufferMemory, in receiveResult.RemoteEndPoint);

                    await requestChannel.Writer.WriteAsync(request, cancellationToken);
                }
            }

            async Task HandleRequestTask()
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    ClientPacket request = await requestChannel.Reader.ReadAsync(cancellationToken);

                    NetworkPacket requestPacket = NetworkPacket.Deserialise(request.RentedBufferMemory);

                    // TODO implement actual request handling, besides just an echo
                    NetworkPacket responsePacket = requestPacket;

                    byte[] responseBuffer = BufferPool.Rent(NetworkPacket.TotalSize);
                    Memory<byte> responseBufferMemory = new Memory<byte>(responseBuffer);

                    NetworkPacket.Serialise(responsePacket, responseBufferMemory);
                    
                    // after the response has been serialised to the response buffer, the request buffer is done with and can be freed
                    BufferPool.Return(request.RentedBuffer, true);

                    ClientPacket response = new ClientPacket(in request.RentedArgs, in responseBuffer, in responseBufferMemory, in request.RemoteEndPoint);

                    await responseChannel.Writer.WriteAsync(response, cancellationToken);
                }
            }

            async Task SendResponseTask()
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    ClientPacket response = await responseChannel.Reader.ReadAsync(cancellationToken);

                    EndPoint remoteEndPoint = response.RemoteEndPoint;
                    Memory<byte> responseBufferMemory = response.RentedBufferMemory;

                    TransmissionResult sendResult =
                        await SocketAsyncOperations
                            .SendToAsync(response.RentedArgs, connection, remoteEndPoint, SocketFlags.None, responseBufferMemory, cancellationToken)
                            .ConfigureAwait(false);

#if DEBUG
                    lock (typeof(Console))
                    {
                        Console.WriteLine($"[Server] Sent {sendResult.Count} bytes to {sendResult.RemoteEndPoint}");
                        Console.WriteLine($"[Server] >>>> {Encoding.UTF8.GetString(sendResult.Buffer.Span)}");
                    }
#endif

                    BufferPool.Return(response.RentedBuffer, true);

                    TransmissionArgsPool.Return(response.RentedArgs);
                }
            }

            // TODO this still functions as the simple example below, in fact it performs worse, with a worse bandwidth :(
            List<Task> completeTaskList = new List<Task>();

            Task[] readRequestTasks = new Task[2];
            for (int i = 0; i < readRequestTasks.Length; i++)
            {
                readRequestTasks[i] = ReceivePacketTask();
                completeTaskList.Add(readRequestTasks[i]);
            }

            Task[] handleRequestTasks = new Task[2];
            for (int i = 0; i < handleRequestTasks.Length; i++)
            {
                handleRequestTasks[i] = HandleRequestTask();
                completeTaskList.Add(handleRequestTasks[i]);
            }

            Task[] writeResponseTasks = new Task[2];
            for (int i = 0; i < writeResponseTasks.Length; i++)
            {
                writeResponseTasks[i] = SendResponseTask();
                completeTaskList.Add(writeResponseTasks[i]);
            }

            await Task.WhenAll(completeTaskList);
            */

            /*
            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            using SocketAsyncEventArgs remoteArgs = GenerateConnectionArgs(remoteEndPoint);

            byte[] requestBuffer = new byte[NetworkPacket.TotalSize];
            Memory<byte> requestBufferMemory = new Memory<byte>(requestBuffer);

            while (!cancellationToken.IsCancellationRequested)
            {
                TransmissionResult receiveResult =
                    await SocketAsyncOperations
                        .ReceiveFromAsync(remoteArgs, connection, remoteEndPoint, SocketFlags.None, requestBufferMemory, cancellationToken)
                        .ConfigureAwait(false);
                
                EndPoint clientEndPoint = receiveResult.RemoteEndPoint;

#if DEBUG
                lock (typeof(Console))
                {
                    Console.WriteLine($"[Server] Received {receiveResult.Count} bytes from {receiveResult.RemoteEndPoint}");
                    Console.WriteLine($"[Server] <<<< {Encoding.UTF8.GetString(receiveResult.Buffer.Span)}");
                }
#endif

                NetworkPacket requestPacket = NetworkPacket.Deserialise(requestBufferMemory);

                ClientRequest request = new ClientRequest(in requestPacket, in clientEndPoint, in cancellationToken);

                Task _ = HandleClientRequest(request);
            }
            */

            /*
            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            using SocketAsyncEventArgs remoteArgs = GenerateConnectionArgs(remoteEndPoint);

            byte[] requestBuffer = new byte[NetworkPacket.TotalSize];
            Memory<byte> requestBufferMemory = new Memory<byte>(requestBuffer);

            while (!cancellationToken.IsCancellationRequested)
            {
                remoteArgs.RemoteEndPoint = remoteEndPoint;

                TransmissionResult receiveResult =
                    await SocketAsyncOperations
                        .ReceiveFromAsync(remoteArgs, connection, remoteEndPoint, SocketFlags.None, requestBufferMemory, cancellationToken)
                        .ConfigureAwait(false);

                EndPoint clientEndPoint = receiveResult.RemoteEndPoint;

#if DEBUG
                lock (typeof(Console))
                {
                    Console.WriteLine($"[Server] Received {receiveResult.Count} bytes from {receiveResult.RemoteEndPoint}");
                    Console.WriteLine($"[Server] <<<< {Encoding.UTF8.GetString(receiveResult.Buffer.Span)}");
                }
#endif

                if (!ConnectedClientHandlerTasks.ContainsKey(clientEndPoint))
                {
                    SocketAsyncEventArgs clientArgs = GenerateConnectionArgs(receiveResult.RemoteEndPoint);

                    BoundedChannelOptions clientChannelOptions = new BoundedChannelOptions(60)
                        { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = true };
                    Channel<NetworkPacket> clientChannel = Channel.CreateBounded<NetworkPacket>(clientChannelOptions);

                    connectedClientTokens[clientEndPoint] = new RemoteDatagramClientToken(in clientChannel);

                    ConnectedClientHandlerTasks[clientEndPoint] = HandleClient(clientArgs, cancellationToken);
                }

                NetworkPacket request = NetworkPacket.Deserialise(requestBufferMemory);

                await connectedClientTokens[clientEndPoint].PacketWriter.WriteAsync(request, cancellationToken);
            }
            */
        }
    }

    internal class ObjectPool<T> where T : class
    {
        internal delegate T CreateObjectDelegate();

        internal delegate bool KeepObjectPredicate(in T instance);

        internal delegate void ResetObjectDelegate(T instance);

        internal delegate void DestroyObjectDelegate(T instance);

        private readonly CreateObjectDelegate createObjectDelegate;
        private readonly KeepObjectPredicate rebufferObjectPredicate;
        private readonly ResetObjectDelegate resetObjectDelegate;
        private readonly DestroyObjectDelegate destroyObjectDelegate;

        private readonly ConcurrentBag<T> objectBuffer;

        public ObjectPool(in CreateObjectDelegate createDelegate, in ResetObjectDelegate resetDelegate, in DestroyObjectDelegate destroyDelegate, in KeepObjectPredicate keepObjectPredicate)
        {
            createObjectDelegate = createDelegate;

            resetObjectDelegate = resetDelegate;

            destroyObjectDelegate = destroyDelegate;

            rebufferObjectPredicate = keepObjectPredicate;

            objectBuffer = new ConcurrentBag<T>();
        }

        public T Rent()
        {
            return objectBuffer.TryTake(out T result) ? result : createObjectDelegate();
        }

        public void Return(T instance)
        {
            if (rebufferObjectPredicate(instance))
            {
                resetObjectDelegate(instance);

                objectBuffer.Add(instance);
            }
            else
            {
                destroyObjectDelegate(instance);
            }
        }
    }
}