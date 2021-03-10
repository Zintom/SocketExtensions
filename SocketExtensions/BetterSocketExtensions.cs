using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Zintom.SocketExtensions
{
    /// <summary>
    /// This class aims to simplify sending/receiving over a Socket connection.
    /// </summary>
    /// <remarks>All data sent with these extension methods are sent in a length-prefixed manner, this way, receive can know exactly when
    /// the next piece of full data has been sent by the peer.</remarks>
    public static class BetterSocketExtensions
    {

        /// <summary>
        /// Holds the state of a receive operation.
        /// </summary>
        private class ReceiveState
        {
            public readonly Socket Socket;

            public readonly Action<byte[], Socket> EndReceiveCallback;

            /// <summary>
            /// The buffer used to receive data into (could be the length prefix or the final data).
            /// </summary>
            public byte[] DataBuffer;

            /// <summary>
            /// The number of bytes read into DataBuffer so far.
            /// </summary>
            public int BytesRead;

            public ReceiveState(Socket socket, byte[] dataBuffer, Action<byte[], Socket> endReceiveCallback)
            {
                Socket = socket;
                DataBuffer = dataBuffer;
                EndReceiveCallback = endReceiveCallback;
            }
        }

        /// <summary>
        /// Sends data to the socket in a length prefixed manner (use BeginReceiveLP to receive this data).
        /// </summary>
        /// <param name="buffer">The byte buffer which contains the data to send.</param>
        /// <remarks><b>Warning:</b> Never use the base <see cref="System.Net.Sockets.Socket"/> Send/Receive methods
        /// in conjunction with this class as they can corrupt the state.</remarks>
        public static void BeginSendLP(this Socket socket, byte[] buffer, Action<Socket> asyncCallback)
        {
            // Get the length of the data we want to send, then combine that as a prefix to the actual data.
            byte[] lengthPrefix = BitConverter.GetBytes(buffer.Length);

            byte[] combined = new byte[lengthPrefix.Length + buffer.Length];
            Array.Copy(lengthPrefix, 0, combined, 0, lengthPrefix.Length);
            Array.Copy(buffer, 0, combined, lengthPrefix.Length, buffer.Length);

            socket.BeginSend(combined, 0, combined.Length, SocketFlags.None, ar =>
            {
                socket.EndSend(ar);
                asyncCallback.Invoke(socket);
            }, null);
        }

        /// <summary>
        /// Begins to receive the next piece of length-prefixed data from the socket, <paramref name="endReceiveCallback"/> is called when the receive operation is complete.
        /// </summary>
        /// <remarks><b>Warning:</b> Never use the base <see cref="System.Net.Sockets.Socket"/> Send/Receive methods
        /// in conjunction with this class as they can corrupt the state.</remarks>
        public static void BeginReceiveLP(this Socket socket, Action<byte[], Socket> endReceiveCallback)
        {
            // Receieve 4 bytes (int), which will tell us the length prefix of the next piece of data to download.
            byte[] lengthPrefixInBytes = new byte[sizeof(int)];

            var state = new ReceiveState(socket, lengthPrefixInBytes, endReceiveCallback);

            socket.BeginReceive(lengthPrefixInBytes, 0, lengthPrefixInBytes.Length, SocketFlags.None, InternalReceiveLengthPrefix, state);
        }

        private static void InternalReceiveLengthPrefix(IAsyncResult ar)
        {
            ReceiveState state = (ReceiveState)ar.AsyncState!;

            state.BytesRead += state.Socket.EndReceive(ar);

            // Check if we have got the full 4 bytes for the length prefix.
            if (state.BytesRead < sizeof(int))
            {
                state.Socket.BeginReceive(state.DataBuffer, state.BytesRead, state.DataBuffer.Length - state.BytesRead, SocketFlags.None, InternalReceiveLengthPrefix, state);
                return;
            }

            int actualDataLength = BitConverter.ToInt32(state.DataBuffer);
            state.BytesRead = 0;
            state.DataBuffer = new byte[actualDataLength];

            state.Socket.BeginReceive(state.DataBuffer, 0, state.DataBuffer.Length, SocketFlags.None, InternalReceiveNext, state);
        }

        private static void InternalReceiveNext(IAsyncResult ar)
        {
            ReceiveState state = (ReceiveState)ar.AsyncState!;

            state.BytesRead += state.Socket.EndReceive(ar);

            if (state.BytesRead < state.DataBuffer.Length)
            {
                state.Socket.BeginReceive(state.DataBuffer, state.BytesRead, state.DataBuffer.Length - state.BytesRead, SocketFlags.None, InternalReceiveNext, state);
                return;
            }

            state.EndReceiveCallback.Invoke(state.DataBuffer, state.Socket);
        }

    }
}
