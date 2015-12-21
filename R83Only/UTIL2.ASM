          FRAME 276
*BASIC/UTILITY #2
*28 SEP 1995
*DMM
*
* Entry points 0-1 are used by Pick. We start at 2
*
*  2 = SWAPSTR -  Part 2 of swap string function
***************************************************************
*
          ORG X'8'                * START WITH 'EP #2' !
*
          EP !SWAPSTR             * REPLACE 'A' WITH 'B' IN 'C'
*
CHARH0    DEFC R0,H0              * DEFINE CHAR O/LAY FOR H0
CHARH1    DEFC R0,H1              * DEFINE CHAR O/LAY FOR H1
CHARH6    DEFC R0,H6              * DEFINE CHAR O/LAY FOR H6
SREG0     DEFS R2,FP1             * RE-DEFINE AS S/R
SREG1     DEFS R2,FP2             * RE-DEFINE AS S/R
SREG2     DEFS R2,FP3             * RE-DEFINE AS S/R
*
          ORG X'360'              * START OF AVAILABLE CODE SPACE!
*
* *********************************************************** *
* *********************************************************** *
*  Replace string function. Replaces string A with string B   *
*  in string C. Called as a user exit from DATA/BASIC in the  *
*  following format:                                          *
*                                                             *
*   RESULT = OCONV(DELIM:A:DELIM:B:DELIM:C,Uxxxx)             *
*                                                             *
*  The input has been parsed and validated prior to getting   *
*  to this point, so we can go ahead with the swap!           *
* *********************************************************** *
* *********************************************************** *
*
!RTN      RTN                     * 'RTN' USED BY JUMPS!
*
!SWAPSTR  EQU *
*
* Check for conditions that invalidate replace operation!
*
          BLEZ CTR20,!RTN         * 'OLD' STRING NULL ?
          BLEZ CTR22,!RTN         * TARGET STRING NULL ?
          BH CTR20,CTR22,!RTN     * 'OLD' STRING > TARGET STRING !
*
* Strings look ok, so point registers to old & new strings, and reset
* 'TS' register to point to start of target string!
*
          MOV S4,R14              * POINT 'R15' 1 BEFORE 'OLD' STRING
          MOV S5,R15              * POINT 'R15' 1 BEFORE 'NEW' STRING
          MOV TS,SREG0            * SAVE POSITION OF END OF TARGET STRING
          MOV S6,TS               * RESTORE 'TS' TO START OF TARGET STRING!
          MIC R14,SC2             * PUT 1ST CHAR OF 'OLD' STRING INTO 'SC2'
*
* Check for 'easy' jobs first. That is, when the old and new strings
* are of the same length. First prize is when both are 1 char long!
*
!SWAP1    EQU *
          BH CTR20,1,!NOT.ONE     * 'OLD' STRING > 1 CHAR IN LENGTH!
          BU CTR20,CTR21,!NOT.ONE * OR LENGTHS ARE NOT EQUAL!
*
* We have found a situation where both old & new strings are 1 char!
*
          MIC R15,CHARH6          * GET FIRST CHARACTER IN 'NEW' STRING
!SWAPCHR  SID TS,X'C1'            * LOOK FOR OCCURRENCE OF 'OLD' CHAR!
          BCE TS,SM,!RTN          * EXIT IF 'END-OF-STRING' FOUND
          MCC CHARH6,TS           * SWAP 'OLD' WITH 'NEW' CHARACTER!
          B !SWAPCHR              * CONTINUE SEARCH!
*
* Either old or new string is > 1 character long. Terminate both the
* old and new strings with segment marks, then test for replace required!
*
!NOT.ONE  INC R15,CTR21           * 'R15' ON LAST CHAR IN 'NEW' STRING
          MOV R15,S7              * SAVE 'S7' AS LAST CHAR IN 'NEW' STRING
          MOV S4,R15              * 'R15' ON 1ST - 1 CHAR IN 'OLD' STRING
          INC R15                 * BUMP UP SO POINTS TO 1ST CHARACTER
          MOV R15,S4              * THEN SAVE THAT POSITION!
          BH CTR21,CTR20,!NEW.GT  * JUMP IF 'NEW' > 'OLD' IN LENGTH!
*
* This is the situation when the new string is shorter than the old, or
* is the same length as the old.
*
          DEC CTR20,CTR21         * DETERMINE DIFFERENCE IN STRING SIZE
!NEW.LE   SID TS,X'C1'            * LOOK FOR 'OLD' STRING START OR END!
          BCE TS,SM,!RTN          * EXIT IF END-OF-STRING
          MOV S4,R14              * 'R14' ON 1ST CHAR IN 'OLD' STRING
          MOV TS,R15              * CLONE POSITION IN TARGET INTO 'R15'
          BSTE R14,R15,X'FF',!NL1 * LOOK FOR MATCH OF 'OLD' STRING
          BCE R14,SM,!NL1         * MATCH FOUND! - GO DO REPLACE!
          B !NEW.LE               * CONTINUE SEARCH!
!NL1      DEC TS                  * THEN BACK 1 FROM 1ST CHAR IN MATCH
          MOV S5,R14              * PUT 'R14' ON 1ST CHAR-1 OF 'NEW'
          MOV S7,R15              * PUT 'R15' ON LAST CHAR IN 'NEW'
          MIIR R14,TS             * COPY 'NEW' STRING INTO TARGET STRING!
          BZ CTR20,!NEW.LE        * IF 'OLD' & 'NEW' SAME LENGTH, CONTINUE!
          MOV TS,R15              * CLONE POSITION IN TARGET INTO 'R15'
          MOV R15,R14             * THEN AGAIN INTO 'R14'
          INC R15,CTR20           * 'R15' AT END OF 'OLD' IN TARGET STRING
          MIID R15,R14,X'C0'      * COPY TARGET BACK AFTER 'NEW'!
          B !NEW.LE               * CONTINUE SEARCH!
*
* This is the situation when the new string is longer than the old. This
* is the most difficult operation, and also is the only one where buffer
* overflow is a possibility. Note that this is not tested for!
*
!NEW.GT   MOV S6,SREG2            * SAVE START OF TARGET STRING POSITION!
!GT0      MOV SREG0,R15           * 'R15' ON START OF NEW BUFFER
          INC R15,CTR21           * ALLOW FOR 'GROWTH'!
          MOV R15,SREG0           * AND SAVE AS NEW TARGET START!
!GT1      SID TS,X'C1'            * LOOK FOR 'OLD' STRING START OR END!
          BCE TS,SM,!RTN          * EXIT IF END-OF-STRING
          MOV S4,R14              * 'R14' ON 1ST CHAR IN 'OLD' STRING
          MOV TS,R15              * CLONE POSITION IN TARGET INTO 'R15'
          BSTE R14,R15,X'FF',!GT2 * LOOK FOR MATCH OF 'OLD' STRING
          BCE R14,SM,!GT2         * MATCH FOUND! - GO DO REPLACE!
          B !GT1                  * CONTINUE SEARCH!
!GT2      MOV R15,SREG1           * SAVE POS IN TARGET AFTER 'OLD' MATCH!
          DEC TS                  * THEN BACK 1 FROM 1ST CHAR IN MATCH
          MOV TS,R15              * CLONE POSITION IN TARGET INTO 'R15'
          MOV SREG0,TS            * 'TS' AT START OF NEW BUFFER AREA!
          MOV S6,R14              * 'R14' ONE BEFORE TARGET STRING START
          MIIR R14,TS             * COPY 1ST BIT OF TARGET INTO NEW BUFFER!
          MOV S5,R14              * PUT 'R14' ON 1ST CHAR-1 OF 'NEW'
          MOV S7,R15              * PUT 'R15' ON LAST CHAR IN 'NEW'
          MIIR R14,TS             * COPY 'NEW' STRING INTO TARGET STRING!
          MOV SREG1,R15           * 'R15' IN TARGET STRING AFTER MATCH!
          DEC R15                 * BACK UP 1 FOR 'MIID' INSTRUCTION
          MOV TS,R14              * 'R14' ON LAST CHAR IN 'NEW' IN TARGET!
          MIID R15,R14,X'C0'      * COPY REST OF TARGET INTO NEW BUFFER
          MOV SREG0,S6            * SET 'S6' TO NEW TARGET STRING START
          MOV R14,SREG0           * ASSUME NEW BUFFER IS AFTER TARGET STR!
          BE S6,SREG2,!GT0        * JUMP IF USING '1ST' BUFFER AREA
          MOV SREG2,SREG0         * ELSE SET NEW BUFFER TO '1ST' BUFFER
          B !GT1                  * REPEAT SEARCH!
*
*EOI
