          FRAME 468
*BASIC/UTILITY #1
*28 SEP 1995
*DMM
*
* Entry points 0-4 are used by Pick. We start at 5
*
*  5 = CONVERT -  Emulate 'AP' Convert Function
*  6 = CHKSUM1 -  Generate uniVerse 'CRC' Value
*  7 = CHKSUM2 -  Generate Unidata 'CRC' Value
*  8 = SWAP    -  Emulate UV 'Change' Function
*  9 = BIG.INP -  Long input function
***************************************************************
*
          ORG X'11'               * START AT 'EP #5' !!
*
          EP !CONVERT             * EMULATE 'AP' CONVERT FUNCTION
          EP !CHKSUM1             * GENERATE UNIVERSE 'CRC' VALUE
          EP !CHKSUM2             * GENERATE UNIDATA 'CRC' VALUE
          EP !SWAP                * EMULATE UV 'CHANGE' FUNCTION
          EP !BIG.INP             * LONG INPUT FUNCTION
*
UTIL2     DEFM 0,276
&SWAPSTR  DEFM 2,UTIL2            * REMAINDER OF 'SWAP' LOGIC!
*
CHARH0    DEFC R0,H0              * REDEFINE AS CHAR
CHARH1    DEFC R0,H1              * REDEFINE AS CHAR
*
          ORG X'220'              * START OF AVAILABLE CODE SPACE!!
*
* ***************************************************************** *
* ***************************************************************** *
* Standard conversion exit point.                                   *
* ***************************************************************** *
* ***************************************************************** *
*
!EXIT     EQU *
          MCI SM,TS               * TERMINATE CRC VALUE WITH 'SM'
*
!EXIT1    EQU *
          DEC TS                  * BACK UP 'TS' FROM TERMINATOR
          MOV TS,TSEND            * SET 'TSEND = TS'
          ENT CONVEXIT            * ALL DONE!
*
* *********************************************************** *
* *********************************************************** *
*  Convert string function. Replaces each character in string *
*  A with the corresponding character in B, in string C.      *
*  Called as a user exit from DATA/BASIC in the following     *
*  format:                                                    *
*                                                             *
*   RESULT = OCONV(DELIM:A:DELIM:B:DELIM:C,Uxxxx)             *
*                                                             *
*   Where: RESULT   = The new string after all conversions    *
*          DELIM    = Single Delimiter Character              *
*                                                             *
*  Elements: R14, R15, TS, S4, TSEND, S5, T0, SC2             *
* *********************************************************** *
* *********************************************************** *
*
!CONVERT  EQU *
          MOV TSBEG,TS            * POINT 'TS' 1 BEFORE PASSED VALUE
          MIC TS,SC2              * LOAD DELIMITER CHAR INTO 'SC2'
          BCE TS,SM,!EXIT1        * ERROR IF EARLY TERMINATION!
          MOV TS,S4               * SAVE START-1 OF 'OLD' STRING
          SID TS,X'C1'            * SCAN TO 'SC2' OR SEGMENT-MARK
          BCE TS,SM,!STRERR       * ERROR IF EARLY TERMINATION!
          MCC SM,TS               * CHANGE STRING TERMINATOR TO 'SM'
          MOV TS,S5               * SAVE START-1 OF 'NEW' STRING
          SID TS,X'C1'            * SCAN TO 'SC2' OR SEGMENT-MARK
          BCE TS,SM,!STRERR       * ERROR IF EARLY TERMINATION!
          MCC SM,TS               * CHANGE STRING TERMINATOR TO 'SM'
          MOV TS,TSBEG            * 'TSBEG' AT START OF RETURN VALUE!
*
!CONV0    INC TS                  * BUMP TO NEXT CHAR IN 'TARGET' STRING
          BCE TS,SM,!EXIT1        * EXIT LOOP IF ALL CHARS CHECKED!
          MCC TS,SC2              * LOAD SCAN CHAR WITH 'CURRENT' CHARACTER
          MOV S4,R14              * RESET 'R14' TO START OF 'OLD' CHAR LIST
          SID R14,X'C1'           * IS 'CURRENT' CHAR IN 'OLD' CHAR LIST?
          BCE R14,SM,!CONV0       * IF NOT, GO REPEAT FOR NEXT CHARACTER!
          MOV S5,R15              * SET 'R15' TO START OF 'NEW' CHAR LIST
          LAD R14,S4              * GET POS OF MATCHED CHAR IN 'OLD' LIST
          SITD R15,X'C0'          * FIND MATCHING 'NEW' CHAR (IF ANY)
          BCE R15,SM,!CONV1       * 'OLD' CHAR TO BE DELETED!
          MCC R15,TS              * ELSE REPLACE 'OLD' WITH 'NEW'
          B !CONV0                * AND GO REPEAT FOR NEXT CHARACTER
*
!CONV1    MOV TS,R14              * CLONE 'TS' POSITION INTO 'R14'
          DEC TS                  * BACKUP 'TS' FROM MATCHING CHARACTER
          MOV TS,R15              * DELETE THAT MATCHING CHARACTER BY
          MIID R14,R15,X'C0'      * MOVING REST OF STRING 'BACK 1 SPOT'
          B !CONV0                * REPEAT LOOP FOR NEXT CHAR IN 'TS'
*
!STRERR   EQU *
          MOV TSBEG,TS            * SETUP 'TS' TO RETURN NULL VALUE!
          B !EXIT                 * AND EXIT
*
* ***************************************************************** *
* ***************************************************************** *
* BASIC user-exit to return a uniVerse compatible CRC value for the *
* passed string. Syntax:  CRC = OCONV(string, 'Uxxxx')              *
*                                                                   *
* Element usage:  R14, R15, TS, TSEND, D0                           *
* ***************************************************************** *
* ***************************************************************** *
*
!CHKSUM1  EQU *
          MOV TSBEG,TS            * USE 'TS' TO TRAVERSE STRING
          SRA R15,CHARH0          * POINT 'R15' TO LOW BYTE
          SRA R14,CHARH1          * POINT 'R14' TO HIGH BYTE
          ZERO T0                 * CLEAR CRC ACCUMULATOR
!LOOP1    INC TS                  * POINT 'TS' TO NEXT CHARACTER
          BCE TS,SM,!ENDSTR       * JUMP IF PAST END OF STRING!
          XOR R14,TS              * XOR CHAR INTO HIGH BYTE OF 'T0'
          MOV 8,H2                * NUMBER OF TIMES TO DO LOOP!
!LOOP2    BDLZ H2,!LOOP1          * DO LOOP 8 TIMES ONLY
          BBS B15,!DOXOR          * JUMP IF HIGH BIT SET
          INC T0,T0               * SHIFT 'T0' LEFT 1 BIT!
          B !LOOP2                * REPEAT INNER LOOP
!DOXOR    INC T0,T0               * SHIFT 'T0' LEFT 1 BIT!
          XOR R15,X'21'           * XOR CURRENT CRC VALUE
          XOR R14,X'10'           * WITH X'1021'
          B !LOOP2                * REPEAT INNER LOOP!
!ENDSTR   MOV TSBEG,TS            * SET 'TS' TO TO TARGET AREA
          ZERO T1                 * CLEAR HIGH TALLY OF 'D0'
          MBD D0,TS               * RETURN +VE 16 BIT CRC VALUE!
          B !EXIT                 * JUMP TO EXIT CODE
*
* ***************************************************************** *
* ***************************************************************** *
* BASIC user-exit to return a Unidata compatible CRC value for the  *
* passed string. Syntax:  CRC = OCONV(string, 'Uxxxx')              *
*                                                                   *
* Element usage: FP0, FP1, CTR1, TS, TSEND                          *
* ***************************************************************** *
* ***************************************************************** *
*
!CHKSUM2  EQU *
          ZERO FP1                * CLEAR CRC ACCUMULATOR
          ZERO CTR1               * CLEAR CHAR POSITION POINTER
          MOV TSBEG,TS            * USE 'TS' TO TRAVERSE STRING
          ZERO T2                 * CLEAR TOP OF 'FP0'
!LOOP3    INC TS                  * POINT 'TS' TO NEXT CHARACTER
          BCE TS,SM,!ALLDONE      * JUMP IF PAST END OF STRING!
          INC CTR1                * INC CHAR POINTER POSITION
          ZERO D0                 * CLEAR ACCUMULATOR
          MCC TS,CHARH0           * LOAD CHAR VALUE INTO 'H0'
          MUL CTR1                * MULTIPLY BY POS IN STRING
          INC FP1,FP0             * INCREMENT OUR CRC VALUE!
          B !LOOP3                * THEN REPEAT LOOP
!ALLDONE  MOV TSBEG,TS            * SET 'TS' TO TO TARGET AREA
          ZB B47                  * ENSURE RESULT IS +VE!
          MBD FP1,TS              * RETURN +VE CRC VALUE
          B !EXIT
*
* *********************************************************** *
* *********************************************************** *
*  Replace string function. Replaces string A with string B   *
*  in string C. Called as a user exit from DATA/BASIC in the  *
*  following format:                                          *
*                                                             *
*   RESULT = OCONV(DELIM:A:DELIM:B:DELIM:C,Uxxxx)             *
*                                                             *
*   Where: RESULT   = The new string after all replaces.      *
*          DELIM    = Single Delimiter Character              *
*                                                             *
* Element usage: TS, TSEND, S4, S5, S6, S7, CTR20, CTR21      *
*                CTR22, FP0, FP1, FP2, FP3                    *
* *********************************************************** *
* *********************************************************** *
*
!SWAP     EQU *
          MOV TSBEG,TS            * POINT 'TS' 1 BEFORE PASSED VALUE
          MIC TS,SC2              * LOAD DELIMITER CHAR INTO 'SC2'
          BCE TS,SM,!EXIT1        * ERROR IF EARLY TERMINATION!
          MOV TS,S4               * SAVE START-1 OF 'OLD' STRING
          BSL !GETSTR             * GET 'OLD' STRING
          BCE TS,SM,!STRERR       * ERROR IF EARLY TERMINATION!
          STORE CTR20             * SAVE LENGTH IN 'CTR20'
          MCC SM,TS               * CHANGE STRING TERMINATOR TO 'SM'
          MOV TS,S5               * SAVE START-1 OF 'NEW' STRING
          BSL !GETSTR             * GET 'NEW' STRING
          BCE TS,SM,!STRERR       * ERROR IF EARLY TERMINATION!
          STORE CTR21             * SAVE LENGTH IN 'CTR21'
          MCC SM,TS               * CHANGE STRING TERMINATOR TO 'SM'
          MOV TS,S6               * SAVE START-1 OF 'TARGET' STRING
          MCC SM,SC2              * IGNORE DELIM CHAR FOR LAST STRING!
          BSL !GETSTR             * GET 'TARGET' STRING
          STORE CTR22             * ELSE SAVE LENGTH IN 'CTR22'
*
* Ok, now go and do the actual replace operation!
*
          BSL &SWAPSTR            * GO SWAP THE STRINGS!
*
* We have finished the seach/replace task. It is assumed that 'TS' will
* be pointing to the segment-mark that terminates the target string, and
* that 'S6' is at start-1 of the target string.
*
          MOV S6,TSBEG            * DEFINE START OF RETURN VALUE
          B !EXIT1              
*
* local subroutine to extract next substring from 'TS' delimited by
* segment-mark, or character in 'SC2'. Return length of the extracted
* string in 'T0'. 'S7' used as a scratch storage register.
*
!GETSTR   EQU *
          MOV TS,S7               * SAVE THIS START POS FOR 'LAD' INSTR.
          ONE T0                  * SETUP COUNTER FOR 'SIDC' INSTRUCTION
          SIDC TS,X'C1'           * SCAN TO 'SC2' OR SEGMENT-MARK
          NEG T0                  * MAKE LENGTH VALUE POSITIVE!
          RTN
*
* *************************************************** *
* *************************************************** *
* Utility function for use with BASIC, to provide a   *
* Long 'INPUT' function. Has same editing functions   *
* as the 'normal' BASIC input function, and echo      *
* status is respected in the same way.                *
* Input will terminate after 'max.length' chars, or   *
* 32256 chars if 'max.length < 1'.                    *
*                                                     *
* NOTE: Does NOT accept input from input-stack        *
*                                                     *
*    Called as a user-exit eg:                        *
*                                                     *
*        STRING = OCONV(max.length,'Uxxxx')           *
*                                                     *
* Element usage: TS, TSEND, R15, R14, FP0             *
*                (plus any used by GETBUF)            *
* *************************************************** *
* *************************************************** *
*
!BIG.INP  EQU *
          MOV TSBEG,R14           * USE 'R14' AS TARGET STRING PTR!
          MOV R14,R15             * CLONE 'TS' INTO 'R15'
          BSL CVDR15              * GET NUMERIC PARAMETER!
          BHZ T0,!COUNT.OK        * JUMP IF NUMBER > 0 PASSED!
          MOV 32255,T0            * ELSE SET TO MAX CHARACTERS TO INPUT
!COUNT.OK BSL GETBUF              * GO GET INPUT!
          MCC SM,R15              * PUT 'SM' ON TERMINATING CHARACTER
          MOV R15,TS              * PUT 'TS' ON THE 'SM' CHAR
          B !EXIT1                * AND EXIT BACK TO BASIC
*
*EOI
