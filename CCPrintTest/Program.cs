using System;
using System.IO;
using System.Globalization;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using CCPrint.UnitTest.Models;

// See https://aka.ms/new-console-template for more information
var config = new ConfigurationBuilder()
  .SetBasePath(Directory.GetCurrentDirectory())
  .AddJsonFile("appsetting.json");

string info = "important info to log";

var cln = new CCPrint.Print(config.Build(), Directory.GetCurrentDirectory());
cln.Message(new StackFrame(), System.Reflection.Assembly.GetExecutingAssembly(), CCPrint.LogTimeType.Local, CCPrint.LogPrintType.Information, $"message local cluster {info}");
//cln.Flush("Stage1");

Test.ToWork(ref cln);
SubTest.ToWork(ref cln);

  /*
 System.Exception: Unable to find the cs file from which you are logging: 
 Path /Users/luisalvarez/Documents/projects/proclogger/ProcLoggerAPI.EFCore 
 and Classname looking for AppEFCoreDbContextFactor: Attempted to find file -
   at CCPrint.Print.PathOfFile(String RawPath, String classname)
  */

//Console.WriteLine(cln.FindClassPath($"/Users/luisalvarez/Documents/projects/proclogger/ProcLoggerAPI.EFCore", "AppEFCoreDbContextFactor"));

//CCPrint.EFCore.AppEFCoreDbContextFactor _factor = new CCPrint.EFCore.AppEFCoreDbContextFactor(ref cln);