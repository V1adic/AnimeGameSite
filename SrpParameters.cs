using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace SRP
{
    public static class SrpParameters
    {
        public static readonly BigInteger N = BigInteger.Parse("167609434410335061345139523764350090260135525329813904557420930309800865859473551531551523800013916573891864789934747039010546328480848979516637673776605610374669426214776197828492691384519453218253702788022233205683635831626913357154941914129985489522629902540768368409482248290641036967659389658897350067939");
        public static readonly BigInteger g = 2;

        public static BigInteger ComputeHash(byte[] input)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(input);
                Array.Reverse(hashBytes);
                return new BigInteger(hashBytes, isUnsigned: true);
            }
        }

        public static BigInteger ComputeK()
        {
            string input = N.ToString() + g.ToString();
            byte[] combined = Encoding.UTF8.GetBytes(input);
            return ComputeHash(combined);
        }
    }
}