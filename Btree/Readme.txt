Btree Notes - rel 1.0  Feb 96
=============================

Disclaimer
----------

IMPORTANT NOTE: This code is being distributed for free. You may do as
you wish with it. There are no restrictions on its use at all. In the
grand tradition of 'you get what you pay for', there is absolutely no
guarantee that the code works as described. Bottom line is: do what
you want with it, at your own risk!


Final warning: This code has never been used in a 'live' situation, so
again I would recommend you test it before relying too heavily on it!


Btree code distribution
-----------------------

This Btree utility set is made up of the following six items:
   
   README.TXT      - This document
   BTREE.UPDATE    - Btree file update function
   BTREE.READ      - Btree file read function
   BTREE.LIST      - Utility, list existing btree file
   BTREE.REGEN     - Utility, regenerate a btree file
   BTREE.ADMIN     - Utility, simple btree admin tool


Porting notes
-------------

General notes:

   - Code is distributed in a format 'ready to compile' on Pick/R83
   - Porting 'codes' are included in the source for conversion to
     Pick/AP and jBase platforms.

     Porting to other flavours should be quite easy, but is left as
     an 'exercise for the reader' :)

This code has been ported and tested (a bit) on the following platforms:

   jBase 3.0 (Linux) Beta build 3352  (Do NOT use on prior 3.0 beta's!)
   R83 3.1M
   AP/SCO 6.1

On each platform, basic 'sanity checks' have been run. To port the Basic
code, the following points should be observed.

1) For those platforms that support 'named common', the interface to
   the btree read function can be amended to take advantage of this
   feature. We normally pass a dimensioned array to/from the BTREE.READ
   function in order to preserve the read 'state'. This is better
   implemented as an array in a named common block on those platforms
   that support this.

   Note: This porting step is optional. The named common usage is
   intended as an optimisation, and is not really required. Do this
   only if you wish.

   To implement this, edit items BTREE.READ and BTREE.LIST
   In each item:

   a) Towards the top of the code, find the lines that read:

      !*!  COMMON /R$BTREE/ STATE(8)
           DIM STATE(8)

      Now change these to read:

           COMMON /R$BTREE/ STATE(8)
      !*!  DIM STATE(8)

   b) In program BTREE.LIST, look for the parameter list in calls to external
      subroutine BTREE.READ (and in that program, look at line 1!). In
      each case, remove the last parameter. This array no longer needs
      to be passed, as its contents will be maintained in 'named common'.

2) On all platforms except R83, the technique used to get command line
   input must be amended. On R83 we use the undocumented 'ERROR()'
   function, but this is not supported outside of this environment. So,
   edit the items BTREE.REGEN and BTREE.LIST. In each, replace the use of
   the ERROR() function with appropriate logic for the target platform.
   For jBase, this is as simple as replacing any occurrences of ERROR()
   with @SENTENCE. For Pick/AP, this line must be amended to use the
   'TCLREAD' statement. The comments in the code should make this clear.


Background
----------

This code implements a 'variation' on the traditional btree data structure.
As this is not intended as a primer on btree characteristics, the book
'File Structures 2nd edition, Michael J. Folk and Bill Zoellick' is
suggested for those interested in the topic.

In brief, a traditional btree contains information grouped as sets of
'pairs'. One member of each 'pair' is a key, the other is the information
associated with that key. These pairs are distributed all over the nodes
of the index. This differs from b+trees where all the keys are stored
in the leaves of the index, sometimes in a separate file called the
sequence set. These leaf nodes are often linked, so that the keys may
be processed in a truly linear manner.

The implementation presented is a hybrid. It resembles a standard btree
in that there is only a single file. However the structure resembles a
b+tree as all the key information is stored in the leaf nodes. These
leaves are linked to permit ascending or descending order traversal.


Implementation features
-----------------------

Various sequencing options: The information associated with the key
value determines the sequence of the index. In the traditional (and most
of the 'native') implementations, the sequence cannot be changed, and
is assumed to be 'ascending, left justified'. This can result in having
to resort to contortions via correlative fields to implement right
justified sequences. For this code, you may define an index sequence
as one of: AL, AR, DL, DR as per the Pick INSERT statement.

Key sequencing options (secondary sort): If non-unique strings are to be
permitted in the index it is possible to control the order of the key
insertions for the same index string. Options include:
     AL, AR, DL, DR as per Pick INSERT statement.
     1 = insert before other keys with same index string.
    -1 = insert after other keys with same index string.

Index option flags: For each index, some characteristics may be asserted
at index definition time. These include:
     - if MV/SVM index info to be stored. If selected, this option causes
       the multi-value/sub-multi-value position of the indexed string to
       be stored in the index.
     - if indexed string must be 'upper cased'. Convert index strings to
       upper-case before any index operation.
     - if unique values only may be indexed. Disallow insertion of index
       strings and keys where the index string already exists in the index.
     - if null values are to be ignored. If the index string presented
       to the index update function is null (ie: of zero length), no
       index operation will take place.
     - if each word is to be indexed. That is, each space delimited 'word'
       of an index string will be indexed separately. For each entry, the
       index key will be the same.

Selectable node size: The index node size may be specified at index
definition time. This can be any value greater than 100 bytes, but is
probably best set to the platform 'frame size'. If set to less than 100,
it will default to 500.

Flexible index string generation: The actual string to be indexed may be
determined in a number of ways. These include:
     - Specification of a particular attribute (or value within an
       attribute) of the record being updated. For example:
          - 3       = index on contents of attribute 3
          - 3.4     = index on attribute 3, value 4
          - 0       = index on the item-id (key value)
     - The string value determined using one of the techniques defined
       above may be further amended using a conversion. This may be
       specified as:
          - code    = any valid conversion code. The index string will
                      have this conversion applied using an OCONV().
          - @name   = an '@' token followed by the name of a cataloged
                      Basic subroutine. This can be used to perform
                      more complex 'massaging' of the index string.
                      This subroutine is assumed to have 5 parameters,
                      and is called internally as:

                      CALL @name(MODE, I.STR, ORIG.REC, RECORD, KVAL)

                      Where: MODE - index update mode
                            I.STR - index string value
                         ORIG.REC - original record image
                           RECORD - current record image
                             KVAL - key being indexed for this 'I.STR'

                      The value of 'I.STR' may be updated within this
                      function, and becomes the new index string value
                      on return. Other parameters should not be changed.
                      
Update locking strategy: The index will be locked to a single user for
the duration of an index update. If an error is encountered during update,
the lock will remain in force until the update is complete, or any (user
specified) error correction/reporting logic has been executed. Note that
multiple indexes may exist in a single index 'file'. Only the index
currently being updated is locked.

Read locking strategy: The index is not locked during read access, with
the exception of when 'MODE' is set to 6 (Build select list of all keys in
index). In this case we lock to inhibit update while using the Query
processor to collect the key list.


File usage
----------

A single btree index file is associated with a data file. The data file
may have as many index definitions as required, with all the indexes being
stored in the same index file.

The index file has the same name as the data file, but is suffixed by
'.IDX'. Therefore, a data file called 'CUSTOMERS' would have an index
file called 'CUSTOMERS.IDX'

In each data file that has indexes defined, an index definition item
will be stored in the dictionary of that file. This will always have
the key '$BTREE'.


Data structures
---------------

Each node in the index file has following structure:

  <1, n>  = Indexed strings
  <2, n>  = Node ptrs (if <3, 1> = 0), else key values
  <3, 1>  = 1 = leaf, 0 = non-leaf
  <3, 2>  = number of entries in node
  <3, 3>  = left sibling node   [leftmost node if root]
  <3, 4>  = right sibling node  [rightmost node if root]
  <3, 5>  = pointer to parent node [null if root]
         [values <3, 6> to <3, 11> stored in root node only!]
  <3, 6>  = last used node item-id
  <3, 7>  = tree 'depth'
  <3, 8>  = sort sequence for <1>
            one of: AL, AR, DL, DR ('null' = AL)
  <3, 9>  = sort sequence for <2>
            one of: 1, -1, AL, AR, DL, DR ('null' = 1)
  <3, 10> = flag to indicate if MV/SVM pointers stored in index
  <3, 11> = max node size (bytes) If < 100, defaults to 500
  <4, n>  = MV/SVM index pairs (svm delimited) for each leaf entry,
            if flag in <3, 10> set to 'true'. Else null.


Each index definition item, stored in the dictionary of the associated
data file as an item called '$BTREE', has the structure:

  <1>    = 'X'
  <2, n> = mv'd - brief description of index definition
  <3, n> = mv'd - index definition. Defines source of index string.
                  eg: 0 (key), 1 (attr 1), 1.4 (attr 1, value 4)
  <4, n> = mv'd - conversion code to apply to value defined in <3,x>.
                  The conversion will be processed using OCONV()
                  unless prefixed by '@', when following value is
                  assumed to be a cataloged Basic subroutine name.
  <5, n> = mv'd - input conversion codes, if any.
  <6, n> = mv'd - indexed string justification codes ('null' = AL)
  <7, n> = mv'd - key value justification codes ('null' = 1)
  <8, n> = mv'd - maximum node sizes, in bytes ('null' = 500)
  <9, n> = mv'd - flags (implemented as an integer) where each 'bit'
                  represents a true/false flag. Eg: a value of 17
                  means 'Index on words' and 'Save MV/SVM ptrs'.

                  bit flags:
                  4 3 2 1 0
                  | | | | |
                  | | | | +- true, if MV/SVM index info to be stored
                  | | | +--- true, if indexed string must be 'upper-cased'
                  | | +----- true, if unique values only may be indexed.
                  | +------- true, if null values are to be ignored
                  +--------- true, if indexing on each word

                  Therefore: 17 equals a 'bit pattern' of '10001'


Core subroutine interfaces
--------------------------

At the heart of this btree implementation are two subroutines. One must be
used in association with every file update on an indexed file, and another
is used to retrieve information from the index itself.

First, let's look at the update function. This is called BTREE.UPDATE, and
has the following interface:

BTREE.UPDATE(MODE, F.INDX, INDEX.DEFN, ORIG.REC, RECORD, RKEY, ERRFLG)

Where:

Parameters passed:

         MODE - 1 = Insert
                2 = Amend
                3 = Delete
       F.INDX - file handle of index file.
   INDEX.DEFN - index definition item for file. Note that the first two
                attributes are assumed to have been removed.
     ORIG.REC - data item, prior to updates (if MODE = 2 or 3)
       RECORD - current data item image (if MODE = 1 or 2)
         RKEY - key to data item 'RECORD' & 'ORIG.REC' in data file.

Parameters returned:

       ERRFLG - 0 = no error.
                1 = error - Invalid 'MODE' value specified.
                2 = error - Index corrupt.


The second core subroutine is used to traverse the index file returning
key and index string information. This function has the interface:

BTREE.READ(MODE, F.INDX, INDEX.POS, MAX.IDS, I.STR, IDLIST, ERRFLG,
           MAT STATE)

Where:

Parameters passed:

         MODE - 0 = Position in index, return next entry.
                1 = Position in index, return previous entry.
                2 = Position at start of index, return next entry.
                3 = Position at end of index, return previous entry.
                4 = Get next value(s).
                5 = Get prev value(s).
                6 = Build select list from index, return ALL keys.
       F.INDX - file handle of index file
    INDEX.POS - Index number we want to read from. This number
                represents the VM index in the '$BTREE' item. 
      MAX.IDS - Number of entries to read. That is, we can read one
                entry at a time, or return 'chunks' of a specified size.
        I.STR - Indexed value to 'find' if MODE < 4
              - Index file name if MODE = 6
        STATE - The 'STATE' array is used to pass preserved 'state'
                between subsequent calls to this function. See section
                1 under the heading 'Porting notes' earlier in this
                document. On jBase and Pick/AP this array may be replaced
                with a named common block.

Parameters returned:

        I.STR - Last index value accessed
       IDLIST - Item keys (may be up to 'MAX.IDS' mv'd keys)
                <1.m> = mv'd list of index 'key' entries
                <2.m> = mv'd list of indexed strings for <1>
                <3.m> = mv'd list of VM/SVM pointers (svm delimited)
                        NB: This last attr only returned if index
                        contains this pointer information.
       ERRFLG - 0 = no error.
               -1 = (minus one) end-of-index detected.
                1 = error - Invalid 'MODE' value specified.
                2 = error - Index corrupt.


Utility functions
-----------------

Three utility functions have been provided. These may be used to maintain
index definition items, regenerate existing index files (perhaps after
adding, deleting or changing the index definition) and displaying the
contents of an existing index. Each will discussed in turn.


BTREE.ADMIN

This function is used to maintain the $BTREE index definition item for a
data file. This item is stored in the dictionary of the data file, and
may exist even when no .IDX file (index file itself) is present. Options
are provided to create, amend or delete index definitions. When the
definition item is filed, it is possible to regenerate the index file to
reflect any changes. Note that this involves running the BTREE.REGEN
program (see next note), and that this is a fairly 'dumb' program. It
will regenerate all indexes from scratch, not just any new or changed
definitions.


BTREE.REGEN

Used to regenerate all the indexes for a specified data file. The existing
index file is cleared, then each item in the data file is read and the
index update function is called. This is very much a 'brute force' way to
rebuild the indexes and may be rather slow for large data sets.


BTREE.LIST

This is a simple tool to allow 'browsing' of an existing index file. The
data file name is specified, then an existing index on that file is chosen.
Various options are provided to permit the index to be listed from the
beginning or end, forwards or backwards and optionally with a specified
starting value.


Example usage
-------------

For indexing to be effective, all updates to an indexed data file must be
presented to the BTREE.UPDATE function. Note that this function will not
do the actual database write, but only takes responsibility for updating
any defined indexes for the file. The calling function must ensure that
the update function knows if a new item is being added to the data file,
or an existing item is being amended or deleted. This is achieved by setting
the 'MODE' variable (see BTREE.UPDATE interface notes) to the appropriate
value. An example may make this clearer.

    DNAME = datafilename
    OPEN '', DNAME TO F.DATA ELSE STOP 201, DNAME
    OPEN 'DICT', DNAME TO F.DICT ELSE STOP 201, 'DICT ':DNAME
    OPEN '', DNAME:'.IDX' TO F.INDX ELSE STOP 201, DNAME:'.IDX'
    *
    READ INDEX.DEFN FROM F.DICT, '$BTREE' THEN
       * Remove 1st two attributes (not required)!
       INDEX.DEFN = DELETE(INDEX.DEFN, 1, 0, 0)
       INDEX.DEFN = DELETE(INDEX.DEFN, 1, 0, 0)
    END ELSE INDEX.DEFN = ""
    ...
    READU ITEM FROM F.DATA, ID ELSE
       MODE = 1       ;* flag to indicate 'adding item'
       ITEM = ''
    END ELSE
       MODE = 2       ;* Assume we are amending an item
    END
    ORIG.ITEM = ITEM  ;* Save original item image!!
    ...
    do any processing for 'ITEM', but leave 'ORIG.ITEM' unchanged!
    If we want to delete the item from file, set 'MODE' to 3
    ...
    * Update step
    IF MODE < 3 THEN
       WRITE ITEM ON F.DATA, ID
    END ELSE
       DELETE F.DATA, ID
    END
    * Now update index file as well!
    CALL BTREE.UPDATE(MODE, F.INDX, INDEX.DEFN, ORIG.ITEM, ITEM, ID, ERR)
    IF ERR THEN
       * Error found while attempting index file update!
       ...
    END
    
It may also be helpful to look at the BTREE.REGEN and BTREE.LIST utilities
as examples of core subroutine usage.
