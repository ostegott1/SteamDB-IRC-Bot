﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Meebey.SmartIrc4net;
using SteamKit2;
using System.Threading;
using System.Configuration;

namespace IRCbot
{
    class Program
    {
        public static IrcClient irc = new IrcClient();

        static void Main(string[] args)
        {
            irc.Encoding = System.Text.Encoding.UTF8;
            irc.SendDelay = 500;
            irc.AutoRetry = true;
            irc.ActiveChannelSyncing = true;
            irc.OnChannelMessage += new IrcEventHandler(IRCHandler.OnChannelMessage);
            irc.AutoRejoin = true;
            irc.AutoRelogin = true;
            irc.AutoReconnect = true;
            irc.AutoRetry = true;
            irc.AutoRejoinOnKick = true;

            string[] serverlist = {ConfigurationManager.AppSettings["irc-server"]};
            string channel = ConfigurationManager.AppSettings["announce_channel"];
            int port = int.Parse(ConfigurationManager.AppSettings["irc-port"]);
            int debug = int.Parse(ConfigurationManager.AppSettings["debug"]);

            try {
                irc.Connect(serverlist, port);
            } catch (ConnectionException e) {
                System.Console.WriteLine("couldn't connect! Reason: "+e.Message);
            }

            try {
                irc.Login("SteamDB", "SteamDB bot", 0, "SteamDB");
                irc.RfcJoin(channel);

                if (debug == 1) { irc.SendMessage(SendType.Message, channel, "Hello friends! Your favorite bot is back!"); }

                new Thread(new ThreadStart(Steam.Loop)).Start();

                irc.Listen();
                irc.Disconnect();
            } catch (ConnectionException) {      
            } catch (Exception e) {
                System.Console.WriteLine("Error occurred! Message: "+e.Message);
                System.Console.WriteLine("Exception: "+e.StackTrace);
            }
        }
    }
}
