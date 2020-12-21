using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SimpleAPI_NetCore50.Schemas
{
    public enum SocketSessionMessageType
    {
        Unknown, // error checking
        Greeting, // sending the initial session token and host token
        Introduction, // for gaining access to channels / providing user data like name/profile pic
        Heartbeat, // keep-alive signals
        Properties, // for properties like the participants list or the host id
        StatusUpdates, // user joined/user left/user is typing/user is asleep/etc
        Notifications, // relationship added/username tagged/ etc
        Reaction, // reaction to a message
        Text, // Text message
        FormattedText, // Text message that should be handled with special cases
        Url, // images/videos/audio/media/files anything linked via url
        ByteArray // binary data
    }
}
