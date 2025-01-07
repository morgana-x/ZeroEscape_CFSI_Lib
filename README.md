# ZeroEscape_CFSI_Lib
A library to interface with CFSI Archives from ZE: Zero Time Dilemma 

Currently only capable of extracting files, as I haven't figured out the logic behind the whitespacing and the section of file data for each file that I presume is responsible for it.


# CFSI DOCUMENTATION

## CFSI_VInt (1-3 Bytes)
ZFSI_Variable Integer for short
+ Read unsigned byte, if the byte doesn't equal 252, that is the value
+ If initial byte equals 252, then the real value is an Integer-16 / UShort

## CFSI_String
+ Byte For Length
+ Array of bytes for text

## CFSI_WhiteSpace
+ Variable Size
+ Skip to the first non zero byte

##  CFSI Archive

+ CFSI_VINT - Number of Folders
  + For number of folders:
      + CFSI_String - Folder Name
      + CFSI_VINT - Number of Files
        + For Number of files:
            + CFSI_String Name
                + Filepath can be assumed as Foldername + Filename
            + 4-bytes ????
            + Int32 - File Size
            + Strongly reccomend storing Filepath and adding to a list of global files as that is the order that the data section follows
+ ==============================
+ Raw File Data Section
+ ============================== 
  + CFSI_WhiteSpace - Whitespace
  + Byte Array [File 1's Size] - File 1's Raw Data
  + CFSI_WhiteSpace
  + Byte Array [File 2's Size] - File 2's Raw Data
  + CFSI_Whitespace
  + ......etc
