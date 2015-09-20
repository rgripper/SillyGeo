using System;
using System.Linq;
using System.Net;

namespace SillyGeo.Data.Storage.Sqlite.Models
{
    internal class FlatIPAddress
    {
        public long? High { get; set; }

        public long Low { get; set; }

        public FlatIPAddress(IPAddress value)
        {
            var revBytes = value.GetAddressBytes().Reverse().ToArray();
            switch (value.AddressFamily)
            {
                case System.Net.Sockets.AddressFamily.InterNetwork:
                    Low = BitConverter.ToInt32(revBytes, 0);
                    break;
                case System.Net.Sockets.AddressFamily.InterNetworkV6:
                    High = BitConverter.ToInt64(revBytes, 8);
                    Low = BitConverter.ToInt64(revBytes, 0);
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        public IPAddress ToIPAddress()
        {
            var bytes = High.HasValue
                ? BitConverter.GetBytes(Low).Concat(BitConverter.GetBytes(High.Value)).ToArray()
                : BitConverter.GetBytes((int)Low);

            return new IPAddress(bytes.Reverse().ToArray());
        }
    }
}
