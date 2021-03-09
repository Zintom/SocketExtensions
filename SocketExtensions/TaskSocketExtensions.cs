using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Zintom.SocketExtensions
{
    /// <summary>
    /// Provides a set of extension methods which return <see cref="Task"/> equivalents of common <see cref="System.Net.Sockets.Socket"/> functions.
    /// </summary>
    /// <remarks>Such as, Accept, Connect, Disconnect, Send, Receive</remarks>
    public static class TaskSocketExtensions
    {

        /// <inheritdoc cref="Socket.Accept"/>
        public static Task<Socket> AcceptAsyncTask(this Socket listener)
        {
            var tcs = new TaskCompletionSource<Socket>(listener);

            listener.BeginAccept(static (ar) =>
            {
                var _tcs = (TaskCompletionSource<Socket>)ar.AsyncState!;
                var _listener = (Socket)_tcs.Task.AsyncState!;

                try { _tcs.TrySetResult(_listener.EndAccept(ar)); }
                catch (Exception e) { _tcs.TrySetException(e); }
            }, tcs);

            return tcs.Task;
        }

        /// <inheritdoc cref="Socket.Disconnect(bool)"/>
        public static Task DisconnectAsyncTask(this Socket socket, bool reuseSocket)
        {
            var tcs = new TaskCompletionSource(socket);

            socket.BeginDisconnect(reuseSocket, static (ar) =>
            {
                var _tcs = (TaskCompletionSource)ar.AsyncState!;
                var _socket = (Socket)_tcs.Task.AsyncState!;

                try { _tcs.TrySetResult(); _socket.EndDisconnect(ar); }
                catch (Exception e) { _tcs.TrySetException(e); }
            }, tcs);

            return tcs.Task;
        }

        /// <inheritdoc cref="Socket.Connect(IPAddress, int)"/>
        public static Task ConnectAsyncTask(this Socket socket, IPAddress address, int port)
        {
            var tcs = new TaskCompletionSource(socket);

            socket.BeginConnect(address, port, static (ar) =>
            {
                var _tcs = (TaskCompletionSource)ar.AsyncState!;
                var _socket = (Socket)_tcs.Task.AsyncState!;

                try { _tcs.TrySetResult(); _socket.EndConnect(ar); }
                catch (Exception e) { _tcs.TrySetException(e); }
            }, tcs);

            return tcs.Task;
        }

        /// <inheritdoc cref="Socket.Send(byte[], int, int, SocketFlags)"/>
        public static Task<int> SendAsyncTask(this Socket socket, byte[] buffer, int offset, int size, SocketFlags socketFlags)
        {
            // We have to pass the Socket and TCS as state objects because we are using a static
            // anonymous function which cannot access locals or fields defined outside of it.
            var tcs = new TaskCompletionSource<int>(socket);

            // We use static anonymous functions as we do not want to capture any outside variables, capturing variables
            // causes each captured variable to be allocated as a boxed object.
            socket.BeginSend(buffer, offset, size, socketFlags, static (ar) =>
            {
                var t = (TaskCompletionSource<int>)ar.AsyncState!;
                var s = (Socket)t.Task.AsyncState!;

                try { t.TrySetResult(s.EndSend(ar)); }
                catch (Exception e) { t.TrySetException(e); }
            }, tcs);

            return tcs.Task;
        }

        /// <inheritdoc cref="Socket.Receive(byte[], int, int, SocketFlags)"/>
        public static Task<int> ReceiveAsyncTask(this Socket socket, byte[] buffer, int offset, int size, SocketFlags socketFlags)
        {
            var tcs = new TaskCompletionSource<int>(socket);

            socket.BeginReceive(buffer, offset, size, socketFlags, static (ar) =>
            {
                var _tcs = (TaskCompletionSource<int>)ar.AsyncState!;
                var _socket = (Socket)_tcs.Task.AsyncState!;

                try { _tcs.TrySetResult(_socket.EndReceive(ar)); }
                catch (Exception e) { _tcs.TrySetException(e); }
            }, tcs);

            return tcs.Task;
        }

    }
}
