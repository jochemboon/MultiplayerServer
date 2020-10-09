using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace GamemakerMultiplayerServer
{
    class Program
    {
        static int Main(string[] args)
        {
            Server.Start();

            return 0;
        }
    }
}
