using GamemakerMultiplayerServer.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace GamemakerMultiplayerServer
{
    public static class Server
    {
        public static ManualResetEvent allDone = new ManualResetEvent(false);

        public static Dictionary<Socket, Player> ConnectedPlayers { get; set; }

        public static void Start()
        {
            Console.WriteLine("Starting server");
            ConnectedPlayers = new Dictionary<Socket, Player>();
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
                    socket.BeginAccept(new AsyncCallback(ConnectPlayer), socket);
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

        public static void ConnectPlayer(IAsyncResult ar)
        {
            allDone.Set();

            // Connect player
            var listener = (Socket)ar.AsyncState;
            var socket = listener.EndAccept(ar);

            Console.WriteLine("Player connected");

            try
            {
                ConnectedPlayers.Add(socket,
                    new Player()
                    {
                        Name = "UNASSIGNED",
                        X = 0,
                        Y = 0,
                        Z = 0,
                        ID = 0
                    });

                // Listen to incoming data
                while (socket != null)
                {
                    var state = new StateObject();
                    state.workSocket = socket;
                    string data = ReadCallback(state);

                    if (data != "")
                        ProcessData(socket, data);
                    //Console.WriteLine(content);
                }
            }
            catch (SocketException)
            {
                Console.WriteLine("Client disconnected forcecully, closing connection.");

                // Sluit verbinding
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Something went wrong.");
                Console.WriteLine(ex.Message);
            }
        }

        // Receiving and sending data
        public static string ReadCallback(StateObject state)
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

        public static void SendCallback(Socket socket, string data)
        {
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            socket.Send(byteData, 0, byteData.Length, 0);
        }

        private static void DisconnectSocket(Socket socket)
        {
            socket.Shutdown(SocketShutdown.Both);
            socket.Close();
        }

        // Handle incoming data
        private static void ProcessData(Socket socket, string data)
        {
            /*
            Player player = null;
            if (ConnectedPlayers[socket] != null)
                player = ConnectedPlayers[socket];
            else
                return; // Can't find the associated player, stop
            */

            // Data is mapped by commands by default
            var command = MapCommand(data);

            if (command == ServerCommands.None || command == ServerCommands.Unknown)
            {
                return; // Can't process command properly, stop
            }

            ProcessCommand(socket, command, data);
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
        }

        private static void Ping(Socket socket)
        {
            SendCallback(socket, "PONG");
        }

        private static void DisconnectPlayer(Socket socket)
        {
            var player = ConnectedPlayers[socket];

            if (player != null)
            {
                Console.WriteLine($"Disconnecting player { player.Name }");
            }

            DisconnectSocket(socket);
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

        #region Async variants
        public static void ReadCallbackAsync(IAsyncResult ar)
        {
            string content = string.Empty;

            var state = (StateObject)ar.AsyncState;
            var handler = state.workSocket;

            // Read data
            int byteCount = handler.EndReceive(ar);
            if (byteCount > 0)
            {
                state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, byteCount));

                content = state.sb.ToString();

                if (content.IndexOf("<EOF>") > -1)
                {
                    // Done reading data
                    Console.WriteLine($"Read {content.Length} bytes from socket. \n Data: {content}");

                    // Process data
                    ProcessData(handler, content);
                }
                else
                {
                    // Continue reading data
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallbackAsync), state);
                }
            }
        }

        private static void SendAsync(Socket handler, String data)
        {
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallbackAsync), handler);
        }

        private static void SendCallbackAsync(IAsyncResult ar)
        {
            try
            {
                var socket = (Socket)ar.AsyncState;

                int bytesSent = socket.EndSend(ar);

                Console.WriteLine($"Sent {bytesSent} bytes to the client");

                // Sluit verbinding
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void DisconnectPlayerAsync(IAsyncResult ar)
        {
            Player player;

            try
            {
                var socket = (Socket)ar.AsyncState;

                if (ConnectedPlayers[socket] != null)
                {
                    player = ConnectedPlayers[socket];
                    Console.WriteLine($"Player {player.Name} is disconnecting");
                }

                // Sluit verbinding
                DisconnectSocket(socket);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        #endregion
    }
}
