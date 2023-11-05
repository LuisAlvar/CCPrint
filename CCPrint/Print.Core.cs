using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CCPrint
{
  public partial class Print: CCPrint.IPrint
  {
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
    /// Private Method: Recursive function to find all possible sub directories from a given path                                   
    /// <param name="ArbPath">This is the root directory</param>
    /// <param name="container">Data structure to contain all of our possible sub directories</param>
    /// <returns></returns>
    private string ChildNodeFolder(string ArbPath, ref ConcurrentBag<string> container)
    {
      if (!Directory.GetDirectories(ArbPath).ToList().Any()) return ArbPath;
      foreach (var item in Directory.GetDirectories(ArbPath).ToList())
      {
        container.Add(ChildNodeFolder(item, ref container));
      }
      return ArbPath;
    }

    /// <summary>
    /// Calculating the confidence rate of a given text line
    /// </summary>
    /// <param name="dataline">A text line a file</param>
    /// <param name="keyword">The log message converted into a list of keywords</param>
    /// <returns></returns>
    private double ProcessLine(string dataline, List<string> keyword)
    {
      double confRate = 100.0 / keyword.Count() + 1;
      double confidence = 0;

      foreach (var item in keyword)
      {
        if (dataline.Contains(item) && dataline.ToLower().Contains(".message")) confidence += confRate;
      }

      return confidence;
    }

    /// <summary>
    /// Selecting the text line of the file with the highest confidence rate.
    /// </summary>
    /// <param name="TextByLines">the file converted into a list of text lines</param>
    /// <param name="keywords">the log message converted into a list of keywords</param>
    /// <returns></returns>
    private TextLine SelectLine(List<string> TextByLines, List<string> keywords)
    {
      int counter = 1;
      double max = 0.0;
      TextLine located = new TextLine(0, 0);

      foreach (var line in TextByLines)
      {
        double current = ProcessLine(line.Trim(), keywords);
        if (current > max)
        {
          max = current;
          located = new TextLine(max, counter);
        }
        ++counter;
      }

      return located;
    }

    /// <summary>
    /// Finding the log message within the file 
    /// </summary>
    /// <param name="File">The file believed to have the log message</param>
    /// <param name="Message">The log messag the user logged</param>
    /// <returns></returns>
    private int FindCodeLine(string File, string Message)
    {
      List<string> lines = System.IO.File.ReadAllLines(File).ToList();
      string polishedmessage = Message.Trim();

      List<string> keywords = new List<string>();
      keywords = polishedmessage.Split(" ").ToList();
      keywords = keywords.Where(str => !string.IsNullOrEmpty(str)).Select(str => str.Trim()).ToList();

      return SelectLine(lines, keywords).linenum;
    }

    /// <summary>
    /// Private method: Find the file in which the log message is being call from
    /// </summary>
    /// <param name="RawPath">The current method directory location</param>
    /// <param name="classname">This is the name of the file attempting to location within the root directory</param>
    /// <returns></returns>
    private string PathOfFile(string ExecutionSpacePath, string ClassName, string MethodName)
    {
      if (_invar.DebugMode)
      {
        Console.WriteLine($"Folder Space: {ExecutionSpacePath}");
        Console.WriteLine($"Class Name: {ClassName}");
        Console.WriteLine($"Method Name: {MethodName}");
      }

      string fileWithTargetClass = FindFilePathBasedOnClass(ExecutionSpacePath, ClassName);

      if(_invar.DebugMode) Console.WriteLine($"This is the file with the target class: {fileWithTargetClass}");

      //Now, check if the filename contains both the class name, method name
      bool bCheck = CheckFileContainsClassNameAndMethodName(fileWithTargetClass, ClassName, MethodName);
      bool isExist = File.Exists(fileWithTargetClass);

      if (!isExist)
      {
        throw new Exception($"Unable to find the cs file from which you are logging: Path {ExecutionSpacePath} and Classname looking for {ClassName}: Attempted to find file - {fileWithTargetClass}");
      }

      return (bCheck) ? fileWithTargetClass : string.Empty;
    }

    private bool CheckFileContainsClassNameAndMethodName(string file, string classname, string methodname)
    {
      Regex rxMethodFunction = new Regex($"{methodname}(.*?)");
      Regex rxClassName = new Regex($"class {classname}");

      List<string> lstReadLines = File.ReadAllLines(file).ToList();
      bool bContainsClass = false;
      bool bContainsMethod = false;
      string strClassline = string.Empty;

      foreach (var item in lstReadLines)
      {
        if (rxClassName.IsMatch(item))
        {
          strClassline = item;
          bContainsClass = true;
        } 

        if(bContainsClass && rxMethodFunction.IsMatch(item))
        {
          if(_invar.DebugMode){
            Console.WriteLine("class detected : " + strClassline);
            Console.WriteLine("Method detected: " + item);
          }
          bContainsMethod = true;
        }
      }

      if (classname.ToLower().Contains("program") && methodname.ToLower().Contains("main"))
      {
        bContainsClass = bContainsMethod = true;
      }
        
      return (bContainsClass && bContainsMethod);
    }

    /// <summary>
    /// Private method: Heavy work of find all possible sub directories and then attempting to find the target class file
    /// </summary>
    private string FindFilePathBasedOnClass(string ArbPath, string ClassName)
    {
      // if the target clas is within the first folder then quick exit 
      string strTargetClassFile = ClassName + "." + _invar.ProgramFileType;
      string response = string.Empty;

      //1. Apply Greedy Approach
      if (_invar.DebugMode)
      {
        Console.WriteLine("Apply Greedy Approach");
        Console.WriteLine($"Looking for the following file :: {strTargetClassFile} ");
      }

      // First, Check if For Example: SubTest.cs file exists within current file path
      if (DoesFileExistWithinTopDir(ArbPath, strTargetClassFile))
      {
        response = Path.Join(ArbPath, strTargetClassFile);
        if (_invar.DebugMode) Console.WriteLine("Found at the top dir: " + response);
        return response;
      }
      // Next, check all file within a current path directory that may contain the class name 
      response = DoesFileExistWithTopDirFiles(ArbPath, ClassName);
      if (!string.IsNullOrEmpty(response)) {
        if (_invar.DebugMode) Console.WriteLine("DoesFileExistsWithTopDirFiles:: " + response);
        return response;
      }

      if (_invar.DebugMode && response == string.Empty) Console.WriteLine("Unable to find with the top dir: FileTypeClass.cs or a File contains the target class");

      // Next, Check if there is a file type of SubTest.cs within all sub folders
      // find all of the subdirectory within this root path
      ConcurrentBag<string> lstOfSubDir = new ConcurrentBag<string>();

      foreach (var item in Directory.GetDirectories(ArbPath))
      {
        if (!(item.Contains("bin") || item.Contains("obj")))
        {
          lstOfSubDir.Add(ChildNodeFolder(item, ref lstOfSubDir));
        }
      }

      if (_invar.DebugMode)
      {
        Console.WriteLine($"these are all of the folders with Top Root Folder {ArbPath}");

        foreach (var item in lstOfSubDir)
        {
          Console.WriteLine($"\t{item}");
        }
      }

      // Search for filetype.cs or classname within these new directories. 
      CancellationTokenSource cts = new CancellationTokenSource();

      ParallelOptions opts = new ParallelOptions();
      opts.CancellationToken = cts.Token;
      opts.MaxDegreeOfParallelism = (System.Environment.ProcessorCount - 2 > 0) ? (System.Environment.ProcessorCount - 2) : System.Environment.ProcessorCount;


      Parallel.ForEach(lstOfSubDir, opts, (currFolder, state) =>
      {

        var result = DoesTargetClassExistsWithinADir(currFolder, ClassName);
        if (!string.IsNullOrEmpty(result))
        {
          response = result;
          state.Break();
        }

        if (string.IsNullOrEmpty(result) && DoesFileExistWithinTopDir(currFolder, strTargetClassFile))
        {
          response = currFolder + Path.DirectorySeparatorChar + strTargetClassFile;
          state.Break();
        }
      });


      return response;
    }

    private string DoesFileExistWithTopDirFiles(string ArbPath, string TargetClassName)
    {
      //Find all files with this ArbPath 
      string response = string.Empty;
      object locks = new object();

      ConcurrentBag<string> lstOfFiles = new ConcurrentBag<string>();
      if (_invar.DebugMode) Console.WriteLine($"All files within {ArbPath}::");

      foreach (var item in Directory.GetFiles(ArbPath).Where(fi => fi.EndsWith(_invar.ProgramFileType)).ToList())
      {
        if (_invar.DebugMode) Console.WriteLine($"{item}");
        lstOfFiles.Add(item);
      }

      //Check all files if they contain the class name
      CancellationTokenSource cts = new CancellationTokenSource();

      ParallelOptions opts = new ParallelOptions();
      opts.CancellationToken = cts.Token;
      opts.MaxDegreeOfParallelism = (System.Environment.ProcessorCount - 2 > 0) ? (System.Environment.ProcessorCount - 2) : System.Environment.ProcessorCount;

      Parallel.ForEach(lstOfFiles, opts, (currFile, state) => {

        string result = DoesTargetClassExistsWithinThisFile(currFile, TargetClassName);

        if (!string.IsNullOrEmpty(result))
        {
          lock (locks){
            response = result;
          }
          state.Break();
        }

      });

      if (_invar.DebugMode && string.IsNullOrEmpty(response))
      {
        Console.WriteLine($"Could not find target class name {TargetClassName} within {ArbPath} directory");
      }

      return response;
     }

    /// <summary>
    /// Private Method: check if the current path contains the target class file
    /// </summary>
    /// <param name="ArbPath">A random path to check if target class file exists</param>
    /// <param name="TargetClass">The name of the target class </param>
    /// <returns></returns>
    private bool DoesFileExistWithinTopDir(string ArbPath, string TargetClassFileType)
    {
      return File.Exists(ArbPath + Path.DirectorySeparatorChar + TargetClassFileType);
    }

    private string DoesTargetClassExistsWithinThisFile(string FilePathName, string TargetClass)
    {
      List<string> lstTextWithiFile = File.ReadAllLines(FilePathName).ToList();
      string fileName = lstTextWithiFile.Where(strLine => strLine.Contains("class") && strLine.Contains(TargetClass)).FirstOrDefault() ?? String.Empty;
      return (!string.IsNullOrEmpty(fileName)) ? FilePathName : string.Empty;
    }

    private string DoesTargetClassExistsWithinADir(string ArbPath, string TargetClass)
    {
      List<string> aryFiles = Directory.GetFiles(ArbPath).ToList();

      foreach (var file in aryFiles)
      {
        int indexOfLastDir = file.LastIndexOf(Path.DirectorySeparatorChar) + 1;
        string scanFile = file.Substring(indexOfLastDir, file.Length - indexOfLastDir);

        int indexOfLastPer = scanFile.LastIndexOf(".") + 1;
        string finalExtension = scanFile.Substring(indexOfLastPer, scanFile.Length - indexOfLastPer);

        if (finalExtension == _invar.ProgramFileType)
        {
          List<string> lstTextWithiFile = File.ReadAllLines(file).ToList();
          string fileLine = string.Empty;

          fileLine = lstTextWithiFile.Where(strLine => strLine.Contains("class") && strLine.Contains(TargetClass)).FirstOrDefault() ?? String.Empty;
          if (!string.IsNullOrEmpty(fileLine)) return file;

        }

      }

      return string.Empty;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="RawPath"></param>
    /// <returns></returns>
    private string ReMapAssemblyPath(string RawPath)
    {
      string binDir = $"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}";
      int indexForSplit = RawPath.IndexOf(binDir);
      return RawPath.Substring(0, indexForSplit);
    }

  }
}
