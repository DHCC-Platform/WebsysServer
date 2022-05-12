﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Windows.Forms;
using System.Reflection;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace WebsysServer.tool
{
    class ScriptShell
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern IntPtr GetForegroundWindow();

        public static string RunAdmin(string cmd)
        {
            Process process = new Process()
            {
                StartInfo =
                {
                    FileName = "cmd.exe",
                    Arguments="/c C:\\Windows\\System32\\cmd.exe",
                    RedirectStandardInput = true,
                    RedirectStandardOutput =true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = false,//不显示程序窗口
                    Verb = "runas"
                }
            };
            process.Start();
            //向cmd窗口发送输入信息           
            cmd = string.IsNullOrEmpty(cmd) ? "exit" : $"{cmd}&exit";
            process.StandardInput.WriteLine(cmd);
            process.WaitForExit();
            process.StandardInput.AutoFlush = true;
            return process.StandardOutput.ReadToEnd();
        }
        /// <summary>
        /// 执行cmd命令
        /// </summary>
        /// <param name="cmd">命令</param>
        /// <returns></returns>
        public static string Run(string cmd)
        {
            return Run(cmd, false);
        }
        /// <summary>
        /// 使用操作系统当前用户运行code
        /// 会把code写到MyCode.txt中,然后调用WebsysScript.exe
        /// </summary>
        /// <param name="mycode">jscripts或vbs代码</param>
        /// <param name="lang">语言类型:vbscript | jscript</param>
        /// <returns>""</returns>
        public static string CurrentUserRun(string mycode,String lang)
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            if (principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                //MessageBox.Show("shell="+Application.StartupPath);
                string curpath = Path.Combine(Application.StartupPath, @"temp");
                string oldCodePath = Path.Combine(curpath, "MyCode.txt");
                if (File.Exists(oldCodePath))
                {
                    File.Delete(oldCodePath);
                }
                string path = Path.Combine(curpath, "MyCode"+ DateTime.Now.ToFileTimeUtc().ToString()+".txt");
                using (StreamWriter sw = File.CreateText(path))
                {
                    sw.WriteLine("/*"+lang+"*/");
                    sw.Write(mycode);
                    sw.Close();
                }
                //var file = new FileInfo(Assembly.GetExecutingAssembly().Location);
                //var exe = Path.Combine(file.DirectoryName, file.Name.Replace(file.Extension, "") + ".exe");
                // 检测到当前进程是以管理员权限运行的，于是降权启动自己之后，把自己关掉。
                //Process.Start("explorer.exe", "cmd.exe");
                Process p = new Process()
                {
                    StartInfo = {
                        FileName = "explorer.exe",
                        //D:\workspace_net\WebsysServerSetup\WebsysScript\bin\x86\Debug\
                        // explorer.exe调用 exe 无法为exe传递参数
                        Arguments= Path.Combine(Application.StartupPath, @"WebsysScript.exe") ,  //"C:\\Windows\\system32\\cmd.exe", //@"D:\workspace_net\WebsysServerSetup\WebsysScript\bin\Debug\WebsysScript.exe", // "C:\\Windows\\system32\\cmd.exe", //@"D:\workspace_net\WebsysServerSetup\WebsysScript\bin\Debug\WebsysScript.exe " + lang+" \""+mycode+"\"",
                        UseShellExecute = false,    //是否使用操作系统shell启动
                        RedirectStandardInput = true,//接受来自调用程序的输入信息
                        RedirectStandardOutput = true,//由调用程序获取输出信息
                        RedirectStandardError = true,//重定向标准错误输出
                        CreateNoWindow = true,//不显示程序窗口
                    }
                };
                p.Start();
                //, @"D:\workspace_net\WebsysServerSetup\WebsysScript\bin\Debug\WebsysScript.exe " + lang+" \""+mycode+"\""); // Assembly.GetEntryAssembly().Location);
                //p.StandardInput.WriteLine(cmd);
                p.StandardInput.AutoFlush = true;
                //p.WaitForExit(); // 等待的是explorer.exe结束，而不是WebsysScript.exe的运行结束
                
                    /*Process[] processes = null;
                    processes = System.Diagnostics.Process.GetProcessesByName("cmd");
                    Process process = default(Process);
                    foreach (Process tempLoopVar_process in processes)
                    {
                        process = tempLoopVar_process;
                        process.StandardInput.Write("ipconfig");
                        string cmdRtn = process.StandardOutput.ReadToEnd();
                        return cmdRtn;
                    }*/
                return p.StandardOutput.ReadToEnd();
                //Environment.Exit(0);
            }
            return "";
        }
        public static string CurrentUserEvalJs(string str, string lang)
        {
            return CurrentUserRun(str,lang);

        }
        public static string Run(string cmd,Boolean isNotReturn)
        {
            Process p = new Process()
            {
                StartInfo = {
                    FileName = "cmd.exe",
                    UseShellExecute = false,    //是否使用操作系统shell启动
                    RedirectStandardInput = true,//接受来自调用程序的输入信息
                    RedirectStandardOutput = true,//由调用程序获取输出信息
                    RedirectStandardError = true,//重定向标准错误输出
                    CreateNoWindow = true,//不显示程序窗口
                }
            };
            p.Start();//启动程序
            //向cmd窗口发送输入信息           
            cmd = string.IsNullOrEmpty(cmd) ? "exit" : $"if 1 equ 1 ({cmd}) & exit";
            p.StandardInput.WriteLine(cmd);
            p.StandardInput.AutoFlush = true;
            //p.StandardInput.WriteLine("exit");
            //向标准输入写入要执行的命令。这里使用&是批处理命令的符号，表示前面一个命令不管是否执行成功都执行后面(exit)命令，如果不执行exit命令，后面调用ReadToEnd()方法会假死
            //同类的符号还有&&和||前者表示必须前一个命令执行成功才会执行后面的命令，后者表示必须前一个命令执行失败才会执行后面的命令
            if (isNotReturn) {
                //不读output流，可以尽早结束线程。而else会卡住进程
                return null;
            }
            else
            {
                //获取cmd窗口的输出信息
                string cmdRtn = p.StandardOutput.ReadToEnd();
                string[] arr = cmdRtn.Split('\n');
                StringBuilder rtn = new StringBuilder(); Boolean startResult = false;
                for (var i = 0; i < arr.Length - 1; i++)
                {
                    if (startResult) rtn.Append(arr[i] + "\n");
                    if (arr[i].Contains("exit")) startResult = true;
                }
                return rtn.ToString();
            }
        }
        // 对应前台的EvalJs, 主要用于导出/打印Excel
        public static string EvalJs(string str,string language) //, string allowUI = "false", string language = "JScript")
        {
                  
            var a = 1;
            string rtn = "";
            MSScriptControl.ScriptControlClass s = new MSScriptControl.ScriptControlClass();
            s.AllowUI = true;
            s.Timeout =  Properties.Settings.Default.EvalJsTimeout * 60 * 1000; // 导出Excel 1000行*20列的数据 JS花费1分钟左右，VB下2秒左右
            s.UseSafeSubset = false;  // false表示低安全运行。设置为true时报： "ActiveX 部件不能创建对象: 'Excel.Application'"
            IntPtr hWndint = GetForegroundWindow();
            s.SitehWnd = hWndint.ToInt32();   //指定弹出窗口在当前弹出，默认为桌面
            if ("vbscript".Equals(language.ToLower()))
            {
                s.Language = "VBScript";
                if (0 != str.ToLower().IndexOf("function")) str = "Function vbs_Test\n" + str + "vbs_Test = 1\n End Function \n";  //默认返回空值，如果业务上有返回也不影响
                s.Reset();
                s.AddCode(str);
                rtn = s.Run("vbs_Test").ToString();
            }
            else
            { 
                if (0!=str.IndexOf( "(function" )) str = "(function test(){" + str + "return 1;})();";  //默认返回空值，如果业务上有返回也不影响
                s.Language = "JScript";
                s.Reset();
                rtn = s.Eval(str).ToString();
            }
            s = null;
            return rtn;

            Type obj = Type.GetTypeFromProgID("ScriptControl");
            if (obj == null) return null;
            object ScriptControl = Activator.CreateInstance(obj);
            //obj.InvokeMember("AllowUI", BindingFlags.SetProperty, null, ScriptControl, new object[] { allowUI });
            obj.InvokeMember("Language", BindingFlags.SetProperty, null, ScriptControl, new object[] { language }); //JScript
            rtn = obj.InvokeMember("Eval", BindingFlags.InvokeMethod, null, ScriptControl, new object[] { str }).ToString();
            obj = null;
            ScriptControl = null;
            return rtn;
        }
        public static string GetConfig()
        {
            CGI cgi = new CGI();
            string rtn = "{\"IP\":\"" + cgi.CurIPAddress + "\",\"Mac\":\"" + cgi.CurMacAddress + "\",\"HostName\":\"" + cgi.HostName + "\",\"IPS\":[";
            for(var i=0; i< cgi.IPList.Count();i++){
                if (i==0) rtn +="\"" +cgi.IPList[i]+"\""; //<string>[i];
                else rtn +=",\""+ cgi.IPList[i]+"\""; //<string>[i];
            }
            return rtn + "]}";
        }
        public static string Regsvr32(string fileName)
        {

            Process p = new Process();
            p.StartInfo.FileName = "regsvr32.exe";
            p.StartInfo.Arguments = "/s \"" + fileName + "\"";
            p.Start();
            return "1";
        }
        public static string Msiexec(string fileName)
        {
            Process p = new Process();
            p.StartInfo.FileName = "msiexec.exe"; // "/i \"" + regFile + "\" /qb";
            p.StartInfo.Arguments = "/i \"" + fileName + "\" /qb";
            p.Start();
            return "1";
        }
        //解压CAB
        public static string Expand(string srcCabFile, string dir)
        {
            Process p = new Process();
            p.StartInfo.FileName = "EXPAND.exe";
            p.StartInfo.Arguments = "-F:*.* \"" + srcCabFile + "\" \"" + dir + "\"";
            p.Start();
            return "1";
        }
        //安装inf
        public static string InfDefaultInstall(string infFile)
        {
            Process p = new Process();
            p.StartInfo.FileName = "InfDefaultInstall.exe";
            p.StartInfo.Arguments = "\"" + infFile + "\"";
            p.Start();
            return "1";
        }
        /**
         * 注册inf内指定要注册的DLL
         */
        public static string RegInfDll(string infFile)
        {
            String LocalDllPath = infFile.Substring(0,infFile.LastIndexOf("\\"));
            String[] infInfo = File.ReadAllLines(infFile);
            Boolean regFlag = false;
            for (int i = 0; i < infInfo.Length; i++)
            {
                if (regFlag)
                {
                    if (infInfo[i].StartsWith("%")) {

                        ScriptShell.Regsvr32(LocalDllPath + infInfo[i].Substring(infInfo[i].LastIndexOf("\\")));   // 注册CAB包中%11%\trakWebEdit3.dll ，暂时只考虑单目录
                    }else{
                        ScriptShell.Regsvr32(LocalDllPath + infInfo[i]);   //注册chrome.inf
                    }
                    //ScriptShell.Run("regsvr32.exe /s \"" + LocalDllPath +infInfo[i] + "\"");
                }
                if (infInfo[i].StartsWith("["))
                {
                    if (infInfo[i].ToLower().Equals("[registerfiles]"))
                    {
                        regFlag = true;
                    }
                    else
                    {//下一类型
                        regFlag = false;
                    }
                }
            }
            return "1";

        }
        /*处理 http://localhost:11996/websys/cmd/cmd 请求 */
        public string CmdRun(string query)
        {
            string lastMthRtn = "";
            String[] dataArry = query.Split('&');
            for (int i = 0; i < dataArry.Length; i++)
            {
                string dataParm = dataArry[i];
                if (!String.IsNullOrEmpty(dataParm))
                {
                    int dIndex = dataParm.IndexOf("=");
                    String key = dataParm.Substring(0, dIndex);
                    String value = dataParm.Substring(dIndex + 1, dataParm.Length - dIndex - 1);
                    String deValue = HTTPRequestHandler.decodeURIComponent(value, Encoding.GetEncoding("utf-8"));
                    if (key.StartsWith("_dllDir")) continue;
                    if (key.StartsWith("_version")) continue;
                    if (key.ToUpper().StartsWith("M_RUN"))
                    {

                        if (deValue.Contains(HTTPRequestHandler.PCOUNT))
                        {
                            string[] Arg = HTTPRequestHandler.ParseMthArg(deValue);
                            if (Arg.Length > 0)
                            {
                                lastMthRtn = ScriptShell.Run(Arg[0]);
                            }
                        }
                    }
                    else if (key.ToUpper().StartsWith("M_EVALJS"))
                    {
                        if (deValue.Contains(HTTPRequestHandler.PCOUNT))
                        {
                            string[] Arg = HTTPRequestHandler.ParseMthArg(deValue);
                            if (Arg.Length > 0)
                            {
                                string lang = "JScript";
                                if (Arg.Length > 1)
                                {
                                    lang = Arg[1];
                                }
                                lastMthRtn = ScriptShell.EvalJs(Arg[0], lang);
                            }
                        }
                    }
                    else if(key.ToUpper().StartsWith("M_CURRENTUSEREVALJS"))
                    {
                        if (deValue.Contains(HTTPRequestHandler.PCOUNT))
                        {
                            string[] Arg = HTTPRequestHandler.ParseMthArg(deValue);
                            if (Arg.Length > 0)
                            {
                                string lang = "JScript";
                                if (Arg.Length > 1)
                                {
                                    lang = Arg[1];
                                }
                                lastMthRtn = ScriptShell.CurrentUserEvalJs(Arg[0], lang);
                            }
                        }
                    }
                    else if (key.ToUpper().StartsWith("M_GETCONFIG"))
                    {
                        lastMthRtn = ScriptShell.GetConfig();
                    }
                }
            }
            return lastMthRtn;
        }
    }
}
