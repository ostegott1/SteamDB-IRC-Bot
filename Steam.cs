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
    class Steam
    {
        public static SteamClient steamClient = new SteamClient();
        public static SteamUser steamUser = steamClient.GetHandler<SteamUser>();
        public static SteamApps steamApps = steamClient.GetHandler<SteamApps>();
        public static SteamFriends steamFriends = steamClient.GetHandler<SteamFriends>();
        public static SteamUserStats stats = steamClient.GetHandler<SteamUserStats>();
        public static IrcClient irc = IRCbot.Program.irc;
        public static List<string> importantapps = new List<string>();
        public static CustomCallbacks customHandler;

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

        public static void Loop()
        {
            uint PreviousChange = 0;
            string channel = ConfigurationManager.AppSettings["announce_channel"];
            string updaterState = "None";

            LoadImportantApps();

            steamClient.AddHandler(new CustomCallbacks());
            customHandler = steamClient.GetHandler<CustomCallbacks>();

            steamClient.Connect();

            bool running = true;

            while (running)
            {
                CallbackMsg msg = steamClient.WaitForCallback(true);

                msg.Handle<SteamClient.ConnectedCallback>(callback =>
                {
                    if (callback.Result != EResult.OK)
                    {
                        irc.SendMessage(SendType.Action, channel, "failed to connect: " + callback.Result);

                        throw new Exception("Could not connect: " + callback.Result);
                    }

                    steamUser.LogOn(new SteamUser.LogOnDetails
                    {
                        Username = ConfigurationManager.AppSettings["steam-username"],
                        Password = ConfigurationManager.AppSettings["steam-password"]
                    });
                });

                msg.Handle<SteamClient.DisconnectedCallback>(callback =>
                {
                    irc.SendMessage(SendType.Action, channel, "disconnected from Steam, reconnecting...");
                    Thread.Sleep(TimeSpan.FromSeconds(15));
                    steamClient.Connect();
                });

                msg.Handle<SteamFriends.PersonaStateCallback>(callback =>
                {
                    Console.WriteLine(callback.Name.ToString() + " + " + callback.State.ToString());

                    if (PreviousChange > 0 && callback.Name == "Jan")
                    {
                        if (updaterState == "None")
                        {
                            updaterState = callback.State.ToString();
                        }
                        else if (updaterState != callback.State.ToString())
                        {
                            if (callback.State == EPersonaState.Busy)
                            {
                                irc.SendMessage(SendType.Action, "#steamdb", "Updater is now back online :)");
                            }
                            else
                            {
                                irc.SendMessage(SendType.Action, "#steamdb", "Updater (or Steam) just died :'( cc Alram and xPaw");
                            }

                            updaterState = callback.State.ToString();
                        }
                    }
                });

                msg.Handle<CustomCallbacks.announcementCallback>(callback =>
                {
                    foreach (var announcement in callback.Result.Body.announcements)
                    {
                        Console.WriteLine(announcement.gid.ToString() + announcement.headline.ToString());

                        irc.SendMessage(SendType.Message, "#steamdb", "Group announcement: " + Colors.GREEN + announcement.headline.ToString() + Colors.NORMAL
                            + " - " + Colors.DARK_BLUE + "http://steamcommunity.com/gid/" + callback.Result.Body.steamid_clan + "/announcements/detail/" + announcement.gid + Colors.NORMAL);
                    }
                });

                msg.Handle<SteamUser.LoggedOnCallback>(callback =>
                {
                    if (callback.Result != EResult.OK)
                    {
                        irc.SendMessage(SendType.Action, channel, "failed to log in: " + callback.Result);
                        System.Threading.Thread.Sleep(1000);
                    }
                    else
                    {
                        steamFriends.SetPersonaName("Jan-willem");
                        steamFriends.SetPersonaState(EPersonaState.Busy);
                        irc.SendMessage(SendType.Action, channel, "is now logged in.");
                        steamApps.PICSGetChangesSince(PreviousChange, true, true);
                    }
                });

                msg.Handle<SteamClient.JobCallback<SteamApps.PICSChangesCallback>>(callback =>
                {
                    if (PreviousChange != callback.Callback.CurrentChangeNumber)
                    {
                        PreviousChange = callback.Callback.CurrentChangeNumber;

                        List<uint> appslist = new List<uint>();
                        List<uint> packageslist = new List<uint>();
                        string appsmsg = "Apps: ";
                        string subsmsg = "Packages: ";

                        irc.SendMessage(SendType.Message, channel, "Received changelist " + Colors.OLIVE + PreviousChange + Colors.NORMAL + " with "
                            + (callback.Callback.AppChanges.Count >= 10 ? Colors.YELLOW : Colors.OLIVE) + callback.Callback.AppChanges.Count + Colors.NORMAL
                            + " apps and " + (callback.Callback.PackageChanges.Count >= 10 ? Colors.YELLOW : Colors.OLIVE) + callback.Callback.PackageChanges.Count
                            + Colors.NORMAL + " packages - " + Colors.DARK_BLUE + "http://steamdb.info/changelist.php?changeid=" + PreviousChange + Colors.NORMAL);

                        foreach (var callbackapp in callback.Callback.AppChanges)
                        {
                            String appname = getAppName(callbackapp.Key.ToString());

                            if (importantapps.Contains(callbackapp.Key.ToString()))
                            {
                                irc.SendMessage(SendType.Message, "#steamdb", "Important app update: " + Colors.OLIVE + appname + Colors.NORMAL
                                    + " - " + Colors.DARK_BLUE + "http://steamdb.info/app/" + callbackapp.Key.ToString() + "/#section_history" + Colors.NORMAL);
                            }

                            appslist.Add(callbackapp.Key);

                            if (!PreviousChange.Equals(callbackapp.Value.ChangeNumber))
                            {
                                if (!appname.Equals(""))
                                {
                                    irc.SendMessage(SendType.Message, channel, "App: " + Colors.LIGHT_GRAY + callbackapp.Key.ToString() + Colors.NORMAL + " (" + appname + ")"
                                        + " - bundled changelist " + Colors.OLIVE + callbackapp.Value.ChangeNumber + Colors.NORMAL
                                        + " - " + Colors.DARK_BLUE + "http://steamdb.info/changelist.php?changeid=" + callbackapp.Value.ChangeNumber + Colors.NORMAL); 
                                }
                                else
                                {
                                    irc.SendMessage(SendType.Message, channel, "App: " + Colors.LIGHT_GRAY + callbackapp.Key.ToString() + Colors.NORMAL + " - bundled changelist "
                                        + Colors.OLIVE + callbackapp.Value.ChangeNumber + Colors.NORMAL + " - "
                                        + Colors.DARK_BLUE + "http://steamdb.info/changelist.php?changeid=" + callbackapp.Value.ChangeNumber + Colors.NORMAL); 
                                }
                            }
                            else
                            {
                                appsmsg += callbackapp.Key.ToString() + " ";

                                if (!appname.Equals(""))
                                {
                                    appsmsg += "(" + appname + ") ";
                                }
                            }
                        }

                        foreach (var callbackpack in callback.Callback.PackageChanges)
                        {
                            Console.WriteLine("KEY: " + callbackpack.Key + " VALUE: " + callbackpack.Value);

                            String subname = getPackageName(callbackpack.Key.ToString());

                            if( callbackpack.Key == 0 )
                            {
                                irc.SendMessage(SendType.Message, "#steamdb", "Important package update: " + Colors.OLIVE + subname + Colors.NORMAL + " - "
                                    + Colors.DARK_BLUE + "http://steamdb.info/sub/" + callbackpack.Key.ToString() + "/#section_history" + Colors.NORMAL);
                            }

                            packageslist.Add(callbackpack.Key);

                            if (!PreviousChange.Equals(callbackpack.Value.ChangeNumber))
                            {
                                if (!subname.Equals(""))
                                {
                                    irc.SendMessage(SendType.Message, channel, "Package: " + Colors.LIGHT_GRAY + callbackpack.Key.ToString() + Colors.NORMAL + " (" + subname + ")"
                                        + " - bundled changelist " + Colors.OLIVE + callbackpack.Value.ChangeNumber + Colors.NORMAL + " - "
                                        + Colors.DARK_BLUE + "http://steamdb.info/changelist.php?changeid=" + callbackpack.Value.ChangeNumber + Colors.NORMAL);
                                }
                                else
                                {
                                    irc.SendMessage(SendType.Message, channel, "Package: " + Colors.LIGHT_GRAY + callbackpack.Key.ToString() + Colors.NORMAL + " - bundled changelist "
                                        + Colors.OLIVE + callbackpack.Value.ChangeNumber + Colors.NORMAL + " - "
                                        + Colors.DARK_BLUE + "http://steamdb.info/changelist.php?changeid=" + callbackpack.Value.ChangeNumber + Colors.NORMAL);
                                }
                            }
                            else
                            {
                                subsmsg += callbackpack.Key.ToString() + " ";

                                if (!subname.Equals(""))
                                {
                                    subsmsg += "(" + subname + ") ";
                                }
                            }
                        }

                        if (callback.Callback.AppChanges.Count != 0 && !appsmsg.Equals("Apps: "))
                        {
                            irc.SendMessage(SendType.Message, channel, appsmsg);
                        }

                        if (callback.Callback.PackageChanges.Count != 0 && !subsmsg.Equals("Packages: "))
                        {
                            irc.SendMessage(SendType.Message, channel, subsmsg);
                        }
                    }

                    steamApps.PICSGetChangesSince(PreviousChange, true, true);
                });

                msg.Handle<SteamClient.JobCallback<SteamApps.PICSProductInfoCallback>>(callback =>
                {
                	string Name = "";
                	string ID = "";
                	
                    foreach (var unknownapp in callback.Callback.UnknownApps)
                    {
                        irc.SendMessage(SendType.Message, channel, "Unknown app: " + Colors.LIGHT_GRAY + unknownapp.ToString() + Colors.NORMAL);
                    }

                    foreach (var unknownsub in callback.Callback.UnknownPackages)
                    {
                        irc.SendMessage(SendType.Message, channel, "Unknown package: " + Colors.LIGHT_GRAY + unknownsub.ToString() + Colors.NORMAL);
                    }

                    foreach (var callbackapp in callback.Callback.Apps)
                    {
                        ID = callbackapp.Key.ToString();

                        if (app.Value.KeyValues["common"]["name"].Value == null)
                        {
                            Name = "AppID " + ID;
                        }
                        else
                        {
                            Name = app.Value.KeyValues["common"]["name"].Value.ToString();
                        }

                        callbackapp.Value.KeyValues.SaveToFile("app/" + ID + ".vdf", false);

                        irc.SendMessage(SendType.Message, channel, "Dump for " + Colors.OLIVE + Name + Colors.NORMAL + " - "
                            + Colors.DARK_BLUE + "http://raw.steamdb.info/app/" + ID + ".vdf" + Colors.NORMAL);
                    }

                    foreach (var callbacksub in callback.Callback.Packages)
                    {
                        ID = callbacksub.Key.ToString();

                        if (kv["name"].Value == null == null)
                        {
                            Name = "SubID " + ID;
                        }
                        else
                        {
                            Name = kv["name"].Value.ToString();
                        }

                        var kv = callback.Callback.Packages[uint.Parse(ID)].KeyValues.Children.FirstOrDefault();
                        kv.SaveToFile("sub/" + ID + ".vdf", false);

                        irc.SendMessage(SendType.Message, channel, "Dump for " + Colors.OLIVE + Name + Colors.NORMAL + " - "
                            + Colors.DARK_BLUE + "http://raw.steamdb.info/sub/" +  + ".vdf" + Colors.NORMAL);
                    }
                });

                msg.Handle<SteamClient.JobCallback<SteamUserStats.NumberOfPlayersCallback>>(callback =>
                {
                    if (callback.Callback.Result != EResult.OK)
                    {
                        irc.SendMessage(SendType.Message, channel, "Unable to request player count:" + callback.Result);
                    }
                    else
                    {
                        irc.SendMessage(SendType.Message, channel, "Players: " + callback.Callback.NumPlayers.ToString());
                    }
                });
            }
        }
    }
}
