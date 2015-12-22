/* ********************************************************************* */
/* ********************************************************************* */
/* The code to decompress the compressed PX0 files was taken from:       */
/* 'TD02IMD.C', which itself was taken from:                             */
/*  http://www.classiccmp.org/dunfield/img06566/imd118sc.zip             */
/* ********************************************************************* */
/* ********************************************************************* */
using System;
using System.IO;

namespace ExtractPX0
{
    internal sealed class Decompress : IDisposable
    {
        // LZSS parameters
        private const ushort SBSIZE = 4096;				    // Size of Ring buffer
        private const ushort LASIZE = 60;				    // Size of Look-ahead buffer
        private const ushort THRESHOLD = 2;				    // Minimum match for compress

        // Huffman coding parameters
        private const ushort N_CHAR = (256 - THRESHOLD + LASIZE);	// Character code (= 0..N_CHAR-1)
        private const ushort TSIZE = (N_CHAR * 2 - 1);   		    // Size of table
        private const ushort ROOT = (TSIZE - 1);		    	    // Root position
        private const ushort MAX_FREQ = 0x8000;				        // Update when cumulative frequency reaches this value

        private readonly ushort[] parent = new ushort[TSIZE + N_CHAR];  // parent nodes (0..T-1) and leaf positions (rest)
        private readonly ushort[] son = new ushort[TSIZE];			    // pointers to child nodes (son[], son[]+1)
        private readonly ushort[] freq = new ushort[TSIZE + 1];	   	    // frequency table

        private ushort Bits, Bitbuff;       	    // buffered bit count and left-aligned bit buffer
        private ushort GBr,			                // Ring buffer position
            GBi,				                	// Decoder index
            GBj,					                // Decoder index
            GBk;					                // Decoder index

        private byte GBstate,			            // Decoder state
            Eof;				                	// End-of-file indicator

        private readonly byte[] ring_buff = new byte[SBSIZE + LASIZE - 1];	// text buffer for match strings

        private readonly byte[] d_code = new byte[256] {		// Huffman decoder tables
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01,
            0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02,
            0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03,
            0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05,
            0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x09, 0x09, 0x09, 0x09, 0x09, 0x09, 0x09, 0x09,
            0x0A, 0x0A, 0x0A, 0x0A, 0x0A, 0x0A, 0x0A, 0x0A, 0x0B, 0x0B, 0x0B, 0x0B, 0x0B, 0x0B, 0x0B, 0x0B,
            0x0C, 0x0C, 0x0C, 0x0C, 0x0D, 0x0D, 0x0D, 0x0D, 0x0E, 0x0E, 0x0E, 0x0E, 0x0F, 0x0F, 0x0F, 0x0F,
            0x10, 0x10, 0x10, 0x10, 0x11, 0x11, 0x11, 0x11, 0x12, 0x12, 0x12, 0x12, 0x13, 0x13, 0x13, 0x13,
            0x14, 0x14, 0x14, 0x14, 0x15, 0x15, 0x15, 0x15, 0x16, 0x16, 0x16, 0x16, 0x17, 0x17, 0x17, 0x17,
            0x18, 0x18, 0x19, 0x19, 0x1A, 0x1A, 0x1B, 0x1B, 0x1C, 0x1C, 0x1D, 0x1D, 0x1E, 0x1E, 0x1F, 0x1F,
            0x20, 0x20, 0x21, 0x21, 0x22, 0x22, 0x23, 0x23, 0x24, 0x24, 0x25, 0x25, 0x26, 0x26, 0x27, 0x27,
            0x28, 0x28, 0x29, 0x29, 0x2A, 0x2A, 0x2B, 0x2B, 0x2C, 0x2C, 0x2D, 0x2D, 0x2E, 0x2E, 0x2F, 0x2F,
            0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x3F 
        };

        private readonly byte[] d_len = new byte[] { 2, 2, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 6, 6, 6, 7 };

        private BinaryReader binReader;

        /*
         * Get decompressed bytes. Note that this is not implemented with a view for speed.
         */
        public static byte[] GetDecompressedBytes(BinaryReader reader)
        {
            using (var dcomp = new Decompress(reader))
            using (var memStream = new MemoryStream())
            {
                for (;;)
                {
                    int c = dcomp.Getbyte();
                    if (c < 0)
                        break; // EOF found!

                    memStream.WriteByte((byte)c);
                }
                return memStream.ToArray();
            }
        }

        private Decompress(BinaryReader reader)
        {
            binReader = reader;
            InitDecompress();
        }

        public void Dispose()
        {
            // Not our job to close/dispose of BinaryReader, but we can remove
            // our reference to it.
            binReader = null;
        }

        /*
         * Initialise the decompressor trees and state variables
         */
        private void InitDecompress()
        {
            ushort i, j;

            GBk = 0;
            GBstate = Eof = 0;

            for (i = j = 0; i < N_CHAR; ++i)
            {
                // Walk up
                freq[i] = 1;
                son[i] = (ushort)(i + TSIZE);
                parent[i + TSIZE] = i;
            }

            while (i <= ROOT)
            {
                // Back down
                freq[i] = (ushort)(freq[j] + freq[j + 1]);
                son[i] = j;
                parent[j] = parent[j + 1] = i++;
                j += 2;
            }

            for (i = 0; i < ring_buff.Length; i++)
                ring_buff[i] = 0x20;  // Set to space char

            freq[TSIZE] = 0xFFFF;
            parent[ROOT] = Bitbuff = Bits = 0;
            GBr = SBSIZE - LASIZE;
        }

        /*
         * Increment frequency tree entry for a given code
         */
        private void Update(ushort c)
        {
            ushort i, j, k;
            ushort l;

            if (freq[ROOT] == MAX_FREQ)
            {
                // Tree is full - rebuild.
                // Halve cumulative freq for leaf nodes
                for (i = j = 0; i < TSIZE; ++i)
                {
                    if (son[i] >= TSIZE)
                    {
                        freq[j] = (ushort)((freq[i] + 1) / 2);
                        son[j] = son[i];
                        ++j;
                    }
                }

                // Make a tree - first connect children nodes
                for (i = 0, j = N_CHAR; j < TSIZE; i += 2, ++j)
                {
                    k = (ushort)(i + 1);
                    var f = freq[j] = (ushort)(freq[i] + freq[k]);
                    for (k = (ushort)(j - 1); f < freq[k]; --k)
                        ;

                    ++k;
                    l = (ushort)(j - k);
                    Array.Copy(freq, k, freq, k + 1, l);
                    freq[k] = f;
                    Array.Copy(son, k, son, k + 1, l);
                    son[k] = i;
                }

                // Connect parent nodes
                for (i = 0; i < TSIZE; ++i)
                {
                    if ((k = son[i]) >= TSIZE)
                        parent[k] = i;
                    else
                        parent[k] = parent[k + 1] = i;
                }
            }

            c = parent[c + TSIZE];
            do
            {
                k = ++freq[c];
                // Swap nodes if necessary to maintain frequency ordering
                if (k > freq[l = (ushort)(c + 1)])
                {
                    while (k > freq[++l]) 
                        ;
                    
                    freq[c] = freq[--l];
                    freq[l] = k;
                    parent[i = son[c]] = l;
                    if (i < TSIZE)
                        parent[i + 1] = l;

                    parent[j = son[l]] = c;
                    son[l] = i;
                    if (j < TSIZE)
                        parent[j + 1] = c;

                    son[c] = j;
                    c = l;
                }
            }
            while ((c = parent[c]) != 0);	// Repeat up to root
        }

        /*
         * Get a byte from the input file and flag Eof at end
         */
        private ushort GetChar()
        {
            ushort c;
            try
            {
                c = binReader.ReadByte();
            }
            catch (EndOfStreamException)
            {
                c = 0;
                Eof = 255;
            }
            return c;
        }

        /*
         * Get a single bit from the input stream
         */
        private ushort GetBit()
        {
            if (0 == Bits)
            {
                Bitbuff |= (ushort)(GetChar() << 8);
                Bits = 7;
            }
            else
            {
                Bits--;
            }
            var t = (ushort)(Bitbuff >> 15);
            Bitbuff <<= 1;
            return t;
        }

        /*
         * Get a byte from the input stream - NOT bit-aligned
         */
        private ushort GetByte()
        {
            if (Bits < 8)
                Bitbuff |= (ushort)(GetChar() << (8 - Bits));
            else
                Bits -= 8;

            var t = (ushort)(Bitbuff >> 8);
            Bitbuff <<= 8;
            return t;
        }

        /*
         * Decode a character value from table
         */
        private ushort DecodeChar()
        {
            // search the tree from the root to leaves.
            // choose node #(son[]) if input bit == 0
            // choose node #(son[]+1) if input bit == 1
            var c = ROOT;
            while ((c = son[c]) < TSIZE)
                c += GetBit();

            Update(c -= TSIZE);
            return c;
        }

        /*
        * Decode a compressed string index from the table
        */
        private ushort DecodePosition()
        {
            // Decode upper 6 bits from given table
            var i = GetByte();
            var c = (ushort)(d_code[i] << 6);

            // Input lower 6 bits directly
            ushort j = d_len[i >> 4];
            while (--j != 0) // Added '!= 0' to avoid compiler err
                i = (ushort)((i << 1) | GetBit());

            return (ushort)((i & 0x3F) | c);
        }

        /*
         * Get a byte from the input file
         */
        public short Getbyte()
        {
            for (; ; )
            {				
                // Decompressor state machine
                if (Eof != 0)	// End of file has been flagged
                    return -1;

                ushort c;
                if (0 == GBstate)
                {
                    // Not in the middle of a string
                    c = DecodeChar();
                    if (c < 256)
                    {
                        // Direct data extraction
                        ring_buff[GBr++] = (byte)c;
                        GBr &= (SBSIZE - 1);
                        return (short)c;
                    }
                    GBstate = 255;		// Begin extracting a compressed string
                    GBi = (ushort)((GBr - DecodePosition() - 1) & (SBSIZE - 1));
                    GBj = (ushort)(c - 255 + THRESHOLD);
                    GBk = 0;
                }
                if (GBk < GBj)
                {
                    // Extract a compressed string
                    c = ring_buff[(GBk++ + GBi) & (SBSIZE - 1)];
                    ring_buff[GBr++] = (byte)c;
                    GBr &= (SBSIZE - 1);
                    return (short)c;
                }
                GBstate = 0; // Reset to non-string state
            }
        }
    }
}
