using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SuperProxy.Extensions
{
    public static class ExtSocket
    {
        public delegate void OnActionComplete(Socket owner, ActorOperationResult operationResult, int result, byte[] buffer, int bufferOffset);

        public static void AsyncReceiveFixed(this Socket serviceSocket, byte[] buffer, OnActionComplete completeCallback)
        {
            AsyncReceiveFixed(serviceSocket, buffer, 0, buffer.Length, completeCallback);
        }

        public static void AsyncReceiveFixed(this Socket serviceSocket, byte[] buffer, int offset, int len, OnActionComplete completeCallback)
        {
            SafeAsyncReceive(serviceSocket, buffer, offset, len, (owner, result, i, bytes, bufferOffset) =>
            {
                if (!serviceSocket.Connected)
                {
                    OperationWasCancelled(serviceSocket, completeCallback, buffer, offset);
                    return;
                }

                if (i < len)
                    AsyncReceiveFixed(serviceSocket, buffer, offset + i, len - i, completeCallback);
                else
                    completeCallback(serviceSocket, ActorOperationResult.Completed, len, buffer, offset);
            });
        }

        public static void SafeAsyncReceive(this Socket serviceSocket, int len, OnActionComplete completeCallback)
        {
            var buffer = new byte[len];
            SafeAsyncReceive(serviceSocket, buffer, 0, len, completeCallback);
        }

        public static void SafeAsyncReceive(this Socket serviceSocket, byte[] buffer, OnActionComplete completeCallback)
        {
            SafeAsyncReceive(serviceSocket, buffer, 0, buffer.Length, completeCallback);
        }

        public static void SafeAsyncReceive(this Socket serviceSocket, byte[] buffer, int offset, int len, OnActionComplete completeCallback)
        {
            if (!serviceSocket.Connected)
            {
                OperationWasCancelled(serviceSocket, completeCallback, buffer, offset);
                return;
            }

            try
            {
                serviceSocket.BeginReceive(buffer, offset, len, SocketFlags.None, out var brerr, ar =>
                {
                    if (!serviceSocket.Connected)
                    {
                        OperationWasCancelled(serviceSocket, completeCallback, buffer, offset);
                        return;
                    }

                    try
                    {
                        var result = serviceSocket.EndReceive(ar, out var ererr);

                        if (result <= 0 || ererr != SocketError.Success)
                        {
                            OperationWasCancelled(serviceSocket, completeCallback, buffer, offset);
                            return;
                        }

                        completeCallback(serviceSocket, ar.CompletedSynchronously ? ActorOperationResult.CompletedSyncroniously : ActorOperationResult.Completed, result, buffer, offset);
                    }
                    catch (SocketException e)
                    {
#if DEBUG
                        Debug.WriteLine($"Socket exception on end receive operation: {e}");
                        OperationWasCancelled(serviceSocket, completeCallback, buffer, offset);
#endif
                    }
                    catch (ObjectDisposedException e)
                    {
#if DEBUG
                        Debug.WriteLine($"Socket disposed exception on end receive operation: {e}");
                        OperationWasCancelled(serviceSocket, completeCallback, buffer, offset);
#endif
                    }

                }, serviceSocket);
            }
            catch (SocketException e)
            {
#if DEBUG
                Debug.WriteLine($"Socket exception on attempt to receive operation: {e}");
                OperationWasCancelled(serviceSocket, completeCallback, buffer, offset);
#endif
            }
            catch (ObjectDisposedException e)
            {
#if DEBUG
                Debug.WriteLine($"Socket disposed exception on attempt to receive operation: {e}");
                OperationWasCancelled(serviceSocket, completeCallback, buffer, offset);
#endif
            }
        }

        public static void SafeAsyncSend(this Socket serviceSocket, byte[] buffer, OnActionComplete completeCallback)
        {
            SafeAsyncSend(serviceSocket, buffer, 0, buffer.Length, completeCallback);
        }

        public static void SafeAsyncSend(this Socket serviceSocket, byte[] buffer, int offset, int len, OnActionComplete completeCallback)
        {
            if (!serviceSocket.Connected)
            {
                OperationWasCancelled(serviceSocket, completeCallback, buffer, offset);
                return;
            }

            try
            {
                serviceSocket.BeginSend(buffer, offset, len, SocketFlags.None, out var bserr, ar =>
                {
                    if (!serviceSocket.Connected)
                    {
                        OperationWasCancelled(serviceSocket, completeCallback, buffer, offset);
                        return;
                    }

                    try
                    {
                        var result = serviceSocket.EndSend(ar, out var eserr);
                        if (result <= 0 || eserr != SocketError.Success)
                        {
                            OperationWasCancelled(serviceSocket, completeCallback, buffer, offset);
                            return;
                        }

                        completeCallback(serviceSocket, ar.CompletedSynchronously ? ActorOperationResult.CompletedSyncroniously : ActorOperationResult.Completed, result, buffer, offset);
                    }
                    catch (SocketException e)
                    {
#if DEBUG
                        Debug.WriteLine($"Socket exception on end send operation: {e}");
                        OperationWasCancelled(serviceSocket, completeCallback, buffer, offset);
#endif
                    }
                    catch (ObjectDisposedException e)
                    {
#if DEBUG
                        Debug.WriteLine($"Socket disposed exception on end send operation: {e}");
                        OperationWasCancelled(serviceSocket, completeCallback, buffer, offset);
#endif
                    }

                }, serviceSocket);

                if (bserr != SocketError.Success)
                    serviceSocket.Close();
            }
            catch (SocketException e)
            {
#if DEBUG
                Debug.WriteLine($"Socket exception on attempt to begin send operation: {e}");
                OperationWasCancelled(serviceSocket, completeCallback, buffer, offset);
#endif
            }
            catch (ObjectDisposedException e)
            {
#if DEBUG
                Debug.WriteLine($"Socket disposed exception on attempt to begin send operation: {e}");
                OperationWasCancelled(serviceSocket, completeCallback, buffer, offset);
#endif
            }
        }

        private static void OperationWasCancelled(Socket serviceSocket, OnActionComplete action, byte[] buffer, int offset)
        {
            action(serviceSocket, ActorOperationResult.Canceled, 0, buffer, offset);
        }

        public static TcpState GetState(this TcpClient tcpClient) => IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections().SingleOrDefault(x => x.LocalEndPoint.Equals(tcpClient.Client.LocalEndPoint))?.State ?? TcpState.Unknown;
    }

    public enum ActorOperationResult
    {
        CompletedSyncroniously,
        Completed,
        Canceled
    }
}
