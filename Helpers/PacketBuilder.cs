using GamemakerMultiplayerServer.Models;
using System.Text;

namespace GamemakerMultiplayerServer.Helpers
{
    /// <summary>
    /// Builds and formats network packets.
    /// </summary>
    public static class PacketBuilder
    {
        public static string BuildPlayerPosition(Player player)
        {
            var stringBuilder = new StringBuilder("CMD_SPPO", 100); stringBuilder.AppendLine();
            stringBuilder.Append(player.ID); stringBuilder.AppendLine(); // ID
            stringBuilder.Append(player.X); stringBuilder.AppendLine(); // X
            stringBuilder.Append(player.Y); stringBuilder.AppendLine(); // Y
            stringBuilder.Append(player.Z); stringBuilder.AppendLine(); // Z
            stringBuilder.Append("<EOF>"); // Message end

            return stringBuilder.ToString();
        }

        public static string BuildPlayerData(Player player)
        {
            var stringBuilder = new StringBuilder("CMD_SPDA", 100); stringBuilder.AppendLine();
            stringBuilder.Append(player.ID); stringBuilder.AppendLine(); // ID
            stringBuilder.Append(player.Name); stringBuilder.AppendLine(); // Name
            stringBuilder.Append(player.Color); stringBuilder.AppendLine(); // Color
            stringBuilder.Append(player.Team); stringBuilder.AppendLine(); // Team
            stringBuilder.Append("<EOF>"); stringBuilder.AppendLine();// Message end

            return stringBuilder.ToString();
        }
    }
}
