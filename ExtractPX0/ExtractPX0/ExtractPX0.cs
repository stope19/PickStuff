/* ********************************************************************* */
/* ********************************************************************* */
/* Console utility to extract diskette data from PX0 files, as posted on */
/* the Pick Systems (Raining Data/Tiger Logic/Whoever) ftp site. These   */
/* files are usually expected to be processed by a utility 'teleget.exe' */
/* also on that ftp site. However, this utility is very old, and is used */
/* to create a floppy disk image from the PX0 file. This process is not  */
/* well suited to modern computers, as it seems to want to 'talk' to the */
/* floppy controller hardware. This makes it impossible to extract the   */
/* diskette images from the PX0 files using virtual floppy mechanisms or */
/* USB type floppy hardware. This utility was created to extract the     */
/* floppy image from the PX0 images, and create simple uncompressed      */
/* image files that can be easily manipluated by other utilities. So, we */
/* are extracting the diskette image from the PX0 in the hope you can do */
/* something useful with it! <g>                                         */
/*                                                                       */
/* The PX0 format looks to be based on the Teledisk 'TD0' format, with   */
/* some variations - probably because the PX0 files seem to be the output*/
/* of a Pick Systems licensed image generator.                           */
/* My main references for TD0 formats are:                               */
/*  http://www.classiccmp.org/dunfield/img06566/td0notes.txt             */
/*  http://www.fpns.net/willy/wteledsk.htm                               */
/* The code to decompress the compressed PX0 files was taken from:       */
/* 'TD02IMD.C' itself taken from:                                        */
/*  http://www.classiccmp.org/dunfield/img06566/imd118sc.zip             */
/* Lots of comments (eg header contents) taken from this file as well.   */
/*                                                                       */
/* NOTE: The links for the http://www.classiccmp.org/dunfield site seem  */
/* to be updated often (to discourage direct links?) - so you may need   */
/* to do some searching to find the referenced files.                    */
/*                                                                       */
/* Output file is an 'img' (image) file, and has a size derived from the */
/* media size used to create the PX0 file. Final image size is calcuated */
/* as Cylinders×Heads×(Sectors per track), e.g., 1440KB=80×2×18×512 for  */
/* 80 cylinders (tracks) and 2 heads (sides) with 18 sectors per track.  */
/*                                                                       */
/* This utility was created for a particular requirement, and is not a   */
/* demonstration of good C# coding practices. Don't like it? Move on..   */
/* Utility has been tested on a few .px0 files, but not a huge number..  */
/*                                                                       */
/* dmm 2012                                                              */
/* ********************************************************************* */
/* ********************************************************************* */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ExtractPX0
{
    public sealed class ExtractPX0
    {
        private static void ShowUsage()
        {
            Console.WriteLine("ExtractPX0 <path>");
            Console.WriteLine("  where: <path> = full path to a .px0 file (eg: \"C:\\Temp\\datafile1.px0\")");
            Console.WriteLine("                  Enclose path string in double quote chars if it has spaces.");
            Console.WriteLine();
            Console.WriteLine("  Output is a file with a '.img' extension in the location as the input file.");
            Console.WriteLine("  This file represents the extracted diskette image.");
            Console.WriteLine();
            Console.WriteLine("Reads a diskette image file with a .PX0 extension, and outputs an image file");
            Console.WriteLine("that represents the extracted, decompressed contents as a diskette image file.");
            Console.WriteLine("This may then be used by many existing utilities - or used to burn a new floppy");
            Console.WriteLine("diskette. The .PX0 files can be found on the Pick Systems ftp site, and would");
            Console.WriteLine("normally be read via the 'teleget' utility - but that relies on talking to the");
            Console.WriteLine("floppy hardware, and is useless on modern systems with USB floppy controllers.");
            Console.WriteLine();
            Console.WriteLine("Example: ExtractPX0 \"c:\\datafiles\\APPRO6.2.27.167\\35.datafiles1.px0");
            Console.WriteLine("will create a file '35.datafiles1.img' in 'c:\\datafiles\\APPRO6.2.27.167\\'");
            return;        
        }

        // Example input (from DOS prompt)
        // ExtractPX0.exe "D:\Applications\APPRO6.2.27.167\OrigPickFiles\35.datafiles1.px0"
        static void Main(string[] args)
        {
            string inPath = (null == args || 1 != args.Length || null == args[0]) ? null : args[0].Trim();
            if (string.IsNullOrEmpty(inPath))
            {
                // We were expecting only a single parameter..
                ShowUsage();
                return;
            }

            var fi = new FileInfo(inPath);
            if (!fi.Exists)
            {
                // Parameter does not represent a file
                Console.WriteLine("File not found! '{0}'", inPath);
                return;
            }
            if (0 != string.Compare(fi.Extension, ".px0", true))
            {
                // Input files are expected to have a '.px0' extension!
                Console.WriteLine("Not a '.px0' file! '{0}'", inPath);
                return;
            }

            string outPath = Path.ChangeExtension(fi.FullName, ".img");
            if (File.Exists(outPath))
            {
                // Do not overwrite existing '.img' files!
                Console.WriteLine("Output file already exists! '{0}'", outPath);
                return;
            }

            var sectorDataList = new List<SectorData>();
            try
            {
                // Get PXO file bytes. If compressed data existed, these bytes will 
                // represent the decompressed form!
                var px0Bytes = GetPX0ImageBytes(inPath);

                // Init a pointer we will use to traverse the PX0 file byte array
                var px0Ptr = 0;

                // Check that bytes represent valid data, read file header structure.
                var px0Hdr = PX0Header.GetHeader(px0Bytes, ref px0Ptr);
                px0Ptr++; // On first byte *after* header

                PX0CommentHeader px0CmtHdr = null;
                if ((px0Hdr.Stepping & 0x80) != 0)
                {
                    // Extract 'comment header'
                    px0CmtHdr = PX0CommentHeader.GetCommentHeader(px0Bytes, ref px0Ptr);
                    px0Ptr++; // On first byte *after* 'comment header'
                }

                // Loop over track headers
                PX0TrackHeader trkHdr;
                while ((trkHdr = PX0TrackHeader.GetTrackHeader(px0Bytes, ref px0Ptr)) != null)
                {
                    if (trkHdr.NumberOfSectors == 255)
                        break; // EOF marker!

                    px0Ptr++; // On first byte *after* 'track header'
                    for (int sNo = 0; sNo < trkHdr.NumberOfSectors; sNo++)
                    {
                        var sectHeader = PX0SectorHeader.GetSectorHeader(px0Bytes, ref px0Ptr);

                        /*
                         * Calculate 'Logical Sector' number (from: http://stackoverflow.com/questions/5774164/lba-and-cluster or
                         * http://en.wikipedia.org/wiki/Cylinder-head-sector)
                         * There are many sector numbering schemes on disk drives. One of the earliest was CHS (Cylinder-Head-Sector).
                         * One sector can be selected by specifying the cylinder (track), read/write head and sector per track triplet.
                         * This numbering scheme depends on the actual physical characteristics of the disk drive.
                         * The first logical sector resides on cylinder 0, head 0, sector 1. The second is on sector 2, and so on.
                         * If there isn't any more sectors on the disk (eg. on a 1.44M floppy disk there's 18 sectors per track),
                         * then the next head is applied, starting on sector 1 again, and so on.
                         * 
                         * You can convert CHS addresses to an absolute (or logical) sector number with a little math:
                         * 
                         * LSN = (C * Nh + H) * Ns + S - 1
                         * 
                         * where C, H and S are the cylinder, head and sector numbers according to CHS adressing, while Nh and Ns are
                         * the number of heads and number of sectors per track (cylinder), respectively. 
                         * 
                         * To convert a logical sector number into a cylinder, head and sector number:
                         * S = (LSN mod Ns) + 1
                         * H = (LSN / Ns) mod Nh
                         * C = LSN / (Ns * Nh) 
                         */

                        var LSN = (trkHdr.CylNumber * px0Hdr.Sides + trkHdr.SideHeadNumber)
                                    * trkHdr.NumberOfSectors + sectHeader.SectorNumber - 1;

                        //System.Diagnostics.Debug.WriteLine(string.Format("LSN = {0} where: C: {1}, Nh: {2}, H: {3}, Ns: {4}, S: {5}",
                        //    LSN, trkHdr.CylNumber, px0Hdr.Sides, trkHdr.SideHeadNumber, trkHdr.NumberOfCylinders, sectHeader.SectorNumber));

                        sectorDataList.Add(new SectorData(LSN, sectHeader.sDta));
                        px0Ptr++; // On first byte *after* 'sector header'
                    }
                }
                if (sectorDataList.Count < 1)
                    throw new Exception("No sector data extracted!");

                // Sort sector data by 'logical sector number'
                sectorDataList.Sort();
            }
            catch (Exception ex)
            {
                Console.WriteLine("*ERROR* - Failed to convert file '{0}' - error: '{1}'", inPath, ex.Message);

                return;
            }

            // Write out the image file!
            try
            {
                using (var binWriter =
                             new BinaryWriter(File.Open(outPath, FileMode.Create)))
                {
                    // Write out the disk image!!
                    foreach (var td in sectorDataList)
                    {
                        binWriter.Write(td.dta);
                    }
                    binWriter.Flush();
                }
                Console.WriteLine("Finished writing image file: " + outPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("*ERROR* - Failed to write image file '{0}' - error: '{1}'", outPath, ex.Message);
            }
        }

        // Generate an error-checking cyclic redundancy check for passed bytes with the
        // polynomial value 41111 (A097 hex) using a specified input crc value ('preset')
        private static ushort GetCRC(byte[] inp, int spos, int lgth, ushort crcInit)
        {
            const ushort poly = 0xA097;
            var crc = crcInit;

            for (var cnt = spos; cnt < spos + lgth; cnt++)
            {
                crc = (ushort)(crc ^ (inp[cnt] << 8));
                for (var i = 0; i < 8; i++)
                {
                    if ((crc & 0x8000) != 0)
                    {
                        crc <<= 1;
                        crc ^= poly;
                    }
                    else
                    {
                        crc <<= 1;
                    }
                }
            }
            return crc;
        }

        // Get PX0 file image as a byte array. If the image contains compressed data,
        // decompress it now, so returned bytes always reflect uncompressed state.
        private static byte[] GetPX0ImageBytes(string fname)
        {
            const int teleGetHdrBytes = 22;

            using (var binReader = new BinaryReader(File.Open(fname, FileMode.Open)))
            {
                // Read first 'TeleGetHdrBytes' bytes. These are never compressed.
                byte[] firstBytes;
                try
                {
                    firstBytes = binReader.ReadBytes(teleGetHdrBytes);
                }
                catch
                {
                    throw new Exception(string.Format("'{0}' NOT a TeleGet file! (Missing header?)", fname));
                }

                byte[] buf = null;
                if (firstBytes[0] == 't' && firstBytes[1] == 'd')
                {
                    // If 1st 2 bytes are lower case 'td' then data following
                    // header has been compressed. Decompress now.
                    buf = Decompress.GetDecompressedBytes(binReader);
                }
                else if (firstBytes[0] == 'T' && firstBytes[1] == 'D')
                {
                    // If 1st 2 bytes are upper case 'TD' then data following
                    // header has NOT compressed. Just read it.
                    var buffer = new byte[16 * 1024];
                    using (var ms = new MemoryStream())
                    {
                        int read;
                        while ((read = binReader.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            ms.Write(buffer, 0, read);
                        }
                        buf = ms.ToArray();
                    }
                }
                else
                {
                    throw new Exception(string.Format("'{0}' NOT a TeleGet file!", fname));
                }
                if (null == buf || buf.Length < 1)
                    return null; // No data after header?

                var decodedData = new byte[buf.Length + firstBytes.Length];
                firstBytes.CopyTo(decodedData, 0);
                buf.CopyTo(decodedData, firstBytes.Length);
                return decodedData;
            }
        }

        /* ************** */
        /* Helper classes */
        /* ************** */

        // Local class to store data for a 'logical sector'
        sealed class SectorData : IComparable
        {
            // Class to represent extracted data for a particular sector.
            private readonly int LogicalSector;
            public readonly byte[] dta;

            public SectorData(int lsn, byte[] d)
            {
                LogicalSector = lsn;
                dta = new byte[d.Length];
                Array.Copy(d, dta, d.Length);
            }
            public int CompareTo(object obj)
            {
                var other = obj as SectorData;
                if (other == null)
                    throw new ArgumentException("Object is not an SectorData");

                return this.LogicalSector - other.LogicalSector;
            }
            public override bool Equals(Object obj)
            {
                var other = obj as SectorData;
                if (other == null)
                    return false;

                return Equals(other);
            }
            public bool Equals(SectorData other)
            {
                if (other == null)
                    return false;

                // Return true if the fields match:
                return (LogicalSector == other.LogicalSector);
            }
            public override int GetHashCode()
            {
                return LogicalSector;
            }
            public override string ToString()
            {
                return string.Format("SectorData: LSN: {0}", LogicalSector);
            }
        }

        sealed class PX0SectorHeader
        {
            // Following the  Track  header  will  be  a  number  of  sector  blocks
            // consisting of a sector header and optional  data  header/data  block.
            // The number of sector blocks is indicated by the  "Number of  sectors"
            // field in the track header.
            //
            // Each sector header has the following format:
            //
            //  Cylinder number             (1 byte)
            //  Side/Head                   (1 byte)
            //  Sector number               (1 byte)
            //  Sector size                 (1 byte)
            //  Flags                       (1 byte)
            //  Cyclic Redundancy Check     (1 byte)
            //
            // Cylinder number (1 byte)
            //
            // This field indicates the logical cylinder number which is  written
            // in the ID field of the disk  sector.  For  most  disk  formats  it
            // matches the Cylinder number indicated in the track header, however
            // this  does  NOT  have  to  be  the  case  -  some  formats  encode
            // non-physical cylinder numbers.
            //
            // Side/Head (1 byte)
            //
            // This field indicates the  logical  Side/Head  indicator  which  is
            // written in the ID field of the disk sector.  For most disk formats
            // it matches the Side/Head number indicated  in  the  track  header,
            // however this does NOT have to be the case -  some  formats  encode
            // non-physical Side/Head numbers.
            //
            // Sector number (1 byte)
            //
            // This field indicates the logical sector number which is wrtten  in
            // the ID field of the disk sector.  Sector numbers do not have to be
            // in any particular order  (the ordering of the  sectors  determines
            // the interleave factor of the track), do not necessarily begin at 0
            // or 1, and are not necessarily an unbroken series of numbers.  Some
            // formats encode seemingly arbitrary sector numbers.
            //
            // Teledisk sometimes creates bogus sectors headers to describe  data
            // that is not in a properly formatted sector.  These  extra  sectors
            // appear to be created with sector numbers begining at 100.
            //
            // Sector size (1 byte)
            //
            // Indicates the size of  the  sector,  according  to  the  following
            // table:
            //
            //   0 = 128 bytes           4 = 2048 bytes
            //   1 = 256 bytes           5 = 4096 bytes
            //   2 = 512 bytes           6 = 8192 bytes
            //   3 = 1024 bytes
            //
            // Note that disk formats exist which  have  different  sector  sizes
            // within the same track,  and Teledisk will encode  them  this  way,
            // however the PC 765  floppy  disk  controller  cannot  format  such
            // tracks, and the disk can not be recreated.
            //
            // Flags
            //
            // This is a bit field indicating characteristics that Teledisk noted
            // about the sector when it  was  recorded.  The  field  contain  the
            // logical OR of the following byte values:
            //
            // 01 = Sector was duplicated within a track
            // 02 = Sector was read with a CRC error
            // 04 = Sector has a "deleted-data" address mark
            // 10 = Sector data was skipped based on DOS allocation [note]
            // 20 = Sector had an ID field but not data [note]
            // 40 = Sector had data but no ID field (bogus header)
            //
            // note:  Bit values 20 or 10 indicate  that  NO  SECTOR  DATA  BLOCK
            // FOLLOWS.
            //
            // The meaning of some of these bits was taken  from  early  Teledisk
            // documentation,  and may not be accurate - For example,  I've  seen
            // images where sectors were duplicated within a track and the 01 bit
            // was NOT set.
            //
            // Cyclic Redundancy Check (1 byte)
            //
            // This  8-bit  field  contains  the   lower   byte   of   a   16-bit
            // error-checking cyclic redundancy check for extracted sector data bytes.
            // It does not appear to include the sector header, data headers ..etc
            //
            //
            // Sector Data Header
            //
            // The sector data header occurs following the sector header  only  when
            // sector data is present.  This is indicated by bits 10 and 20  of  the
            // Flags value NOT being set. When present it has the following format:
            //
            //  Data block size             (2 bytes)
            //  Encoding method             (1 byte)
            //
            // Data block size (2 bytes)
            //
            // This indicates the size of the sector data  block,  including  the
            // encoding method (ie: data block size + 1).
            //
            // Encoding method (1 byte)
            //
            // This field describes how the sector data is encoded.  It can  have
            // three possible values:
            //
            // Raw sector data
            //
            // Encoding method == 0 indicates that "sector size"  bytes of raw
            // sector data follow.  This is the actual data  content  for  the
            // sector.
            //
            // Repeated 2-byte pattern
            //
            // Encoding method == 1 indicates that a repeated  2-byte  pattern
            // is used.  Note that this may occur  multiple  times  until  the
            // entire sector has been  recreated,  as  determined  by  "sector
            // size" in the sector header.
            //
            // Each entry consits of two 16-bit values.  The first is a  count
            // value indicating how many times the second (the data value)  is
            // repeated.
            //
            // Run Length Encoded data
            //
            // Encoding == 2 indicates that an RLE  data  block  occurs.  Note
            // that this may occur multiple times until the entire sector  has
            // been recreated,  as determined by  "sector size"  in the sector
            // header.
            //
            // Each entry begins with a 1 byte length value or 00 (NUL).
            //
            // If 00,  then this entry is for a literal block.  The next  byte
            // indicates a length 'n',  and the following 'n' bytes are copied
            // into  the  sector  data  as  raw  bytes  (similar  to  Encoding
            // method==0 except for only a portion of the sector).
            //
            // If not 00,  then the length 'l'  is determined as the value ** 2
            // The next byte indicates a repeat  count  'r'.  A block of  'l'
            // bytes is then  read  once  from  the  file,  and repeated in the
            // sector data 'r' times.
            //
            // Sector headers and data blocks occur until  all  sectors  for  the
            // track have been accounted for.

            public const byte SEC_DUP = 0x01;		// Sector was duplicated
            public const byte SEC_CRC = 0x02;		// Sector has CRC error
            public const byte SEC_DAM = 0x04;		// Sector has Deleted Address Mark
            public const byte SEC_DOS = 0x10;		// Sector not allocated
            public const byte SEC_NODAT = 0x20;		// Sector has no data field
            public const byte SEC_NOID = 0x40;		// Sector has no ID field

            public readonly byte CylNumber;
            public readonly byte SideHeadNumber;
            public readonly byte SectorNumber;
            public readonly byte SectorSize;
            public readonly byte Flags;
            public readonly byte CRC;

            public readonly byte[] sDta;

            private PX0SectorHeader(byte cn, byte sh, byte sn, byte ss, byte f, byte crc)
            {
                CylNumber = cn;
                SideHeadNumber = sh;
                SectorNumber = sn;
                SectorSize = ss;
                Flags = f;
                CRC = crc;

                // Initialise sector data array
                // Sector size flags
                //    0 = 128 bytes           4 = 2048 bytes
                //    1 = 256 bytes           5 = 4096 bytes
                //    2 = 512 bytes           6 = 8192 bytes
                //    3 = 1024 bytes
                switch (SectorSize)
                {
                    case 0: sDta = new byte[128]; break;
                    case 1: sDta = new byte[256]; break;
                    case 2: sDta = new byte[512]; break;
                    case 3: sDta = new byte[1024]; break;
                    case 4: sDta = new byte[2048]; break;
                    case 5: sDta = new byte[4096]; break;
                    case 6: sDta = new byte[8192]; break;
                    default:
                        throw new Exception("Unknown/invalid sector size code! - " + this.SectorSize);
                }
                for (var i = 0; i < sDta.Length; i++)
                    sDta[i] = 0xe5;
            }

            public static PX0SectorHeader GetSectorHeader(byte[] dta, ref int pos)
            {
                if (pos >= dta.Length)
                    throw new Exception("Sector header data missing?");

                var hdr = new PX0SectorHeader(dta[pos], dta[pos + 1], dta[pos + 2],
                    dta[pos + 3], dta[pos + 4], dta[pos + 5]);

                pos += 5; // On last byte of track header
                if ((hdr.Flags & SEC_DOS) == 0 && (hdr.Flags & SEC_NODAT) == 0)
                {
                    var dtaBlkSize = (ushort)(dta[pos + 2] << 8 | dta[pos + 1]);
                    byte compMethod = dta[pos + 3];

                    pos += 3; // On last byte of sector data header

                    // Note that we are expanding 'dtaBlkSize-1' bytes into 'hdr.sDta.Length' bytes!
                    var dptr = 0;
                    while (dptr < hdr.sDta.Length)
                    {
                        switch (compMethod)
                        {
                            case 0:
                                // Raw sector data
                                for (var j = 0; j < hdr.sDta.Length; j++)
                                    hdr.sDta[dptr++] = dta[++pos];

                                break;
                            case 1:
                                // Repeated 2-byte pattern
                                var cnt = (ushort)(dta[pos + 2] << 8 | dta[pos + 1]);
                                var b1 = dta[pos + 3];
                                var b2 = dta[pos + 4];
                                pos += 4; // On last byte of pattern defn field
                                while (cnt > 0)
                                {
                                    cnt--;
                                    hdr.sDta[dptr++] = b1;
                                    hdr.sDta[dptr++] = b2;
                                }
                                break;
                            case 2:
                                // RLE block
                                // Each entry begins with a 1 byte length value or 0.
                                //
                                // If 0 then this entry is for a literal block.  The next  byte
                                // indicates a length 'lgth',  and the following 'lgth' bytes are copied
                                // into  the  sector  data  as  raw  bytes  (similar  to  Encoding
                                // method==0 except for only a portion of the sector).
                                //
                                // If not 0,  then the length 'lgth'  is determined as the length value ** 2
                                // The next byte indicates a repeat count 'kount'. A block of  'lgth' bytes
                                // is then  read  once  from  the  file, and repeated in the sector data
                                // 'kount' times.
                                var lgth = dta[++pos]; // 'pos' left on length byte
                                if (0 == lgth)
                                {
                                    // Literal data block
                                    lgth = dta[++pos]; // 'pos' left on length byte
                                    while (lgth > 0)
                                    {
                                        lgth--;
                                        hdr.sDta[dptr++] = dta[++pos];
                                    }
                                }
                                else
                                {
                                    // Repeated fragment
                                    lgth = (byte)(1 << lgth);   // Length
                                    byte kount = dta[++pos];    // Count ('pos' left on count byte)
                                    var frag = new byte[lgth];
                                    for (var j = 0; j < lgth; j++)
                                        frag[j] = dta[++pos];

                                    while (kount > 0)
                                    {
                                        kount--;
                                        for (var j = 0; j < lgth; j++)
                                            hdr.sDta[dptr++] = frag[j];
                                    }
                                }
                                break;
                            default:
                                throw new Exception("Invalid data mode in sector!");
                        }
                    }

                    var crc = (byte)(GetCRC(hdr.sDta, 0, hdr.sDta.Length, 0) & 0xff);
                    if (crc != hdr.CRC)
                        throw new Exception("Sector data CRC is invalid!");
                }
                return hdr;
            }
        }

        sealed class PX0TrackHeader
        {
            // Every disk track recorded in  the  image  will  begin  with  a  track
            // header, which has the following format:
            //
            // Number of cylinders         (1 byte)
            // Number of sectors           (1 byte)
            // Cylinder number             (1 byte)
            // Side/Head number            (1 byte)
            // Cyclic Redundancy Check     (1 byte)
            //
            // Number of cylinders (1 byte)
            //
            // The number of cylinders for the media (eg: 80 == 3.5" HD diskette)
            //
            // Number of sectors (1 byte)
            //
            // This field indicates how many sectors are recorded for this track.
            // This also indicates how many sector headers  to  expect  following
            // the track header.
            //
            // A number of sectors of 255 (FF hex) indicates the end of the track
            // list.  No other fields occur in this record,  and the CRC  is  not
            //  checked.
            //
            // Cylinder number (1 byte)
            //
            // This field encodes the physical cylinder  number  (head  position)
            // for this track, in a range of 0-(#tracks on drive-1).
            //
            // Side/Head number (1 byte)
            //
            // This field encodes the disk side  (0 or 1)  that this track occurs
            // on in it's lower bit.
            //
            //  The high bit of this field is  used  to  indicate  the  track  was
            //  recorded in single-density.  This allows mixed-density disks to be
            //  represented (FM on some tracks, and MFM on others).
            //
            //  FM disks that I recorded had this bit set for every track, and NOT
            //  the FM indicator in bit 7 of the  "Data rate"  field of the  image
            //  header.  I cannot confirm this,  but I suspect that early versions
            //  of Teledisk did not support mixed density disks, using only the FM
            //  bit in the image header.  If this is the case, then a track should
            //  be interpreted as single density if either of the two FM indicator
            //  bits are set.
            //
            // Cyclic Redundancy Check (1 byte)
            //
            // This  8-bit  field  contains  the   lower   byte   of   a   16-bit
            //  error-checking cyclic redundancy check for the  header  calculated
            //  with the polynomial value 41111  (A097 hex)  using an input preset
            //  value of 0.  The CRC is calculated over the first three  bytes  of
            //  the header and should match the forth byte.
            //
            // Track headers and sector block lists occur until all  tracks  on  the
            // disk have been accounted for.  When the last track record and  sector
            // blomck list has been read,  a 255  (FF hex)  byte indicates the end of
            // the image.

            public readonly byte NumberOfCylinders;
            public readonly byte NumberOfSectors;
            public readonly byte CylNumber;
            public readonly byte SideHeadNumber;
            public readonly byte CRC;

            private PX0TrackHeader(byte ns, byte cl, byte sh, byte hd, byte crc)
            {
                NumberOfCylinders = cl;
                NumberOfSectors = ns;
                CylNumber = sh;
                SideHeadNumber = hd;
                CRC = crc;
            }

            public static PX0TrackHeader GetTrackHeader(byte[] dta, ref int pos)
            {
                if (pos >= dta.Length)
                    return null;

                var hdr = new PX0TrackHeader(dta[pos], dta[pos + 1], dta[pos + 2], dta[pos + 3], dta[pos + 4]);
                var crc = (byte)(GetCRC(dta, pos, 4, 0) & 0xff);
                if (crc != hdr.CRC && hdr.NumberOfSectors != 255)
                    throw new Exception("Track Header CRC is invalid!");

                pos += 4; // On last byte of track header

                return hdr;
            }
        }

        sealed class PX0CommentHeader
        {
            // Comment Header / Data block
            // The comment block encodes an ASCII comment as well  as  the  creation
            // date.  It's presence is indicated by the high bit of  the  "Stepping"
            // field in the image header being set.
            //
            // When present,  it occurs immediately after the Image  header  in  the
            // following format:
            //     Cyclic Redundancy Check     (2 bytes)
            //     Data length                 (2 bytes)
            //     Year since 1900             (1 byte)
            //     Month                       (1 byte)
            //     Day                         (1 byte)
            //     Hour                        (1 byte)
            //     Minite                      (1 byte)
            //     Second                      (1 byte)
            //
            // Following the comment header are comment line records,  consisting of
            // ASCII text terminated by NUL (00) bytes.
            //
            // Cyclic Redundancy Check (2 bytes)
            //     This 16-bit field contains the  error-checking  cyclic  redundancy
            //     check for the header calculated with the  polynomial  value  41111
            //     (A097 hex) using an input preset value of 0. The CRC is calculated
            //     over the entire header block  (beginning at offset 2 - just  after
            //     the CRC) and the data records.
            //
            // Data length (2 bytes)
            //
            //     This is the length of the comment data  block  which  follows  the
            //     comment header.  To display the comment data, read and output this
            //     many bytes following the header,  translating NUL (00)  bytes into
            //     newline sequences.
            //
            // Year (1 byte)
            //
            //     Gives the year the image was created as an offset from  1900.  eg:
            //     2007 is encoded as 2007 - 1900 = 107 (6B hex).
            //
            // Month (1 byte)
            //
            //     Gives the month the image was created  using  a  zero  index.  ie:
            //     0=January, 11=December.
            //
            // Day (1 byte)
            //
            //     Gives the day  (of the month)  the image was created using a range
            //     of 1-31.
            //
            // Hour, Minite, Second (1 byte each)
            //
            //     Gives the time of day the image was created using 24-hour time.
            //
            // Comment data block (variable size)
            //
            //     Contains the ASCII text of the  comment  as  NUL  (00)  terminated
            //     lines.  The size of this block is given by  "Data length"  in  the
            //     comment header.  To display the comment,  read  and  output  "Data
            //     length bytes"  from this block,  translating NUL  (00)  bytes into
            //     newline sequences.

            public readonly ushort CRC;
            public readonly ushort DataLen;
            public readonly byte Year;
            public readonly byte Month;
            public readonly byte Day;
            public readonly byte Hour;
            public readonly byte Minute;
            public readonly byte Second;
            public readonly string Comment;

            private PX0CommentHeader(ushort crc, ushort dl, byte y, byte m, byte d, byte h, byte min, byte s, string c)
            {
                CRC = crc;
                DataLen = dl;
                Year = y;
                Month = m;
                Day = d;
                Hour = h;
                Minute = min;
                Second = s;
                Comment = c;
            }

            public static PX0CommentHeader GetCommentHeader(byte[] dta, ref int pos)
            {
                var dlen = (ushort)(dta[pos + 3] << 8 | dta[pos + 2]);
                var cBytes = new byte[dlen];
                for (var i = 0; i < dlen; i++)
                {
                    byte b = dta[pos + 10 + i];
                    if (b == 0)
                        b = (byte)'\n';

                    cBytes[i] = b;
                }

                var hdr = new PX0CommentHeader(
                    (ushort)(dta[pos + 1] << 8 | dta[pos]), dlen,
                    dta[pos + 4], dta[pos + 5], dta[pos + 6], dta[pos + 7], dta[pos + 8], dta[pos + 9],
                    Encoding.ASCII.GetString(cBytes, 0, dlen)
                );

                var crc = GetCRC(dta, pos + 2, 8 + dlen, 0);
                if (crc != hdr.CRC)
                    throw new Exception("Comment Header CRC is invalid!");

                pos += (dlen + 9); // On last byte of comment header

                return hdr;
            }
        }

        sealed class PX0Header
        {
            // Header seems to consist of:
            // Signature (2 bytes) 'td' = data after header is compressed, 'TD' data not compressed
            // Originator (8 bytes) string 'name' of TeleGet licence holder (eg: 'PICKSYST')
            // Unknown (4 bytes) not sure what these are
            // Teledisk version  (1 byte) version number of the Teledisk program  which  created
            //                            the image in the form High-nibble.low-nibble. eg: 15 = 1.5
            // Data rate (1 byte) Encodes the data rate used for the diskette in lower 2 bits.
            //             0 = 250kbps
            //             1 = 300kbps
            //             2 = 500kbps
            //            High bit indicates single-density diskette  (I believe this is for
            //            older versions only which did not support mixed density disks).
            // Drive type (1 byte) Indicates the type of drive the disk was made on.
            //            Early Teledisk document indicates the encoding is:
            //             0 = 5.25 96 tpi disk in 48 tpi drive
            //             1 = 5.25
            //             2 = 5.25 48-tpi
            //             3 = 3.5
            //             4 = 3.5
            //             5 = 8-inch
            //             6 = 3.5
            //            Use Data rate to determine appropriate drive density.
            // Stepping (1 byte) Encodes step type in lower 2 bits
            //             0 = Single-Step
            //             1 = Double-step
            //             2 = Even-only step (96 tpi disk in 48 tpi drive)
            //            High bit indicates presence of optional comment block
            // DOS allocation flag (1 byte) non-zero means the disk was read using the DOS FAT table
            //            to skip unallocted sectors.
            // Sides (1 byte) Encodes the number of sides read from the disk.
            //             01 = One
            //            anything-else = Two
            // Cyclic Redundancy Check (2 bytes) This field contains the error-checking cyclic redundancy
            //            check for the header calculated with the polynomial value 41111  (A097  hex)
            //            using an input preset value of 0.  The CRC is calculated over the first 20 bytes of the
            //            header, and should match the value stored in this field.

            public readonly string Sig;
            public readonly string Licencee;
            public readonly byte Version;
            public readonly byte DataRate;
            public readonly byte DriveType;
            public readonly byte Stepping;
            public readonly byte DosAllocFlag;
            public readonly byte Sides;
            public readonly ushort CRC;

            private PX0Header(string sig, string lic, byte v, byte dr, byte dt, byte st, byte df, byte sd, ushort crc)
            {
                Sig = sig;
                Licencee = lic;
                Version = v;
                DataRate = dr;
                DriveType = dt;
                Stepping = st;
                DosAllocFlag = df;
                Sides = sd;
                CRC = crc;
            }

            public static PX0Header GetHeader(byte[] dta, ref int pos)
            {
                // Validate Header checksum on first 20 bytes
                var crc = GetCRC(dta, pos, pos + 20, 0);
                var dtaCrc = (ushort)(dta[pos + 21] << 8 | dta[pos + 20]);
                if (crc != dtaCrc)
                    throw new Exception("Header CRC is invalid!");

                var hdr = new PX0Header(
                    Encoding.ASCII.GetString(dta, pos, 2),
                    Encoding.ASCII.GetString(dta, pos + 2, 8),
                    dta[pos + 14], dta[pos + 15], dta[pos + 16], dta[pos + 17], dta[pos + 18], dta[pos + 19], crc);

                pos += 21;  // On last byte of header

                return hdr;
            }
        }
    }
}