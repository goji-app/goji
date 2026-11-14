using System.Text;
using SevenZip.Compression.LZMA;

// Упаковщик payload установщика Godji VPN: рекурсивно берёт файлы каталога (publish_stage)
// и пишет единый контейнер payload.bin, сжатый LZMA (словарь 64 МБ, fastbytes 273 —
// профиль прежнего Inno Setup lzma2/max). Контейнер:
//
//   header (не сжат): magic "GOJIPAYL" | int32 count | int64 totalFilesBytes | int64 rawStreamSize
//   5 байт свойств LZMA (WriteCoderProperties)
//   lzma-поток:       для каждого файла: int32 pathLen | utf8 path | int64 lastWriteTicks |
//                     int64 dataLen | data
//
// Header и свойства хранятся открытыми: установщик показывает «Размер/Файлов» на первом
// экране без распаковки, а Decoder.Code получает точные in/out размеры. LZMA на этом
// контенте (большие .NET DLL + Go-бинарники) даёт ~330 МБ → ~90 МБ; BrotliStream из .NET
// не годится — окно не настраивается и ratio почти вдвое хуже. Кодек — LZMA-SDK,
// один и тот же пакет кодирует (здесь) и декодирует (в Setup).
// Запуск: dotnet run --project installer/PayloadTool -- <srcDir> <outFile>  (минуты)

if (args.Length >= 2 && args[0] == "test")
{
    // Тест сжатия одного файла — подбор параметров для payload.
    var f = await File.ReadAllBytesAsync(args[1]);
    var ms = new MemoryStream();
    var sw = System.Diagnostics.Stopwatch.StartNew();
    Encode(new MemoryStream(f), ms);
    sw.Stop();
    Console.WriteLine($"lzma9 d64: {f.Length / 1024 / 1024} МБ -> {ms.Length / 1024 / 1024} МБ, {sw.Elapsed.TotalMinutes:0}:{sw.Elapsed.Seconds:00}");

    // Round-trip: декод тем же путём, что в InstallerEngine (5 байт свойств + точные размеры).
    ms.Position = 0;
    var props = new byte[5];
    if (ms.Read(props, 0, 5) != 5) throw new IOException("props");
    var back = new MemoryStream();
    var decoder = new SevenZip.Compression.LZMA.Decoder();
    decoder.SetDecoderProperties(props);
    decoder.Code(ms, back, ms.Length - 5, f.Length, null);
    var sha = System.Security.Cryptography.SHA256.HashData(back.ToArray());
    var sha2 = System.Security.Cryptography.SHA256.HashData(f);
    Console.WriteLine(sha.SequenceEqual(sha2) ? "ROUND-TRIP OK" : "ROUND-TRIP MISMATCH");
    return sha.SequenceEqual(sha2) ? 0 : 1;
}

// decodecheck <payload.bin> — тот же путь декодирования, что в InstallerEngine:
// header 28 байт + 5 байт свойств, Decoder.Code точными размерами, разбор записей.
// Позволяет проверить контейнер без WPF-установщика и UAC.
if (args.Length == 2 && args[0] == "decodecheck")
{
    using var fs = File.OpenRead(args[1]);
    var cHeader = new byte[8 + 4 + 8 + 8];
    fs.ReadExactly(cHeader);
    if (Encoding.ASCII.GetString(cHeader, 0, 8) != "GOJIPAYL") { Console.Error.WriteLine("bad magic"); return 1; }
    int cCount = BitConverter.ToInt32(cHeader, 8);
    long cFilesBytes = BitConverter.ToInt64(cHeader, 12);
    long cRaw = BitConverter.ToInt64(cHeader, 20);
    var cProps = new byte[5];
    fs.ReadExactly(cProps);
    long cCompressed = fs.Length - cHeader.Length - cProps.Length;
    Console.WriteLine($"count={cCount} filesBytes={cFilesBytes} raw={cRaw} compressed={cCompressed}");

    var tempPath = Path.Combine(Path.GetTempPath(), $"payload-check-{Guid.NewGuid():N}.tmp");
    var cWatch = System.Diagnostics.Stopwatch.StartNew();
    try
    {
        await using (var temp = File.Create(tempPath, 1024 * 1024, FileOptions.DeleteOnClose))
        {
            var decoder = new SevenZip.Compression.LZMA.Decoder();
            decoder.SetDecoderProperties(cProps);
            decoder.Code(fs, temp, cCompressed, cRaw, null);
            cWatch.Stop();
            Console.WriteLine($"decode OK: {temp.Length} bytes, {cWatch.Elapsed.TotalSeconds:0} s");
            temp.Position = 0;
            using var br = new BinaryReader(temp, Encoding.UTF8, leaveOpen: true);
            long filesSum = 0;
            for (int i = 0; i < cCount; i++)
            {
                int pathLen = br.ReadInt32();
                string rel = Encoding.UTF8.GetString(br.ReadBytes(pathLen));
                br.ReadInt64();
                long dataLen = br.ReadInt64();
                br.ReadBytes((int)dataLen);
                filesSum += dataLen;
                if (i < 3 || i >= cCount - 3) Console.WriteLine($"  entry {i}: {rel} ({dataLen})");
            }
            Console.WriteLine($"records OK, filesBytes sum={filesSum} (header {cFilesBytes})");
        }
    }
    finally
    {
        try { File.Delete(tempPath); } catch { }
    }
    return 0;
}

if (args.Length != 2)
{
    Console.Error.WriteLine("Использование: PayloadTool <srcDir> <outFile> | test <file> | decodecheck <payload.bin>");
    return 1;
}

var src = Path.GetFullPath(args[0]);
var outPath = Path.GetFullPath(args[1]);

var files = Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories)
    .Select(f => (Full: f, Rel: Path.GetRelativePath(src, f).Replace('\\', '/')))
    .OrderBy(x => x.Rel, StringComparer.Ordinal)
    .ToList();

long totalFilesBytes = files.Sum(x => new FileInfo(x.Full).Length);
long rawStreamSize = 0;
foreach (var (_, rel) in files)
    rawStreamSize += 4 + Encoding.UTF8.GetByteCount(rel) + 8 + 8 + new FileInfo(Path.Combine(src, rel)).Length;

await using var outStream = File.Create(outPath);

// Открытый header: magic + счётчики.
outStream.Write("GOJIPAYL"u8);
outStream.Write(BitConverter.GetBytes(files.Count));
outStream.Write(BitConverter.GetBytes(totalFilesBytes));
outStream.Write(BitConverter.GetBytes(rawStreamSize));

var watch = System.Diagnostics.Stopwatch.StartNew();

// Сырой поток записей → LZMA. Запись строго попарно «заголовок + данные» на файл —
// читатель в установщике идёт ровно в этом порядке. (Когда-то заголовки писались
// отдельным проходом перед данными: поток рассинхронился после первой записи,
// распаковка получала мусорные пути и размеры.)
var rawStream = new MemoryStream((int)Math.Min(int.MaxValue, rawStreamSize));
using (var bw = new BinaryWriter(rawStream, Encoding.UTF8, leaveOpen: true))
{
    var buffer = new byte[1024 * 1024];
    foreach (var (full, rel) in files)
    {
        var pathBytes = Encoding.UTF8.GetBytes(rel);
        var fi = new FileInfo(full);
        bw.Write(pathBytes.Length);
        bw.Write(pathBytes);
        bw.Write(fi.LastWriteTimeUtc.Ticks);
        bw.Write(fi.Length);

        using var fs = File.OpenRead(full);
        int read;
        while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
            bw.Write(buffer, 0, read);
        if (fs.Length != fi.Length) throw new IOException($"Файл изменился при упаковке: {rel}");
    }
}

rawStream.Position = 0;
Encode(rawStream, outStream);

watch.Stop();
Console.WriteLine($"PayloadTool: файлов {files.Count}, исходно {totalFilesBytes / 1024 / 1024} МБ -> payload.bin {outStream.Length / 1024 / 1024} МБ, {watch.Elapsed.Minutes}:{watch.Elapsed.Seconds:00}");
return 0;

static void Encode(Stream rawStream, Stream target)
{
    var encoder = new SevenZip.Compression.LZMA.Encoder();
    // Профиль 7z -md=64m -fb=273 (уровень 9, BT4): максимум сжатия при разумном времени.
    var propIDs = new[]
    {
        SevenZip.CoderPropID.DictionarySize,
        SevenZip.CoderPropID.PosStateBits,
        SevenZip.CoderPropID.LitContextBits,
        SevenZip.CoderPropID.LitPosBits,
        SevenZip.CoderPropID.Algorithm,
        SevenZip.CoderPropID.NumFastBytes,
        SevenZip.CoderPropID.MatchFinder
    };
    var properties = new object[]
    {
        (Int32)(64 * 1024 * 1024),
        (Int32)2,
        (Int32)3,
        (Int32)0,
        (Int32)2,
        (Int32)273,
        "BT4"
    };
    encoder.SetCoderProperties(propIDs, properties);
    encoder.WriteCoderProperties(target);
    encoder.Code(rawStream, target, rawStream.Length, -1, null);
}
