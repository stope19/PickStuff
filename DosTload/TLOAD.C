/* *********************************************************** */
/* *********************************************************** */
/* tload - Function to emulate the R83 'T-LOAD' verb. Enables  */
/*         user to list or load to disk, items from floppy     */
/*         disks created using the R83 T-LOAD command.         */
/*                                                             */
/* Note: - Only double sided media is supported!               */
/*       - spanned disks are supported.                        */
/*       - Tested with R83 3.1 T-DUMP media.                   */
/*       - had problems loading files with names like 'COM1'   */
/*         as (I guess) this clashes with DOS device names.    */
/*         Allow for this by attempting to regenerate the Dos  */
/*         file name with a leading '_' character.             */
/*                                                             */
/* Rel: 1.0   Aug 1997 - dmm       (using MS Quick-C rel: 2.5) */
/* *********************************************************** */
/* *********************************************************** */

#define _DISKINFO_T_DEFINED  /* Stop 'diskinfo_t' being redefined!   */

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <direct.h>
#include <conio.h>
#include <ctype.h>
#include <fcntl.h>
#include <bios.h>
#include <dos.h>
#include <io.h>
#include <errno.h>
#include <sys\types.h>
#include <sys\stat.h>

#define TRUE  1
#define FALSE 0

#define START_SECTOR    1
#define START_HEAD      0
#define START_TRACK     0
#define BAD_FILE       -1    /* 'Bad file handle' status code.       */

#define FID_SIZE      500    /* Pick data bytes per frame.           */
#define ITEMSIZE      775    /* Output buffer size ( >= FID_SIZE )   */
#define MAXNF2KEY      64    /* Max allowed size for Pick item-id    */
#define SECSIZE       512    /* floppy sector size                   */
#define DEFSTART       12    /* Default offset for 500 byte 'blocks' */

#define DISPDEPTH      20    /* Display lines per page (if vflg true)*/
#define READ   _DISK_READ    /* Read disk sectors                    */
#define MAXPATH _MAX_PATH    /* Maximum path length                  */

#define MODULUS      4093    /* Used in OS file name 'hash' func.    */
#define DOSNAMELEN      8    /* Max length of dos file name          */
#define REPLACING_CHAR  '_'  /* Char to replace 'forbidden chars'    */
#define FORBIDDEN_CHARS "\"\1.\\|+/:;,-[]{}*?$><= "

/* Global definitions */

struct diskinfo_t            /* data structure for bios disk reads   */
{                            /* NB: This structure taken from the    */
    unsigned drive;          /* <bios.h> include file, then extended */
    unsigned head;           /* to include our 'global' data. This   */
    unsigned track;          /* is maxtracks, maxsectors ...etc      */
    unsigned sector;
    unsigned nsectors;
    void _far *buffer;
    int maxtracks;
    int maxsectors;
    int currlogsect;
    int lastlogsect;
    int currreelno;
    unsigned labeled;
};

static struct diskinfo_t dinfo;      /* Disk info 'control block'    */
static unsigned char buff[SECSIZE];  /* Disk sector buffer from read */
static unsigned vflg = FALSE;        /* Global 'verbose' mode flag   */
static int lcnt      = 0;            /* Verbose ouput line counter   */

/* Function prototypes */

unsigned char read_cmos( int location );
unsigned read_disk( void );
void print_buff( void );
void getosname( char* item, char* doskey, unsigned pfx_flag );
unsigned start_disk( int *reel, int *blk, unsigned *labeled );
unsigned _dos_getdevstatus( int fptr, unsigned *iostat );

/* Ok, 'stuff' defined - so lets do it! */

int main( int argc, char *argv[] )
{
    int i, tplen, idlen, ptr, iptr, blksize, reelno;
    int offset, fptr, idcnt, fblks;

    unsigned oflg = FALSE;
    unsigned aflg = FALSE;
    unsigned lflg = FALSE;
    unsigned sflg = FALSE;
    unsigned uflg = FALSE;
    unsigned iflg = FALSE;
    unsigned iostat, fdrive, pflg, eof, eoi, skipitem;
    unsigned labeled, fmode, objcode;

    unsigned char ch, byte, last_byte, doskey[DOSNAMELEN+4], item[ITEMSIZE];
    unsigned char targetpath[MAXPATH];

    struct stat filestat;

    if (argc > 1 && strchr("-(", argv[--argc][0]))
    {
       /* Look at option letters */
       for (i = 1; (ch = (char)toupper(argv[argc][i])) != '\0'; i++)
       {
          if (ch == 'A') aflg = TRUE;
          if (ch == 'O') oflg = TRUE;
          if (ch == 'L') lflg = TRUE;
          if (ch == 'S') sflg = TRUE;
          if (ch == 'V') vflg = TRUE;
          if (ch == 'U') uflg = TRUE;
          if (ch == 'I') iflg = TRUE;
       }
       argc--;
    }

    if (!((argc == 1 && lflg == TRUE) || (argc == 2 && lflg == FALSE)))
    {
        printf("Usage: TLOAD drive_letter subdir [ -options ]\n");
        printf("where: drive_letter is 'A' or 'B' (colon optional)\n");
        printf("       subdir is target directory, e.g. 'C:\\MYDIR'\n");
        printf("       Note that specified directory must already exist!\n");
        printf("       option letters: O = Overwrite existing items of same name.\n");
        printf("                       A = Attribute marks are retained. Default\n");
        printf("                           action is to convert AM to LF characters.\n");
        printf("                       L = List items on media (no load performed)\n");
        printf("                           so no 'subdir' parameter is required)\n");
        printf("                       S = Standard density floppy\n");
        printf("                       I = Save Pick item-id as first attribute\n");
        printf("                       U = Force block size to be 512\n");
        printf("                       V = Verbose output\n");
        return ( TRUE );
    }

    /* Get (and validate) drive letter */

    ch = (char)toupper(argv[1][0]);

    if (!(ch == 'A' || ch == 'B'))
    {
       printf("Invalid drive letter - must be one of 'A' or 'B'!\n");
       return ( TRUE );
    }

    byte = read_cmos(20);
    if (vflg)
    {
       lcnt++;
       printf("CMOS equipment byte   : %02X\n", byte);
    }

    i = ((byte >> 6 ) & 3) + 1;
    if (i < 1)
    {
       printf("No floppy drives detected!\n");
       return ( TRUE );
    }
    fdrive = (ch == 'B');

    if (i == 1 && fdrive == 1)
    {
       printf("Drive 'B' not installed or detected!\n");
       return ( TRUE );
    }
 
    /* Get destination path (if any) and create if required! */

    if (argc == 2)
    {
       getcwd(item, MAXPATH);       /* Save current working dir */
       strcpy(targetpath, argv[2]); /* Target directory path    */
    
       if (chdir(targetpath) != FALSE)
       {
	  /* Cannot 'cd' to nominated directory! Therefore */
          /* we assume that the specified path is invalid. */

          printf("Invalid destination path '%s' specified!\n", targetpath);
          return ( TRUE );
       }
       strcat(targetpath, "\\\0" ); /* Add trailing '\' & '\0' to string */
       tplen = strlen(targetpath);  /* Save length of directory prefix   */

       /* Point back to original working directory! */

       chdir(item);                 /* Restore working directory!        */
    }
    
    /* Get floppy drive data */

    byte = read_cmos(16);
    if (vflg)
    {
       lcnt++;
       printf("CMOS floppy data bytes: A=%02X B=%02X \n", (byte >> 4), byte & 15);
    }
       
    if (fdrive == 0)
       byte >>= 4;
    else
       byte &= 15;

    if (byte < 1 || byte > 4)
    {
       if (byte == 0)
          printf("Floppy drive not installed/detected!\n");
       else
          printf("Bad floppy drive data in CMOS\n");

       return ( TRUE );
    }

    dinfo.drive    = fdrive;
    dinfo.head     = START_HEAD;
    dinfo.track    = START_TRACK;
    dinfo.sector   = START_SECTOR;
    dinfo.nsectors = 1;
    dinfo.buffer   = &buff;

    switch (byte)
    {
       case 1:
          /* 360K drive */
          dinfo.maxtracks  = 40;
          dinfo.maxsectors = 9;
          break;
       case 2:
          /* 1.2M drive */
          if (sflg)
          {
             dinfo.maxtracks  = 40;
             dinfo.maxsectors = 9;
          }
          else
          {
             dinfo.maxtracks  = 80;
             dinfo.maxsectors = 15;
          }
          break;
       case 3:
          /* 720K drive */
          dinfo.maxtracks  = 80;
          dinfo.maxsectors = 9;
          break;
       case 4:
          /* 1.44M drive */
          dinfo.maxtracks = 80;
          if (sflg)
             dinfo.maxsectors = 9;
          else
             dinfo.maxsectors = 18;

          break;
    }

    /* Initialise an entry in the drive info table that defines the */
    /* last 'logical sector' on this media. Note that we assume     */
    /* that all diskettes are double sided!                         */
    /* The logical sector numbering starts at '1'.                  */

    dinfo.lastlogsect = dinfo.maxsectors * 2 * dinfo.maxtracks;

    if (vflg)
    {
       lcnt++;
       printf("Max# tracks: %d - Max# sectors: %d - Last logical sector: %d\n",
              dinfo.maxtracks, dinfo.maxsectors, dinfo.lastlogsect);
    }

    /* We now have gathered about all the data we need from the system.  */
    /* The user command line values have been verified, and we are ready */
    /* to begin the 'tload' procedure itself!                            */

    if (start_disk(&reelno, &blksize, &labeled) != FALSE)
       return ( TRUE );

    dinfo.currreelno = 1;
    if (labeled)
    {
       if (blksize == SECSIZE || uflg)
          offset = 0;
       else
          offset = DEFSTART;

       /* Validate that we are on first reel! */

       if (reelno != 1 && reelno != 0)
       {
          printf("Incorrect labeled media! Expected disk 1, but this is disk %d\n",
                 reelno);

          return ( TRUE );
       }
       dinfo.labeled = TRUE;
    }
    else
    {
       /* Unlabeled media. Assume values based on defaults or flags. */

       dinfo.labeled = FALSE;
       if (uflg)
          offset = 0;
       else
          offset = DEFSTART;
    }

    /* Set file 'open' mode, depending on overwrite (ie: 'O') option */

    if (oflg)
       fmode = ( O_RDWR | O_CREAT | O_TRUNC | O_BINARY );
    else
       fmode = ( O_RDWR | O_CREAT | O_EXCL | O_BINARY );

    /* Start the tload operation! */

    idcnt = 0;
    ptr   = offset;
    eof   = FALSE;

    do
    {
       do
       {
          if (ptr == SECSIZE)
          {
             ptr = offset;
             if (read_disk() != FALSE)
                return ( TRUE );
          }
          byte = buff[ptr++];
       }
       while (byte == 0xFB);

       if ((eof = (byte == 0xFF)) == FALSE)
       {
          pflg = FALSE;
          if (byte == 0xFD)
          {
             /* Pointer file item found! */

             pflg = TRUE;
             if (ptr == SECSIZE)
             {
                ptr = offset;
                if (read_disk() != FALSE)
                   return ( TRUE );
             }
             byte = buff[ptr++];
          }

          /* Get item id */

          idlen = 0;
          do
          {
             if (idlen < MAXNF2KEY)
                item[idlen++] = byte;

             if (ptr == SECSIZE)
             {
                ptr = offset;
                if (read_disk() != FALSE)
                   return ( TRUE );
             }
             byte = buff[ptr++];
          }
          while (!(byte == 0xFF || byte == 0xFE || byte == 0xFB));

          if ((eof = (byte == 0xFF)) == FALSE)
          {
             idcnt++;   /* Inc item count! */

             if (item[0] == 0xFE)
             {
                /* Null item-id - skip it! */

                lcnt++;
                iptr     = 0;
                skipitem = TRUE;
                printf("Null item-id detected. Item skipped.\n");
             }
             else
             {
                item[idlen] = '\0';
                skipitem    = FALSE;

                if (lflg)
                {
                   lcnt++;
                   printf("%s\n", item);
                }
                else
                {
		   /* Go and get DOS version (ie: 8.3 format) of name */

                   getosname(item, doskey, FALSE);
                   sprintf(&targetpath[tplen], "%s", doskey);

		   /* Check file in read-only mode first! */

		   if (_dos_open(targetpath, O_RDONLY, &fptr))
		   {
		      if (!stat(targetpath, &filestat))
		      {
			 if ((filestat.st_mode & S_IFMT) != S_IFREG)
			 {
			    /* Name exists, but is NOT a 'regular' file! */
			    /* Eg: may be a directory name ..            */

			    getosname(item, doskey, TRUE);
			    sprintf(&targetpath[tplen], "%s", doskey);
			 }
		      }
		   }
		   else
		   {
		      /* For open file, call int 21h function 4400h (not */
		      /* supported in MS-C library) to verify that the   */
		      /* handle corresponds to a disk file.              */
		      /* _dos_getdevstatus() returns a 16-bit flag, and  */
		      /* bit 7 set to zero indicates a disk file! Ie     */
		      /* status bit for a device is masked by 0x0080     */

		      if (!_dos_getdevstatus(fptr, &iostat))
		      {
			 if ((iostat & 0x0080) == 0x0080)
			 {
			    /* Name exists, but is _not_ a disk file! */
			    /* Eg: may be a dos device like COM1 ..   */

			    getosname(item, doskey, TRUE);
			    sprintf(&targetpath[tplen], "%s", doskey);
			 }
		      }
		      _dos_close(fptr);
		   }

		   /* Re-open file in appropriate mode with 'write' access! */

		   fptr = open(targetpath, fmode, S_IWRITE | S_IREAD);
		   if (fptr == BAD_FILE)
		   {
		      /* File open error! */

		      lcnt++;
		      if (errno == EEXIST)
		      {
			 if (oflg)
			    printf("File '%s' exists, but cannot be opened for update!\n", targetpath);
			 else
			    printf("File '%s' already exists!\n", targetpath);
		      }
		      else
		      {
			 if (errno == EACCES)
			    printf("File access failed for '%s' - permission denied!\n", targetpath);
			 else
			    printf("File open/create operation failed for '%s'\n", targetpath);
		      }
		      skipitem = TRUE;
		   }
		   else
                   {
		      lcnt++;
                      printf("saving %s as %s\n", item, doskey);
                   }
                }

                /* Determine start position 'iptr' in output buffer */

                if (iflg)
                {
                   /* Retain item id in output buffer! */

                   iptr = idlen;
		   if (!aflg)
                      item[iptr++] = 0x0A;
                   else
                      item[iptr++] = 0xFE;
                }
                else
                {
                   iptr = 0;
                }
             }

             /* Now get the item itself! */

             if (byte == 0xFB)
             {
                /* End of item found ? */

                if (iflg && iptr > 0)
                   iptr--;      /* Back up from item-id terminator */
             }
             else
             {
                eoi = FALSE;
                do
                {
                   last_byte = byte;
                   if (ptr == SECSIZE)
                   {
                      ptr = offset;
                      if (read_disk() != FALSE)
                         return ( TRUE );
                   }
                   byte = buff[ptr++];
		   if (byte != 0xFF)
                   {
                      if (byte == 0xFB)
                      {
                         if (last_byte != 0xFF)
                         {
                            if (pflg)
                            {
                               /* This indicates the end of a list-item */
                               /* 'header'. As these are not that long, */
                               /* we should have the entire header in   */
                               /* the output buffer 'item[]'. Reset the */
                               /* Buffer pointer & pick up 'real' item. */
                               /* Also check if this is 'object code'.  */
                               /* If so, force item id terminator (if   */
                               /* 'iflg' true) to be an attribute mark. */
                               
                               if (iflg)
                                  iptr = idlen + 1;
                               else
                                  iptr = 0;

                               i = iptr;
                               fblks = 0;
                               if (item[i++] == 'C')
                               {
                                  ch = item[i++];
                                  if (ch == 'C' || ch == 'L')
                                  {
                                     objcode = (ch == 'C');
				     if (objcode && iflg && !aflg)
					item[idlen] = 0xFE;

                                     if (item[i++] == 0xFE)
                                     {
                                        /* Assume good list pointer item has */
                                        /* been found. Skip to next AM char, */
                                        /* and the number that follows is the*/
                                        /* number of 'frames' of item data!  */

                                        while (item[i++] != 0xFE && i < ITEMSIZE);

                                        while(i < ITEMSIZE && isdigit(item[i]))
                                           fblks = fblks * 10 + item[i++] - 48;

                                        if (i == ITEMSIZE)
					   fblks = 0;   /* Should'nt happen? */
                                     }
                                  }
                               }

			       /* Finally, read the list item! Note that we  */
			       /* use a work buffer the same size as the R83 */
			       /* data frame size!                           */

			       if (iptr > 0)
			       {
				  if (!(lflg || skipitem) && fblks > 0)
				  {
				     /* Write item-id (if any) to file! */

				     write(fptr, &item, iptr);
				  }
				  iptr = 0;
			       }

                               while (fblks-- > 0)
                               {
                                  for (i = 0; i < FID_SIZE; i++)
                                  {
                                     if (ptr == SECSIZE)
                                     {
                                        ptr = offset;
                                        if (read_disk() != FALSE)
                                           return ( TRUE );
                                     }
				     byte = buff[ptr++];

				     if (byte == 0xFE && !aflg && !objcode)
					byte = 0x0A;   /* Convert AM to LF */

				     item[iptr++] = byte;

				     if (iptr == FID_SIZE)
                                     {
					/* We have a full work buffer! */

					if (fblks == 0 && !objcode)
                                        {
					   /* This is LAST data frame in a */
					   /* non object code 'list' item! */
					   /* Terminate on 1st 'SM' found. */

                                           iptr = 0;
					   while (iptr < FID_SIZE && item[iptr] != 0xFF)
                                              iptr++;
                                        }
					if (iptr > 0)
					{
					   if (!(lflg || skipitem))
                                              write(fptr, &item, iptr);

					   iptr = 0;
					}
                                     }
                                  }
                               }
                            }

                            /* Assume end of item encountered! */

                            eoi = TRUE;
                         }
                      }
                      else
                      {
                         /* Add 'good' data byte to item buffer! */

                         if (byte == 0xFE && !aflg && !pflg)
                            byte = 0x0A;     /* Convert AM to LF */

                         item[iptr++] = byte;
                         if (iptr == ITEMSIZE)
                         {
			    /* Item buffer full! - Write to file. */

			    if (!(lflg || skipitem))
			       write(fptr, &item, iptr);

                            iptr = 0;
                         }
                      }
                   }
                }
                while (!eoi);
             }

             /* Finish writing out item - if chars still in buffer */

	     if (!(lflg || skipitem))
             {
                /* Add contents of item buffer to item being output! */

		if (iptr > 0)
		   write(fptr, &item, iptr);

		/* Close our file! */

		close(fptr);
             }
          }
       }
    }
    while (!eof);

    printf("\n%d items on media\n", idcnt);

    return ( FALSE );
}

/* *********************************** */
/* Read a byte from CMOS ram. Passed   */
/* location value must be in the range */
/* 11 to 63 (no check on this done!)   */
/* *********************************** */
unsigned char read_cmos( int location )
{
    outp( 0x70, location );
    return( (unsigned char ) inp( 0x71 ) );
}

/* ************************************ */
/* Read a sector from disk into 'buff'. */
/* ************************************ */
unsigned read_disk()
{
    int i, reel, blk;
    unsigned erc, labeled;
    unsigned char ch;

    if (dinfo.currlogsect > dinfo.lastlogsect)
    {
       /* We are attempting to read past the end of media! */
       /* Prompt user to insert a new disk, then reset our */
       /* logical sector counter and continue.             */

       erc = FALSE;
       do
       {
          lcnt += 2;
          printf("\nLoad diskette number %d - Press 'C' to continue:",
                 dinfo.currreelno+1);

	  i = getche();
	  ch = (char)toupper(i);
          printf("\n");

          if (ch == 'Q')
	     return ( TRUE );        /* Abort! */

          if (start_disk(&reel, &blk, &labeled) != FALSE)
             return ( TRUE );

          if (ch == 'O')
          {
             /* Force 'override' of any tests! */

             erc = TRUE;
          }
          else
          {
             if (labeled)
             {
                if (!dinfo.labeled)
                {
                   printf("This disk volume is 'labeled' - original was not.\n");
                }
                if (reel == dinfo.currreelno+1)
                {
                   dinfo.currreelno++;
                   erc = TRUE;
                }
                else
                {
                   printf("This is disk number %d - expected disk %d\n",
                          reel, dinfo.currreelno+1);
                }
             }
             else
             {
                if (dinfo.labeled)
                   printf("This disk volume is 'unlabeled' - original was.\n");
                else
                   erc = TRUE;
             }
          }
       }
       while (!erc);
    }

    i = dinfo.currlogsect;
    if ((dinfo.sector = i % dinfo.maxsectors) == 0)
       dinfo.sector = dinfo.maxsectors;

    i -= dinfo.sector;
    i = i / dinfo.maxsectors;
    dinfo.head = i % 2;
    dinfo.track = (i - dinfo.head) / 2;

    if ((erc = _bios_disk(READ, &dinfo)) != dinfo.nsectors)
    {
       /* Read failed! - Retry before exiting with error. */

       if ((erc = _bios_disk(READ, &dinfo)) != dinfo.nsectors)
       {
          printf("Cannot read from diskette drive - disk not ready?\n");
          return ( TRUE );
       }
    }

    /* Increment 'logical sector' number! */

    dinfo.currlogsect++;

    /* Display sector to screen (if vflg) */

    if (vflg)
       print_buff();

    return ( FALSE );
}

/* ******************************** */
/* Display current buffer to screen */
/* ******************************** */
void print_buff()
{
    int i, j;
    unsigned char ch;

    lcnt += 3;
    if (lcnt >= DISPDEPTH)
    {
       printf("Press any key to continue...");
       getch();
       printf("\n");
       lcnt = 3;
    }
    printf("\nDrive: %c  Logical sector: %d\n", 'A' + dinfo.drive, dinfo.currlogsect-1);
    printf("Head= %d, Track= %d, Sector= %d\n", dinfo.head, dinfo.track, dinfo.sector);
    for (i = 0; i < SECSIZE; i += 16)
    {
       if (lcnt >= DISPDEPTH)
       {
          printf("Press any key to continue...");
          getch();
          printf("\n");
	  lcnt = 0;
       }
       printf("%03d: ", i);
       for (j = 0; j < 16; j++)
          printf("%02X ", buff[i + j]);

       printf("\t");
       for (j = 0; j < 16; j++)
       {
          ch = buff[i + j];
          if (isprint(ch))
          {
             printf("%c", ch);
          }
          else
          {
             ch = (char) ((ch & 0x1F) | 0x50);
             if (strchr("_^]\\[", ch))
                printf("%c", ch);
             else
                printf(".");
          }
       }
       printf("\n");
       lcnt++;
    }
    return;
}

/* ******************************************** */
/* Convert Pick item name passed in item[] into */
/* a DOS name in 8.3 format.                    */
/* If 'pfx_flag' set to true, then we force the */
/* generated Dos name to have a prefix of '_'   */
/* Do this in an attempt to save Pick items     */
/* with names like 'COM1'. In such cases we     */
/* also force the name to have an extension.    */
/* ******************************************** */
void getosname( char* item, char* doskey, unsigned pfx_flag )
{
    int i, j, k, start, end, modified;
    int len = strlen(item);
    unsigned int hvalue = 0;

    j = 0;
    modified = 0;      /* Count of changes made to original key! */
    if (pfx_flag)
    {
       doskey[j++] = '_';    /* Add a prefix character to key.   */
       modified = 2;         /* Set to any value > 1 (force ext) */
    }

    /* Build up the 8.3 format file name */

    k = (DOSNAMELEN + 4) - j;
    for (i = 0; (i < len) && (i < k); i++)
    {
       if (strchr(FORBIDDEN_CHARS, item[i]))
       {
          doskey[j++] = REPLACING_CHAR;
          modified++;
       }
       else
       {
          /* Lower case letters are converted to upper */
          /* case so add to modified count             */

          if (islower(item[i]))
          {
              modified++;
              doskey[j++] = (char)toupper(item[i]);
          }
          else
          {
              doskey[j++] = item[i];
          }
       }
    }
    doskey[j] = '\0';

    /* If no conversion needed then the name maybe ok!   */

    if (len <= DOSNAMELEN && !modified)
       return;
     
    /* Check if key was already in 8.3 format. Looking   */
    /* for just 1 modified character and 8.1, 8.2 or 8.3 */
    /* formats. If we find a '.' then replace modified   */
    /* character with the original '.'.                  */

    if ((modified == 1) && (len <= DOSNAMELEN + 4) && (item[0] != '.'))
    {
       start = len - 2;
       if ((end = len - 4) < 0)
          end = 0;

       for (i = start; i >= end; i--)
       {
          if (item[i] == '.')
          {
             if (i <= DOSNAMELEN)
             {
                doskey[i] = '.';
                return;
             }
             else
             {
                break;
             }
          }
       }
    }

    /* Ok, key has to be converted to an 8.3 format so hash */
    /* the whole key, truncate key at 8 and add hash as the */
    /* filename extension!                                  */

    for (i = 0; i < len; ++i)
        hvalue = hvalue * 10 + (int)item[i];

    hvalue %= MODULUS;

    if (pfx_flag)
       len++;

    if (len > DOSNAMELEN)
       len = DOSNAMELEN;

    sprintf(&doskey[len], ".%03X", hvalue);
    doskey[len+4] = '\0';
    return;
}

/* ********************************************* */
/* This function used to read a diskette for the */
/* first time. It reads any label, extracts the  */
/* diskette volume number and block size. If no  */
/* error, we exit having read the first data blk.*/
/* ********************************************* */
unsigned start_disk( int *reel, int *blk, unsigned *labeled )
{
    int i;

    dinfo.currlogsect = 1;
    if (read_disk() != FALSE)
       return ( TRUE );

    /* Check current sector for a 'tape label' starting */
    /* at offset 12 in buffer. The expected format is   */
    /* _L xxxx HH:MM:SS  DD MMM YYYY (47 char text)^nn  */
    /* Where: _ = SM, ^ = AM, xxxx = hex block size,    */
    /* nn = reel number.                                */

    *labeled = FALSE;
    if (buff[DEFSTART] == 0xFF && buff[DEFSTART+1] == 'L' &&
       buff[DEFSTART+2] == ' ' && buff[DEFSTART+77] == 0xFE);
    {
       /* Label found, get blocksize and reel number! */

       sscanf(&buff[15], "%4x", blk);

       *reel = buff[DEFSTART+78] - 48;
       *reel = *reel * 10 + buff[DEFSTART+79] - 48;

       *labeled = TRUE;

       printf("\nLabel found - media block size = %d  volume number = %d\n",
              *blk, *reel);

       for (i = 13; i < 92; i++)
       {
          if (buff[i] == 0xFE)
             printf("^");
          else
             printf("%c", buff[i]);
       }
       printf("\n\n");
       lcnt += 4;

       /* Now read first 'data' block! */

       if (read_disk() != FALSE)
          return ( TRUE );
    }
    return ( FALSE );
}

/* ***************************************************** */
/* Functions to support IOCTL retrieval of file/device   */
/* status information. Function returns 0 if successful. */
/* ***************************************************** */
unsigned _cdecl _dos_getdevstatus( int fptr, unsigned *iostat )
{
   _asm {
         mov ax,4400h 
	 mov bx,fptr
         int 21h
         jc done
	 mov bx,iostat
         mov [bx],ax
         xor ax,ax
   done:
   }
}
