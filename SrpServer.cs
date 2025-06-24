using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace SRP
{
    public class SrpServer
    {
        private readonly string _username;
        private readonly BigInteger _v;
        private BigInteger _b;
        private BigInteger _B;
        private BigInteger _S;
        private readonly BigInteger _salt;
        private BigInteger _K;

        public SrpServer(string username, BigInteger salt, BigInteger v)
        {
            _username = username;
            _salt = salt;
            _v = v;
        }

        public (BigInteger, BigInteger) GeneratePublicKeyAndSalt()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] randomBytes = new byte[32];
                rng.GetBytes(randomBytes);
                _b = new BigInteger(randomBytes, isUnsigned: true);
                BigInteger k = SrpParameters.ComputeK();
                _B = (k * _v + BigInteger.ModPow(SrpParameters.g, _b, SrpParameters.N)) % SrpParameters.N;
                if (_B < 0) _B += SrpParameters.N;
            }
            return (_B, _salt);
        }

        public byte[] ComputeSessionKey(BigInteger A)
        {
            if (A % SrpParameters.N == 0) throw new ArgumentException("Invalid client public key A");

            byte[] abBytes = Encoding.UTF8.GetBytes(A.ToString() + _B.ToString());
            BigInteger u = SrpParameters.ComputeHash(abBytes);
            if (u == 0) throw new CryptographicException("Invalid u value (u cannot be zero)");

            BigInteger vU = BigInteger.ModPow(_v, u, SrpParameters.N);
            _S = BigInteger.ModPow(A * vU, _b, SrpParameters.N);
            if (_S < 0) _S += SrpParameters.N;

            _K = SrpParameters.ComputeHash(Encoding.UTF8.GetBytes(_S.ToString()));

            byte[] hashBytes;
            using (var sha256 = SHA256.Create())
            {
                hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(_S.ToString()));
            }
            return hashBytes;
        }

        public bool VerifyClientProof(BigInteger A, BigInteger M1)
        {
            byte[] proofBytes = Encoding.UTF8.GetBytes(A.ToString() + _B.ToString() + _K.ToString());
            BigInteger expectedM1 = SrpParameters.ComputeHash(proofBytes);
            return expectedM1 == M1;
        }

        public BigInteger ComputeServerProof(BigInteger A, BigInteger M1)
        {
            byte[] proofBytes = Encoding.UTF8.GetBytes(A.ToString() + M1.ToString() + _K.ToString());
            return SrpParameters.ComputeHash(proofBytes);
        }
    }
}