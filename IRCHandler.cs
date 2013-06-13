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
        public static char NORMAL = (char)15;
        public static char BOLD = (char)2;
        public static char UNDERLINE = (char)31;
        public static char REVERSE = (char)22;
        public static string WHITE = (char)3 + "00";
        public static string BLACK = (char)3 + "01";
        public static string DARK_BLUE = (char)3 + "02";        
        public static string DARK_GREEN = (char)3 + "03";
        public static string RED = (char)3 + "04";
        public static string BROWN = (char)3 + "05";
        public static string PURPLE = (char)3 + "06";
        public static string OLIVE = (char)3 + "07";
        public static string YELLOW = (char)3 + "08";
        public static string GREEN = (char)3 + "09";
        public static string TEAL = (char)3 + "10";
        public static string CYAN = (char)3 + "11";
        public static string BLUE = (char)3 + "12";
        public static string MAGENTA = (char)3 + "13";
        public static string DARK_GRAY = (char)3 + "14";
        public static string LIGHT_GRAY = (char)3 + "15";
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
            if (e.Data.Type != ReceiveType.ChannelAction)
            {
                return;
            }

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
