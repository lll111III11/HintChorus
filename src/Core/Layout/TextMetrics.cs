using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;

namespace HintChorus.Core.Layout;

public static class TextMetrics
{
	private const string ResourceName = "HintChorus.textwidth.bin";

	public const float ReferenceFontSize = 100f;

	private static float[] _palette = Array.Empty<float>();

	private static int[] _codePoints = Array.Empty<int>();

	private static byte[] _slot = Array.Empty<byte>();

	private static float[] _blockAdvance = Array.Empty<float>();

	public static bool IsLoaded { get; private set; }

	public static int EntryCount { get; private set; }

	public static int PaletteSize { get; private set; }

	public static int PackedBytes { get; private set; }

	public static float FullWidthAdvance { get; private set; }

	public static float MeanAdvance { get; private set; }

	public static bool Load()
	{
		if (IsLoaded)
		{
			return true;
		}
		try
		{
			using Stream stream = typeof(TextMetrics).Assembly.GetManifestResourceStream("HintChorus.textwidth.bin");
			if (stream == null)
			{
				Logger.Error((object)"[HintChorus] 未找到内嵌度量表 HintChorus.textwidth.bin, 排版将退回估算模式");
				return false;
			}
			using MemoryStream memoryStream = new MemoryStream();
			stream.CopyTo(memoryStream);
			PackedBytes = (int)memoryStream.Length;
			memoryStream.Position = 0L;
			using DeflateStream deflateStream = new DeflateStream(memoryStream, CompressionMode.Decompress);
			using MemoryStream memoryStream2 = new MemoryStream();
			deflateStream.CopyTo(memoryStream2);
			memoryStream2.Position = 0L;
			using BinaryReader binaryReader = new BinaryReader(memoryStream2, Encoding.UTF8);
			byte[] array = binaryReader.ReadBytes(4);
			if (array.Length != 4 || array[0] != 72 || array[1] != 73 || array[2] != 84 || array[3] != 87)
			{
				Logger.Error((object)"[HintChorus] 度量表魔数不匹配, 排版将退回估算模式");
				return false;
			}
			binaryReader.ReadByte();
			int num = binaryReader.ReadUInt16();
			_palette = new float[num];
			double num2 = 0.0;
			for (int i = 0; i < num; i++)
			{
				_palette[i] = binaryReader.ReadSingle();
				num2 += (double)_palette[i];
			}
			PaletteSize = num;
			int num3 = binaryReader.ReadInt32();
			int count = binaryReader.ReadInt32();
			byte[] array2 = binaryReader.ReadBytes(count);
			byte[] slot = binaryReader.ReadBytes(num3);
			_codePoints = new int[num3];
			_slot = slot;
			int num4 = -1;
			int num5 = 0;
			for (int j = 0; j < num3; j++)
			{
				int num6 = 0;
				int num7 = 0;
				while (true)
				{
					byte b = array2[num5++];
					num6 |= (b & 0x7F) << num7;
					if ((b & 0x80) == 0)
					{
						break;
					}
					num7 += 7;
				}
				num4 += num6;
				_codePoints[j] = num4;
			}
			EntryCount = num3;
			ComputeBlockAdvances();
			FullWidthAdvance = DominantWidth();
			MeanAdvance = ((num == 0) ? 0f : ((float)(num2 / (double)num)));
			IsLoaded = true;
			StartupLog.Info($"[HintChorus] 文字度量表已载入: {EntryCount} 个字符, " + $"{PaletteSize} 色调色板, {(float)PackedBytes / 1024f:F1} KB(压缩)");
			return true;
		}
		catch (Exception ex)
		{
			Logger.Error((object)("[HintChorus] 载入度量表失败, 排版将退回估算模式: " + ex.Message));
			return false;
		}
	}

	public static float GetAdvance(int codePoint)
	{
		if (!IsLoaded)
		{
			return FallbackAdvance(codePoint);
		}
		int num = IndexOf(codePoint);
		if (num < 0)
		{
			return FallbackAdvance(codePoint);
		}
		return _palette[_slot[num]];
	}

	public static float MeasureRaw(string? text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return 0f;
		}
		float num = 0f;
		for (int i = 0; i < text.Length; i++)
		{
			char c = text[i];
			if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
			{
				num += GetAdvance(char.ConvertToUtf32(c, text[i + 1]));
				i++;
			}
			else
			{
				num += GetAdvance(c);
			}
		}
		return num;
	}

	public static float MeasureAtSize(string? text, float fontSize)
	{
		return MeasureRaw(text) * (fontSize / 100f);
	}

	public static void Unload()
	{
		_palette = Array.Empty<float>();
		_codePoints = Array.Empty<int>();
		_slot = Array.Empty<byte>();
		IsLoaded = false;
		EntryCount = 0;
		PaletteSize = 0;
		PackedBytes = 0;
	}

	private static int IndexOf(int codePoint)
	{
		int num = 0;
		int num2 = _codePoints.Length - 1;
		while (num <= num2)
		{
			int num3 = num + (num2 - num >> 1);
			int num4 = _codePoints[num3];
			if (num4 == codePoint)
			{
				return num3;
			}
			if (num4 < codePoint)
			{
				num = num3 + 1;
			}
			else
			{
				num2 = num3 - 1;
			}
		}
		return -1;
	}

	private static void ComputeBlockAdvances()
	{
		double[] array = new double[256];
		int[] array2 = new int[256];
		for (int i = 0; i < _codePoints.Length; i++)
		{
			int num = _codePoints[i] >> 8;
			if (num >= 0 && num < 256)
			{
				array[num] += _palette[_slot[i]];
				array2[num]++;
			}
		}
		_blockAdvance = new float[256];
		for (int j = 0; j < 256; j++)
		{
			_blockAdvance[j] = ((array2[j] >= 4) ? ((float)(array[j] / (double)array2[j])) : 0f);
		}
	}

	private static float DominantWidth()
	{
		if (_palette.Length == 0)
		{
			return 0f;
		}
		int[] array = new int[_palette.Length];
		byte[] slot = _slot;
		foreach (byte b in slot)
		{
			array[b]++;
		}
		int num = 0;
		for (int j = 1; j < array.Length; j++)
		{
			if (array[j] > array[num])
			{
				num = j;
			}
		}
		return _palette[num];
	}

	private static float FallbackAdvance(int codePoint)
	{
		int num = codePoint >> 8;
		if (num >= 0 && num < _blockAdvance.Length)
		{
			float num2 = _blockAdvance[num];
			if (num2 > 0f)
			{
				return num2;
			}
		}
		if (IsWideScript(codePoint))
		{
			if (!(FullWidthAdvance > 0f))
			{
				return 63.05f;
			}
			return FullWidthAdvance;
		}
		if (!(MeanAdvance > 0f))
		{
			return 36.5f;
		}
		return MeanAdvance;
	}

	private static bool IsWideScript(int cp)
	{
		if ((cp < 4352 || cp > 4447) && (cp < 11904 || cp > 12350) && (cp < 12353 || cp > 13311) && (cp < 13312 || cp > 19903) && (cp < 19968 || cp > 40959) && (cp < 40960 || cp > 42191) && (cp < 44032 || cp > 55203) && (cp < 63744 || cp > 64255) && (cp < 65072 || cp > 65135) && (cp < 65280 || cp > 65376) && (cp < 65504 || cp > 65510))
		{
			if (cp >= 127744)
			{
				return cp <= 129791;
			}
			return false;
		}
		return true;
	}
}
