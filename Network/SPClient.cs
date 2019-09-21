using MessagePack;
using MessagePack.Resolvers;
using NLog;
using SuperProxy.Extensions;
using SuperProxy.Network.Attributes;
using SuperProxy.Network.Events;
using SuperProxy.Network.Messages;
using SuperProxy.Network.Messages.Events;
using SuperProxy.Network.Serialization.Delegate;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SuperProxy.Network
{
    public class SPClient : IDisposable
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public Guid NetIdentity { get; set; }
        public Socket Socket { get; set; }
        public byte[] WaitPacketLength { get; set; }
        public bool ReadyForRMI { get; set; }
        private string _rmiChannel { get; set; }
        private object _selfHosted { get; set; }

        private ConcurrentDictionary<string, Action<PublishEvent>> _subsribedActions;

        private ConcurrentDictionary<string, RemoteMethodEvent> _mountedHostedMethods;

        private ConcurrentDictionary<string, RemoteEventWait> _remoteMethodCallbacks;

        private ConcurrentDictionary<string, object> _activatedReplicationObjects;

        private List<string> _remoteReplicationObjects;

        private readonly SemaphoreSlim _sockSem;

        public SPClient()
        {
            NetIdentity = Guid.NewGuid();
            WaitPacketLength = new byte[2] { 2, 0 };

            _subsribedActions = new ConcurrentDictionary<string, Action<PublishEvent>>();
            _remoteMethodCallbacks = new ConcurrentDictionary<string, RemoteEventWait>();
            _mountedHostedMethods = new ConcurrentDictionary<string, RemoteMethodEvent>();
            _activatedReplicationObjects = new ConcurrentDictionary<string, object>();
            _remoteReplicationObjects = new List<string>();
            _sockSem = new SemaphoreSlim(1, 1);
            _rmiChannel = "";
        }

        public SPClient(string rmiChannel) : this()
        {
            _rmiChannel = rmiChannel;
        }

        public SPClient(object hosted, string rmiChannel) : this(rmiChannel)
        {
            _selfHosted = hosted;
            _mountedHostedMethods = new ConcurrentDictionary<string, RemoteMethodEvent>();
            _remoteMethodCallbacks = new ConcurrentDictionary<string, RemoteEventWait>();

            MountHostedObject();

            _log.Info($"Hosted object methods mounted: {_mountedHostedMethods.Count}");
        }

        #region Remote method invoke methods

        public async void RMIMethodCalled(RemoteMethodEvent remoteEvent)
        {
            if(remoteEvent.Type == RemoteMethodType.RETURN)
            {
                _remoteMethodCallbacks[remoteEvent.CallbackGuid].Result = remoteEvent;
                _remoteMethodCallbacks[remoteEvent.CallbackGuid].Event.Set();
                return;
            }

            if (remoteEvent.Type != RemoteMethodType.CALL && remoteEvent.Type != RemoteMethodType.MOVE)
                return;

            if (_mountedHostedMethods.TryGetValue(remoteEvent.MethodName, out var targetMethod))
            {
                var resultHeader = RemoteMethodType.RETURN;
                var parameters = new List<object>();

                parameters.AddRange((object[])remoteEvent.Data);

                var localeMethodParameters = _selfHosted.GetType().GetMethod(remoteEvent.MethodName).GetParameters();
                for (int i = 0; i < localeMethodParameters.Length; i++)
                {
                    var isGenericType = parameters[i].GetType().IsGenericType;
                    var localParameter = localeMethodParameters[i].ParameterType;
                    if (localParameter.IsGenericType)
                        continue;

                    if (isGenericType)
                        parameters[i] = localParameter.ConvertProperties((Dictionary<object, object>)parameters[i]);
                    else
                        parameters[i] = Convert.ChangeType(parameters[i], localParameter);
            
                }

                var result = await targetMethod.Method.InvokeWrapper(targetMethod.HasAsyncResult, _selfHosted, parameters.ToArray());
                var rmiEvent = new RemoteMethodEvent() { Type = resultHeader, CallbackGuid = remoteEvent.CallbackGuid, Data = result, Method = null, MethodName = remoteEvent.MethodName, Channel = _rmiChannel };
                var packetData = SerializeMessagePackToFrame(rmiEvent);

                SendData(packetData);
            }
        }

        public async Task<T> RemoteCall<T>(string channel, string identifier, params object[] param)
        {
            var sw = Stopwatch.StartNew();

            var callbackGuid = Guid.NewGuid().ToString();

            _remoteMethodCallbacks.TryAdd(callbackGuid, new RemoteEventWait());

            var remoteEvent = new RemoteMethodEvent() { Type = RemoteMethodType.MOVE, MethodName = identifier, CallbackGuid = callbackGuid, Data = param, Channel = channel };
            var packetData = SerializeMessagePackToFrame(remoteEvent);

            await _sockSem.WaitAsync();

            SendData(packetData, true);

            _remoteMethodCallbacks[callbackGuid].Event.WaitOne();

            var result = _remoteMethodCallbacks[callbackGuid].Result;

            _remoteMethodCallbacks.TryRemove(callbackGuid, out _);

            var typeofImplicit = typeof(T);

            sw.Stop();

            if (result.Data.GetType().IsGenericType && !typeofImplicit.IsGenericType)
            {              
                var miResult = (T)Activator.CreateInstance(typeofImplicit).GetType().ConvertProperties((Dictionary<object, object>)result.Data);

                _log.Debug($"Remote call object {identifier} time is {sw.ElapsedMilliseconds / (float)1000} seconds");

                return miResult;
            }

            _log.Debug($"Remote call object {identifier} time is {sw.ElapsedMilliseconds / (float)1000} seconds");

            return (T) Convert.ChangeType(result.Data, typeof(T));
        }

        public async Task RemoteCall(string channel, string identifier, params object[] param)
        {
            var sw = Stopwatch.StartNew();
            var callbackGuid = Guid.NewGuid().ToString();

            _remoteMethodCallbacks.TryAdd(callbackGuid, new RemoteEventWait());

            var remoteEvent = new RemoteMethodEvent()
            {
                Type = RemoteMethodType.CALL,
                MethodName = identifier,
                CallbackGuid = callbackGuid,
                Data = param,
                Channel = channel,
            };

            var packetData = SerializeMessagePackToFrame(remoteEvent);

            await _sockSem.WaitAsync();

            SendData(packetData, true);
           
            _remoteMethodCallbacks[callbackGuid].Event.WaitOne();
            _remoteMethodCallbacks.TryRemove(callbackGuid, out _);

            sw.Stop();

            _log.Debug($"Remote call object {identifier} time is {(sw.ElapsedMilliseconds / (float)1000)} seconds");
        }

        private void MountHostedObject()
        {
            foreach (MethodInfo sharedMethod in _selfHosted.GetType().GetMethods())
            {
                if (sharedMethod.GetCustomAttribute(typeof(SPMessageAttribute)) is SPMessageAttribute attribute)
                {
                    if (_mountedHostedMethods.ContainsKey(sharedMethod.Name))
                    {
                        _log.Warn($"selfHosted object contain dublicate method! {sharedMethod.Name} skiped.");
                        continue;
                    }

                    var isAsync = sharedMethod.GetCustomAttribute(typeof(AsyncStateMachineAttribute)) is AsyncStateMachineAttribute;
                    var returnType = sharedMethod.ReturnType;
                    var hasAsyncResult = returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>);

                    if (!hasAsyncResult && isAsync && returnType != typeof(Task))
                        throw new Exception($"self hosted function {sharedMethod.Name} is asynchronous but does not return a Task!");

                    _mountedHostedMethods.TryAdd(sharedMethod.Name, new RemoteMethodEvent()
                    {
                        MethodName = sharedMethod.Name,
                        Type = RemoteMethodType.NONE,
                        CallbackGuid = Guid.Empty.ToString(),
                        Method = DelegateWrapper.CreateMethodWrapper(_selfHosted.GetType(), sharedMethod),
                        HasAsyncResult = hasAsyncResult
                    }); 
                }
            }

            foreach(var field in _selfHosted.GetType().GetProperties())
            {
                if(field.GetCustomAttribute(typeof(SPGenericReplicationAttribute)) is SPGenericReplicationAttribute attribute)
                {
                    if (field.PropertyType.IsGenericType)
                    {
                        var installClient = field.PropertyType.GetMethod("InstallClient");
                        var activatedProperty = Activator.CreateInstance(field.PropertyType);

                        installClient.Invoke(activatedProperty, new object[] { this, attribute.ObjectName });

                        _selfHosted.GetType().GetProperty(field.Name).SetValue(_selfHosted, activatedProperty);
                        _remoteReplicationObjects.Add(attribute.ObjectName);

                        if (!_activatedReplicationObjects.ContainsKey(attribute.ObjectName))
                            _activatedReplicationObjects.TryAdd(attribute.ObjectName, activatedProperty);
                    }
                }
                else if(field.GetCustomAttribute(typeof(SPReplicationAttribute)) is SPReplicationAttribute attr)
                {
                    var installClient = field.PropertyType.GetMethod("InstallClient");
                    var activatedProperty = Activator.CreateInstance(field.PropertyType);

                    installClient.Invoke(activatedProperty, new object[] { this, attr.ObjectName });

                    _selfHosted.GetType().GetProperty(field.Name).SetValue(_selfHosted, activatedProperty);
                    _remoteReplicationObjects.Add(attr.ObjectName);

                    if (!_activatedReplicationObjects.ContainsKey(attr.ObjectName))
                        _activatedReplicationObjects.TryAdd(attr.ObjectName, activatedProperty);
                }
            }
        }
        #endregion

        #region Replication methods

        public void RemoteListWillUpdate(NotifyCollectionChangedEventArgs ev, string objectName) => SendData(SerializeMessagePackToFrame(new ReplicationListUpdateEvent() { Channel = _rmiChannel, ObjectName = objectName, NewItems = ev.NewItems, OldItems = ev.OldItems }));

        public void PrimitiveWillUpdate(string objectName, object value) => SendData(SerializeMessagePackToFrame(new ReplicationPrimitiveUpdateEvent() { ObjectName = objectName, Value = value }));

        private void ListUpdateEvent(ReplicationListUpdateEvent ev)
        {
            var prop = _selfHosted.GetType().GetProperties().FirstOrDefault(s =>
            s.GetCustomAttribute(typeof(SPGenericReplicationAttribute)) != null &&
            s.GetCustomAttribute<SPGenericReplicationAttribute>().ObjectName == ev.ObjectName).PropertyType;

            if (prop != null && prop.IsGenericType)
            {
                var baseTypeDefinition = prop.GenericTypeArguments[0];

                var newItems = Activator.CreateInstance(typeof(List<>).MakeGenericType(baseTypeDefinition)) as IList;
                var oldItems = Activator.CreateInstance(typeof(List<>).MakeGenericType(baseTypeDefinition)) as IList;

                if (ev.NewItems != null)
                {
                    newItems = ev.NewItems;
                    for (int i = 0; i < ev.NewItems.Count; i++)
                        newItems[i] = Activator.CreateInstance(baseTypeDefinition).GetType().ConvertProperties((Dictionary<object, object>)newItems[i]);
                }

                if (ev.OldItems != null)
                {
                    oldItems = ev.OldItems;
                    for (int i = 0; i < ev.OldItems.Count; i++)
                        oldItems[i] = Activator.CreateInstance(baseTypeDefinition).GetType().ConvertProperties((Dictionary<object, object>)oldItems[i]);
                }

                if (_activatedReplicationObjects.ContainsKey(ev.ObjectName))                
                    _activatedReplicationObjects[ev.ObjectName].GetType().GetMethod("UpdateReceive").Invoke(_activatedReplicationObjects[ev.ObjectName], new object[] { newItems, oldItems });                
            }
        }

        private void PrimitiveUpdateEvent(ReplicationPrimitiveUpdateEvent ev)
        {
            var prop = _selfHosted.GetType().GetProperties().FirstOrDefault(s => s.GetCustomAttribute(typeof(SPReplicationAttribute)) != null && s.GetCustomAttribute<SPReplicationAttribute>().ObjectName == ev.ObjectName).PropertyType;
            if(prop != null)            
                if (_activatedReplicationObjects.ContainsKey(ev.ObjectName))
                {
                    var methodInfo = _activatedReplicationObjects[ev.ObjectName].GetType().GetMethod("UpdateReceive");
                    methodInfo.Invoke(_activatedReplicationObjects[ev.ObjectName], new object[] { ev.Value });
                }   
        }

        #endregion

        #region Network 
        public void Connect(string host, int port)
        {          
            Socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            try
            {
                Socket.Connect(new IPEndPoint(IPAddress.Parse(host), port));
                
                if (_mountedHostedMethods != null && _mountedHostedMethods.Count > 0)
                    NotyfiServerAboutReadyForRMI();

                if (_remoteReplicationObjects != null && _remoteReplicationObjects.Count > 0)
                    NotyfiServerAboutReplicationInfos();

                Socket.BeginReceive(WaitPacketLength, 0, WaitPacketLength.Length, SocketFlags.None, ServerMessageCallback, null);

                _log.Info($"Connected to channel {_rmiChannel}");
            }
            catch(Exception ex)
            {
                _log.Warn($"Cant connect to {host}:{port} reconnecting!");
                Connect(host, port);
            }
        }

        private void ServerMessageCallback(IAsyncResult result)
        {
            try
            {
                var readed = Socket.EndReceive(result, out var err);
                if (err != SocketError.Success || readed <= 0)
                {
                    Socket.Disconnect(false);
                    Socket.Close();
                    return;
                }

                var len = BitConverter.ToUInt16(WaitPacketLength, 0);
                if (len > 0)
                {
                    var packetDataBuffer = new byte[len];
                    var r = 0;

                    while (r < packetDataBuffer.Length)
                        r += Socket.Receive(packetDataBuffer, r, packetDataBuffer.Length - r, SocketFlags.None);

                    ThreadPool.QueueUserWorkItem((callback) =>
                    {
                        var message = MessagePackSerializer.Deserialize<IMessageEvent>(packetDataBuffer);
                        switch (message)
                        {
                            case PublishEvent publish:
                                if (_subsribedActions != null && _subsribedActions.ContainsKey(publish.Channel) && _subsribedActions[publish.Channel] != null)
                                    _subsribedActions[publish.Channel].Invoke(publish);
                                break;
                            case RemoteMethodEvent remoteEvent:
                                RMIMethodCalled(remoteEvent);
                                break;
                            case ReplicationListUpdateEvent replicationListEvent:
                                ListUpdateEvent(replicationListEvent);
                                break;
                            case ReplicationPrimitiveUpdateEvent primitiveEvent:
                                PrimitiveUpdateEvent(primitiveEvent);
                                break;
                        }
                    });
                    Socket.BeginReceive(WaitPacketLength, 0, WaitPacketLength.Length, SocketFlags.None, ServerMessageCallback, null);
                }
                else
                {
                    Thread.Sleep(1);
                    Socket.BeginReceive(WaitPacketLength, 0, WaitPacketLength.Length, SocketFlags.None, ServerMessageCallback, null);
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                _log.Warn($"Error while receive message:\n{ex}");
            }
        }

        private void SendData(byte[] data, bool isAsync = false)
        {
            if (Socket.Connected)
                ExtSocket.SafeAsyncSend(Socket, data, 0, data.Length, (q, w, e, r, t) => MessageCompleteCallback(q, w, e, r, t, isAsync));
        }

        private void NotyfiServerAboutReadyForRMI() => SendData(SerializeMessagePackToFrame(new RMINotyfiEvent() { Channel = _rmiChannel, MethodNames = _mountedHostedMethods.Keys.ToArray() }));

        private void NotyfiServerAboutReplicationInfos() => SendData(SerializeMessagePackToFrame(new ReplicationNotyfiEvent() { Channel = _rmiChannel, ObjectsToReplicate = _remoteReplicationObjects }));
       
        public void SentRemoteMethodCall(RemoteMethodEvent remoteEvent) => SendData(SerializeMessagePackToFrame(remoteEvent));

        private void MessageCompleteCallback(Socket owner, ActorOperationResult operationResult, int result, byte[] buffer, int bufferOffset, bool isAsync)
        {
            if (isAsync)
                _sockSem.Release();
        }

        #endregion

        #region Remote events methods
        public void Subsribe(string channel, Action<PublishEvent> action)
        {
            if (Socket == null || !Socket.Connected)
                throw new Exception("First you need connect to server then subscribe on events.");

            SendData(SerializeMessagePackToFrame(new SubscribeEvent() { Channel = channel }));

            if (!_subsribedActions.ContainsKey(channel))
                _subsribedActions.TryAdd(channel, action);
        }

        public void Unsubscribe(string channel, bool fromAll = false) => SendData(SerializeMessagePackToFrame(new UnsubscribeEvent() { Channel = channel, FromAll = fromAll }));

        public void Publish(string channel, object obj, string messageHeader = "")
        {           
            ThreadPool.QueueUserWorkItem((callback) => 
            {
                try
                {
                    SendData(SerializePublishMessage(channel, obj, messageHeader));
                }
                catch (ObjectDisposedException)
                {
                }
                catch
                {
                }
            });   
        }

        public void ReplicationListUpdate(ReplicationListUpdateEvent updateInfo)
        {
            ThreadPool.QueueUserWorkItem((callback) =>
            {
                try
                {
                    SendData(SerializeMessagePackToFrame(updateInfo));
                }
                catch (ObjectDisposedException)
                {
                }
                catch
                {
                }
            });
        }

        #endregion

        #region Serialization
        private byte[] SerializePublishMessage(string channel, object data, string messageHeader) => SerializeMessagePackToFrame(new PublishEvent()
        {
            Channel = channel,
            Message = new PublishMessage()
            {
                Header = messageHeader,
                Data = data
            }
        });

        private byte[] SerializeMessagePackToFrame(IMessageEvent message)
        {
            try
            {
                var serializedMessage = MessagePackSerializer.Serialize(message, ContractlessStandardResolver.Instance);
                var packetData = new byte[serializedMessage.Length + 2];
                Buffer.BlockCopy(BitConverter.GetBytes((ushort)serializedMessage.Length), 0, packetData, 0, 2);
                Buffer.BlockCopy(serializedMessage, 0, packetData, 2, serializedMessage.Length);
                return packetData;
            }
            catch(Exception ex)
            {
                _log.Warn($"Error while message has serialized:\n{ex}");
            }
            return null;
        }
        #endregion

        public void Reset()
        {
            Socket = null;
            NetIdentity = Guid.Empty;
        }

        public void Dispose()
        {
            try
            {
                if (Socket.Connected)
                    Socket.Disconnect(false);
            }
            catch (ObjectDisposedException)
            {
                Socket = null;
            }
            finally
            {
                Reset();
            }
        }
    }
}
