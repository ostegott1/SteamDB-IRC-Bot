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

        public static void OnChannelMessage(object sender, IrcEventArgs e)
        {
            switch (e.Data.MessageArray[0])
            {
                case "!app":
                {
                    if (!e.Data.MessageArray[1].ToString().Equals(""))
                    {
                        uint appid;

                        if (uint.TryParse(e.Data.MessageArray[1].ToString(), out appid))
                        {
                            Steam.DumpApp(appid);
                        }
                        else
                        {
                            irc.SendMessage(SendType.Message, "#steamdb-announce", "Invalid AppID format!");
                        }
                    }

                    break;
                }
                case "!sub":
                {
                    if (!e.Data.MessageArray[1].ToString().Equals(""))
                    {
                        uint subid;

                        if (uint.TryParse(e.Data.MessageArray[1].ToString(), out subid))
                        {
                            Steam.DumpSub(subid);
                        }
                        else
                        {
                            irc.SendMessage(SendType.Message, "#steamdb-announce", "Invalid SubID format!");
                        }
                    }

                    break;
                }
                case "!numplayers":
                {
                    uint targetapp;

                    if (uint.TryParse(e.Data.MessageArray[1].ToString(), out targetapp))
                    {
                        Steam.getNumPlayers(targetapp);
                    }
                    else
                    {
                        irc.SendMessage(SendType.Message, "#steamdb-announce", "Invalid NumPlayers format!");
                    }

                    break;
                }
                case "!reload":
                {
                    // TODO: Check if user is op
                    Steam.LoadImportantApps();

                    break;
                }
            }
        }
    }
}
