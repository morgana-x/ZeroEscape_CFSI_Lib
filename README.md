# ZeroEscape_CFSI_Lib
A library to interface with CFSI Archives from ZE: Zero Time Dilemma 

Other tools for Zero Time Dilemma:
+ https://github.com/morgana-x/ZeroEscape_ZTD_BIN_Extract - .bin - localised text
+ https://github.com/enler/RTZParser - .rtz - scripts (Mostly disassembling not recompiling to knowledge)
+ https://gamebanana.com/tools/19281 a reupload of an older repack extract tool for CFSI files
### Capabilities
+ Extracting all files
+ Repacking all files (including modded regardless of extra files / larger / smaller size)
+ Extracting individual files via API in code
# Credit
While I Reverse-Engineered 95% of the archive and it's quirks on my own, I wasn't able to figure out the padding and alignment, as such when I looked at ALuigi's BMS script it ended up helping me quite a bit. (Before that when extracting I just skipped any zero bytes when padding was present, and was unable to progress in repacking)

Resources used:
+ https://www.zenhax.com/viewtopic.php@t=2697.html
+ http://aluigi.org/bms/zero_time_dilemma.bms
+ https://en.wikipedia.org/wiki/Data_structure_alignment#Computing_padding

# CFSI DOCUMENTATION

## CFSI_VInt (1-3 Bytes)
Short for CFSI_Variable_Integer
+ Read unsigned byte, if the byte doesn't equal 252, that is the value
+ If initial byte equals 252, then the real value is an UInt16/UShort that comes after it

## CFSI_String
+ Byte For Length
+ Array of bytes for text

## CFSI_Archive
+ CFSI_VINT - Number of Folders
  + For number of folders:
      + CFSI_String - Folder Name
      + CFSI_VINT - Number of Files
        + For Number of files:
            + CFSI_String Name
                + Filepath can be assumed as Foldername + Filename
            + Int32 - Offset (Needs to be multiplied by 16)
            + Int32 - File Size
+ DATA_OFFSET = The cursor position of the stream at this point is the start of the data section
+ Use the algorithm on https://en.wikipedia.org/wiki/Data_structure_alignment#Computing_padding for alignment on DATA_OFFSET, with Align 0x10
+ foreach file:
  + fileDataLocation = DATA_OFFSET + file_Offset
  + byte[fileSize] - raw file data @fileDataLocation
  
