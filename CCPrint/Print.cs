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

public class Print: CCPrint.IPrint
{
  /// <summary>
  /// Utilize to collect all of the message from the posible ,
  /// </summary>
  private readonly string _flushPath;

  private readonly string _rootDir;

  private readonly Invariants _invar;

  /// <summary>
  /// 4
  /// </summary>
  /// <typeparam name="string"></typeparam>
  /// <returns></returns>
  private ConcurrentBag<string> _collection = new ConcurrentBag<string>();

  public Print(IConfiguration configuration)
  {
    _flushPath = configuration["LogFlushPath"];
    _rootDir = string.Empty;
    _invar = configuration.GetSection(nameof(Invariants)).Get<Invariants>() ?? new Invariants();
  }


  public Print(IConfiguration configuration, string rootDirectory)
  {
    _flushPath = configuration["LogFlushPath"];
    _rootDir = rootDirectory;
    _invar = configuration.GetSection(nameof(Invariants)).Get<Invariants>() ?? new Invariants();
  }

  /// <summary>
  /// Returns the current data time in UTC format
  /// </summary>
  /// <returns></returns>
  private string LogUtcTimeStamp()
  {
    return $"{DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff")}" + _invar.LogDelimited;
  }

  /// <summary>
  /// Returns the current date time in local time format.
  /// </summary>
  /// <returns></returns>
  private string LogLocalTimeStamp()
  {
    return $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}" + _invar.LogDelimited;
  }

  /// <summary>
  /// Private Method: check if the current path contains the target class file
  /// </summary>
  /// <param name="ArbPath">A random path to check if target class file exists</param>
  /// <param name="TargetClass">The name of the target class </param>
  /// <returns></returns>
  private bool DoesFileExistWithinDir(string ArbPath, string TargetClass)
  {
    return File.Exists(ArbPath + Path.DirectorySeparatorChar + TargetClass);
  }

  private string DoesTargetClassExistsWithinAFile(string ArbPath, string TargetClass)
  {
    List<string> aryFiles = Directory.GetFiles(ArbPath).ToList();

    foreach (var file in aryFiles)
    {
      int indexOfLastDir = file.LastIndexOf(Path.DirectorySeparatorChar) + 1;
      string scanFile = file.Substring(indexOfLastDir, file.Length - indexOfLastDir);

      int indexOfLastPer = scanFile.LastIndexOf(".") + 1;
      string finalExtension = scanFile.Substring(indexOfLastPer, scanFile.Length - indexOfLastPer);

      if(finalExtension == _invar.ProgramFileType)
      {
        string fileInAString = File.ReadAllText(file);
        if (fileInAString.Contains(TargetClass)) return file;
      }

    }
    
    return string.Empty;
  }

  /// <summary>
  /// Private Method: Recursive function to find all possible sub directories from a given path                                   
  /// <param name="ArbPath">This is the root directory</param>
  /// <param name="container">Data structure to contain all of our possible sub directories</param>
  /// <returns></returns>
  private string ChildNodeFolder(string ArbPath, ref ConcurrentBag<string> container)
  {
    if(!Directory.GetDirectories(ArbPath).ToList().Any()) return ArbPath;

    foreach (var item in Directory.GetDirectories(ArbPath).ToList())
    {
      container.Add(ChildNodeFolder(item, ref container));
    }

    return ArbPath;
  }


  /// <summary>
  /// Private method: Heavy work of find all possible sub directories and then attempting to find the target class file
  /// </summary>
  private string FindClassPath(string ArbPath, string ClassName)
  {
    // if the target clas is within the first folder then quick exit 
    string strTargetClassFile =  ClassName + "." +  _invar.ProgramFileType;
    string response = string.Empty;

    if(DoesFileExistWithinDir(ArbPath, strTargetClassFile)) return ArbPath + Path.DirectorySeparatorChar +  strTargetClassFile;

    response = DoesTargetClassExistsWithinAFile(ArbPath, ClassName);

    Console.WriteLine($"response: {response}");

    if(!string.IsNullOrEmpty(response)) return response;

    //find all of the subdirectory within this root path
    ConcurrentBag<string> lstOfSubDir = new ConcurrentBag<string>();
    foreach (var item in Directory.GetDirectories(ArbPath))
    {
      lstOfSubDir.Add(ChildNodeFolder(item, ref lstOfSubDir));
    }

    foreach (var item in lstOfSubDir)
    {
      Console.WriteLine($"\t{item}");
    }


    CancellationTokenSource cts = new CancellationTokenSource();

    ParallelOptions opts = new ParallelOptions();
    opts.CancellationToken = cts.Token;
    opts.MaxDegreeOfParallelism = (System.Environment.ProcessorCount - 2 > 0) ? (System.Environment.ProcessorCount - 2) : System.Environment.ProcessorCount;

    Parallel.ForEach(lstOfSubDir, opts, (currFolder, state) => {
      
      var result = DoesTargetClassExistsWithinAFile(currFolder, ClassName);
      if(!string.IsNullOrEmpty(result))
      {
        response = result;
        state.Break();
      }

      if(DoesFileExistWithinDir(currFolder, strTargetClassFile)) {
        response = currFolder + Path.DirectorySeparatorChar + strTargetClassFile;
        state.Break();
      }
    });

    return response;
  }

  public class TextLine
  {
    public double confidence = 0.0;
    public int linenum = 0;

    public TextLine(double conf, int num)
    {
      confidence = conf;
      linenum = num;
    }
  }

  private double ProcessLine(string dataline, List<string> keyword)
  {
    double confRate = 100.0 / keyword.Count() + 1;
    double confidence = 0;

    foreach (var item in keyword)
    {
      if(dataline.Contains(item) && dataline.ToLower().Contains(".message")) confidence += confRate;
    }

    return confidence;
  }

  private TextLine SelectLine(List<string> TextByLines, List<string> keywords)
  {
    List<TextLine> lstTotalLines = new List<TextLine>();
    
    int counter = 1;
    double max = 0.0;
    TextLine located = new TextLine(0,0);

    foreach(var line in TextByLines)
    {
       double current = ProcessLine(line.Trim(), keywords);
       if(current > max) {
        max = current;
        located = new TextLine(max, counter);
       }
       ++counter;
    }

    return located;
  }

  private int FindCodeLine(string File, string Message)
  {
    List<string> lines = System.IO.File.ReadAllLines(File).ToList();
    string polishedmessage= Message.Trim();

    List<string> keywords = new List<string>();
    keywords = polishedmessage.Split(" ").ToList();
    keywords =  keywords.Where(str => !string.IsNullOrEmpty(str)).Select(str => str.Trim()).ToList();
    
    return SelectLine(lines, keywords).linenum;
  }

  /// <summary>
  /// Private method: Find the file in which the log message is being call from
  /// </summary>
  /// <param name="RawPath">The current method directory location</param>
  /// <param name="classname">This is the name of the file attempting to location within the root directory</param>
  /// <returns></returns>
  private string PathOfFile(string RawPath, string classname, string methodname=null)
  {
    string binDir = $"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}";
    int indexForSplit = RawPath.IndexOf(binDir);
    string prefixPath = RawPath.Substring(0, indexForSplit);

    Console.WriteLine($"prefixPath: {prefixPath}");

    string polishedStr = FindClassPath(prefixPath, classname);

    bool isExist = File.Exists(polishedStr);

    if(!isExist)
    {
      throw new Exception($"Unable to find the cs file from which you are logging: Path {prefixPath} and Classname looking for {classname}: Attempted to find file - {polishedStr}");
    }

    return polishedStr;
  }

  private string FileOnSpecificPath(string folder, string classname)
  {
    string polishedStr = FindClassPath(folder, classname);

    bool isExist = File.Exists(polishedStr);

    if(!isExist)
    {
      throw new Exception($"Unable to find the cs file from which you are logging: Path {folder} and Classname looking for {classname}: Attempted to find file - {polishedStr}");
    }

    return polishedStr;
  }

  /// <summary>
  /// Utilize this method to log a message and a object using Newtonsoft Json
  /// </summary>
  /// <param name="Frame">This is a newly instance of StackFrame</param>
  /// <param name="Ably">This is the current assembly calling this method</param>
  /// <param name="CodeLine">The line of code of this log message</param>
  /// <param name="TimeType">Select the time format for the log message</param>
  /// <param name="LogType">Select the type of log message</param>
  /// <param name="ConsoleMessage">This the message you want to print to the console log</param>
  /// <param name="Data">This can be any class object</param>
  public void Message(StackFrame Frame, Assembly Ably, int CodeLine, LogTimeType TimeType, LogPrintType LogType, string ConsoleMessage, object DataObject)
  {  

    MethodBase? callingMethod = Frame.GetMethod();

    if (callingMethod == null)
    {
      throw new ArgumentException("Stack Frame not provided for callling method");
    }

    if(callingMethod != null)
    {

      Type? targetMethodName = callingMethod.DeclaringType;

      if(targetMethodName == null)
      {
        throw new ArgumentException("Calling Methods does not have a declaring type");
      }

      string fileClass = PathOfFile(Ably.Location, targetMethodName.Name.ToString()).Trim();

      bool HasRoot = fileClass.Contains(_rootDir);
      if(HasRoot && _rootDir.Length > 0)
      {
        fileClass = fileClass.Replace(_rootDir, string.Empty);
        int firstInstance = fileClass.IndexOf(Path.DirectorySeparatorChar);
        fileClass = fileClass.Substring(firstInstance + 1, fileClass.Length - 1 );
      }
      
      string formatCallingClass = $"[{targetMethodName.Name.ToString().ToLower()}]" ;
      string formatMethod = $"[{callingMethod.Name.ToString().Replace("<","").Replace(">","").Replace("$","").ToLower() }]";
      string formatLogType = $"[{LogType.ToString().ToLower()}]" ;
      string formatFileClass = $"[{fileClass}:{CodeLine}]";

      string dataObjStr = string.Empty;
      string baseMessStr = string.Empty;

      if(DataObject != null)
      {
        dataObjStr = JsonConvert.SerializeObject(DataObject);
        baseMessStr = formatLogType + formatCallingClass + formatMethod + formatFileClass  + " - " + ConsoleMessage + "\n" + dataObjStr;
      }else 
      {
        baseMessStr = formatLogType + formatCallingClass + formatMethod + formatFileClass  + " - " + ConsoleMessage;
      }

      string polishedStr =  (TimeType.ToString() == LogTimeType.Local.ToString())  ? (LogLocalTimeStamp() + baseMessStr) : (LogUtcTimeStamp() + baseMessStr);

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
  /// Utilize this method to log a message
  /// </summary>
  /// <param name="Frame">This is a newly instance of StackFrame</param>
  /// <param name="Ably">This is the current assembly calling this method</param>
  /// <param name="CodeLine">The line of code of this log message</param>
  /// <param name="TimeType">Select the time format for the log message</param>
  /// <param name="LogType">Select the type of log message</param>
  /// <param name="ConsoleMessage">This the message you want to print to the console log</param>
  public void Message(StackFrame Frame, Assembly Ably, int CodeLine, LogTimeType TimeType, LogPrintType LogType, string ConsoleMessage)
  {
    MethodBase? callingMethod = Frame.GetMethod();

    if (callingMethod == null)
    {
      throw new ArgumentException("Stack Frame not provided for callling method");
    }

    if(callingMethod != null)
    {
      Type? targetMethodName = callingMethod.DeclaringType;

      if (targetMethodName == null)
      {
        throw new ArgumentException("Calling Methods does not have a declaring type");
      }

      string fileClass = PathOfFile(Ably.Location, targetMethodName.Name.ToString());

      bool HasRoot = fileClass.Contains(_rootDir);
      if(HasRoot && _rootDir.Length > 0)
      {
        fileClass = fileClass.Replace(_rootDir, string.Empty);
        int firstInstance = fileClass.IndexOf(Path.DirectorySeparatorChar);
        fileClass = fileClass.Substring(firstInstance + 1, fileClass.Length - 1 );
      }

      string formatCallingClass = $"{targetMethodName.Name.ToString().ToLower()}" + _invar.LogDelimited;
      string formatMethod = $"{callingMethod.Name.ToString().Replace("<","").Replace(">","").Replace("$","").ToLower()}" + _invar.LogDelimited;
      string formatLogType = $"{LogType.ToString().ToLower()}" + _invar.LogDelimited;
      string formatFileClass = $"{fileClass}:{CodeLine}" + _invar.LogDelimited;

      string baseMessStr = formatLogType + formatCallingClass + formatMethod + formatFileClass  + " - " + ConsoleMessage;

      string polishedStr =  (TimeType.ToString() == LogTimeType.Local.ToString())  ? (LogLocalTimeStamp() + baseMessStr) : (LogUtcTimeStamp() + baseMessStr);

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

  public void Message(StackFrame Frame, Assembly Ably, LogTimeType TimeType, LogPrintType LogType, string ConsoleMessage)
  {
    MethodBase? callingMethod = Frame.GetMethod();

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

      Console.WriteLine($"Ably.Location --> {Ably.Location}");

      string fileClass = PathOfFile(Ably.Location, targetClassName.Name.ToString(), callingMethod.Name.ToString());

      Console.WriteLine(fileClass);

      int MessageOnCodeLine = FindCodeLine(fileClass, ConsoleMessage);

      bool HasRoot = fileClass.Contains(_rootDir);
      if(HasRoot && _rootDir.Length > 0)
      {
        fileClass = fileClass.Replace(_rootDir, string.Empty);
        int firstInstance = fileClass.IndexOf(Path.DirectorySeparatorChar);
        fileClass = fileClass.Substring(firstInstance + 1, fileClass.Length - 1 );
      }

      string formatCallingClass = $"{targetClassName.Name.ToString().ToLower()}" + _invar.LogDelimited;
      string formatMethod = $"{callingMethod.Name.ToString().Replace("<","").Replace(">","").Replace("$","").ToLower()}" + _invar.LogDelimited;
      string formatLogType = $"{LogType.ToString().ToLower()}" + _invar.LogDelimited;
      string formatFileClass = $"{fileClass}:{MessageOnCodeLine}" + _invar.LogDelimited;

      Console.WriteLine($"This is class name: " +  formatCallingClass);
      Console.WriteLine($"This is method name: " + formatMethod);

      string baseMessStr = formatLogType + formatCallingClass + formatMethod + formatFileClass  + " - " + ConsoleMessage;

      string polishedStr =  (TimeType.ToString() == LogTimeType.Local.ToString())  ? (LogLocalTimeStamp() + baseMessStr) : (LogUtcTimeStamp() + baseMessStr);

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

  public void Message(StackFrame Frame, string folder, LogTimeType TimeType, LogPrintType LogType, string ConsoleMessage)
  {
    MethodBase? callingMethod = Frame.GetMethod();

    if (callingMethod == null)
    {
      throw new ArgumentException("Stack Frame not provided for callling method");
    }

    if(callingMethod != null)
    {
      Type? targetMethodName = callingMethod.DeclaringType;

      if (targetMethodName == null)
      {
        throw new ArgumentException("Calling Methods does not have a declaring type");
      }

      string fileClass = FileOnSpecificPath(folder, targetMethodName.Name.ToString());
      int MessageOnCodeLine = FindCodeLine(fileClass, ConsoleMessage);

      bool HasRoot = fileClass.Contains(_rootDir);
      if(HasRoot && _rootDir.Length > 0)
      {
        fileClass = fileClass.Replace(_rootDir, string.Empty);
        int firstInstance = fileClass.IndexOf(Path.DirectorySeparatorChar);
        fileClass = fileClass.Substring(firstInstance + 1, fileClass.Length - 1 );
      }

      string formatCallingClass = $"{targetMethodName.Name.ToString().ToLower()}" + _invar.LogDelimited;
      string formatMethod = $"{callingMethod.Name.ToString().Replace("<","").Replace(">","").Replace("$","").ToLower()}" + _invar.LogDelimited;
      string formatLogType = $"{LogType.ToString().ToLower()}" + _invar.LogDelimited;
      string formatFileClass = $"{fileClass}:{MessageOnCodeLine}" + _invar.LogDelimited;

      string baseMessStr = formatLogType + formatCallingClass + formatMethod + formatFileClass  + " - " + ConsoleMessage;

      string polishedStr =  (TimeType.ToString() == LogTimeType.Local.ToString())  ? (LogLocalTimeStamp() + baseMessStr) : (LogUtcTimeStamp() + baseMessStr);

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

  public void Message(StackFrame Frame, Assembly Ably, LogPrintType LogType, string ConsoleMessage)
  {
    MethodBase? callingMethod = Frame.GetMethod();

    if (callingMethod == null)
    {
      throw new ArgumentException("Stack Frame not provided for callling method");
    }

    if(callingMethod != null)
    {
      Type? targetMethodName = callingMethod.DeclaringType;

      if (targetMethodName == null)
      {
        throw new ArgumentException("Calling Methods does not have a declaring type");
      }

      string fileClass = FileOnSpecificPath(Ably.Location, targetMethodName.Name.ToString());
      int MessageOnCodeLine = FindCodeLine(fileClass, ConsoleMessage);

      bool HasRoot = fileClass.Contains(_rootDir);
      if(HasRoot && _rootDir.Length > 0)
      {
        fileClass = fileClass.Replace(_rootDir, string.Empty);
        int firstInstance = fileClass.IndexOf(Path.DirectorySeparatorChar);
        fileClass = fileClass.Substring(firstInstance + 1, fileClass.Length - 1 );
      }

      string formatCallingClass = $"{targetMethodName.Name.ToString().ToLower()}" + _invar.LogDelimited;
      string formatMethod = $"{callingMethod.Name.ToString().Replace("<","").Replace(">","").Replace("$","").ToLower()}" + _invar.LogDelimited;
      string formatLogType = $"{LogType.ToString().ToLower()}" + _invar.LogDelimited;
      string formatFileClass = $"{fileClass}:{MessageOnCodeLine}" + _invar.LogDelimited;

      string baseMessStr = formatLogType + formatCallingClass + formatMethod + formatFileClass  + " - " + ConsoleMessage;

      string polishedStr =  LogLocalTimeStamp() + baseMessStr;

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


  public void Message(StackFrame Frame, Assembly Ably, LogPrintType LogType, string ConsoleMessage, object DataObject)
  {
    MethodBase? callingMethod = Frame.GetMethod();

    if (callingMethod == null)
    {
      throw new ArgumentException("Stack Frame not provided for callling method");
    }

    if(callingMethod != null)
    {
      Type? targetMethodName = callingMethod.DeclaringType;

      if (targetMethodName == null)
      {
        throw new ArgumentException("Calling Methods does not have a declaring type");
      }

      string fileClass = FileOnSpecificPath(Ably.Location, targetMethodName.Name.ToString());
      int MessageOnCodeLine = FindCodeLine(fileClass, ConsoleMessage);

      bool HasRoot = fileClass.Contains(_rootDir);
      if(HasRoot && _rootDir.Length > 0)
      {
        fileClass = fileClass.Replace(_rootDir, string.Empty);
        int firstInstance = fileClass.IndexOf(Path.DirectorySeparatorChar);
        fileClass = fileClass.Substring(firstInstance + 1, fileClass.Length - 1 );
      }

      string formatCallingClass = $"{targetMethodName.Name.ToString().ToLower()}" + _invar.LogDelimited;
      string formatMethod = $"{callingMethod.Name.ToString().Replace("<","").Replace(">","").Replace("$","").ToLower()}" + _invar.LogDelimited;
      string formatLogType = $"{LogType.ToString().ToLower()}" + _invar.LogDelimited;
      string formatFileClass = $"{fileClass}:{MessageOnCodeLine}" + _invar.LogDelimited;

      string dataObjStr = string.Empty;
      string baseMessStr = string.Empty;


      if(DataObject != null)
      {
        dataObjStr = JsonConvert.SerializeObject(DataObject);
        baseMessStr = formatLogType + formatCallingClass + formatMethod + formatFileClass  + " - " + ConsoleMessage + "\n" + dataObjStr;
      }else 
      {
        baseMessStr = formatLogType + formatCallingClass + formatMethod + formatFileClass  + " - " + ConsoleMessage;
      }

      string polishedStr =  LogLocalTimeStamp() + baseMessStr;

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
    string strBaseDirForLogs = _flushPath;
    string strFileLogName = _invar.BaseLogFileName + EndOfStageName + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + _invar.BaseLogFileExtension;
    string strLogPath = _flushPath + Path.DirectorySeparatorChar + strFileLogName;

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

/// <summary>
/// Time perference to log a mesage
/// </summary>
public enum LogTimeType 
{
  Local,
  UTC
}

/// <summary>
/// Types of Log Messages
/// </summary>
public enum LogPrintType
{
  Information, 

  Warning, 

  Error
}