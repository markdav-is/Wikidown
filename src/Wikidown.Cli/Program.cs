using System.Text;
using Wikidown.Cli;

// Windows consoles default to an OEM code page, which mangles non-ASCII page
// text both ways: `read > page.md` on the way out, `--stdin` on the way in.
var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
var (originalOut, originalIn) = (Console.OutputEncoding, Console.InputEncoding);
try
{
    Console.OutputEncoding = utf8;
    Console.InputEncoding = utf8;
}
catch (IOException) { /* no console attached (e.g. launched windowless) */ }

try
{
    return CommandRunner.Run(args);
}
finally
{
    Console.Out.Flush();
    try
    {
        Console.OutputEncoding = originalOut;
        Console.InputEncoding = originalIn;
    }
    catch (IOException) { }
}
