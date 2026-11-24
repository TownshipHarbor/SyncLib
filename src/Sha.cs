using System;

namespace SyncLib
{
    internal static class Sha
    {
        static readonly uint[] K = {
            0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
            0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
            0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
            0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
            0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
            0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
            0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
            0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
        };

        static uint R(uint x, int n)
        {
            return (x >> n) | (x << (32 - n));
        }

        internal static byte[] Hash(byte[] data)
        {
            uint[] h = { 0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a, 0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19 };
            long bits = (long)data.Length * 8;
            int padded = ((data.Length + 9 + 63) / 64) * 64;
            byte[] m = new byte[padded];
            Buffer.BlockCopy(data, 0, m, 0, data.Length);
            m[data.Length] = 0x80;
            for (int i = 0; i < 8; i++) m[padded - 1 - i] = (byte)(bits >> (8 * i));
            uint[] w = new uint[64];
            for (int block = 0; block < padded; block += 64)
            {
                for (int i = 0; i < 16; i++) w[i] = (uint)(m[block + i * 4] << 24 | m[block + i * 4 + 1] << 16 | m[block + i * 4 + 2] << 8 | m[block + i * 4 + 3]);
                for (int i = 16; i < 64; i++)
                {
                    uint s0 = R(w[i - 15], 7) ^ R(w[i - 15], 18) ^ (w[i - 15] >> 3);
                    uint s1 = R(w[i - 2], 17) ^ R(w[i - 2], 19) ^ (w[i - 2] >> 10);
                    w[i] = w[i - 16] + s0 + w[i - 7] + s1;
                }
                uint a = h[0], b = h[1], c = h[2], d = h[3], e = h[4], f = h[5], g = h[6], k = h[7];
                for (int i = 0; i < 64; i++)
                {
                    uint t1 = k + (R(e, 6) ^ R(e, 11) ^ R(e, 25)) + ((e & f) ^ (~e & g)) + K[i] + w[i];
                    uint t2 = (R(a, 2) ^ R(a, 13) ^ R(a, 22)) + ((a & b) ^ (a & c) ^ (b & c));
                    k = g; g = f; f = e; e = d + t1; d = c; c = b; b = a; a = t1 + t2;
                }
                h[0] += a; h[1] += b; h[2] += c; h[3] += d; h[4] += e; h[5] += f; h[6] += g; h[7] += k;
            }
            byte[] r = new byte[32];
            for (int i = 0; i < 8; i++)
            {
                r[i * 4] = (byte)(h[i] >> 24);
                r[i * 4 + 1] = (byte)(h[i] >> 16);
                r[i * 4 + 2] = (byte)(h[i] >> 8);
                r[i * 4 + 3] = (byte)h[i];
            }
            return r;
        }
    }
}
