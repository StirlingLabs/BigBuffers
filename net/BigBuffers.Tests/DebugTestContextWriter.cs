#nullable enable 
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace BigBuffers.Tests
{
  internal sealed class DebugTestContextWriter : TextWriter
  {
    public object GetLifetimeService()
      => _logger.GetLifetimeService();

    public void Dispose()
      => _logger.Dispose();

    public override object InitializeLifetimeService()
      => _logger.InitializeLifetimeService();

    public override void Close()
      => _logger.Close();

    public override ValueTask DisposeAsync()
      => _logger.DisposeAsync();

    public override void Flush()
    {
      lock (_logger) _logger.Flush();
    }

    public override Task FlushAsync()
    {
      lock (_logger) return _logger.FlushAsync();
    }

    public override void Write(char value)
    {
      lock (_logger)
      {
        System.Diagnostics.Debug.Write(value);
        _logger.Write(value);
      }
    }

    public override void Write(char[]? buffer)
    {
      lock (_logger)
      {
        System.Diagnostics.Debug.Write(new(buffer));
        _logger.Write(buffer);
      }
    }

    public override void Write(char[] buffer, int index, int count)
    {
      lock (_logger)
        Write(new ReadOnlySpan<char>(buffer, index, count));
    }

    public override void Write(ReadOnlySpan<char> buffer)
    {
      lock (_logger)
      {
        System.Diagnostics.Debug.Write(new(buffer));
        _logger.Write(buffer);
      }
    }

    public override void Write(string? value)
    {

      lock (_logger)
      {
        System.Diagnostics.Debug.Write(value);
        _logger.Write(value);
      }
    }

    public override void Write(StringBuilder? value)
    {
      lock (_logger)
      {
        System.Diagnostics.Debug.Write(value?.ToString());
        _logger.Write(value);
      }
    }

    public override void WriteLine()
    {
      lock (_logger)
      {
        System.Diagnostics.Debug.WriteLine("");
        _logger.WriteLine();
      }
    }

    public override void WriteLine(char value)
    {
      lock (_logger)
      {
        System.Diagnostics.Debug.WriteLine(value);
        _logger.WriteLine(value);
      }
    }

    public override void WriteLine(char[]? buffer)
    {
      lock (_logger)
      {
        System.Diagnostics.Debug.WriteLine(new(buffer));
        _logger.WriteLine(buffer);
      }
    }

    public override void WriteLine(char[] buffer, int index, int count)
    {
      lock (_logger)
        WriteLine(new ReadOnlySpan<char>(buffer, index, count));
    }

    public override void WriteLine(ReadOnlySpan<char> buffer)
    {
      lock (_logger)
      {
        System.Diagnostics.Debug.WriteLine(new(buffer));
        _logger.WriteLine(buffer);
      }
    }

    public override void WriteLine(string? value)
    {

      lock (_logger)
      {
        System.Diagnostics.Debug.WriteLine(value);
        _logger.WriteLine(value);
      }
    }

    public override void WriteLine(StringBuilder? value)
    {
      lock (_logger)
      {
        System.Diagnostics.Debug.WriteLine(value?.ToString());
        _logger.WriteLine(value);
      }
    }

    public override Encoding Encoding => _logger.Encoding;

    public override IFormatProvider FormatProvider => _logger.FormatProvider;

    public override string NewLine
    {
      get => _logger.NewLine;
      set => _logger.NewLine = value;
    }

    private TextWriter _logger;

    public DebugTestContextWriter(TextWriter logger)
      => _logger = logger;
  }
}
