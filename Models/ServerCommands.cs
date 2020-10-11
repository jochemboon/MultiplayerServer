using System;
using System.Collections.Generic;
using System.Text;

namespace MultiplayerServer
{
    public enum ServerCommands
    {
        None = 0,
        Unknown = 1,
        Ping = 2,
        SetPlayerData = 10,
        SetPlayerPosition = 11,
        Disconnect = 99
    }
}
