Generating checksums on Pick{like} systems
==========================================

The functions presented are intended to permit the generation of
'standard' CRC values on a variety of platforms. It must be stressed
that this a task 'not well suited' to Basic.
 
The are 8 files provided as part of the checksum 'package'. They are:


README.TXT

This documentation file.


CHKSUM

Interface: SUBROUTINE CHKSUM(SVAL, CRC)

This is a very simple, but also very slow bit of Basic code that may
be used to generate a 16 bit checksum for the string passed in 'SVAL'.
The decimal checksum value returned in variable 'CRC' is compatible
with the UniVerse CHECKSUM() function. As this code uses no 'bitwise'
operators, it can be used on platforms like R83. If your platform
supports bit operators or functions, consider porting a version of
CRC16UDT.


CRC16UDT

Interface: SUBROUTINE CRC16UDT(MODE, SVAL, CRC)

This code was developed to produce a 16 bit checksum on a UniData
platform, and takes advantage of the UniData Basic functions for
bit manipulation. In addition, tables of pre-computed values are
used to boost performance. There are two entry points. The first (when
'MODE' is set to 2) is used to load the pre-computed table values into
named common. This needs only to be done once. Subsequent calls with
'MODE' set to 1 will generate a 16 bit CRC for the string value passed
in parameter 'SVAL'. The CRC is returned in variable 'CRC'. Note that
the returned value is compatible with the uniVerse CHECKSUM() function,
but is in hex format.

Example usage:

   CALL CRC16UDT(2, '', '')      ;* Load pre-computed values into common!
   FOR I = 1 TO N
      S = STR(CHAR(I), 10)
      CALL CRC16UDT, 1, S, CRC)  ;* Generate CRC value for contents of 'S'!
      CRT 'CRC = ':CRC
   NEXT I


CRC32UDT

Interface: SUBROUTINE CRC32UDT(MODE, SVAL, CRC)

This function generates a 32 bit checksum value. I believe that this
checksum is the same as that used in PKZIP. The interface is the same
as for function CRC16UDT, and it should be portable to any platform that
supports bit operators or functions. Note that the crc is returned as a
hex value.

Example usage:

   CALL CRC32UDT(2, '', '')      ;* Load pre-computed values into common!
   FOR I = 1 TO N
      S = STR(CHAR(I), 10)
      CALL CRC32UDT, 1, S, CRC)  ;* Generate CRC value for contents of 'S'!
      CRT 'CRC = ':CRC
   NEXT I


CRC16 & CRC16.ASM    (R83 Only)

Interface: CRC = OCONV(SVAL, "U01FF")

This is some R83 Assembler code to generate 16 bit checksum values for
a string. It is quite fast. The returned value will be compatible with
the CHKSUM() and CRC16UDT functions. The code is accessed via the Basic
conversion processor, and either decimal or hex CRC values can be
generated. For example:

     CRC = OCONV(STRING, 'U01FF')  - return decimal CRC, or

     CRC = ICONV(STRING, 'U01FF')  - return 4 digit hex CRC

You can change the 'mode' from 511 to whatever is appropriate on your
system. Note: CRC16 is the Assembler source code, and CRC16.ASM is
the object code.


CRC32 & CRC32.ASM    (R83 Only)

Interface: CRC = OCONV(SVAL, "U0200")

This is some R83 Assembler code to generate 32 bit checksum values for
a string. It is quite fast. The returned value will be compatible with
the CRC32UDT function. The code is accessed via the Basic conversion
processor, and either decimal or hex CRC values can be generated.
For example:

     CRC = OCONV(STRING, 'U0200') - to return decimal CRC, or

     CRC = ICONV(STRING, 'U0200') - to return 8 digit hex CRC

You can change the mode from '512' to whatever is appropriate on your
system. Note: CRC32 is the Assembler source code, and CRC32.ASM is
the object code.


Resources
---------

To find out more about crc generation (in fact, more than you may ever
wish to know) check out:

http://www.ross.net/crc/
