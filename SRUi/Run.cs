/*
Copyright (c) 2020 Richard King

stressRefine is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

stressRefine is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

The terms of the GNU General Public License are explained in the file COPYING.txt,
also available at <https://www.gnu.org/licenses/>
 
stressRefine makes uses of the free software program CalculiX cgx http://www.dhondt.de/
which is also subject to the terms of the GNU General Public License
*/


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Threading;

namespace SRUi
{
    public partial class Form1 : Form
    {
        public bool cancelClicked = false;
        string srcmd = null;
        bool Solve()
        {
            try
            {

                cancelClicked = false;

                if (breakoutRun)
                {
                    if (!fillSRcmd(false))
                        return false;
                    string settingLine = null;
                    settingLine = "savebreakout";
                    if (breakoutAtNode)
                    {
                        settingLine += " node ";
                        settingLine += breakoutNode;
                    }
                    //savebreakout run:
                    if (!fillSRsettingsFile(settingLine))
                        return false;
                    if (!doSolution(false))
                    {
                        writeToLog("breakout solution failed");
                        return false;
                    }
                    //check for abnormal termination:
                    if (abnormalSolve)
                    {
                        return false;
                    }
                }
                else
                {
                    if (!fillSRsettingsFile("//full model run"))
                        return false;
                }
                //p-adaptive solution run of full model or breakout model:
                if (!fillSRcmd(true))
                    return false;
                if (!doSolution(true))
                    return false;
                string filename = SRFolder + "report.txt";
                if (abnormalSolve || !File.Exists(filename))
                {
                    writeToLog(" dorun doSolution(true)");
                    return false;
                }
            }
            catch
            {
                fatalMsg("solve");
                return false;
            }
            return true;
        }

        private bool doSolution(bool ploopRun)
        {
            try
            {
                writeToLog("doSolution " + ploopRun);
                if (ploopRun)
                    CreateModelessMsgDlg("Adaptive Analysis...");
                else
                    CreateModelessMsgDlg("Preparing Breakout Model...");

                string engineLoc = installDir + "SRwithMkl.exe";

                Process SRexe = new Process();
                SRexe.StartInfo.FileName = engineLoc;
                writeToLog("Srexe: " + engineLoc);
                SRexe.StartInfo.UseShellExecute = true;
                SRexe.StartInfo.CreateNoWindow = false;
                SRexe.StartInfo.Arguments = workingDir;
                mainTimer.Start();
                modelessMsgDlg.Show();
                SRexe.Start();
                SRexe.WaitForExit();
                userDelay();
                modelessMsgDlg.Close();

                appendSRlogtoUILog();
                if (!fillResultsText(ploopRun))
                {
                    abnormalSolve = true;
                    string logName = workingDir + "srui.log";
                    ShowMessage("adaptive analysis failed. please see log file for details: " + newline + logName);
                    return false;
                }

                if (ploopRun)
                    moveF06ResultsFile();
            }
            catch
            {
                fatalMsg(": doSolution");
                return false;
            }
            return true;
        }

        private bool fillSRcmd(bool ploopRun)
        {
            try
            {
                writeToLog("fillSRcmd ");
                if (breakoutRun && ploopRun)
                    SRtail += "_breakout";
                SRFolder = SRDefFolder + SRtail + "\\";
                //write model file name to ModelFileName.txt
                srcmd = workingDir + "ModelFileName.txt";
                writeToLog("srcmd " + srcmd);
                using (StreamWriter tmp = new StreamWriter(srcmd))
                {
                    int nn = SRFolder.LastIndexOf('\\');
                    string s = SRFolder.Substring(0, nn);
                    tmp.WriteLine(s);
                    tmp.Close();
                }
            }
            catch
            {
                fatalMsg(": fillSRcmd");
                return false;
            }
            return true;
        }

        private bool fillSRsettingsFile(string settingLine)
        {
            try
            {
                writeToLog("fillSRsettingsFile " + settingLine);

                string filename = SRFolder + "\\report.txt";
                if (File.Exists(filename))
                    File.Delete(filename);

                //write setting for engine run
                //to settings file (.srs):
                filename = SRFolder + SRtail + ".srs";
                if (File.Exists(filename))
                    File.Delete(filename);
                using (StreamWriter tmp = new StreamWriter(filename))
                {
                    tmp.WriteLine(settingLine);
                    if (!f06results)
                        tmp.WriteLine("ReadDispStressSRR");
                    else
                        tmp.WriteLine("ReadF06Results " + f06File);
                    if (partialDisplacements)
                        tmp.WriteLine("partialDisplacements");
                    if (!useStressUnits)
                        tmp.WriteLine("NOUNITS");
                    else
                    {
                        if (stressUnits != 0)
                        {
                            tmp.WriteLine("stress_conversion " + SRoutStressConversion + " " + stressUnitString);
                            tmp.WriteLine("length_conversion " + lengthConvert + " " + lengthUnitStr);
                        }
                    }
                    if (econSolve)
                        tmp.WriteLine("econSolve");
                    if (ignoreSacrificial)
                        tmp.WriteLine("\nNOSACRIFICIAL");
                    if (minP != 2 || maxP != 8)
                        tmp.WriteLine("\nPLIMITS " + minP + " " + maxP);
                    if (maxIts != 3)
                        tmp.WriteLine("\nMAXITERATIONS " + maxIts);
                    //sanity check for errTol:
                    if (errTol < 1.0)
                        errTol = 1.0;
                    if (errTol > 25.0)
                        errTol = 25.0;
                    if (Math.Abs(errTol - 5.0) > 1.0e-8)
                        tmp.WriteLine("\nERRORTOL " + errTol);
                    if (useSoftSprings)
                        tmp.WriteLine("needSoftSprings");
                    if (maxPJump < 10)
                        tmp.WriteLine("maxPJump " + maxPJump);
                    if (uniformP)
                        tmp.WriteLine("uniformP");
                    if(outputF06)
                        tmp.WriteLine("outputF06");
                }
            }
            catch
            {
                fatalMsg(": fillSRsettingsFile");
                return false;
            }
            return true;
        }

        void appendSRlogtoUILog()
        {

            try
            {
                writeToLog("=================================================");
                writeToLog("From engine:");
                string filename = workingDir + "engine.log";
                if (!File.Exists(filename))
                    return;
                StreamReader inStream = new StreamReader(filename, false);

                int num = 0;
                while (true)
                {
                    if (inStream.EndOfStream)
                        break;
                    num++;
                    if (num > 10000)
                        break; //infinite loop prevention
                    string line = inStream.ReadLine();
                    writeToLog(line + newline);
                }
                inStream.Close();
                writeToLog("=================================================");
            }
            catch
            {
                catchWarningMsg("appendSRlogtoUILog");
            }
        }

        bool DelSRFolder()
        {
            try
            {
                if (Directory.Exists(SRFolder))
                {
                    string[] files = Directory.GetFiles(SRFolder, "*.*", SearchOption.TopDirectoryOnly);
                    for (int i = 0; i < files.Length; i++)
                        File.Delete(files[i]);
                }
            }
            catch
            {
                return false;
            }
            return true;
        }

        void clearResultStrings()
        {
            try
            {
                if (resultStrings != null)
                    resultStrings.Clear();
            }
            catch
            {
                catchWarningMsg("clearResultStrings");
            }
        }

        void moveF06ResultsFile()
        {
            try
            {
                string tmpf06File = SRFolder + SRtail + "_SR.f06";
                if (!File.Exists(tmpf06File))
                    return;
                string f06File = feaFolder + "\\" + FEtail + "_SR.f06";
                if (File.Exists(f06File))
                    File.Delete(f06File);
                File.Move(tmpf06File, f06File);
            }
            catch
            {
                return;
            }
        }
    }
}