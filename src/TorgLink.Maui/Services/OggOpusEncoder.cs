using System.Buffers.Binary;
using Concentus;
using Concentus.Enums;
using Concentus.Oggfile;

namespace TorgLink.Maui.Services;

internal static class OggOpusEncoder
{
    private const int OpusSampleRate = 48_000;
    private const int MinAudiblePeak = 32;

    public static byte[] EncodeWavPcm(string wavPath, int bitrateBps = MediaEconomy.DefaultSpeechBitrateBps)
    {
        ArgumentException.ThrowIfNullOrEmpty(wavPath);
        return EncodeWavPcm(File.ReadAllBytes(wavPath), bitrateBps);
    }

    public static byte[] EncodeWavPcm(byte[] wavBytes, int bitrateBps = MediaEconomy.DefaultSpeechBitrateBps)
    {
        ArgumentNullException.ThrowIfNull(wavBytes);
        var (samples, sampleRate, channels) = ReadWavPcm16(wavBytes);
        if (samples.Length == 0)
            throw new InvalidOperationException("WAV is empty.");

        var mono = channels == 1 ? samples : MixToMono(samples, channels);
        EnsureAudible(mono);

        using var output = new MemoryStream();
        var encoder = OpusCodecFactory.CreateEncoder(OpusSampleRate, 1, OpusApplication.OPUS_APPLICATION_VOIP);
        encoder.Bitrate = Math.Clamp(bitrateBps, MediaEconomy.MinVoiceBitrateBps, 64_000);
        var tags = new OpusTags();
        var ogg = new OpusOggWriteStream(encoder, output, tags, sampleRate);
        ogg.WriteSamples(mono, 0, mono.Length);
        ogg.Finish();
        var bytes = output.ToArray();
        if (bytes.Length == 0)
            throw new InvalidOperationException("Пустой выход кодера.");
        return bytes;
    }

    private static void EnsureAudible(short[] mono)
    {
        var peak = 0;
        for (var i = 0; i < mono.Length; i++)
        {
            var a = Math.Abs(mono[i]);
            if (a > peak)
                peak = a;
        }

        if (peak < MinAudiblePeak)
            throw new InvalidOperationException(
                "Запись без звука (микрофон молчит или недоступен). Проверьте доступ к микрофону.");
    }

    private static short[] MixToMono(short[] interleaved, int channels)
    {
        var frames = interleaved.Length / channels;
        var mono = new short[frames];
        for (var i = 0; i < frames; i++)
        {
            var acc = 0;
            var baseIdx = i * channels;
            for (var c = 0; c < channels; c++)
                acc += interleaved[baseIdx + c];
            mono[i] = (short)(acc / channels);
        }

        return mono;
    }

    private static (short[] samples, int sampleRate, int channels) ReadWavPcm16(byte[] bytes)
    {
        if (bytes.Length < 44)
            throw new InvalidOperationException("WAV header is too short.");
        if (bytes[0] != (byte)'R' || bytes[1] != (byte)'I' || bytes[2] != (byte)'F' || bytes[3] != (byte)'F')
            throw new InvalidOperationException("Not a RIFF WAV file.");

        var sampleRate = 0;
        var channels = 0;
        var bits = 0;
        var dataOffset = -1;
        var dataLen = 0;
        var cursor = 12;
        while (cursor + 8 <= bytes.Length)
        {
            var chunkId = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(cursor));
            var chunkSize = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(cursor + 4));
            var payload = cursor + 8;
            if (chunkSize < 0 || payload + chunkSize > bytes.Length)
                break;
            if (chunkId == 0x20746D66) // "fmt "
            {
                var format = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(payload));
                channels = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(payload + 2));
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(payload + 4));
                bits = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(payload + 14));
                // 1 = PCM, 0xFFFE = WAVE_FORMAT_EXTENSIBLE (treat as PCM when subtype is PCM)
                if (format is not (1 or 0xFFFE))
                    throw new InvalidOperationException("Only PCM WAV is supported.");
                if (format == 0xFFFE && chunkSize >= 40)
                {
                    // SubFormat GUID at offset 24 in extensible fmt; PCM = 00000001-0000-0010-8000-00aa00389b71
                    var sub = bytes.AsSpan(payload + 24, 16);
                    if (BinaryPrimitives.ReadUInt32LittleEndian(sub) != 1)
                        throw new InvalidOperationException("Only PCM WAV is supported.");
                }
            }
            else if (chunkId == 0x61746164) // "data"
            {
                dataOffset = payload;
                dataLen = chunkSize;
                break;
            }

            cursor = payload + chunkSize + (chunkSize & 1);
        }

        if (dataOffset < 0 || channels is < 1 or > 2 || bits != 16 || sampleRate is < 8000 or > 96_000)
            throw new InvalidOperationException("Unsupported WAV format.");

        var count = dataLen / 2;
        var samples = new short[count];
        for (var i = 0; i < count; i++)
            samples[i] = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(dataOffset + i * 2));
        return (samples, sampleRate, channels);
    }
}
