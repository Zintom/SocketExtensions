using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Zintom.SocketExtensions
{

    /// <summary>
    /// A wrapper around <see cref="System.Net.Sockets.Socket"/> which provides a way to send data back and forth
    /// in a length-prefixed and async manner.
    /// </summary>
    public class LengthPrefixedSocket : ISendReceiveAsync
    {

        /// <summary>
        /// Holds the state of a receive operation.
        /// </summary>
        private class ReceiveState
        {
            /// <summary>
            /// The TCS which represents the completion of this receive operation.
            /// </summary>
            public TaskCompletionSource<byte[]> TaskCompletionSource;

            public SocketFlags SocketFlags;

            /// <summary>
            /// The buffer used to receive data into (could be the length prefix or the final data).
            /// </summary>
            public byte[] DataBuffer;

            /// <summary>
            /// The number of bytes read into DataBuffer so far.
            /// </summary>
            public int BytesRead;

            public ReceiveState(TaskCompletionSource<byte[]> taskCompletionSource, byte[] dataBuffer)
            {
                TaskCompletionSource = taskCompletionSource;
                DataBuffer = dataBuffer;
            }
        }

        private readonly Socket _socket;

        /// <summary>
        /// The underlying socket which represents this connection.
        /// </summary>
        public Socket Socket { get => _socket; }

        public LengthPrefixedSocket(Socket underlyingSocket)
        {
            _socket = underlyingSocket;
        }

        #region Operator Overloads
        public static implicit operator Socket(LengthPrefixedSocket lps)
        {
            return lps.Socket;
        }

        public static explicit operator LengthPrefixedSocket(Socket underlyingSocket)
        {
            return new LengthPrefixedSocket(underlyingSocket);
        }
        #endregion

        /// <inheritdoc cref="SendAsync(byte[], SocketFlags)"/>
        public Task<int> SendAsync(byte[] buffer)
        {
            return SendAsync(buffer, SocketFlags.None);
        }

        /// <summary>
        /// Sends data to the socket in a length prefixed manner.
        /// </summary>
        /// <param name="buffer">The byte buffer which contains the data to send.</param>
        /// <remarks><b>Warning:</b> Never use the base <see cref="System.Net.Sockets.Socket"/> Send/Receive methods
        /// in conjunction with this class as they can corrupt the state.</remarks>
        /// <returns>The number of bytes sent to the socket.</returns>
        public Task<int> SendAsync(byte[] buffer, SocketFlags socketFlags)
        {
            // Get the length of the data we want to send, then combine that as a prefix to the actual data.
            byte[] lengthPrefix = BitConverter.GetBytes(buffer.Length);

            byte[] combined = new byte[lengthPrefix.Length + buffer.Length];
            Array.Copy(lengthPrefix, 0, combined, 0, lengthPrefix.Length);
            Array.Copy(buffer, 0, combined, lengthPrefix.Length, buffer.Length);

            var tcs = new TaskCompletionSource<int>(_socket);

            _socket.BeginSend(combined, 0, combined.Length, socketFlags, static ar =>
            {
                var _tcs = (TaskCompletionSource<int>)ar.AsyncState!;
                var _socket = (Socket)_tcs.Task.AsyncState!;

                try { _tcs.TrySetResult(_socket.EndSend(ar)); }
                catch (Exception e) { _tcs.TrySetException(e); }
            }, tcs);

            return tcs.Task;
        }

        /// <inheritdoc cref="ReceiveNextAsync(SocketFlags)"/>
        public Task<byte[]> ReceiveNextAsync()
        {
            return ReceiveNextAsync(SocketFlags.None);
        }

        /// <summary>
        /// Receives the next piece of data from the socket.
        /// </summary>
        /// <param name="socketFlags"></param>
        /// <remarks><b>Warning:</b> Never use the base <see cref="System.Net.Sockets.Socket"/> Send/Receive methods
        /// in conjunction with this class as they can corrupt the state.</remarks>
        /// <returns>The next piece of full data sent by the peer.</returns>
        public Task<byte[]> ReceiveNextAsync(SocketFlags socketFlags)
        {
            // Receieve 4 bytes (int), which will tell us the length prefix of the next piece of data to download.
            byte[] lengthPrefixInBytes = new byte[sizeof(int)];

            var tcs = new TaskCompletionSource<byte[]>();
            var state = new ReceiveState(tcs, lengthPrefixInBytes)
            {
                SocketFlags = socketFlags
            };

            _socket.BeginReceive(lengthPrefixInBytes, 0, lengthPrefixInBytes.Length, socketFlags, InternalReceiveLengthPrefix, state);

            return tcs.Task;
        }

        private void InternalReceiveLengthPrefix(IAsyncResult ar)
        {
            ReceiveState state = (ReceiveState)ar.AsyncState!;

            state.BytesRead += _socket.EndReceive(ar);

            // Check if we have got the full 4 bytes for the length prefix.
            if (state.BytesRead < sizeof(int))
            {
                _socket.BeginReceive(state.DataBuffer, state.BytesRead, state.DataBuffer.Length - state.BytesRead, state.SocketFlags, InternalReceiveLengthPrefix, state);
                return;
            }

            int actualDataLength = BitConverter.ToInt32(state.DataBuffer);
            state.BytesRead = 0;
            state.DataBuffer = new byte[actualDataLength];

            _socket.BeginReceive(state.DataBuffer, 0, state.DataBuffer.Length, state.SocketFlags, InternalReceiveNext, state);
        }

        private void InternalReceiveNext(IAsyncResult ar)
        {
            ReceiveState state = (ReceiveState)ar.AsyncState!;

            state.BytesRead += _socket.EndReceive(ar);

            if (state.BytesRead < state.DataBuffer.Length)
            {
                _socket.BeginReceive(state.DataBuffer, state.BytesRead, state.DataBuffer.Length - state.BytesRead, state.SocketFlags, InternalReceiveNext, state);
                return;
            }

            try { state.TaskCompletionSource.TrySetResult(state.DataBuffer); }
            catch (Exception e) { state.TaskCompletionSource.TrySetException(e); }
        }

    }
}