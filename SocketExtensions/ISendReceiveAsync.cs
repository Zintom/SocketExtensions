using System.Threading.Tasks;

namespace Zintom.SocketExtensions
{
    /// <summary>
    /// A class which can send and receive <see cref="byte"/>'s to and from peers.
    /// </summary>
    public interface ISendReceiveAsync
    {

        /// <summary>
        /// Whether the transport layer is connected.
        /// </summary>
        public bool Connected { get; }

        /// <summary>
        /// Send the given <paramref name="buffer"/> of data to the peer.
        /// </summary>
        /// <param name="buffer">The data to be sent.</param>
        /// <returns>The number of bytes sent to the peer.</returns>
        public Task<int> SendAsync(byte[] buffer);

        /// <summary>
        /// Receives the next piece of full data provided by the peer.
        /// </summary>
        /// <returns>A byte[] with the data received in it.</returns>
        public Task<byte[]> ReceiveNextAsync();

    }
}