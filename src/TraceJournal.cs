using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeepLength;

// A bounded, single-session journal. Diagnostic failures must not change poses.
internal sealed class TraceJournal : IDisposable
{
    readonly StreamWriter writer;
    readonly int maxRecords;
    readonly long maxBytes;
    int records;
    long bytes;
    public bool Closed { get; private set; }
    static readonly JsonSerializerOptions Options = new() { NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
    public TraceJournal(string path, int maxRecords=20000, long maxBytes=32*1024*1024)
    {
        this.maxRecords=maxRecords;this.maxBytes=maxBytes;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        writer=new StreamWriter(new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read),new UTF8Encoding(false));
    }
    public void Write(object value)
    {
        if(Closed)return;
        string line=JsonSerializer.Serialize(value,Options);
        int size=Encoding.UTF8.GetByteCount(line)+Encoding.UTF8.GetByteCount(Environment.NewLine);
        if(records>=maxRecords || bytes+size>maxBytes){Dispose();return;}
        writer.WriteLine(line);bytes+=size;records++;
        if(records%60==0)writer.Flush();
    }
    public void Dispose(){if(Closed)return;Closed=true;writer.Dispose();}
}
