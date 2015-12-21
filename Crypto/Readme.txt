
Encryption utility functions                 rev: 1.1
============================

Changes (Most recent first)
-------

9 Sept 2001 - Fixed problem in the MD5 subroutine caused by the "MX"
              conversion working differently when in UV PRIME/IDEAL
              account, as it works in a UV PICK account (or on R83 ..etc)
            - Made similar changes to TEST.BLOWFISH to avoid using the
              "MX" conversion code.
            - Made adjustments to use of BITxxx() functions (Universe &
              Unidata) after discovering that these returned different
              results on different platforms for some input values
              (1 or both input values > 0x7fffffff)

Disclaimer
----------

IMPORTANT NOTE: This code is being distributed for free. You may do as
you wish with it. There are no restrictions on its use at all, other
than any legal limitations on the algorithms themselves. In the
grand tradition of 'you get what you pay for', there is absolutely no
guarantee that the code works as described. Bottom line is: do what
you want with it, at your own risk!

Final warning: This code relies heavily on 'bit' operations. While it
can be ported to platforms that do not include these intrinsic functions
in BASIC, performance will be very (and I _do_ mean very) poor.

Encryption code distribution
----------------------------

The encryption utility set is made up of the following 14 items:
   
   README.TXT      - This document
   BASE64          - Base64 encoding utility
   CIPHER          - Stream cipher function
   ICRYPT          - En/decrypt demo program
   IDEA            - 'IDEA' crypto function
   MD5             - 'MD5' hashing function
   SHA1            - 'SHA1' hashing function
   BLOWFISH        - Blowfish crypto function
   TEST.BASE64     - Verify base64 function
   TEST.CIPHER     - Verify stream cipher utility
   TEST.IDEA       - Verify IDEA function
   TEST.MD5        - Verify MD5 function
   TEST.SHA1       - Verify SHA1 function
   TEST.BLOWFISH   - Verify BLOWFISH function


Porting notes
-------------

General notes:

   - Code is distributed in a format 'ready to compile' on UniVerse
   - Porting 'codes' are included in the source for conversion to
     Pick/AP/R83 and UniData platforms. Note that as UniData is so
     similar to UniVerse, this is a _very_ easy 'port'. In fact, the
     only change for UniData involves uncommenting 2 lines in the
     ICRYPT program.

     NOTE: The BLOWFISH crypto function requires 'named common'.
           Therefore, it is only provided in uv/udt 'flavour'.

Porting to other flavours _should_ be quite easy, but is left as
an 'exercise for the reader' :)

This code has been ported and tested (a bit) on the following platforms:

   UniVerse & UniData
   R83 3.1M
   AP/SCO 6.1

On each platform, the basic 'sanity checks' have been run (ie: the TEST.xxx
functions). To port the Basic code, the following points should be observed.

1) For those platforms that support 'named common', the interface to the
   MD5, SHA1 and CIPHER functions has been amended to take advantage of this
   feature. For other platforms, we need to pass a dimensioned array to/from
   the functions to maintain 'state' over successive calls. This 'state' is
   internal to the functions, so the state 'array' need not be initialised
   in the calling routine. Of couse, its contents should never be amended
   by any external routine either!

2) For the R83 port of the ICRYPT program we have used the undocumented
   'ERROR()' function to return the command line. If you are porting to
   other generic Pick type platforms, this code must be amended as required.
   For example, Pick/AP might require this line to be amended to use the
   'TCLREAD' statement.

3) The BASE64 function was written to run on any platform. That is, it does
   not rely on bitwise operators/functions. It is therefore not likely to be
   the fastest implementation possible. If speed is a real consideration,
   this function should be optimised for the target platform (and maybe changed
   to be table-driven ..etc)

4) The BLOWFISH crypto function requires 'named common'. Therefore, it is
   only provided in uv/udt 'flavour'.

The actual porting tokens in the code are very simple. There are lines that
are commented out with tokens, and lines that have tokens as comments.
For example:

     COMMON /C$MD5/ MD5.STATE(7) ;* udt/uv
*r83 DIM MD5.STATE(7)

In this example, to port to R83, you would uncomment the second line, and
comment out (or delete) the line above. The lines with tokens indicate that
they are specific to particular environments.

Possible tokens are:  'r83', 'udt' and 'uv' - Therefore:

*r83 DIM MD5.STATE(7)

means 'uncomment this line for a R83 port'

COMMON /C$MD5/ MD5.STATE(7) ;* udt/uv

means this line is specific to the UniData and Universe ports. Therefore it
would be removed or commented out for a R83 port.

The only exception to this 'rule' is for subroutine declaration statements.
There are a couple of subroutines that require different parameter lists.
This might look like:

     SUBROUTINE MD5(MODE, SVAL)
*r83 SUBROUTINE MD5(MODE, SVAL, MAT MD5.STATE)

This means that the R83 port should use the 2nd statement, in which case the
first should be deleted.

After porting and compiling, use the TEST.xxx programs to validate the code
on your new platform. If these report any error, you have a problem :)


Background
----------

The code presented includes a fairly naive implementation of the IDEA
encryption algorithm, and the MD5 hash function. These in turn are used by
an example 'stream-cipher' function that enables strings to be encrypted
or decrypted. An example program called ICRYPT is provided as an example
of how this cipher function could be used. A base64 encoding utility is
also included. Note that some of these functions (eg: MD5, BASE64) can be
used independently of the cipher utility. An additional hash function to
generate SHA1 hash codes is included, along with an implementation of the
BLOWFISH encrytpion algorithm.


Implementation notes
--------------------

The hash and encryption functions require a lot of bit manipulation. For
platforms that do not have these intrinsic functions in Basic, there is
code included to perform these operations. Note that this will be very
slow! I make no claim that the implementations of these 'longhand' bit
manipulation functions are optimal.
    
It may be helpful to look at the ICRYPT utility as an example of core
subroutine usage. This program can be used to encrypt or decrypt an item.
For R83 ports, the encrypted item is stored in hex (to avoid 0xFF char
issues!) - so you need to take into account the larger size of this item
(ie: will you have 32k string size problems?) For UniVerse and UniData,
the encrypted string is stored base64 encoded. This is not really required,
but I wanted to include an example of how this function was called.

*end-of-readme*
