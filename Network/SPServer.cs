using MessagePack;
using NLog;
using SuperProxy.Collection;
using SuperProxy.Events;
using SuperProxy.Network.Messages;
using SuperProxy.Network.Messages.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SuperProxy.Network
{
    public class SPServer
    {
        private readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly HashSet<Thread> _listeningThreads = new HashSet<Thread>();
        private readonly Socket _listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        private ObjectsPool<SPClient> _connectionsPool;

        public void Initialize(string host, int port, int maxConnections, int backLog, int acceptThreads)
        {
            _connectionsPool = new ObjectsPool<SPClient>(maxConnections);
            _listeningSocket.Bind(new IPEndPoint(IPAddress.Parse(host), port));
            _listeningSocket.Listen(backLog);

            for (int i = 0; i < acceptThreads; i++)
            {
                var th = new Thread(() => _listeningSocket.BeginAccept(AcceptCallback, null));
                th.Start();
                _listeningThreads.Add(th);
            }

            _log.Info($"SuperProxy server listening started on {host}:{port}");
        }

        public void Release()
        {
            _listeningSocket.Shutdown(SocketShutdown.Both);
            _listeningSocket.Close();

            var listeningThreads = _listeningThreads.ToList();
            foreach(var lt in listeningThreads)            
                if (lt.IsAlive)
                    lt.Abort();

            _listeningThreads.Clear();

            var connections = _connectionsPool?.GetUsedObjects();
            if(connections != null && connections.Any())
            {
                foreach(var connection in connections)
                {
                    connection.Dispose();

                    _connectionsPool.Release(connection);
                }
            }
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            Socket sock = null;

            try
            {
                sock = _listeningSocket.EndAccept(ar);

                var connection = _connectionsPool.Get();
                if (connection == null)
                    _log.Warn("Connections limit reached! Can't accept new connection");
                else
                {
                    _log.Info($"Client {sock.RemoteEndPoint} connected!");

                    connection.Socket = sock;
                    connection.Socket.BeginReceive(connection.WaitPacketLength, 0, 2, SocketFlags.None, ReadCallback, connection);
                }
            }
            catch (Exception ex)
            {
                sock?.Close();

                _log.Error(ex);
            }

            _listeningSocket.BeginAccept(AcceptCallback, null);
        }

        private void ReadCallback(IAsyncResult ar)
        {
            var connection = (SPClient)ar.AsyncState;

            try
            {
                var readed = connection.Socket.EndReceive(ar, out var err);
                if (err != SocketError.Success || readed <= 0)
                {
                    connection.Socket.Disconnect(false);
                    connection.Socket.Close();
                    _connectionsPool.Release(connection);
                    return;
                }

                var buf = connection.WaitPacketLength;
                var len = BitConverter.ToUInt16(buf, 0);
                if (len > 0)
                {
                    var packetDataBuffer = new byte[len];                   
                    var r = 0;                  
                    while (r < packetDataBuffer.Length)
                        r += connection.Socket.Receive(packetDataBuffer, r, packetDataBuffer.Length - r, SocketFlags.None);

                    ThreadPool.QueueUserWorkItem((callback) => 
                    {
                        var message = MessagePackSerializer.Deserialize<IMessageEvent>(packetDataBuffer);

                        switch (message)
                        {
                            case PublishEvent publish:
                                EventAggregator.Publish(publish.Channel, publish.Message.Data, publish.Message.Header, connection);
                                break;
                            case SubscribeEvent subscribe:
                                EventAggregator.Subscribe(subscribe.Channel, connection);
                                break;
                            case UnsubscribeEvent unsubsribe:
                                if (unsubsribe.FromAll)
                                    EventAggregator.UnsubscribeFromAllChannels(connection);
                                else
                                    EventAggregator.Unsubscribe(unsubsribe.Channel, connection);
                                break;
                            case RMINotyfiEvent rmiEvent:
                                RemoteMethodEventAggregator.Subscribe(connection, rmiEvent);
                                break;
                            case RemoteMethodEvent remoteEvent:
                                RemoteMethodEventAggregator.Publish(remoteEvent, connection);
                                break;
                            case ReplicationListUpdateEvent repListEvent:
                                ReplicationEventAggregator.DispatchGenericReplicationInfo(connection, repListEvent);
                                break;
                            case ReplicationNotyfiEvent repNotyfi:
                                ReplicationEventAggregator.SetReplicationInfo(connection, repNotyfi.ObjectsToReplicate);
                                break;
                            case ReplicationPrimitiveUpdateEvent primitiveUpdateEvent:
                                ReplicationEventAggregator.DispatchPrimitiveReplicationInfo(connection, primitiveUpdateEvent);
                                break;
                        }
                    });
                }
                else
                {
                    connection.Socket.BeginReceive(connection.WaitPacketLength, 0, connection.WaitPacketLength.Length, SocketFlags.None, ReadCallback, connection);
                    return;
                }

                connection.Socket.BeginReceive(connection.WaitPacketLength, 0, connection.WaitPacketLength.Length, SocketFlags.None, ReadCallback, connection);
            }
            catch (ObjectDisposedException)
            {
                connection.ReadyForRMI = false;
            }
            catch (Exception e)
            {
                if (connection != null)
                    Disconnect(connection);

                _log.Error(e);
            }
        }

        public void Disconnect(SPClient client)
        {
            try
            {
                EventAggregator.UnsubscribeFromAllChannels(client);
                RemoteMethodEventAggregator.UnsubscribeFromAllChannels(client);
                ReplicationEventAggregator.ReleaseClient(client);

                client.Reset();
                client.Socket.BeginDisconnect(false, EndDisconect, client);
            }
            catch (Exception e)
            {
                _log.Error($"Exception occured on begin disconnect, {e}");
            }
        }

        private void EndDisconect(IAsyncResult ar)
        {
            var connection = (SPClient)ar.AsyncState;

            try
            {
                connection.Socket.EndDisconnect(ar);
            }
            catch (Exception e)
            {
                _log.Error($"Exception occured on end disconnect, {e}");
            }
            finally
            {
                connection.Socket.Close();
                _connectionsPool.Release(connection);
            }
        }
    }
}
