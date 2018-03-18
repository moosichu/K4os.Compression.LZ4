﻿using System;
using K4os.Compression.LZ4.Encoders;
using K4os.Compression.LZ4.Internal;
using Xunit;
using Xunit.Abstractions;

// ReSharper disable InlineOutVariableDeclaration

namespace K4os.Compression.LZ4.Test
{
	public unsafe class LZ4EncoderTests
	{
		public LZ4EncoderTests(ITestOutputHelper output) { }

		[Theory]
		[InlineData(1024, 50, 0)]
		[InlineData(1024, 1024, 0)]
		[InlineData(1024, 1026, 0)]
		[InlineData(1024, 1100, 0)]
		[InlineData(1024, 0x10000, 0)]
		[InlineData(1024, 0x20000, 100)]
		public void SmallBlocksWithNoShift(int blockSize, int totalSize, int extraBlocks)
		{
			Assert.Equal(
				FastStreamManual(blockSize, totalSize),
				FastStreamEncoder(blockSize, totalSize, extraBlocks));
		}

		[Theory]
		[InlineData(0x10000, 50, 0)]
		[InlineData(0x10000, 0x10000, 0)]
		[InlineData(0x10000, 0x20000, 5)]
		[InlineData(0x10000, 0x50000, 5)]
		public void MediumBlocksWithNoShift(int blockSize, int totalSize, int extraBlocks)
		{
			Assert.Equal(
				FastStreamManual(blockSize, totalSize),
				FastStreamEncoder(blockSize, totalSize, extraBlocks));
		}

		[Theory]
		[InlineData(0x20000, 0x50000, 1)]
		[InlineData(0x20000, 0x100000, 1)]
		public void LargeBlocksWithDictShifting(int blockSize, int totalSize, int extraBlocks)
		{
			Assert.Equal(
				FastStreamManual(blockSize, totalSize),
				FastStreamEncoder(blockSize, totalSize, extraBlocks));
		}

		public uint FastStreamEncoder(int blockLength, int sourceLength, int extraBlocks = 0)
		{
			sourceLength = Mem.RoundUp(sourceLength, blockLength);
			var targetLength = 2 * sourceLength;

			var source = new byte[sourceLength];
			var target = new byte[targetLength];

			Lorem.Fill(source, 0, source.Length);

			using (var encoder = new LZ4FastStreamEncoder(blockLength, extraBlocks))
			{
				var sourceP = 0;
				var targetP = 0;

				while (sourceP < sourceLength && targetP < targetLength)
				{
					encoder.TopupAndEncode(
						source,
						sourceP,
						Math.Min(blockLength, sourceLength - sourceP),
						target,
						targetP,
						targetLength - targetP,
						true,
						out var loaded,
						out var encoded);
					sourceP += loaded;
					targetP += encoded;
				}

				return Tools.Adler32(target, 0, targetP);
			}
		}

		public uint FastStreamManual(int blockLength, int sourceLength)
		{
			sourceLength = Mem.RoundUp(sourceLength, blockLength);
			var targetLength = 2 * sourceLength;

			var context = (LZ4_xx.LZ4_stream_t*) Mem.AllocZero(sizeof(LZ4_xx.LZ4_stream_t));
			var source = (byte*) Mem.Alloc(sourceLength);
			var target = (byte*) Mem.Alloc(targetLength);

			try
			{
				Lorem.Fill(source, sourceLength);

				var sourceP = 0;
				var targetP = 0;

				while (sourceP < sourceLength && targetP < targetLength)
				{
					targetP += LZ4_64.LZ4_compress_fast_continue(
						context,
						source + sourceP,
						target + targetP,
						Math.Min(blockLength, sourceLength - sourceP),
						targetLength - targetP,
						1);
					sourceP += blockLength;
				}

				return Tools.Adler32(target, targetP);
			}
			finally
			{
				Mem.Free(context);
				Mem.Free(source);
				Mem.Free(target);
			}
		}
	}
}
