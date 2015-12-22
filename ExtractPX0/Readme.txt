C# Command line utility
=======================

This is a VS2012 project.

Purpose:

Console utility to extract diskette data from .PX0 files, as posted on the Pick Systems (Raining Data/Tiger Logic/Whoever) ftp site.
These files are usually expected to be processed by a utility 'teleget.exe' also on that ftp site. However, this utility is very old
and is used to create a (physical) floppy disk image from the PX0 file. This process is not well suited to modern computers, as it
seems to want to 'talk' to the floppy controller hardware. This makes it impossible to extract the diskette images from the PX0 files
using virtual floppy mechanisms or USB type floppy hardware. This tool was created to extract the floppy image from the PX0 images,
and create simple uncompressed image files that can be easily manipluated by other utilities.
