using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using AuxiliaryLibraries.IO;
using P5R_Packager.Common;
using PersonaEditorLib;
using PersonaEditorLib.FileContainer;
using PersonaEditorLib.Other;

namespace P5R_Packager.DataTool.TableImport
{
    internal sealed class TableImporter
    {
        private const string TextDir = "TEXT";
        private const string TableZip = "P5R_tables.zip";

        private readonly Dictionary<string, Func<GameFile, PersonaEncoding, PersonaEncoding, bool>> handlers;

        private readonly PersonaEncoding _oldEncoding;
        private readonly PersonaEncoding _newEncoding;

        private Dictionary<string, Dictionary<string, Dictionary<int, string>>> _tableTranslate;

        public TableImporter(string translatePath, PersonaEncoding oldEncoding, PersonaEncoding newEncoding)
        {
            handlers = new Dictionary<string, Func<GameFile, PersonaEncoding, PersonaEncoding, bool>>
            {
                { "BATTLE\\TABLE\\NAME.TBL", ToNameTBL },
                { "BATTLE\\TABLE.PAC", ToTablePac },
                { "INIT\\CMPTABLE.BIN", ToCMPTableBIN },
                { "FIELD\\PANEL\\MISSION_LIST\\MISSION_LIST.TBL", ToMissionListTBL },
                { "INIT\\CMM.BIN", ToCMMBIN },
                { "INIT\\FCLTABLE.BIN", ToFCLTableBIN },
                { "INIT\\TTRTABLE.BIN", ToTTRTableBIN },
                { "CALENDAR\\GOODGAUGE.PAC", ToGoodgaugePAC },
                { "FIELD\\PANEL\\LMAP\\FLDLMAPLOCKEDCORPNAME.FTD", ToLMAPlockedCorpNameFTD },
                { "FIELD\\PANEL\\LMAP\\FLDLMAPSTATION.FTD", ToLMAPStationFTD },
                { "FIELD\\PANEL\\FLDWHOLEMAPTABLE.FTD", ToWholeMapTableFTD },
                { "FIELD\\PANEL\\FLDWHOLEMAPTABLEDNG.FTD", ToWholeMapTableDngFTD },
                { "FIELD\\PANEL\\ROADMAP\\ROADMAP.TBL", ToRoadmapTBL },
                { "FIELD\\FTD\\FLDACTIONNAME.FTD", ToFieldFTD },
                { "FIELD\\FTD\\FLDARCANANAME.FTD", ToFieldFTD },
                { "FIELD\\FTD\\FLDASSISTLISTNAME_SOCIAL.FTD", ToFieldFTD },
                { "FIELD\\FTD\\FLDASSISTLISTNAME_TRAIN.FTD", ToFieldFTD },
                { "FIELD\\FTD\\FLDCHECKNAME.FTD", ToFieldFTD },
                { "FIELD\\FTD\\FLDDNGCHECKNAME.FTD", ToFieldFTD },
                { "FIELD\\FTD\\FLDKFECHECKNAME.FTD", ToFieldFTD },
                { "FIELD\\FTD\\FLDNPCNAME.FTD", ToFieldFTD },
                { "FIELD\\FTD\\FLDPARAMUPFUNCNAME.FTD", ToFieldFTD },
                { "FIELD\\FTD\\FLDPLACENAME.FTD", ToFieldFTD },
                { "FIELD\\FTD\\FLDSCRIPTNAME.FTD", ToFieldFTD },
                { "FIELD\\FTD\\FLDSAVEDATAPLACE.FTD", ToFieldFTD2 },
                { "INIT\\MYPTABLE.BIN", ToMyPTableBIN },
                { "MINIGAME\\DARTS\\DARTS_CHARAABILITY.BIN", ToDartsBIN },
                { "MINIGAME\\DARTS\\DARTS_TEXT.BIN", ToDartsBIN }
            };

            _oldEncoding = oldEncoding;
            _newEncoding = newEncoding;

            ReadTableTranslate(translatePath);
        }

        private void ReadTableTranslate(string translatePath)
        {
            var path = Path.Combine(translatePath, TextDir, TableZip);

            var result = new Dictionary<string, Dictionary<string, Dictionary<int, string>>>();

            using (var zip = ZipFile.OpenRead(path))
            {
                foreach (var entry in zip.Entries)
                {
                    var name = Path.GetFileNameWithoutExtension(entry.Name);

                    string[] lines;
                    using (var entryStream = entry.Open())
                        lines = SomeHelpers.ReadAllLines(entryStream);

                    int blockNameInd;
                    int blockInd;
                    int translInd;
                    {
                        var titleSpl = lines[0].Split('\t');
                        blockNameInd = Array.IndexOf(titleSpl, "BLOCK_NAME");
                        blockInd = Array.IndexOf(titleSpl, "ID");
                        translInd = Array.IndexOf(titleSpl, "TRANSL");
                    }

                    if (blockNameInd == -1 || blockInd == -1 || translInd == -1)
                        continue;

                    foreach (var line in lines.Skip(1))
                    {
                        var spl = line.Split('\t');

                        var blockName = spl[blockNameInd];

                        if (string.IsNullOrEmpty(blockName))
                            continue;

                        var blockIndex = int.Parse(spl[blockInd]);
                        var transl = spl[translInd];

                        if (!result.TryGetValue(name, out var blocksDict))
                        {
                            blocksDict = new Dictionary<string, Dictionary<int, string>>();
                            result[name] = blocksDict;
                        }

                        if (!blocksDict.TryGetValue(blockName, out var blockDict))
                        {
                            blockDict = new Dictionary<int, string>();
                            blocksDict[blockName] = blockDict;
                        }

                        blockDict.Add(blockIndex, transl);
                    }
                }
            }

            _tableTranslate = result;
        }

        public bool Import(string relFilePath, GameFile gameFile)
        {
            if (handlers.TryGetValue(relFilePath.ToUpper(), out var func))
            {
                return func(gameFile, _oldEncoding, _newEncoding);
            }

            return false;
        }

        private bool ToNameTBL(GameFile arg, PersonaEncoding oldEnc, PersonaEncoding newEnc)
        {
            if (!TryGetTranslate("NAME_TBL", out var translate))
                return false;

            var tbl = arg.GameData as TBL;
            for (int i = 0; i < tbl.SubFiles.Count; i += 2)
            {
                var trName = "NAME.TBL(" + i.ToString().PadLeft(2, '0') + ")";

                if (!translate.TryGetValue(trName, out var blockTranslate))
                    throw new Exception();

                var posData = tbl.SubFiles[i];
                var data = tbl.SubFiles[i + 1];

                var inputData = new List<byte[]>();

                {
                    var inputPos = new List<int>();
                    using (var ms = new MemoryStream(posData.GameData.GetData()))
                    using (var reader = new BinaryReaderEndian(ms))
                    {
                        while (ms.Position < ms.Length)
                            inputPos.Add(reader.ReadUInt16());
                    }

                    using (var ms = new MemoryStream(data.GameData.GetData()))
                    using (var reader = new BinaryReaderEndian(ms))
                    {
                        for (int k = 0; k < inputPos.Count; k++)
                        {
                            var cur = inputPos[k];
                            var next = k == inputPos.Count - 1 ? ms.Length : inputPos[k + 1];
                            var len = next - cur - 1;

                            ms.Position = cur;
                            inputData.Add(reader.ReadBytes((int)len));
                        }
                    }
                }

                var outputData = new List<byte[]>();

                for (int k = 0; k < inputData.Count; k++)
                {
                    string text;
                    if (blockTranslate.TryGetValue(k, out string newText) && !string.IsNullOrEmpty(newText))
                    {
                        text = newText;
                    }
                    else
                    {
                        text = oldEnc.GetString(inputData[k]);
                    }

                    outputData.Add(newEnc.GetBytes(text));
                }

                List<int> pos = new List<int>();

                int offset = 0;
                foreach (var a in outputData)
                {
                    pos.Add(offset);
                    offset += a.Length + 1;
                }

                using (MemoryStream MS = new MemoryStream())
                using (var writer = new BinaryWriterEndian(MS))
                {
                    foreach (var a in pos)
                    {
                        writer.Write((ushort)a);
                    }
                    tbl.SubFiles[i].GameData = new DAT(MS.ToArray());
                }

                using (MemoryStream MS = new MemoryStream())
                using (var writer = new BinaryWriterEndian(MS))
                {
                    foreach (var a in outputData)
                    {
                        MS.Write(a, 0, a.Length);
                        MS.WriteByte(0);
                    }

                    tbl.SubFiles[i + 1].GameData = new DAT(MS.ToArray());
                }
            }

            return true;
        }

        private bool ToTablePac(GameFile arg, PersonaEncoding oldEnc, PersonaEncoding newEnc)
        {
            var nameTbl = (arg.GameData as BIN).SubFiles.Find(x => x.Name == "NAME.TBL");

            return ToNameTBL(nameTbl, oldEnc, newEnc);
        }

        private bool ToCMPTableBIN(GameFile arg, PersonaEncoding oldEnc, PersonaEncoding newEnc)
        {
            if (!TryGetTranslate("CMPTABLE_BIN", out var translate))
                return false;

            var bin = arg.GameData as BIN;

            var names = new string[]
            {
                "CMPQUESTNAME.CTD",
                "CMPQUESTTARGETNAME.CTD",
                "CMPPERSONAPARAM.CTD",
                "CMPSYSTEMMENU.CTD",
                "CMPSYSTEMHELP.CTD",
                "CMPCONFIGHELP.CTD",
                "CMPDIFFICULTNAME.CTD",
                "CMPCONFIGITEM.CTD",
                "CMPCALNAME.CTD",
                "CMPARBEITNAME.CTD",
                "CHATTITLENAME.CTD",
                "CMPMONEYPANELSTRING.CTD",
                "CMPCONFIGHELPNX.CTD",
                "CMPCONFIGHELPPS5.CTD",
                "CMPCONFIGHELPXBOX.CTD",
                "CMPCONFIGHELPSTEAM.CTD",
                "CMPCONFIGITEMNX.CTD",
                "CMPCONFIGITEMPS5.CTD",
                "CMPCONFIGITEMXBOX.CTD",
                "CMPCONFIGITEMSTEAM.CTD"
            };

            foreach (var file in bin.SubFiles)
            {
                var name = file.Name.ToUpper();

                if (!names.Contains(name))
                    continue;

                if (!translate.TryGetValue(name, out var trans))
                    throw new Exception();

                var ftd = file.GameData as FTD;

                ftd.ImportText_1Entry(oldEnc, newEnc, trans);
            }

            return true;
        }

        private bool ToMissionListTBL(GameFile arg, PersonaEncoding oldEnc, PersonaEncoding newEnc)
        {
            if (!TryGetTranslate("MISSION_LIST_TBL", out var translate))
                return false;

            var bin = arg.GameData as BIN;

            var names = new string[]
            {
                "BTL_MISSION_TITLE.FTD",
                "DNG_MISSION_TITLE.FTD",
                "FLD_MISSION_TITLE.FTD",
                "KFE_MISSION_TITLE.FTD",
                "MAIN_MISSION_TITLE.FTD"
            };

            foreach (var file in bin.SubFiles)
            {
                var name = file.Name.ToUpper();

                if (!names.Contains(name))
                    continue;

                if (!translate.TryGetValue(name, out var trans))
                    throw new Exception();

                var ftd = file.GameData as FTD;

                ftd.ImportText_MultiEntry(oldEnc, newEnc, trans);
            }

            return true;
        }

        private bool ToCMMBIN(GameFile arg, PersonaEncoding oldEnc, PersonaEncoding newEnc)
        {
            if (!TryGetTranslate("CMM_BIN", out var translate))
                return false;

            var bin = arg.GameData as BIN;

            var names = new string[]
            {
                "CMMNAME.CTD",
                "CMMNAME_EXTRA.CTD",
                "CMMARCANASPHELP.CTD",
                "CMMCLUBNAME.CTD",
                "CMMMAILORDER_TEXT.CTD",
                "CMMMEMBERNAME.CTD",
                "CMMPC_PARAM_NAME.CTD",
                "CMMPHANTOMTHIEFNAME.CTD",
                "CMMFIXSTRING.CTD",
                "CMMFUNCTIONNAME.CTD",
                "CMMAREANAME.CTD"
            };

            string hackName1 = "CMMNETREPORTTABLE.CTD";
            string hackName2 = "CMMPC_PARAM_HELP.CTD";

            foreach (var file in bin.SubFiles)
            {
                var name = file.Name.ToUpper();

                if (!names.Contains(name) && name != hackName1 && name != hackName2)
                    continue;

                if (!translate.TryGetValue(name, out var trans))
                    throw new Exception();

                var ftd = file.GameData as FTD;

                if (name == hackName1)
                {
                    ftd.ImportText_1Entry(oldEnc, newEnc, trans, leftPadding: 4);
                }
                else if (name == hackName2)
                {
                    ftd.Import_cmmPC_PARAM_Help(oldEnc, newEnc, trans);
                }
                else
                {
                    ftd.ImportText_1Entry(oldEnc, newEnc, trans);
                }
            }

            return true;
        }

        private bool ToFCLTableBIN(GameFile arg, PersonaEncoding oldEnc, PersonaEncoding newEnc)
        {
            if (!TryGetTranslate("FCLTABLE_BIN", out var translate))
                return false;

            var bin = arg.GameData as BIN;

            string[] names = new[]
            {
                "FCLWEAPONTYPENAME.FTD",
                "FCLGUNTYPENAME.FTD",
                "FCLWEAPONSELLLIST.FTD",
                "FCLWEAPONTOPMENU.FTD",
                "FCLWEAPONHELP.FTD",
                "FCLWEAPONCATEGORYNAME.FTD",
                "FCLSPREADTEXT.FTD",
                "FCLSIMPLEHELP.FTD",
                "FCLCMBCOMTEXT.FTD",
                "FCLSUGGESTTYPENAME.FTD",
                "FCLSEARCHTEXT.FTD",
                "FCLVIEWTYPETEXT.FTD",
                "FCLLOGFORMATTEXT.FTD",
                "FCLLOGCONJUNCTIONTEXT.FTD",
                "PWEATHERBONUS.FTD",
                "FCLINJECTIONNAME.FTD",
                "FCLSETITEMNAME.FTD",
                "FCLPUBLICSHOPNAME.FTD",
                "TEAMNAMEENTRYNGWORD.FTD",
                "FCLCUSTOMPARTSNAME.FTD",
                "FCLMMTSHOPNUMHEROMESSAGE.FTD",
                "FCLMMTSHOPSELMESSAGE.FTD"
            };

            string[] hackNames = new[]
            {
                "FCLHELPTABLE_COMBINE_ROOT.FTD",
                "FCLHELPTABLE_COMBINE_SUB.FTD",
                "FCLHELPTABLE_COMBINE_G.FTD",
                "FCLHELPTABLE_COMBINE_G_HELP.FTD",
                "FCLHELPTABLE_COMBINE_HELP.FTD",
                "FCLHELPTABLE_COMPEND.FTD",
                "FCLHELPTABLE_COMPEND_HELP.FTD",
                "FCLHELPTABLE_COMPEND_HELP_L.FTD",
                "FCLHELPTABLE_CELL.FTD",
                "FCLHELPTABLE_CELL_HELP.FTD"
            };

            foreach (var file in bin.SubFiles)
            {
                var name = file.Name.ToUpper();

                if (!names.Contains(name) && !hackNames.Contains(name))
                    continue;

                if (!translate.TryGetValue(name, out var trans))
                    throw new Exception();

                var ftd = file.GameData as FTD;

                if (hackNames.Contains(name))
                {
                    ftd.ImportText_1Entry(oldEnc, newEnc, trans, leftPadding: 8, rightPadding: 4);
                }
                else
                {
                    ftd.ImportText_1Entry(oldEnc, newEnc, trans);
                }
            }

            return true;
        }

        private bool ToTTRTableBIN(GameFile arg, PersonaEncoding oldEnc, PersonaEncoding newEnc)
        {
            if (!TryGetTranslate("TTRTABLE_BIN", out var translate))
                return false;

            var bin = arg.GameData as BIN;

            string[] names = new[]
            {
                "TTRTITLENAME_BATTLE.TTD",
                "TTRTITLENAME_COMBINE.TTD",
                "TTRTITLENAME_DAILY.TTD",
                "TTRTITLENAME_DUNGEON.TTD",
                "TTRTITLENAME_SYSTEM.TTD",
                "TTRTITLENAME_STORY.TTD",
                "TTRTITLENAME_MYPALACE.TTD"
            };

            foreach (var file in bin.SubFiles)
            {
                var name = file.Name.ToUpper();

                if (!names.Contains(name))
                    continue;

                if (!translate.TryGetValue(name, out var trans))
                    throw new Exception();

                var ftd = file.GameData as FTD;

                ftd.ImportText_1Entry(oldEnc, newEnc, trans);
            }

            return true;
        }

        private bool ToGoodgaugePAC(GameFile arg, PersonaEncoding oldEnc, PersonaEncoding newEnc)
        {
            if (!TryGetTranslate("GOODGAUGE_PAC", out var translate))
                return false;

            var bin = arg.GameData as BIN;

            string[] names = new[]
            {
                "CLDCOMMENTTABLE.FTD",
                "CLDEVTCOMMENTTABLE.FTD"
            };

            foreach (var file in bin.SubFiles)
            {
                var name = file.Name.ToUpper();

                if (!names.Contains(name))
                    continue;

                if (!translate.TryGetValue(name, out var trans))
                    throw new Exception();

                var ftd = file.GameData as FTD;

                ftd.ImportText_MultiEntry(oldEnc, newEnc, trans);
            }

            return true;
        }

        private bool ToLMAPlockedCorpNameFTD(GameFile arg, PersonaEncoding oldEnc, PersonaEncoding newEnc)
        {
            if (!TryGetTranslate("FIELD_PANEL_LMAP", out var translate))
                return false;

            var ftd = arg.GameData as FTD;

            var name = arg.Name.ToUpper();

            if (!translate.TryGetValue(name, out var trans))
                throw new Exception();

            ftd.ImportText_1Entry(oldEnc, newEnc, trans);

            return true;
        }

        private bool ToLMAPStationFTD(GameFile arg, PersonaEncoding oldEnc, PersonaEncoding newEnc)
        {
            if (!TryGetTranslate("FIELD_PANEL_LMAP", out var translate))
                return false;

            var ftd = arg.GameData as FTD;

            var name = arg.Name.ToUpper();

            if (!translate.TryGetValue(name, out var trans))
                throw new Exception();

            ftd.ImportText_fldLMapStation(oldEnc, newEnc, trans);

            return true;
        }

        private bool ToWholeMapTableFTD(GameFile arg, PersonaEncoding oldEnc, PersonaEncoding newEnc)
        {
            if (!TryGetTranslate("FLDWHOLEMAPTABLE_FTD", out var translate))
                return false;

            var ftd = arg.GameData as FTD;

            var name = arg.Name.ToUpper();

            if (!translate.TryGetValue(name, out var trans))
                throw new Exception();

            ftd.ImportText_fldPanelMsg(oldEnc, newEnc, trans);

            return true;
        }

        private bool ToWholeMapTableDngFTD(GameFile arg, PersonaEncoding oldEnc, PersonaEncoding newEnc)
        {
            if (!TryGetTranslate("FLDWHOLEMAPTABLEDNG_FTD", out var translate))
                return false;

            var ftd = arg.GameData as FTD;

            var name = arg.Name.ToUpper();

            if (!translate.TryGetValue(name, out var trans))
                throw new Exception();

            ftd.ImportText_fldPanelMsg(oldEnc, newEnc, trans);

            return true;
        }

        private bool ToRoadmapTBL(GameFile arg, PersonaEncoding oldEnc, PersonaEncoding newEnc)
        {
            if (!TryGetTranslate("ROADMAP_TBL", out var translate))
                return false;

            var bin = arg.GameData as BIN;

            string[] names = new[]
            {
                "FLD_TEXPACK_TITLE.FTD"
            };

            foreach (var file in bin.SubFiles)
            {
                var name = file.Name.ToUpper();

                if (!names.Contains(name))
                    continue;

                if (!translate.TryGetValue(name, out var trans))
                    throw new Exception();

                var ftd = file.GameData as FTD;

                ftd.ImportText_MultiEntry(oldEnc, newEnc, trans);
            }

            return true;
        }

        private bool ToFieldFTD(GameFile arg, PersonaEncoding oldEnc, PersonaEncoding newEnc)
        {
            if (!TryGetTranslate("FIELD_FTD", out var translate))
                return false;

            var ftd = arg.GameData as FTD;

            var name = arg.Name.ToUpper();

            if (!translate.TryGetValue(name, out var trans))
                throw new Exception();

            ftd.ImportText_MultiEntry(oldEnc, newEnc, trans);

            return true;
        }

        private bool ToFieldFTD2(GameFile arg, PersonaEncoding oldEnc, PersonaEncoding newEnc)
        {
            if (!TryGetTranslate("FIELD_FTD", out var translate))
                return false;

            var ftd = arg.GameData as FTD;

            var name = arg.Name.ToUpper();

            if (!translate.TryGetValue(name, out var trans))
                throw new Exception();

            ftd.ImportText_1EntryUTF8(oldEnc, newEnc, trans, leftPadding: 4);

            return true;
        }

        private bool ToMyPTableBIN(GameFile arg, PersonaEncoding oldEnc, PersonaEncoding newEnc)
        {
            if (!TryGetTranslate("MYPTABLE_BIN", out var translate))
                translate = new Dictionary<string, Dictionary<int, string>>();

            var bin = arg.GameData as BIN;

            var names = new[]
            {
                "MYPVIDEONAMETABLE.MTD",
                "MYPSOUNDNAMETABLE.MTD",
                "MYPIMAGENAMETABLE.MTD",
                "MYPITEMNAMETABLE.MTD",
                "MYPAWARDNAMETABLE.MTD",
                "MYPORNAMENTNAMETABLE.MTD"
            };

            foreach (var file in bin.SubFiles)
            {
                var name = file.Name.ToUpper();

                if (!names.Contains(name))
                    continue;

                if (!translate.TryGetValue(name, out var trans))
                    trans = new Dictionary<int, string>();

                var data = file.GameData as DAT;
                var mtd = new MTD(data.Data);

                for (int i = 0; i < mtd.Entities.Count; i++)
                {
                    var entity = mtd.Entities[i];
                    if (!trans.TryGetValue(i, out var newText) || string.IsNullOrEmpty(newText))
                        newText = oldEnc.GetString(entity).TrimEnd('\0');

                    Array.Clear(entity, 0, entity.Length);
                    var newData = SomeHelpers.TrimLength(newText, newEnc, entity.Length - 1);
                    Array.Copy(newData, entity, newData.Length);
                }

                file.GameData = new DAT(mtd.GetData());
            }

            return true;
        }

        private bool ToDartsBIN(GameFile arg, PersonaEncoding oldEnc, PersonaEncoding newEnc)
        {
            const int DartsBlockSize = 0x80;

            if (!TryGetTranslate("DARTS", out var translation))
                translation = new Dictionary<string, Dictionary<int, string>>();

            var fileName = arg.Name.ToUpper();

            if (!translation.TryGetValue(fileName, out var fileTranslation))
                fileTranslation = new Dictionary<int, string>();

            var dat = arg.GameData as DAT;
            var buffer = new byte[DartsBlockSize];
            var index = 0;
            using (var memoryStream = new MemoryStream(dat.Data))
            {
                while (true)
                {
                    var readed = memoryStream.Read(buffer, 0, DartsBlockSize);

                    if (readed == 0)
                        break;

                    if (readed != DartsBlockSize)
                        throw new Exception("Неожиданный результат для DARTS");

                    string transl;
                    if (!fileTranslation.TryGetValue(index, out transl) || string.IsNullOrEmpty(transl))
                        transl = oldEnc.GetString(buffer).TrimEnd('\0');

                    index++;

                    Array.Fill<byte>(buffer, 0);
                    var newData = SomeHelpers.TrimLength(transl, newEnc, DartsBlockSize - 1);
                    Array.Copy(newData, buffer, newData.Length);

                    memoryStream.Seek(-DartsBlockSize, SeekOrigin.Current);
                    memoryStream.Write(buffer, 0, buffer.Length);
                }
            }

            return true;
        }

        private bool TryGetTranslate(string name, out Dictionary<string, Dictionary<int, string>> translate)
        {
            if (!_tableTranslate.TryGetValue(name, out translate))
            {
                translate = null;
                return false;
            }

            return true;
        }
    }
}