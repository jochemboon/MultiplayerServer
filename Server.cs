using MultiplayerServer.Helpers;
using MultiplayerServer.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MultiplayerServer
{
    public static class Server
    {
        public static ManualResetEvent allDone = new ManualResetEvent(false);

        public static Dictionary<Socket, Player> ConnectedPlayers { get; set; }

        public static Stopwatch stopWatch { get; set; }

        public static void Start()
        {
            Console.WriteLine("Starting server");
            ConnectedPlayers = new Dictionary<Socket, Player>();
            stopWatch = new Stopwatch();
            int port = 2525;

            // Create listener
            var ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            var ipAddress = ipHostInfo.AddressList[1];
            var localEndPoint = new IPEndPoint(ipAddress, 2525);

            // Create TCP socket
            var socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            Console.WriteLine($"Server started on {ipAddress.ToString()}:{port}");

            // Listen for incoming connections
            try
            {
                socket.Bind(localEndPoint);
                socket.Listen(100);

                Console.WriteLine("Started listening to players");

                for (; ; )
                {
                    allDone.Reset();

                    // Start an asynchronous socket to listen for connections.  
                    socket.BeginAccept(new AsyncCallback(SetupPlayerConnection), socket);
                    allDone.WaitOne();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            Console.WriteLine("\nServer closed.");
            Console.WriteLine("\nPress ENTER to continue...");
            Console.Read();
        }

        /// <summary>
        /// Adds a player to the pool of connected player and starts listening to data from the connected socket.
        /// </summary>
        /// <param name="ar">The player</param>
        public static void SetupPlayerConnection(IAsyncResult ar)
        {
            allDone.Set();

            // Connect player
            var listener = (Socket)ar.AsyncState;
            var socket = listener.EndAccept(ar);

            Console.WriteLine("Player connected");
            var player = AddPlayer(socket);
            UpdatePlayerData(player);
            try
            {
                while (socket != null)
                {
                    var state = new StateObject();
                    state.workSocket = socket;
                    string data = ReadData(state);

                    if (data != "")
                        ProcessData(socket, data);
                }
            }
            catch (SocketException)
            {
                Console.WriteLine("Client disconnected forcefully, closing connection.");
                DisconnectPlayer(socket);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Something went wrong.");
                Console.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// Adds a player to the socket
        /// </summary>
        /// <param name="socket"></param>
        public static Player AddPlayer(Socket socket)
        {
            int id = 0;

            if(ConnectedPlayers.Count != 0)
                id = ConnectedPlayers.Max(cp => cp.Value.ID) + 1;

            var player = new Player()
            {
                ID = id,
                Name = "UNASSIGNED",
                X = 0,
                Y = 0,
                Z = 0,
                Team = "UNASSIGNED",
                Color = "UNASSIGNED"
            };

            ConnectedPlayers.Add(socket, player);

            return player;
        }

        #region Data handling
        /// <summary>
        /// 
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public static string ReadData(StateObject state)
        {
            string data = string.Empty;
            var socket = state.workSocket;

            while (true)
            {
                var byteCount = socket.Receive(state.buffer, 0, StateObject.BufferSize, 0);

                // Read data
                if (byteCount > 0)
                {
                    state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, byteCount));

                    data = state.sb.ToString();

                    if (data.IndexOf("<EOF>") > -1)
                    {
                        return data;
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="data"></param>
        public static void SendData(Socket socket, string data)
        {
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            socket.Send(byteData, 0, byteData.Length, 0);
        }

        /// <summary>
        /// Handles incoming data by mapping it to the relevant command.
        /// </summary>
        /// <param name="socket">The socket on which the data was received.</param>
        /// <param name="data">Data read from the client.</param>
        private static void ProcessData(Socket socket, string data)
        {
            // Data is mapped by commands by default
            var command = MapCommand(data);

            if (command == ServerCommands.None || command == ServerCommands.Unknown)
            {
                return; // Can't process command properly, stop
            }

            ProcessCommand(socket, command, data);
        }
        #endregion

        /// <summary>
        /// Synchronizes the data of a single player among all other players.
        /// </summary>
        /// <param name="player"></param>
        private static void UpdatePlayerPosition(Player player)
        {
            stopWatch.Reset();
            stopWatch.Start();

            string data = PacketBuilder.BuildPlayerPosition(player);

            foreach (var socket in ConnectedPlayers.Keys)
            {
                SendData(socket, data);
            }

            stopWatch.Stop();
            Console.WriteLine($"Updating position of {player.ID}:{ player.Name } to { ConnectedPlayers.Count } players took { stopWatch.ElapsedMilliseconds } ms");
        }

        private static void UpdatePlayerData(Player player)
        {
            stopWatch.Reset();
            stopWatch.Start();

            string data = PacketBuilder.BuildPlayerData(player);

            foreach (var socket in ConnectedPlayers.Keys)
            {
                SendData(socket, data);
            }

            stopWatch.Stop();
            Console.WriteLine($"Updating data of {player.ID}:{ player.Name } to { ConnectedPlayers.Count } players took { stopWatch.ElapsedMilliseconds } ms");
        }

        private static void DisconnectPlayer(Socket socket)
        {
            var player = ConnectedPlayers[socket];

            if (player != null)
            {
                Console.WriteLine($"Disconnecting player { player.Name }");
            }

            socket.Shutdown(SocketShutdown.Both);
            ConnectedPlayers.Remove(socket);
            socket.Close();
        }

        #region Commands
        private static void SetPlayerData(Socket socket, string data)
        {
            var player = ConnectedPlayers[socket];

            if (string.IsNullOrWhiteSpace(data) || player == null)
                return;

            string[] dataLines = SplitData(data);

            // Map values
            player.Name = dataLines[1];
            player.Color = dataLines[2];
            player.Team = dataLines[3];

            Console.WriteLine($"Name: { player.Name }, color: { player.Color }, team: { player.Team }");
            UpdatePlayerData(player);
        }

        private static void SetPlayerPosition(Socket socket, string data)
        {
            var player = ConnectedPlayers[socket];

            if (string.IsNullOrWhiteSpace(data) || player == null)
                return;

            string[] dataLines = SplitData(data);

            // Map values
            int _X;
            if (int.TryParse(dataLines[1], out _X))
            {
                player.X = _X;

            }

            int _Y;
            if (int.TryParse(dataLines[2], out _Y))
            {
                player.Y = _Y;

            }

            int _Z;
            if (int.TryParse(dataLines[3], out _Z))
            {
                player.Z = _Z;

            }

            Console.WriteLine($"{ player.Name } moved to: X { player.X } Y: { player.Y } Z: { player.Z }");
            UpdatePlayerPosition(player);
        }

        private static void Ping(Socket socket)
        {
            SendData(socket, "PONG");
        }
        #endregion

        #region Processing commands
        private static void ProcessCommand(Socket socket, ServerCommands command, string data)
        {
            Player player;

            if (ConnectedPlayers[socket] != null)
                player = ConnectedPlayers[socket];

            switch (command)
            {
                case ServerCommands.Ping:
                    Ping(socket);
                    break;
                case ServerCommands.Disconnect:
                    DisconnectPlayer(socket);
                    break;
                case ServerCommands.SetPlayerData:
                    SetPlayerData(socket, data);
                    break;
                case ServerCommands.SetPlayerPosition:
                    SetPlayerPosition(socket, data);
                    break;
            }
        }

        private static ServerCommands MapCommand(string data)
        {
            string command;

            // Fetch command string
            if (data.Contains("CMD_"))
            {
                int commandIndex = data.IndexOf("CMD_");
                command = data.Substring(commandIndex + 4, 4);
            }
            else
            {
                return ServerCommands.None;
            }

            // Map command string
            switch (command)
            {
                case "SPDA":
                    return ServerCommands.SetPlayerData;
                case "SPPO":
                    return ServerCommands.SetPlayerPosition;
                case "DISC":
                    return ServerCommands.Disconnect;
                case "PING":
                    return ServerCommands.Ping;
                default:
                    return ServerCommands.Unknown;
            }
        }

        private static string[] SplitData(string data)
        {
            return data.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        }
        #endregion
    }
}
