using System;
using System.Text;
using System.Diagnostics;
using System.ComponentModel;
using System.Reflection;
using System.IO;
using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace CCPrint;

public partial class Print: CCPrint.IPrint
{

  public void Message(StackFrame Frame, Assembly Ably, LogPrintType LogType, string ConsoleMessage)
  {
    MethodBase? callingMethod = Frame.GetMethod();
    string strCallingMethodNameFormated = string.Empty; 

    if (callingMethod == null)
    {
      throw new ArgumentException("Stack Frame not provided for callling method");
    }

    if(callingMethod != null)
    {
      Type? targetClassName = callingMethod.DeclaringType;

      if (targetClassName == null)
      {
        throw new ArgumentException("Calling Methods does not have a declaring type");
      }

      string folderExecutionSpace = ReMapAssemblyPath(Ably.Location);

      if (_invar.DebugMode) Console.WriteLine($"This is the Execution Space: " + folderExecutionSpace);


      if (callingMethod.Name.ToString().Contains("$"))
      {
        strCallingMethodNameFormated = callingMethod.Name.ToString().Replace("<", "").Replace(">", "").Replace("$", "");
      } else
      {
        strCallingMethodNameFormated = callingMethod.Name.ToString();
      }

      string fileClass = PathOfFile(folderExecutionSpace, targetClassName.Name.ToString(), strCallingMethodNameFormated);

      if (_invar.DebugMode) Console.WriteLine($"This is file name containing the class name and mehtod :: {fileClass}");

      int MessageOnCodeLine = FindCodeLine(fileClass, ConsoleMessage);

      if (_invar.DebugMode) Console.WriteLine($"This is line number ({MessageOnCodeLine}) that we found message {ConsoleMessage} with file ({fileClass})");

      if (_invar.DebugMode)
      {
        Console.WriteLine("This is the rootDir: " + _rootDir);
      }

      bool HasRoot = fileClass.Contains(_rootDir);
      if (HasRoot && _rootDir.Length > 0)
      {
        fileClass = fileClass.Replace(_rootDir, string.Empty);
        int firstInstance = fileClass.IndexOf(Path.DirectorySeparatorChar);
        fileClass = fileClass.Substring(firstInstance + 1, fileClass.Length - 1);
      }

      string formatCallingClass = $"{targetClassName.Name.ToString().ToLower()}" + _invar.LogDelimited;
      string formatMethod = $"{strCallingMethodNameFormated.ToLower()}" + _invar.LogDelimited;
      string formatLogType = $"{LogType.ToString().ToLower()}" + _invar.LogDelimited;
      string formatFileClass = $"{fileClass}:{MessageOnCodeLine}" + _invar.LogDelimited;

      string baseMessStr = formatLogType + formatCallingClass + formatMethod + formatFileClass + " - " + ConsoleMessage;

      string polishedStr = LogLocalTimeStamp() + baseMessStr;

      switch (LogType)
      {
        case LogPrintType.Information:
          System.Console.ForegroundColor = ConsoleColor.Green;
          break;

        case LogPrintType.Warning:
          System.Console.ForegroundColor = ConsoleColor.Yellow;
          break;

        case LogPrintType.Error:
          System.Console.ForegroundColor = ConsoleColor.Red;
          break;

        default:
          System.Console.ResetColor();
          break;
      }

      _collection.Add(polishedStr);
      System.Console.WriteLine(polishedStr);

      System.Console.ResetColor();
    }
  

  }




  /// <summary>
  /// User can flush out all of the logs collected
  /// </summary>
  /// <param name="EndOfStageName">The name of this collection of logs</param>
  public void Flush(string EndOfStageName)
  {
    string strBaseDirForLogs = _invar.LogFlushDir;
    string strFileLogName = _invar.BaseLogFileName + EndOfStageName + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + _invar.BaseLogFileExtension;
    string strLogPath = _invar.LogFlushDir + Path.DirectorySeparatorChar + strFileLogName;

    FileStream outStream;
    StreamWriter sWriter;

    try
    {
      outStream = new FileStream(strLogPath, FileMode.OpenOrCreate, FileAccess.Write);
      sWriter = new StreamWriter(outStream);
      foreach (var line in _collection)
      {
        sWriter.WriteLine(line);
      }

      sWriter.Close();
      outStream.Close();
      _collection = new ConcurrentBag<string>();
    }
    catch (System.Exception ex)
    {
      string strErrorMessage = "Print.Flush method error - " 
      + "\nMessage: " + ex.Message
      + "\nSource: " + ex.Source
      + "\nStackTrace: " + ex.StackTrace;

      throw new Exception(strErrorMessage);
    }
  
  }

}

