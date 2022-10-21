﻿using a2lextract;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

// ETA
long lastETAUpdate = 0;
double instancesCount = 0;
double lastProgressCounter = 0.0f;
double progressCounter = 0.0f;
string fileContent = "";


Dictionary<string, ComputeMethod> compu_methods = new Dictionary<string, ComputeMethod>();

Console.BackgroundColor = ConsoleColor.DarkGreen;
Console.Write("[a2lextract] : Extract all RAM variables infos using an A2L file");
Console.BackgroundColor = ConsoleColor.Blue;
Console.WriteLine("(The07K.wiki)" + Environment.NewLine);
Console.ResetColor();

if (Environment.GetCommandLineArgs().Length > 1) {
    string fileName = Environment.GetCommandLineArgs()[1];
    string filePath = Directory.GetCurrentDirectory() + "\\" + fileName;

    using (StreamReader sr = new StreamReader(filePath, Encoding.GetEncoding("iso-8859-1"))) {
        fileContent = sr.ReadToEnd();
    }


    /*
     * Finding COMPU_METHODS *
     */

    Console.BackgroundColor = ConsoleColor.Blue;
    Console.WriteLine("Finding COMPU_METHOD...");
    Console.ResetColor();

    using (ProgressBar progress = new ProgressBar()) {
        MatchCollection mC = Regex.Matches(fileContent, $"(?s)/begin COMPU_METHOD(.+?)/end COMPU_METHOD");
        instancesCount = Convert.ToDouble(mC.Count);

        foreach (Match c in mC) {
            LinkedList<string> compu_content = new LinkedList<string>(); // Important to keep order

            foreach (string content in c.Groups[1].Value.Split(Environment.NewLine)) {
                string trimmed_str = content.Trim();
                if (trimmed_str != "") compu_content.AddLast(trimmed_str);
            }

            for (int i = 5; i < compu_content.Count; i++) {
                if (compu_content.ElementAt(i).ToUpper().Contains("COEFFS")) {
                    string compu_id = compu_content.ElementAt(0);
                    string compu_unit = compu_content.ElementAt(4).Replace("\"", "");
                    string[] compu_coeffs;
                    compu_coeffs = compu_content.ElementAt(i).Split(" ");
                    compu_methods.Add(compu_id, new ComputeMethod(compu_unit, compu_coeffs));

                    break;
                }
            }

            progress.UpdateETA(CalculateETA());
            progress.Report(++progressCounter / instancesCount);
        }
    }

    ResetProgressBarValues();

    string fileOutput = @";
; Sample ECU characteristics for logging with R32Logger
; 
; Generated by A2LExtract (The07K.wiki)

; Everything but the  [Measurements] section is ignored in R32Logger. 
; Keep exactly the same format as in this file - no extra spaces, blank,commas, lines etc.
;
; You can hand-edit this file to add new measurement variable definitions
; or to change existing definitions, e.g. to change a conversion formula.
;
[Measurements]
; Conversion factors:
;   S -> 0 = unsigned, 1 = signed value
;   I -> 0 = normal, 1 = inverse conversion
;   A -> factor
;   B -> offset
; Normal conversion:  phys = A * internal - B
; Inverse conversion: phys = A / (internal - B)
; 
;Name                       , {Alias}                , Address,Size, Bitmask, {Unit}        , S, I,         A,            B, Comment
;
;*****************************************GENERAL ***************************************************************************************
;" + Environment.NewLine;

    Console.BackgroundColor = ConsoleColor.Blue;
    Console.WriteLine("Extracting measurements datas...");
    Console.ResetColor();

    using (ProgressBar progress = new ProgressBar()) {
        MatchCollection mC = Regex.Matches(fileContent, $"(?s)/begin MEASUREMENT(.+?)/end MEASUREMENT");
        instancesCount = Convert.ToDouble(mC.Count);

        foreach (Match c in mC) {
            LinkedList<string> measurement_content = new LinkedList<string>(); // Important to keep order

            foreach (string content in c.Groups[1].Value.Split(Environment.NewLine)) {
                string trimmed_str = content.Trim();
                if (trimmed_str != "") measurement_content.AddLast(trimmed_str);
            }

            for (int i = 5; i < measurement_content.Count; i++) {
                if (measurement_content.ElementAt(i).ToUpper().Contains("ECU_ADDRESS")) {
                    string name = measurement_content.ElementAt(0);
                    string aliasComment = measurement_content.ElementAt(1).Replace("\"", "").Replace("'", "''");
                    string bytes = getByteSize(measurement_content.ElementAt(2));
                    string signed = measurement_content.ElementAt(2).Substring(0, 1).ToUpper() == "U" ? "0" : "1";

                    int address = int.Parse(measurement_content.ElementAt(i).Split(" ")[1].Substring(2), NumberStyles.HexNumber);
                    int bitmask = 0;

                    for (int e = 5; e < measurement_content.Count; e++) {
                        if (measurement_content.ElementAt(e).ToUpper().Contains("BIT_MASK")) {
                            bitmask = int.Parse(measurement_content.ElementAt(e).Split(" ")[1].Substring(2), NumberStyles.HexNumber);
                            break;
                        }
                    }

                    string compute_name = measurement_content.ElementAt(3);

                    ComputeMethod cm = compu_methods.GetValueOrDefault(compute_name);
                    if (cm != null) {
                        fileOutput += $"{name,-28}, {{}}                     , 0x{address:X6},  {bytes},  0x{bitmask:X4}, {"{" + cm.unit + "}", -14}, {signed}, 0,         {cm.factor + ",", -8}      {cm.offset}, {{{aliasComment}}}" + Environment.NewLine;
                    } else {
                        fileOutput += $"{name,-28}, {{}}                     , 0x{address:X6},  {bytes},  0x{bitmask:X4}, {"{}",-14}, {signed}, 0,         {"1,",-8}      {"0"}, {{{aliasComment}}}" + Environment.NewLine;
                    }

                    break;
                }
            }

            progress.UpdateETA(CalculateETA());
            progress.Report(++progressCounter / instancesCount);
        }
    }


    /*
     * Generating .ecu files
     */

    string fileNameWithoutExtension = fileName.Split(".")[0];
    string saveName = $"{fileNameWithoutExtension}.ecu";
    string savePath = Directory.GetCurrentDirectory() + "\\" + saveName;

    try {
        using (StreamWriter sw = new StreamWriter(savePath, false)) {
            sw.WriteLine(fileOutput);
        }
        Console.BackgroundColor = ConsoleColor.DarkGreen;
        Console.WriteLine("Extraction complete ! File has been saved under the name:");
        Console.BackgroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine(saveName);
        Console.ResetColor();
    } catch (Exception) {
        Console.BackgroundColor = ConsoleColor.DarkRed;
        Console.WriteLine("Unexpected error : couldn't write the extracted file");
        Console.ResetColor();
    }
}

void ResetProgressBarValues() {
    progressCounter = 0.0f;
    lastProgressCounter = 0.0f;
    instancesCount = 0;
    lastETAUpdate = 0;
}

string getByteSize(string dataType) {
    int bytes;

    switch (dataType.ToUpper()) {
        case "UBYTE":
        case "SBYTE":
            bytes = 1;
            break;
        case "UWORD":
        case "SWORD":
            bytes = 2;
            break;
        case "ULONG":
        case "SLONG":
        case "FLOAT32_IEEE":
            bytes = 4;
            break;
        default:
            bytes = 0;
            break;
    }

    return bytes.ToString();
}

long CalculateETA() {
    int etaMS = 10;
    double timeEllapsed = DateTimeOffset.Now.ToUnixTimeMilliseconds() - lastETAUpdate;
    if (timeEllapsed > 1000) {
        if (lastETAUpdate != 0) { // Updating ETA instead of using predefined one
            double wordsTranslated = progressCounter - lastProgressCounter;
            etaMS = (int)(wordsTranslated / timeEllapsed);
        }

        lastETAUpdate = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        lastProgressCounter = progressCounter;
    }

    return etaMS * (long)(instancesCount - progressCounter);
}
