using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SimpleAPI_NetCore50.Schemas
{
    public enum SocketSessionMessageType
    {
        Unknown, // error checking
        Property, // for properites like the participants list or the host id
        Heartbeat, // keep-alive signals
        StatusUpdate, // user joined/user left/user is typing/user is asleep/etc
        Notification, // relationship added/username tagged/ etc
        Reaction, // reaction to a message
        Text, // Text message
        FormattedText, // Text message that should be handled with special cases
        Url // images/videos/audio/media/files anything linked via url
    }
}
