using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using SteamKit2;
using SteamKit2.Internal; // this namespace stores the generated protobuf message structures

namespace IRCbot
{
    class CustomCallbacks : ClientMsgHandler
    {
        public class announcementCallback : CallbackMsg
        {
            public ClientMsgProtobuf<CMsgClientClanState> Result { get; private set; }
            internal announcementCallback(ClientMsgProtobuf<CMsgClientClanState> res)
            {
                Result = res;
            }
        }

        public override void HandleMsg(IPacketMsg packetMsg)
        {

            //Console.WriteLine(packetMsg.MsgType.ToString());
            // the MsgType exposes the EMsg (type) of the message
            switch (packetMsg.MsgType)
            {
                case EMsg.ClientClanState:
                    HandleGroupAnnouncement(packetMsg);
                    break;
            }
        }

        void HandleGroupAnnouncement(IPacketMsg packetMsg)
        {
            var announcementResponse = new ClientMsgProtobuf<CMsgClientClanState>(packetMsg);
            Client.PostCallback(new announcementCallback(announcementResponse));
        }
    }
}
