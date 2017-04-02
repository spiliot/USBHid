using System;

namespace UsbHid.USB.Classes.Messaging
{
    public class CommandMessage : IMesage
    {
        private byte[] _parameters;
        public byte[] MessageData { get { return GetMessageData(); } }

        private byte[] GetMessageData()
        {
            var result = new byte[65];
            result[0] = 0;
            result[1] = Command;
            if (Parameters != null)
            {
                Array.Copy(Parameters, 0 ,result, 2, Parameters.Length);
            }
            return result;
        }

        public byte Command { get; set; }
    
        public byte[] Parameters
        {
            get { return _parameters; }
            set
            {
                if (value.Length < 1) throw new ArgumentOutOfRangeException("value", "Paramater needs to be at least 1 byte long");
                if (value.Length > 63) throw new ArgumentOutOfRangeException("value", "Paramater canot be longer than 63 bytes");
                _parameters = value;
            }
        }

        public CommandMessage ( byte command )
        {
            Command = command;
        }

        public CommandMessage( byte command, byte[] parameters) : this(command)
        {
            Parameters = parameters;
        }

    }
}
