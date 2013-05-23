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
    class IRCHandler
    {
        public static IrcClient irc = IRCbot.Program.irc;

        public static void OnDisconnected(object sender, EventArgs e)
        {
            irc.Reconnect();
        }
        public static void OnChannelMessage(object sender, IrcEventArgs e)
        {
            switch (e.Data.MessageArray[0])
            {
                case "!app":
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
                case "!sub":
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
                case "!numplayers":
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
        }
    }
}
