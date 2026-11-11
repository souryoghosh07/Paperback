using System.IO;
using System.IO.Compression;
using System.Text;

namespace Paperback.Tests;

public static class EpubGenerator
{
    public static void GenerateSampleEpub(string outputPath)
    {
        string? dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        var utf8NoBom = new UTF8Encoding(false);

        using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Create);

        // 1. mimetype (must be first, uncompressed)
        var mimeEntry = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
        using (var writer = new StreamWriter(mimeEntry.Open(), Encoding.ASCII))
        {
            writer.Write("application/epub+zip");
        }

        // 2. META-INF/container.xml
        var containerEntry = zip.CreateEntry("META-INF/container.xml", CompressionLevel.Optimal);
        using (var writer = new StreamWriter(containerEntry.Open(), utf8NoBom))
        {
            writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<container version=""1.0"" xmlns=""urn:oasis:names:tc:opendocument:xmlns:container"">
  <rootfiles>
    <rootfile full-path=""OEBPS/content.opf"" media-type=""application/oebps-package+xml""/>
  </rootfiles>
</container>");
        }

        // 3. OEBPS/content.opf
        var opfEntry = zip.CreateEntry("OEBPS/content.opf", CompressionLevel.Optimal);
        using (var writer = new StreamWriter(opfEntry.Open(), utf8NoBom))
        {
            writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<package xmlns=""http://www.idpf.org/2007/opf"" unique-identifier=""BookId"" version=""2.0"">
  <metadata xmlns:dc=""http://purl.org/dc/elements/1.1/"" xmlns:opf=""http://www.idpf.org/2007/opf"">
    <dc:title>A Scandal in Bohemia</dc:title>
    <dc:creator opf:role=""aut"">Arthur Conan Doyle</dc:creator>
    <dc:language>en</dc:language>
    <dc:identifier id=""BookId"">urn:uuid:12345-sherlock</dc:identifier>
    <dc:description>To Sherlock Holmes she is always THE woman. I have seldom heard him mention her under any other name.</dc:description>
  </metadata>
  <manifest>
    <item id=""ncx"" href=""toc.ncx"" media-type=""application/x-dtbncx+xml""/>
    <item id=""chap1"" href=""chap1.xhtml"" media-type=""application/xhtml+xml""/>
    <item id=""chap2"" href=""chap2.xhtml"" media-type=""application/xhtml+xml""/>
  </manifest>
  <spine toc=""ncx"">
    <itemref idref=""chap1""/>
    <itemref idref=""chap2""/>
  </spine>
</package>");
        }

        // 4. OEBPS/toc.ncx
        var ncxEntry = zip.CreateEntry("OEBPS/toc.ncx", CompressionLevel.Optimal);
        using (var writer = new StreamWriter(ncxEntry.Open(), utf8NoBom))
        {
            writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<ncx xmlns=""http://www.daisy.org/z3986/2005/ncx/"" version=""2005-1"">
  <head>
    <meta name=""dtb:uid"" content=""urn:uuid:12345-sherlock""/>
  </head>
  <docTitle><text>A Scandal in Bohemia</text></docTitle>
  <navMap>
    <navPoint id=""navPoint-1"" playOrder=""1"">
      <navLabel><text>Chapter I: The Arrival</text></navLabel>
      <content src=""chap1.xhtml""/>
    </navPoint>
    <navPoint id=""navPoint-2"" playOrder=""2"">
      <navLabel><text>Chapter II: The Photograph</text></navLabel>
      <content src=""chap2.xhtml""/>
    </navPoint>
  </navMap>
</ncx>");
        }

        // 5. OEBPS/chap1.xhtml
        var chap1Entry = zip.CreateEntry("OEBPS/chap1.xhtml", CompressionLevel.Optimal);
        using (var writer = new StreamWriter(chap1Entry.Open(), utf8NoBom))
        {
            writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE html PUBLIC ""-//W3C//DTD XHTML 1.1//EN"" ""http://www.w3.org/TR/xhtml11/DTD/xhtml11.dtd"">
<html xmlns=""http://www.w3.org/1999/xhtml"">
<head><title>Chapter I</title></head>
<body>
  <h1>Chapter I: The Arrival</h1>
  <p>To Sherlock Holmes she is always THE woman. I have seldom heard him mention her under any other name. In his eyes she eclipses and predominates the whole of her sex.</p>
  <p>All emotions, and that one particularly, were abhorrent to his cold, precise but admirably balanced mind.</p>
</body>
</html>");
        }

        // 6. OEBPS/chap2.xhtml
        var chap2Entry = zip.CreateEntry("OEBPS/chap2.xhtml", CompressionLevel.Optimal);
        using (var writer = new StreamWriter(chap2Entry.Open(), utf8NoBom))
        {
            writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE html PUBLIC ""-//W3C//DTD XHTML 1.1//EN"" ""http://www.w3.org/TR/xhtml11/DTD/xhtml11.dtd"">
<html xmlns=""http://www.w3.org/1999/xhtml"">
<head><title>Chapter II</title></head>
<body>
  <h1>Chapter II: The Photograph</h1>
  <p>I had seen little of Holmes lately. My marriage had drifted us away from each other. My own complete happiness was sufficient to absorb all my attention.</p>
</body>
</html>");
        }
    }
}
