# P5R-Translation-Tool

A tool for packaging your own translation of the Persona 5 Royal.
Supported versions of the game:
* Steam: 1.0.4
* Gamepass: 1.0.4
* Switch: 1.0.2

An example of packing together with pre-unpacked original text and textures is attached in [current archive (21 sep 2024)](https://drive.google.com/file/d/1-nPeoZfIy_sDoG_6VgwVXzoYb5sCO3KQ). **You must download it.**

# How to use P5R-Packager.exe
## Insert translation to CPK

CPK must first be fully unpacked to any folder.
The English version of the game is taken as a basis, so you need to unpack the EN.CPK (or ALL_USEU.CPK and PATCH1.CPK for Nintendo Switch).

The following arguments are used:
1. `DATA` - always as first argument.
2. `-input <path>` - instead of \<path\>, the path to directory with unpacked CPK file.
3. `-output <path>` - instead of \<path\>, the path to directory where the "translated" files will be saved.
4. `-translation <path>` - instead of \<path\>, the path to directory where is the translation.
5. `-oldenc <name>` - instead of \<name\>, the file name (without extension) of old encoding.
6. `-newenc <name>` - instead of \<name\>, the file name (without extension) of new encoding.
7. (Optional) `-copy2out` - command to copy all files to the output directory, not just the "translated" ones. Just to save time, so as not to copy the rest of the files separately before packing the new CPK. Useful for the PC version.

#### Example:
`P5R-Packager.exe DATA -input "D:\P5R\EN_CPK" -output "D:\P5R\NEW_CPK" -translation "D:\P5R\Translation" -oldenc P5R_ENG -newenc P5R_RUS -copy2out`

## Insert translation to EXE

The following arguments are used:
1. `EXE` - always as first argument.
2. `STEAM` or `GAMEPASS` or `SWITCH` - always second argument, executable type.
2. `-input <path>` - instead of \<path\>, the path to executable file.
3. `-output <path>` - instead of \<path\>, the path to **directory** where the "translated" executable (or patch) will be saved.
4. `-translation <path>` - instead of \<path\>, the path to directory where is the translation.
5. `-oldenc <name>` - instead of \<name\>, the file name (without extension) of old encoding.
6. `-newenc <name>` - instead of \<name\>, the file name (without extension) of new encoding.
7. (Optional, Recommended) `-patch` - command to create a patch instead of a new executable file.

#### Example (create new Steam exe):
`P5R-Packager.exe EXE STEAM -input "D:\P5R\P5R.exe" -output "D:\P5R\NEW_EXE" -translation "D:\P5R\Translation" -oldenc P5R_ENG -newenc P5R_RUS`
#### Example 2 (create Switch patch):
`P5R-Packager.exe EXE SWITCH -input "D:\P5R\main" -output "D:\P5R\SW_PATCH" -translation "D:\P5R\Translation" -oldenc P5R_ENG -newenc P5R_RUS -patch`

#### Note:
It is recommended to specify `-patch` argument.
In this case, two files will be created for the PC version (both Steam and Gamepass): `TRANSL.DAT` and `xinput1_4.dll`. `xinput1_4.dll` - a simple proxy DLL that patches translated data on the fly when loading the game. `TRANSL.DAT` - data for patching the game.
A standard IPS patch will be created for the Switch version.

Otherwise, if you do not specify argument, then for all platforms, the modified executable file will be in the output folder. This method may be suitable only for the Steam version. There are certain difficulties in installing the executable file in Gamepass and Switch version.

# Translation Folder

Folder structure example:
```
Translation
├── PLG
│   └── SOME.PLG
├── TEX
│   └── SOME.PNG
├── TEXT
│   ├── P5R_crossword.zip
│   ├── P5R_exe.zip
│   ├── P5R_tables.zip
│   ├── P5R_text.zip
│   └── readme.txt
├── P5R_ENG.FNT
├── P5R_ENG.FNTMAP
├── P5R_RUS.FNT
└── P5R_RUS.FNTMAP
```

In this folder, you will need to add the translated text, translated textures and vector files (.PLG). You will also need to store there the original font and encoding (.FNTMAP), and if necessary, a modified font and encoding.

## Font and encoding

The P5R_ENG.FNT and P5R_ENG.FNTMAP files are present in the attached archive.

To create your own font and encoding, use the application [PersonaEditor](https://github.com/Meloman19/PersonaEditor).

If the old encoding is used for translation, it is enough to specify the same name in the `-newenc` argument as in the `-oldenc` argument.

## TEXT

The text is divided into 4 groups: crosswords (P5R_crossword.zip), text from the executable file (P5R_exe.zip), text from table files (P5R_tables.zip) and main game text (P5R_text.zip )

The archives contain text files in TSV format. The format and structure of files cannot be changed and I recommend using Google spreadsheet or Excel for editing. In the file readme.txt you can read about the limitations associated with each type of text.

You can also use ready-made Google spreadsheets: [сrosswords](https://docs.google.com/spreadsheets/d/1E__7Hg7GzCPpqybmc5k-O8gz9aRjZFkulE2nNhZC23k), [exe](https://docs.google.com/spreadsheets/d/1qBjBim8zqVx4W6pUXSQF1gxuX6PYjauvzD1uq6vrHDc), [main text](https://docs.google.com/spreadsheets/d/1ECJy0gnOeqJLQTE7YhbnYG8j_BXYwVzdAFg6uiFSiL8) and [tables](https://docs.google.com/spreadsheets/d/1zhP6AgLfeqK1HTO4T0tmx_PLZ2SXvVSGOLh7RUm1m0I). Just open and create your own copy, you can also translate in these spreadsheets.

Also, these spreadsheets already have a script that will pack all the text to the archive and save it to your Google drive. To do this, go to "Extensions -> Macros -> Import macro" and select "Add function" for "Download". After that run "Extensions -> Macros -> Download".

## TEX and PLG

The TEX and PLG folders are used to store translated textures and vector files.

To edit the sizes and coordinates of textures in SPD files, you can use PersonaEditor. It has a user-friendly graphical interface for editing. After changing the coordinates and dimensions, you need to save them to an XML file and copy them to the appropriate folder.

To save or open an XML file, you need to right-click on the SPD file in the tree view on the left and click Replace or Save As.

**Important**: 

# Important

### Gamepass EXE
To create a translation for the Gamepass version, a decrypted EXE is required. You can get such an executable file, for example, using [UWPDumper](https://github.com/Wunkolo/UWPDumper).

### Texture translation
You need to translate only those textures that are in the attached archive. And you need to keep the directory and file structure!

### Text translation
The translation must be entered in the column with the title TRANSL.

### Text packaging
#### EXE
To pack the text into an EXE, you must first make a complete translation (you can make a draft). Otherwise, there may be errors related to the missing translation in the TRANSL column.
#### Crossword
For crosswords, a complete translation is also required. Before that, it is enough to delete the file `Translation\TEXT\P5R_crossword.zip` so that no changes are made.
#### Other text
There are no restrictions listed above for tables and the main text. You can pack an incomplete translation. If there is no translation, the original text will be used for repacking (if the encoding changes).

### Switch EXE
For the switch version, the executable file was not fully researched. Some not important text are missing. It doesn't hurt to play.