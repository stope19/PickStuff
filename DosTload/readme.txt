DOS utility to read R83 T-DUMP format diskettes     Rel 1.0  Aug 1997
===============================================

This utility was created to provide a mechanism whereby R83 T-DUMP
format diskettes could have their contents loaded into DOS. This is
sometimes useful when other more sophisticated transfer options are
not available.

Files in this distribution:

   tload.c    - 'C' source code.
   tload.exe  - executable.
   tload.bat  - batch file used to build the executable.
   readme.txt - this file.

Note: Software provided 'as is', and for free. Distribute as you wish.
As is usual for this type of stuff, I take no responsibility for anything,
and make no claims that the code is bug free ... etc etc
Aside: Those 'C' coders out there will quickly ascertain that this is not
a language I use often :)

The 'C' executable 'tload.exe' was created using MS 'Quick-C 2.5', which
is a rather old compiler. Some changes may be required to the code in
order to recompile in other environments.

   Usage: tload drive_letter subdir [ -options ]

   where: drive_letter is 'A' or 'B' (colon optional)

          subdir is target directory, e.g. 'C:\MYDIR'
          Note that specified directory must already exist!

          option letters: O = Overwrite existing items of same name.
                          A = Attribute marks are retained. Default
                              action is to convert AM to LF characters.
                          L = List items on media (no load performed)
                              so no 'subdir' parameter is required)
                          S = Standard density floppy. Default is 'high'.
                          I = Save Pick item-id as first attribute.
                          U = Force block size to be 512
                          V = Verbose output. Off by default.

          Note: Option letters may be specified in upper or lower case.


Example usage:

   tload a -L

   Lists all items in T-DUMP media file. No items are loaded to disk.

   tload a c:\temp -o

   Load data items into directory 'c:\temp' overwriting existing items
   with the same name.

   tload a c:\temp -u

   Load data items into 'c:\temp', assuming that source diskette was
   created with a blocksize of 512 without a label! Note that if a label
   was present, we could have determined the block size from that.


Limitations:

Currently, the data is assumed to start at the beginning of the disk. It is
not possible to 'skip' files. Only a single file may be loaded, although that
file may span multiple diskettes.

Not tested for media produced on any platform other than PC/R83 rel: 3.1


Notes:

When R83 Basic object code is loaded, the 'A' option is ignored.
