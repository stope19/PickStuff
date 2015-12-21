          FRAME 282
*BASIC/UTILITY #3
*22 DEC 1995
*DMM
*
* Entry points 0-2 are used by Pick. We start at 3
*
*  3 = READ.FRM -  READ A FRAME FROM DISC
*  4 = WRIT.FRM -  WRITE A FRAME BACK TO DISC
*  5 = RUN.TOT  -  GENERATE 'RUNNING-TOTAL' FOR ACCESS
*  6 = LAST.VAL -  RETURN LAST NON-NULL VALUE FROM A STRING
*********************************************************************
*
          ORG X'B'                * START WITH 'EP #3' !
*
          EP !READ.FRM            * READ A FRAME FROM DISC
          EP !WRIT.FRM            * WRITE A FRAME BACK TO DISC
          EP !RUN.TOT             * GENERATE RUNNING TOTAL
          EP !LAST.VAL            * RETURN LAST NON-NULL VALUE
*
CHARH0    DEFC R0,H0              * DEFINE H0 IN ACCUMULATOR AS A CHAR
TOTACCUM  DEFF R0,D4              * DEFINE FTLY O/LAY OVER D4-D5
*
          ORG X'330'              * START OF AVAILABLE CODE SPACE!
*
%CONFIG   ADDR T%CONFIG           * ADDRESS OF CONFIG TABLE
*
* ***************************************************************** *
* ***************************************************************** *
* Standard conversion exit point.                                   *
* ***************************************************************** *
* ***************************************************************** *
*
!EXIT     EQU *
          MCI SM,TS               * TERMINATE CRC VALUE WITH 'SM'
          DEC TS                  * BACK UP 'TS' FROM TERMINATOR
          MOV TS,TSEND            * SET 'TSEND = TS'
          ENT CONVEXIT            * ALL DONE!
*
*********************************************************************
*  Frame Editor Read/Write Functions
*
*   Two entry points. The first is used to read a frame from disk. This
*   may be an ABS frame or DATA frame. The second entry point is used
*   to write a frame image (in hex) back to disk.
*   Both are used from DATA/BASIC, and have the following syntax:
*
*    1. To read a frame:
*
*       frame.data = OCONV(fid,'U311A')
*
*       where: fid         = frame number to read
*              user.exit   = the user exit where this frame is loaded
*              frame.data  = returned data with the format:
*                            Attr<1> = frame image in hex
*                            Attr<2> = frame image in ascii
*                            Note: System delimiters are converted to
*                            [,\,],^,_ characters, and non-displayable
*                            chars are converted to dots in the ascii
*                            frame image. Note that port 0 is assumed to
*                            be a PC monitor, so can display more chars!
*                            Returned as null if 'fid' is invalid.
*
*    2. To write a frame:
*
*       error.flg = OCONV(frame.data,'U411A')
*
*       where: frame.data  = data for frame update, in format:
*                            Attr<1> = frame number to write (fid)
*                            Attr<2> = frame image in hex
*
*              error.flg   = 0 if no error, write was ok
*                            1 if error. Bad 'fid' or item format
*
* Element usage: TS, TSEND, SB10, SB12, FP0, R14, R15, CTR1, CTR20
*
*
* ********************************************* *
* ********************************************* *
* Read Specified Frame. Return In The Format:   *
* Attribute <1> = Frame Image In Hex            *
*           <2> = Frame Image In Ascii          *
* Note: - Convert 512 Bytes For Data Frames And *
*         2048 For Abs Frames.                  *
*       - System Delimiters Are Converted In    *
*         Ascii String To [,\,],^,_ Characters  *
*         Non-Displayable Characters To Dots    *
*       - Return A Null String If Invalid Frame *
*         Number Is Specified.                  *
* ********************************************* *
* ********************************************* *
*
!READ.FRM EQU *
          SB SB12                 * ASSUME WE ARE ON PORT 0
          BSL LINESUB             * GET OUR PORT NUMBER IN 'D0'
          BZ D0,!RFRAME0          * JUMP IF ASSUMPTION WAS CORRECT!
          ZB SB12                 * ELSE CLEAR THE 'PORT 0' FLAG
!RFRAME0  BSL !VAL.FID            * VERIFY FID, NULL OUTPUT STRING (TS)
          BBS SB10,!EXIT          * EXIT IF ERROR (BAD FID NUMBER)
          MOV CTR20,CTR1          * CLONE FRAME SIZE FROM CTR20 TO CTR1
!GFLOOP   MBXN 2,R14;H0,TS        * CONVERT 1 BYTE TO 2 HEX CHARS INTO TS
          BDLEZ CTR1,!GASCII      * EXIT LOOP IF ALL DONE
          INC R14                 * INCREMENT R14 TO NEXT BYTE IN FRAME
          B !GFLOOP               * AND DO THE LOOP AGAIN !
!GASCII   MCI AM,TS               * TERMINATE ATTR 1 (HEX IMAGE) WITH AM
          MOV TS,R14              * SAVE R14 AS ATTR 2 START (LESS 1)
          MOV TSBEG,TS            * TS TO 1 BEFORE HEX IMAGE JUST BUILT
!GFLOOP1  MOV X'0002',T3          * SETUP T3 (H6 & H7) FOR 'MFX' INSTR
          ZERO FP0                * CLEAR THE ACCUMULATOR
          MFX TS                  * CONVERT 2 HEX CHARS TO BINARY
          MCI CHARH0,R14          * SAVE AS 1 ASCII CHAR IN TS
          BCL R14,X'20',!DOT      * JUMP IF NOT A PRINTABLE CHARACTER
          BCL R14,X'FB',!AGAIN1   * JUMP IF NOT A PICK DELIMITER
          AND R14,X'5F'           * CONVERT DELIMITER TO DISPLAY CHAR!
          B !AGAIN                * GO TEST FOR END OF LOOP
!DOT      MCC C'.',R14            * OVERWRITE CHAR WITH A DOT!
!AGAIN    BDLEZ CTR20,!RFRAMEX    * EXIT IF ALL CHARS CONVERTED
          B !GFLOOP1              * ELSE DO LOOP AGAIN !
!AGAIN1   BBS SB12,!AGAIN         * IF ON PORT 0, THEN ACCEPT CHAR
          BCL R14,X'80',!AGAIN    * ALSO IF CHAR IS < HEX '80'
          AND R14,X'7F'           * ELSE STRIP OFF THE 'HIGH' BIT!
          BCL R14,X'20',!DOT      * JUMP IF STILL NOT PRINTABLE CHAR!
          B !AGAIN                * THEN CONTINUE TO NEXT CHAR
!RFRAMEX  MOV R14,TS              * REINSTATE TS POINTER TO END OF BUFFER
          B !EXIT                 * ALL DONE, EXIT BACK TO BASIC
*
* **************************************** *
* **************************************** *
* Write Specified Frame Image Back To Disc *
* TS Buffer Attr 1 Contains Frame Number & *
* Attr 2 Contains The Hex Image To Update. *
* !!Warning!! Incorrect Use Of This User   *
* Exit Will Cause Serious Damage To Your   *
* System. TS Returned As Null If Invalid   *
* FID Number Is Found, Else Write Was Ok.  *
* Return '0' if all ok, else '1' for err!  *
* **************************************** *
* **************************************** *
*
!WRIT.FRM EQU *
          BSL !VAL.FID            * VERIFY FRAME NUMBER
          BBS SB10,!SETERR        * JUMP IF FID NUMBER IN ERROR
          BCU R15,AM,!SETERR      * JUMP IF CHAR AFTER FID NOT 'AM'
          MOV R15,TS              * POSITION TS ON THE AM AFTER THE FID
!WFLOOP1  MOV X'0002',T3          * SETUP T3 (H6 & H7) FOR 'MFX' INSTR
          ZERO FP0                * CLEAR THE ACCUMULATOR
          MFX TS                  * CONVERT 2 HEX CHARS TO BINARY
          MCC CHARH0,R14          * PUT THIS CHARACTER INTO FRAME
          BDLEZ CTR20,!ALLOK      * EXIT IF ALL CHARS CONVERTED
          INC R14                 * INCREMENT TO NEXT BYTE
          B !WFLOOP1              * ELSE DO THE LOOP AGAIN !
*
* Set TS to return '1' if an error has occured, else '0' if all was ok.
*
!SETERR   MCI C'1',TS             * ASSUME ERROR OCCURED
          B !EXIT
!ALLOK    MOV TSBEG,TS            * RESET THE 'TS' BUFFER POINTER
          MCI C'0',TS             * THEN CLEAR THE ERROR FLAG
          B !EXIT
*
* ************************************ *
* ************************************ *
* Subroutine To Validate Fid Number    *
* And To Determine Max Frame Length.   *
*  Input: TSBEG  - 1 Before Fid Number *
* Output: TS     - At TSBEG            *
*         D0     - Frame Number        *
*         R14    - At Byte 0 In Frame  *
*         CTR20  - No. Chars In Frame  *
*         R15    - At Char Past Fid In *
*                  TS Buffer.          *
*         SB10   - Set If Any Error.   *
* ************************************ *
* ************************************ *
*
!VAL.FID  EQU *
          SB SB10                 * ASSUME AN ERROR
          MOV TSBEG,TS            * RESET INPUT BUFFER POINTER
          MOV TS,R15              * R15 AT START OF INPUT STRING
          MSDB R15                * GET FID FROM TS AS BINARY NUMBER
          BLEZ D0,!VRTN           * EXIT IF FID <= 0
          MOV %CONFIG,R14         * R14 POINTS TO CONFIG TABLE
          BH D0,CFG.MAXFID,!VRTN  * EXIT IF FID > MAXFID
          MOV 512,CTR20           * ASSUME A DATA FRAME
          BHE D0,CFG.PCB0,!NOTABS * JUMP IF FID >= PCB0 (DATA FRAME)
          MOV 2048,CTR20          * ELSE MUST BE AN ABS FRAME
!NOTABS   ZB SB10                 * CLEAR THE ERROR FLAG
          SB B31                  * SET 'UNLINKED' FORMAT BIT IN D0
          SETUP0 R14              * R14 AT BYTE 0 OF OUR FRAME
!VRTN     RTN
*
* ************************************ *
* ************************************ *
* Generate running total for ACCESS.   *
* (Only 1 per statement) Used as a     *
* conversion code in dictionary.       *
* Element usage: FP0, D4, D5, SB20     *
*                TS, TSEND, R15        *
* ************************************ *
* ************************************ *
*
!RUN.TOT  EQU *
          BBS SB20,!NOTFIRST      * JUMP IF NOT FIRST TIME HERE!
          ZERO TOTACCUM           * INITIALISE OUR ACCUMULATOR
          SB SB20                 * AND CLEAR FIRST TIME FLAG
!NOTFIRST MOV TSBEG,R15           * 'R15' 1 BEFORE PASSED VALUE
          BSL CVDR15              * CONVERT TO NUMBER IN 'FP0'
          ADD TOTACCUM            * ADD OUR ACCUMULATOR CONTENTS
          STORE TOTACCUM          * AND THEN SAVE IF FOR NEXT TIME
          MOV TSBEG,R15           * RESET 'R15' TO START OF TARGET STRING
          BSL MBDSUB              * AND CONVERT 'FP0' TO ASCII NUMBER
          MOV R15,TS              * PUT TS ON LAST BYTE OF STRING
          B !EXIT
*
* ************************************ *
* ************************************ *
* Return the last NON-NULL value from  *
* the passed string. Used from BASIC   *
* Element usage: TSBEG, TS, TSEND, R15 *
* ************************************ *
* ************************************ *
*
!LAST.VAL EQU *
          MOV TSBEG,TS            * POINT 'TS' TO 1 BEFORE STRING START
!LVAL0    MOV TS,R15              * 'R15' ON LAST CHAR TO BE RETURNED
!LVAL1    INC TS                  * LOOK AT NEXT CHARACTER
          BCE TS,SM,!LVAL2        * JUMP IF AT END OF SOURCE STRING
          BCHE TS,SVM,!LVAL1      * IF DELIMITER CHAR, REPEAT LOOP
          DEC TS                  * BACK UP FROM DELIMITER
          MOV TS,TSBEG            * AND SAVE AS NEW START POSITION
          SID TS,X'F8'            * SCAN TO NEXT DELIMITER CHAR
          DEC TS                  * BACK UP FROM DELIMITER
          B !LVAL0                * REPEAT FOR NEXT DELIMITED STRING
!LVAL2    MOV R15,TS              * UPDATE 'TS' TO LAST CHARACTER POS.
          B !EXIT                 * AND THEN EXIT!
*
*EOI
