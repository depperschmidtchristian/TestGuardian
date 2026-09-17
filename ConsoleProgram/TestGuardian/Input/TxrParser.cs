using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace TestGuardian.Input;

interface ITxrParser
{
    void ReadTxrFile(string filePath);
}

public class TxrParser : ITxrParser
{
    public TxrParser() { }

    public void ReadTxrFile(string filePath)
    {
        var doc = XDocument.Load(filePath);
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

        var results = doc.Descendants(ns + "UnitTestResult")
            .Select(r => new
            {
                TestName = (string)r.Attribute("testName"),
                Outcome = (string)r.Attribute("outcome"),
                Duration = (string)r.Attribute("duration"),
                ErrorMessage = r.Descendants(ns + "Message").FirstOrDefault()?.Value
            });

        foreach (var r in results)
            Console.WriteLine($"{r.TestName}: {r.Outcome} ({r.Duration})");
    }
}

