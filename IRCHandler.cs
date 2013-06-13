using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SteamKit2;
using Meebey.SmartIrc4net;
using System.Threading;
using System.IO;
using System.Configuration;
using MySql.Data.MySqlClient;

namespace IRCbot
{
    class Colors
    {
        static char ETX = '\x03';

        public static char NORMAL = '\x15';
        public static char BOLD = '\x02';
        public static char UNDERLINE = '\x31';
        public static char REVERSE = '\x22';
        public static string WHITE = ETX + "00";
        public static string BLACK = ETX + "01";
        public static string DARK_BLUE = ETX + "02";
        public static string DARK_GREEN = ETX + "03";
        public static string RED = ETX + "04";
        public static string BROWN = ETX + "05";
        public static string PURPLE = ETX + "06";
        public static string OLIVE = ETX + "07";
        public static string YELLOW = ETX + "08";
        public static string GREEN = ETX + "09";
        public static string TEAL = ETX + "10";
        public static string CYAN = ETX + "11";
        public static string BLUE = ETX + "12";
        public static string MAGENTA = ETX + "13";
        public static string DARK_GRAY = ETX + "14";
        public static string LIGHT_GRAY = ETX + "15";
    }

    class IRCHandler
    {
        public static IrcClient irc = IRCbot.Program.irc;

        public static void Send( string channel, string format, params object[] args )
        {
            irc.SendMessage( SendType.Message, channel, string.Format( format, args ) );
        }

        public static void SendEmote( string channel, string format, params object[] args )
        {
            irc.SendMessage( SendType.Action, channel, string.Format( format, args ) );
        }

        public static void OnChannelMessage(object sender, IrcEventArgs e)
        {
            switch (e.Data.MessageArray[0])
            {
                case "!app":
                {
                    uint appid;

                    if (e.Data.MessageArray.Length == 2 && uint.TryParse(e.Data.MessageArray[1].ToString(), out appid))
                    {
                        Steam.DumpApp(appid);
                    }
                    else
                    {
                        Send("#steamdb-announce", "Usage: !app <appid>");
                    }

                    break;
                }
                case "!sub":
                {
                    uint subid;

                    if (e.Data.MessageArray.Length == 2 && uint.TryParse(e.Data.MessageArray[1].ToString(), out subid))
                    {
                        Steam.DumpSub(subid);
                    }
                    else
                    {
                        Send("#steamdb-announce", "Usage: !sub <subid>");
                    }

                    break;
                }
                case "!numplayers":
                {
                    uint targetapp;

                    if (e.Data.MessageArray.Length == 2 && uint.TryParse(e.Data.MessageArray[1].ToString(), out targetapp))
                    {
                        Steam.getNumPlayers(targetapp);
                    }
                    else
                    {
                        Send("#steamdb-announce", "Usage: !numplayers <appid>");
                    }

                    break;
                }
                case "!reload":
                {
                    Channel ircChannel = irc.GetChannel(e.Data.Channel);

                    foreach (ChannelUser user in ircChannel.Users.Values)
                    {
                        if (user.IsOp && e.Data.Nick == user.Nick)
                        {
                            Steam.LoadImportantApps();
                            Steam.LoadImportantPackages();
                            SendEmote("#steamdb-announce", "reloaded important apps");

                            break;
                        }
                    }

                    break;
                }
                case "!force":
                {
                    Channel ircChannel = irc.GetChannel(e.Data.Channel);

                    foreach (ChannelUser user in ircChannel.Users.Values)
                    {
                        if (user.IsOp && e.Data.Nick == user.Nick)
                        {
                            Steam.GetPICSChanges();
                            SendEmote("#steamdb-announce", "forced check");

                            break;
                        }
                    }

                    break;
                }
            }
        }
    }
}
