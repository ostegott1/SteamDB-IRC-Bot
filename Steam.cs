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
            while (Reader.Read())
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

        public static void Loop()
        {

            uint PreviousChange = 0;
            string channel = ConfigurationManager.AppSettings["announce_channel"];
            string PrevChangeFile = @"lastchangenumber";

            steamClient.Connect();

            List<string> importantapps = new List<string>();

            MySqlDataReader Reader = DbWorker.ExecuteReader(@"SELECT AppID FROM ImportantApps WHERE `Announce` = 1");
            while (Reader.Read())
            {
                importantapps.Add(GetDBString("AppID", Reader));
            }
            Reader.Close();
            Reader.Dispose();

            bool running = true;
            while (running)
            {
                CallbackMsg msg = steamClient.WaitForCallback(true);
                msg.Handle<SteamClient.ConnectedCallback>(callback =>
                {
                    if (callback.Result != EResult.OK)
                        throw new Exception("Could not connect: " + callback.Result);

                    steamUser.LogOn(new SteamUser.LogOnDetails
                    {
                        Username = ConfigurationManager.AppSettings["steam-username"],
                        Password = ConfigurationManager.AppSettings["steam-password"]
                    });
                });
                msg.Handle <SteamClient.DisconnectedCallback>(callback =>
                {
                    irc.SendMessage(SendType.Message, channel, "Disconnected! Reconnecting..");
                    Thread.Sleep(TimeSpan.FromSeconds(15));
                    steamClient.Connect();
                });
                msg.Handle<SteamUser.LoggedOnCallback>(callback =>
                {
                    if (callback.Result != EResult.OK)
                    {
                        irc.SendMessage(SendType.Message, channel, "Could not log in: " + callback.Result);
                        System.Threading.Thread.Sleep(1000);
                       // irc.SendMessage(SendType.Message, channel, "Connecting..");
                        //steamClient.Connect();
                    }
                    else
                    {
                        steamFriends.SetPersonaName("Jan");
                        steamFriends.SetPersonaState(EPersonaState.Busy);
                        irc.SendMessage(SendType.Action, channel, "is now logged in.");
                        if (File.Exists(PrevChangeFile) && PreviousChange == 0)
                        {
                            steamApps.PICSGetChangesSince(uint.Parse(File.ReadAllText(PrevChangeFile).ToString()), true, true);
                        }
                        else
                        {
                            steamApps.PICSGetChangesSince(0, true, true);
                        }
                    }
                });

                msg.Handle<SteamClient.JobCallback<SteamApps.PICSChangesCallback>>(callback =>
                {
                    if (File.Exists(PrevChangeFile))
                    {
                        PreviousChange = uint.Parse(File.ReadAllText(PrevChangeFile).ToString());
                    }
                    else
                    {
                        File.WriteAllText(PrevChangeFile, callback.Callback.CurrentChangeNumber.ToString());
                    }
                    if (PreviousChange != callback.Callback.CurrentChangeNumber)
                    {

                        List<uint> appslist = new List<uint>();
                        List<uint> packageslist = new List<uint>();
                        string appsmsg = "Apps: ";
                        string subsmsg = "Packages: ";

                       // irc.SendMessage(SendType.Message, channel, "Received changelist " + callback.Callback.CurrentChangeNumber + " with " + callback.Callback.AppChanges.Count + " apps and " + callback.Callback.PackageChanges.Count + " packages - http://steamdb.info/changelist.php?changeid=" + callback.Callback.CurrentChangeNumber);
                        irc.SendMessage(SendType.Message, channel, "Received changelist" + '\x03' + "7 " + callback.Callback.CurrentChangeNumber + "\x0F with\x03" + (callback.Callback.AppChanges.Count > 10 ? "8" : "7") + " " + callback.Callback.AppChanges.Count + "\x0F apps and\x03" + (callback.Callback.PackageChanges.Count > 10 ? "8" : "7") + " " + callback.Callback.PackageChanges.Count + "\x0F packages -" + '\x03' + "2 http://steamdb.info/changelist.php?changeid=" + callback.Callback.CurrentChangeNumber + '\x0F');
                        foreach (var callbackapp in callback.Callback.AppChanges)
                        {
                            if (importantapps.Contains(callbackapp.Key.ToString()))
                            {
                                   irc.SendMessage(SendType.Message, "#steamdb", "Important app update:" + '\x03' + "7 " + getAppName(callbackapp.Key.ToString()) + "\x0F -" + '\x03' + "2 http://steamdb.info/app/" + callbackapp.Key.ToString() + "/#section_history\x0F");
                            }
                            appslist.Add(callbackapp.Key);
                            String appname = getAppName(callbackapp.Key.ToString());
                            if (!callback.Callback.CurrentChangeNumber.Equals(callbackapp.Value.ChangeNumber))
                            {
                                if (!appname.Equals(""))
                                {
                                    irc.SendMessage(SendType.Message, channel, "App: " + callbackapp.Key.ToString() + " (" + appname + ")" + " - bundled changelist" + '\x03' + "7 " + callbackapp.Value.ChangeNumber + "\x0F -" + '\x03' + "2 http://steamdb.info/changelist.php?changeid=" + callbackapp.Value.ChangeNumber + '\x0F'); 
                                }
                                else
                                {
                                    irc.SendMessage(SendType.Message, channel, "App: " + callbackapp.Key.ToString() + " - bundled changelist" + '\x03' + "7 " + callbackapp.Value.ChangeNumber + "\x0F -" + '\x03' + "2 http://steamdb.info/changelist.php?changeid=" + callbackapp.Value.ChangeNumber + '\x0F'); 
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
                            packageslist.Add(callbackpack.Key);
                            String subname = getPackageName(callbackpack.Key.ToString());
                            if (!callback.Callback.CurrentChangeNumber.Equals(callbackpack.Value.ChangeNumber))
                            {
                                if (!subname.Equals(""))
                                {
                                    irc.SendMessage(SendType.Message, channel, "Package: " + callbackpack.Key.ToString() + " (" + subname + ")" + " - bundled changelist" + '\x03' + "7 " + callbackpack.Value.ChangeNumber + "\x0F -" + '\x03' + "2 http://steamdb.info/changelist.php?changeid=" + callbackpack.Value.ChangeNumber + '\x0F');
                                }
                                else
                                {
                                    irc.SendMessage(SendType.Message, channel, "Package: " + callbackpack.Key.ToString() + " - bundled changelist" + '\x03' + "7 " + callbackpack.Value.ChangeNumber + "\x0F -" + '\x03' + "2 http://steamdb.info/changelist.php?changeid=" + callbackpack.Value.ChangeNumber + '\x0F');
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
                        PreviousChange = callback.Callback.CurrentChangeNumber;
                        File.Delete(PrevChangeFile);
                        File.WriteAllText(PrevChangeFile, callback.Callback.CurrentChangeNumber.ToString());
                        //steamApps.PICSGetProductInfo(appslist, packageslist);
                    }
                    steamApps.PICSGetChangesSince(PreviousChange, true, true);
                });
                msg.Handle<SteamClient.JobCallback<SteamApps.PICSProductInfoCallback>>(callback =>
                {
                    foreach (var unknownapp in callback.Callback.UnknownApps)
                    {
                        irc.SendMessage(SendType.Message, channel, "Unknown app: " + unknownapp.ToString());
                    }
                    foreach (var unknownsub in callback.Callback.UnknownPackages)
                    {
                        irc.SendMessage(SendType.Message, channel, "Unknown sub: " + unknownsub.ToString());
                    }
                    foreach (var callbackapp in callback.Callback.Apps)
                    {
                        callbackapp.Value.KeyValues.SaveToFile("app/" + callbackapp.Key.ToString() + ".vdf", false);
                        irc.SendMessage(SendType.Message, channel, "http://raw.steamdb.info/app/" + callbackapp.Key.ToString() + ".vdf");
                    }
                    foreach (var callbacksub in callback.Callback.Packages)
                    {
                        var kv = callback.Callback.Packages[uint.Parse(callbacksub.Key.ToString())].KeyValues.Children.FirstOrDefault();
                        kv.SaveToFile("sub/" + callbacksub.Key.ToString() + ".vdf", false);
                        irc.SendMessage(SendType.Message, channel, "http://raw.steamdb.info/sub/" + callbacksub.Key.ToString() + ".vdf");
                    }
                });
                msg.Handle<SteamClient.JobCallback<SteamUserStats.NumberOfPlayersCallback>>(callback =>
                {
                    if (callback.Callback.Result != EResult.OK)
                    {
                        irc.SendMessage(SendType.Message, channel, "Something went wrong, bruvs.");
                    }
                    else
                    {
                        irc.SendMessage(SendType.Message, channel, callback.Callback.NumPlayers.ToString());
                        Console.WriteLine(callback.JobID.ToString());
                    }
                    
                });
            }
        }
    }
}
