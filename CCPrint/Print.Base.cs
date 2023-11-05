using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPrint
{
  public partial class Print: CCPrint.IPrint
  {
    /// <summary>
    /// Utilize to collect all of the message from the posible ,
    /// </summary>
    private readonly string _rootDir;

    /// <summary>
    /// Invariant is a configuration data object mapped from appsetings.json
    /// </summary>
    private readonly Invariants _invar;

    /// <summary>
    /// Collects all of the log statement until users calls the Flush method
    /// </summary>
    /// <typeparam name="string"></typeparam>
    /// <returns></returns>
    private ConcurrentBag<string> _collection = new ConcurrentBag<string>();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="configuration"></param>
    public Print(IConfiguration configuration)
    {
      _rootDir = string.Empty;
      _invar = configuration.GetSection(nameof(Invariants)).Get<Invariants>() ?? new Invariants();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="configuration"></param>
    /// <param name="rootDirectory"></param>
    public Print(IConfiguration configuration, string rootDirectory)
    {
      _rootDir = rootDirectory;
      _invar = configuration.GetSection(nameof(Invariants)).Get<Invariants>() ?? new Invariants();
    }


  }

  /// <summary>
  /// 
  /// </summary>
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



}
