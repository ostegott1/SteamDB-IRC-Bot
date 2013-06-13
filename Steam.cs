using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SteamKit2;
using System.Threading;
using System.IO;
using System.Configuration;
using MySql.Data.MySqlClient;

namespace IRCbot
{
    class Steam
    {
        static SteamClient steamClient = new SteamClient();
        static SteamUser steamUser = steamClient.GetHandler<SteamUser>();
        static SteamApps steamApps = steamClient.GetHandler<SteamApps>();
        static SteamFriends steamFriends = steamClient.GetHandler<SteamFriends>();
        static SteamUserStats stats = steamClient.GetHandler<SteamUserStats>();
        static List<string> importantapps = new List<string>();
        static CallbackManager manager;
        static uint PreviousChange = 0;
        static string channel;
        static EPersonaState updaterState = EPersonaState.Max;

        private static string GetDBString(string SqlFieldName, MySqlDataReader Reader)
        {
            return Reader[SqlFieldName].Equals(DBNull.Value) ? String.Empty : Reader.GetString(SqlFieldName);
        }

        public static string getPackageName(string subid)
        {
            String name = "";

            MySqlDataReader Reader = DbWorker.ExecuteReader(@"SELECT Name FROM Subs WHERE SubID = @SubID", new MySqlParameter[]
            {
                new MySqlParameter("SubID", subid)
            });

            if (Reader.Read())
            {
                name = GetDBString("Name", Reader);
            }

            Reader.Close();
            Reader.Dispose();

            return name;
        }

        public static string getAppName(string appid)
        {
            String name = "";

            MySqlDataReader Reader = DbWorker.ExecuteReader(@"SELECT IF(StoreName = '', Name, StoreName) as Name FROM Apps WHERE AppID = @AppID", new MySqlParameter[]
            {
                new MySqlParameter("AppID", appid)
            });

            if (Reader.Read())
            {
                name = GetDBString("Name", Reader);
            }

            Reader.Close();
            Reader.Dispose();

            if (name.Equals("") || name.StartsWith("ValveTestApp"))
            {
                MySqlDataReader Reader2 = DbWorker.ExecuteReader(@"SELECT NewValue FROM AppsHistory WHERE AppID = @AppID AND Action = 'created_info' AND `Key` = 1 LIMIT 1", new MySqlParameter[]
                {
                    new MySqlParameter("AppID", appid)
                });

                if (Reader2.Read())
                {
                    name = GetDBString("NewValue", Reader2);
                }

                Reader2.Close();
                Reader2.Dispose();
            }

            return name;
        }

        public static void getNumPlayers(uint appid)
        {
            stats.GetNumberOfCurrentPlayers(appid);
        }

        public static void DumpApp(uint appid)
        {
            steamApps.PICSGetProductInfo(appid, null, false, false);
        }

        public static void DumpSub(uint subid)
        {
            steamApps.PICSGetProductInfo(null, subid, false, false);
        }

        public static void LoadImportantApps()
        {
            importantapps.Clear();

            MySqlDataReader Reader = DbWorker.ExecuteReader(@"SELECT AppID FROM ImportantApps WHERE `Announce` = 1");

            while (Reader.Read())
            {
                importantapps.Add(GetDBString("AppID", Reader));
            }

            Reader.Close();
            Reader.Dispose();
        }

        public static void GetPICSChanges()
        {
            steamApps.PICSGetChangesSince(PreviousChange, true, true);
        }

        public static void Loop()
        {
            channel = ConfigurationManager.AppSettings["announce_channel"];

            LoadImportantApps();

            manager = new CallbackManager(steamClient);

            new Callback<SteamClient.ConnectedCallback>(OnConnected, manager);
            new Callback<SteamClient.DisconnectedCallback>(OnDisconnected, manager);

            new Callback<SteamUser.LoggedOnCallback>(OnLoggedOn, manager);
            new Callback<SteamUser.LoggedOffCallback>(OnLoggedOff, manager);

            new Callback<SteamFriends.PersonaStateCallback>(OnPersonaState, manager);
            new Callback<SteamFriends.ClanStateCallback>(OnClanState, manager);

            new JobCallback<SteamApps.PICSChangesCallback>(OnPICSChanges, manager);
            new JobCallback<SteamApps.PICSProductInfoCallback>(OnPICSProductInfo, manager);
            new JobCallback<SteamUserStats.NumberOfPlayersCallback>(OnNumberOfPlayers, manager);

            Console.WriteLine("Connecting to Steam...");

            steamClient.Connect();

            bool isRunning = true;

            while (isRunning)
            {
                manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
            }
        }

        static void OnClanState(SteamFriends.ClanStateCallback callback)
        {
            Console.WriteLine("Got some new stuff!");

            string ClanName = callback.ClanName;

            if (ClanName == null)
            {
                ClanName = steamFriends.GetClanName(callback.ClanID);
            }

            if (ClanName == "") // [unknown]
            {
                ClanName = "Group";
            }
            else
            {
                ClanName = string.Format("{0}{1}{2}", Colors.OLIVE, ClanName, Colors.NORMAL);
            }

            foreach (var announcement in callback.Announcements)
            {
                Console.WriteLine(announcement.Headline);
                IRCHandler.Send("#steamdb", "{0} announcement: {1}{2}{3} -{4} http://steamcommunity.com/gid/{5}/announcements/detail/{6}", ClanName, Colors.GREEN, announcement.Headline.ToString(), Colors.NORMAL, Colors.DARK_BLUE, callback.ClanID, announcement.ID);
            }

            foreach(var groupevent in callback.Events)
            {
                if (groupevent.JustPosted == true)
                {
                    IRCHandler.Send("#steamdb", "{0} event: {1}{2}{3} -{4} http://steamcommunity.com/gid/{5}/events/{6}", ClanName, Colors.GREEN, groupevent.Headline.ToString(), Colors.NORMAL, Colors.DARK_BLUE, callback.ClanID, groupevent.ID);
                }
            }
        }

        static void OnConnected(SteamClient.ConnectedCallback callback)
        {
            updaterState = EPersonaState.Max;

            if (callback.Result != EResult.OK)
            {
                IRCHandler.SendEmote(channel, "failed to connect: " + callback.Result);

                throw new Exception("Could not connect: " + callback.Result);
            }

            Console.WriteLine("Connected! Logging in...");
            steamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = ConfigurationManager.AppSettings["steam-username"],
                Password = ConfigurationManager.AppSettings["steam-password"]
            });
        }

        static void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            IRCHandler.SendEmote(channel, "disconnected from Steam, reconnecting...");

            Thread.Sleep(TimeSpan.FromSeconds(15));

            steamClient.Connect();
        }

        static void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            Console.WriteLine("Logged on.");
            if (callback.Result != EResult.OK)
            {
                IRCHandler.SendEmote(channel, "failed to log in: " + callback.Result);

                Thread.Sleep(TimeSpan.FromSeconds(2));
            }
            else
            {
                steamFriends.SetPersonaName("Jan-willem");
                steamFriends.SetPersonaState(EPersonaState.Busy);
                GetPICSChanges();

                IRCHandler.SendEmote(channel, "is now logged in.");
            }
        }

        static void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            IRCHandler.SendEmote(channel, "logged off of Steam.");
        }

        static void OnPersonaState(SteamFriends.PersonaStateCallback callback)
        {
            Console.WriteLine(callback.Name.ToString() + " - State: " + callback.State.ToString() + " - PreviousChange: " + PreviousChange);

            if (PreviousChange > 0 && callback.Name.Equals("Jan"))
            {
                if (updaterState != callback.State)
                {
                    if (updaterState != EPersonaState.Max)
                    {
                        if (callback.State == EPersonaState.Busy)
                        {
                            IRCHandler.SendEmote(channel, "Updater is back online"); // TODO: Test this message in announce channel for now, can switch to main channel later
                        }
                        else
                        {
                            IRCHandler.SendEmote("#steamdb", "Updater (or Steam) just died :'( cc Alram and xPaw");
                            IRCHandler.SendEmote(channel, "Updater (or Steam) just died :'("); // Send to both channels
                        }
                    }

                    updaterState = callback.State;
                }
            }
        }

        static void OnPICSChanges(SteamApps.PICSChangesCallback callback, JobID job)
        {
            GetPICSChanges(); // TODO: If this causes troubles, move it to the end of the function

            if (PreviousChange != callback.CurrentChangeNumber)
            {
                PreviousChange = callback.CurrentChangeNumber;

                if (!callback.RequiresFullUpdate)
                {
                    // Colors are fun
                    IRCHandler.Send(channel, "Received changelist {0}{1}{2} with {3}{4}{5} apps and {6}{7}{8} packages -{9} http://steamdb.info/changelist/{10}/",
                        Colors.OLIVE, PreviousChange, Colors.NORMAL,
                        callback.AppChanges.Count >= 10 ? Colors.YELLOW : Colors.OLIVE, callback.AppChanges.Count, Colors.NORMAL,
                        callback.PackageChanges.Count >= 10 ? Colors.YELLOW : Colors.OLIVE, callback.PackageChanges.Count, Colors.NORMAL,
                        Colors.DARK_BLUE, PreviousChange
                    );
                }

                ProcessAppChanges(callback);
                ProcessSubChanges(callback);
            }
        }

        private static void ProcessAppChanges(SteamApps.PICSChangesCallback callback)
        {
            string Message = "";
            string Name = "";

            foreach (var callbackapp in callback.AppChanges)
            {
                Name = getAppName(callbackapp.Key.ToString());

                if (importantapps.Contains(callbackapp.Key.ToString()))
                {
                    IRCHandler.Send("#steamdb", "Important app update: {0}{1}{2} -{3} http://steamdb.info/app/{4}/#section_history", Colors.OLIVE, Name, Colors.NORMAL, Colors.DARK_BLUE, callbackapp.Key.ToString());
                }

                if (Name.Equals(""))
                {
                    Name = string.Format("{0}{1}{2}", Colors.LIGHT_GRAY, callbackapp.Key.ToString(), Colors.NORMAL);
                }
                else
                {
                    Name = string.Format("{0}{1}{2} ({3})", Colors.LIGHT_GRAY, callbackapp.Key.ToString(), Colors.NORMAL, Name);
                }

                if (!PreviousChange.Equals(callbackapp.Value.ChangeNumber))
                {
                    IRCHandler.Send(channel, "App: {0} - bundled changelist {1}{2}{3} -{4} http://steamdb.info/changelist/{5}/", Name, Colors.OLIVE, callbackapp.Value.ChangeNumber, Colors.NORMAL, Colors.DARK_BLUE, callbackapp.Value.ChangeNumber);
                }
                else
                {
                    Message += " " + Name;
                }
            }

            if (!Message.Equals(""))
            {
                IRCHandler.Send(channel, "Apps:{0}", Message); // No space here because Message already has it
            }
        }

        private static void ProcessSubChanges(SteamApps.PICSChangesCallback callback)
        {
            string Message = "";
            string Name = "";

            foreach (var callbackpack in callback.PackageChanges)
            {
                Name = getPackageName(callbackpack.Key.ToString());

                if (callbackpack.Key.Equals(0))
                {
                    IRCHandler.Send("#steamdb", "Important package update: {0}{1}{2} -{3} http://steamdb.info/sub/{4}/#section_history", Colors.OLIVE, Name, Colors.NORMAL, Colors.DARK_BLUE, callbackpack.Key.ToString());
                }

                if (Name.Equals(""))
                {
                    Name = string.Format("{0}{1}{2}", Colors.LIGHT_GRAY, callbackpack.Key.ToString(), Colors.NORMAL);
                }
                else
                {
                    Name = string.Format("{0}{1}{2} ({3})", Colors.LIGHT_GRAY, callbackpack.Key.ToString(), Colors.NORMAL, Name);
                }

                if (!PreviousChange.Equals(callbackpack.Value.ChangeNumber))
                {
                    IRCHandler.Send(channel, "Package: {0} - bundled changelist {1}{2}{3} -{4} http://steamdb.info/changelist/{5}/", Name, Colors.OLIVE, callbackpack.Value.ChangeNumber, Colors.NORMAL, Colors.DARK_BLUE, callbackpack.Value.ChangeNumber);
                }
                else
                {
                    Message += " " + Name;
                }
            }

            if (!Message.Equals(""))
            {
                IRCHandler.Send(channel, "Packages:{0}", Message); // No space here because Message already has it
            }
        }

        static void OnPICSProductInfo(SteamApps.PICSProductInfoCallback callback, JobID job)
        {
            string Name = "";
            string ID = "";

            foreach (var unknownapp in callback.UnknownApps)
            {
                IRCHandler.Send(channel, "Unknown app: {0}{1}{2}", Colors.LIGHT_GRAY, unknownapp.ToString(), Colors.NORMAL);
            }

            foreach (var unknownsub in callback.UnknownPackages)
            {
                IRCHandler.Send(channel, "Unknown package: {0}{1}{2}", Colors.LIGHT_GRAY, unknownsub.ToString(), Colors.NORMAL);
            }

            foreach (var callbackapp in callback.Apps)
            {
                ID = callbackapp.Key.ToString();

                if (callbackapp.Value.KeyValues["common"]["name"].Value == null)
                {
                    Name = "AppID " + ID;
                }
                else
                {
                    Name = callbackapp.Value.KeyValues["common"]["name"].Value.ToString();
                }

                callbackapp.Value.KeyValues.SaveToFile("app/" + ID + ".vdf", false);

                IRCHandler.Send(channel, "Dump for {0}{1}{2} -{3} http://raw.steamdb.info/app/{4}.vdf", Colors.OLIVE, Name, Colors.NORMAL, Colors.DARK_BLUE, ID);
            }

            foreach (var callbacksub in callback.Packages)
            {
                ID = callbacksub.Key.ToString();

                var kv = callback.Packages[uint.Parse(ID)].KeyValues.Children.FirstOrDefault();

                if (kv["name"].Value == null)
                {
                    Name = "SubID " + ID;
                }
                else
                {
                    Name = kv["name"].Value.ToString();
                }

                kv.SaveToFile("sub/" + ID + ".vdf", false);

                IRCHandler.Send(channel, "Dump for {0}{1}{2} -{3} http://raw.steamdb.info/sub/{4}.vdf", Colors.OLIVE, Name, Colors.NORMAL, Colors.DARK_BLUE, ID);
            }
        }

        static void OnNumberOfPlayers(SteamUserStats.NumberOfPlayersCallback callback, JobID job)
        {
            if (callback.Result != EResult.OK)
            {
                IRCHandler.Send(channel, "Unable to request player count: {0}", callback.Result);
            }
            else
            {
                IRCHandler.Send(channel, "Players: {0}", callback.NumPlayers.ToString());
            }
        }
    }
}
