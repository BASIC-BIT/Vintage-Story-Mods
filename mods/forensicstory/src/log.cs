using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace forensicstory.src
{
    class Log
    {
        //TODO: Update user data type
        private String user = null;
        private String userID = null;
        private DateTime timeStamp = new DateTime(1999, 12, 30, 23, 59, 59);

        public Log(String playerName, String ID, DateTime time)
        {
            userID = ID;
            user = playerName;
            timeStamp = time;
        }

        //user field should only be set by constructor
        public String getPlayer()
        {
            return user;
        }

        //userID field should only be set by constructor
        public String getPlayerID()
        {
            return userID;
        }

        //timeStamp field should only be set by constructor
        public DateTime getTime()
        {
            return timeStamp;
        }

        //Automatically formats the log data to an easily readable string
        public override string ToString()
        {
            String output = "";
            output = ("User ID: " + userID + " | Name: " + user + " | Timestamp: " + timeStamp);
            return output;
        }
    }
}
