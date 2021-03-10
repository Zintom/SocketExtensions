using System;
using System.Net.Sockets;
using Zintom.SocketExtensions;

namespace DemoProject
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            Socket s = new Socket(null);
            s.BeginSendLP(new byte[1024], EndSend);

            s.BeginReceiveLP(EndReceive);
        }

        public static void EndSend(Socket s)
        {

        }

        public static void EndReceive(byte[] buffer, Socket socket)
        {

        }
    }
}
