using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;

namespace ABK_insert
{
    class Program
    {
        enum Signature : uint
        {
            invalid = 0,
            RIFF = 0x46464952,
            smpl = 0x6C706D73,
            data = 0x61746164,

            ABKC = 0x434B4241,
            BNKl = 0x6C4B4E42,
            S10A = 0x41303153,

            PT = 0x5450
        }
        private static int samplerate;
        private static int channels;
        private static int num_of_samples;
        private static long wav_data_offset = 0x2C; // not "data"

        // swap LE - BE
        private static uint byte_swap(uint le)
        {
            return (uint)((int)(le & (uint)0xFF) * 0x1000000 + (int)(le >> 8 & (uint)byte.MaxValue) * 0x10000 + (int)(le >> 16 & (uint)byte.MaxValue) * 256) + (le >> 24 & (uint)0xFF);
        }
        private static uint Align(uint value, uint aligment)
        {
            uint t = aligment - 1;
            return (value + t) & (~t);
        }

        private static void read_wav(string name)
        {
            BinaryReader binaryReader = new BinaryReader((Stream)new FileStream(name, FileMode.Open));
            if (binaryReader.ReadUInt32() != (uint)Signature.RIFF)
            {
                throw new Exception("I need WAV file.");
            }
            binaryReader.BaseStream.Seek(12, SeekOrigin.Current);

            switch (binaryReader.ReadUInt32())
            {
                case 16:
                    if (binaryReader.ReadUInt16() != 1)
                    {
                        throw new Exception("I need WAV file.");
                    }
                    Program.channels = (int)binaryReader.ReadUInt16();
                    Program.samplerate = binaryReader.ReadInt32();
                    binaryReader.BaseStream.Seek(6, SeekOrigin.Current);
                    if (binaryReader.ReadUInt16() != 16)
                    {
                        throw new Exception("I need WAV file.");
                    }
                    break;
                case 40:
                    if (binaryReader.ReadUInt16() != 65534)
                    {
                        throw new Exception("I need WAV file.");
                    }
                    Program.channels = (int)binaryReader.ReadUInt16();
                    Program.samplerate = binaryReader.ReadInt32();
                    binaryReader.BaseStream.Seek(6, SeekOrigin.Current);
                    if (binaryReader.ReadUInt16() != 16)
                    {
                        throw new Exception("I need WAV file.");
                    }
                    binaryReader.BaseStream.Seek(24, SeekOrigin.Current);
                    break;
                default:
                    throw new Exception("I need WAV file.");
            }

            Signature sign = (Signature)binaryReader.ReadUInt32();
            while (sign != Signature.data)
            {
                if (sign == Signature.smpl)
                {
                    binaryReader.BaseStream.Seek(binaryReader.ReadUInt32(), SeekOrigin.Current);
                    sign = (Signature)binaryReader.ReadUInt32();
                }
                else
                {
                    throw new Exception("Data not found.");
                }
            }

            Program.num_of_samples = binaryReader.ReadInt32() / Program.channels / 2;
            wav_data_offset = binaryReader.BaseStream.Position;
            Stream Wav = binaryReader.BaseStream;
            binaryReader.Close();
            Wav.Close();
        }
        enum TAG : byte
        {
            // 6
            // 0x13
            // 0x8A
            SplitB = 0xB,
            Split = 0x80,
            Channels = 0x82,
            Compression = 0x83,
            SampleRate = 0x84,
            NumSamples = 0x85,
            LoopOffset = 0x86,
            LoopLength = 0x87,
            DataStart1 = 0x88,
            DataStart2 = 0x89,
            BytesPerSample = 0x92,
            SplitCompression = 0xA0,
            Skip1 = 0xFC,
            Skip2 = 0xFD,
            Skip3 = 0xFE,
            End = 0xFF
        }
        enum codecs
        {
            ADPCM_EA_XA_R2 = 0, // default
            ADPCM_EA_XAS,
            PCM_S16LE
        }
        class EA_ADPCM_codec
        {
            [System.Runtime.InteropServices.DllImport("EA_ADPCM_codec", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
            public static extern void encode_XAS(byte[] XAS, byte[] in_PCM, uint n_samples_per_channel, uint n_channels);

            [System.Runtime.InteropServices.DllImport("EA_ADPCM_codec", CallingConvention = CallingConvention.Cdecl)]
            public static extern uint GetXASEncodedSize(uint n_samples_per_channel, uint n_channels);

            //public static void encode_XAS
        }

        private readonly string[] codec_names = { "ADPCM EA-XA R2", "ADPCM EA-XAS", "PCM S16LE" };

        private static long CountSoundSize(BinaryReader binaryReader, int samples, codecs codec)
        {
            var start = binaryReader.BaseStream.Position;
            long size;
            switch (codec)
            {
                case codecs.PCM_S16LE:
                    size = samples * 2;
                    break;
                case codecs.ADPCM_EA_XA_R2:
                    const int sizeof_compr_EA_XA_R23_block = 15;
                    const int sizeof_uncompr_EA_XA_R23_block = 61;
                    while (samples > 0)
                    {
                        binaryReader.BaseStream.Seek(
                            (binaryReader.ReadByte() == 0xEE
                                ? sizeof_uncompr_EA_XA_R23_block
                                : sizeof_compr_EA_XA_R23_block
                            ) - 1, SeekOrigin.Current);
                        samples -= 28;
                    }
                    size = binaryReader.BaseStream.Position - start;
                    break;
                default:
                    throw new NotImplementedException();
            }
            // binaryReader.BaseStream.Seek(start, SeekOrigin.Begin);
            return size;
        }

        static int ReadBE(BinaryReader reader, int num_bytes)
        {
            int shift_bytes = 4 - num_bytes;
            int shift = shift_bytes * 8;
            int val = (int)(byte_swap(reader.ReadUInt32() << shift) );
            val = (val << shift) >> shift; // shift sign bit
            reader.BaseStream.Seek(-shift_bytes, SeekOrigin.Current);
            return val;
        }

        static void WriteBE(BinaryWriter writer, int num_bytes, int val)
        {
            writer.Write((byte)num_bytes);
            for (var i = 0; i < num_bytes; i++)
            {
                byte t = (byte)( val >> ((num_bytes - i - 1) * 8) );
                writer.Write(t);
            }
        }

        static void ReWriteBE(BinaryWriter writer, int num_bytes, int val)
        {
            writer.BaseStream.Seek(-(num_bytes + 1), SeekOrigin.Current);
            WriteBE(writer, 4, val);
        }

        private static int InsertInBNKl(BinaryReader IN_ABK_FILE_Reader, BinaryWriter OUT_ABK_Writer,
            int IN_sound_num, int ABK_sfxbanksize, string[] args)
        {
            const string raw_file_name = "xa.raw";
            Stream IN_ABK_FILE = IN_ABK_FILE_Reader.BaseStream;
            Stream OUT_ABK_FILE = OUT_ABK_Writer.BaseStream;
            int ABK_sfxbankoffset = (int)IN_ABK_FILE.Position - 4;
            string sx_codec = "eaxa_blk"; // also known as ADPCM EA-XA R2(3?)

            OUT_ABK_Writer.Write((uint)Signature.BNKl);
            short unk = IN_ABK_FILE_Reader.ReadInt16();
            OUT_ABK_Writer.Write(unk);
            short num_sounds = IN_ABK_FILE_Reader.ReadInt16();
            OUT_ABK_Writer.Write(num_sounds);
            Console.WriteLine("Sounds: " + num_sounds);
            int bnk_size = IN_ABK_FILE_Reader.ReadInt32(); // recalc later
            OUT_ABK_Writer.Write(bnk_size);
            OUT_ABK_Writer.Write(0);
            OUT_ABK_Writer.Write(0);
            IN_ABK_FILE.Seek(8L, SeekOrigin.Current);
            long[] IN_PT_offsets = new long[num_sounds];
            long[] OUT_PT_offsets = new long[num_sounds];
            //long[] DataOffsets = new long[num_sounds];
            for (int index = 0; index < num_sounds; ++index)
            {
                IN_PT_offsets[index] = IN_ABK_FILE.Position + IN_ABK_FILE_Reader.ReadInt32();
                OUT_ABK_Writer.Write(0);
            }

            const int num_channels_supported = 1;
            long[,] IN_data_offsets = new long[num_channels_supported, num_sounds];
            long[,] OUT_data_offset_offsets = new long[num_channels_supported, num_sounds];
            int[] IN_nSamples = new int[num_sounds];
            codecs[] IN_codecs = new codecs[num_sounds];


            // write metadata
            for (int sound_index = 0; sound_index < num_sounds; ++sound_index)
            {
                int Channels = 1;
                int split = 0;
                IN_ABK_FILE.Seek(IN_PT_offsets[sound_index], SeekOrigin.Begin);
                if (IN_ABK_FILE_Reader.ReadInt32() != (int)Signature.PT) // "PT"
                    continue;

                OUT_ABK_FILE.Seek(Align((uint)OUT_ABK_FILE.Position, 4), SeekOrigin.Begin);
                OUT_PT_offsets[sound_index] = OUT_ABK_FILE.Position;
                OUT_ABK_Writer.Write((uint)Signature.PT); // "PT"
                bool is_sample_rate_written = false;
                IN_codecs[sound_index] = codecs.ADPCM_EA_XA_R2;
                while (true)
                {
                    TAG tag;
                    byte length;
                    int value;
                    tag = (TAG)IN_ABK_FILE_Reader.ReadByte();
                    OUT_ABK_Writer.Write((byte)tag);
                    if (tag == TAG.End)
                    {
                        if (sound_index == IN_sound_num && !is_sample_rate_written)
                        {
                            OUT_ABK_FILE.Seek(-1, SeekOrigin.Current);
                            OUT_ABK_Writer.Write((byte)TAG.SampleRate);
                            WriteBE(OUT_ABK_Writer, 4, Program.samplerate);
                            OUT_ABK_Writer.Write((byte)TAG.End);
                        }
                        break;
                    }
                    if (tag >= TAG.Skip1 && tag <= TAG.Skip3)
                        continue;
                    length = IN_ABK_FILE_Reader.ReadByte();
                    value = ReadBE(IN_ABK_FILE_Reader, length);
                    WriteBE(OUT_ABK_Writer, length, value);
                    switch (tag)
                    {
                        case TAG.DataStart1:
                            IN_data_offsets[0, sound_index] = value;
                            OUT_ABK_FILE.Seek(-(length + 1), SeekOrigin.Current);
                            OUT_data_offset_offsets[0, sound_index] = OUT_ABK_FILE.Position + 1;
                            WriteBE(OUT_ABK_Writer, 4, 0);
                            break;
                        case TAG.NumSamples:
                            IN_nSamples[sound_index] = value;
                            break;
                        case TAG.SplitCompression:
                            if (value == 8)
                            {
                                IN_codecs[sound_index] = codecs.PCM_S16LE;
                            }
                            break;
                        default:
                            break;
                    }
                    if (sound_index == IN_sound_num)
                    {
                        switch (tag)
                        {
                            case TAG.SampleRate:
                                is_sample_rate_written = true;
                                ReWriteBE(OUT_ABK_Writer, length, samplerate);
                                break;
                            case TAG.NumSamples:
                                ReWriteBE(OUT_ABK_Writer, length, Program.num_of_samples);
                                break;
                            case TAG.LoopLength:
                                ReWriteBE(OUT_ABK_Writer, length, Program.num_of_samples - 1);
                                break;
                            default:
                                break;
                        }
                    }
                }
            }

            const int PT_size_diff = 16;

            if (OUT_ABK_FILE.Position - IN_ABK_FILE.Position > 16)
            {
                throw new Exception("Large files size difference: " + (OUT_ABK_FILE.Position - IN_ABK_FILE.Position) + " bytes");
            }

            if (IN_codecs[IN_sound_num] == codecs.PCM_S16LE)
            {
                sx_codec = "s16l_blk";
                Console.WriteLine("Using uncompressed PCM");
            }

            // make diff 16 bytes
            OUT_ABK_FILE.Seek(IN_ABK_FILE.Position - OUT_ABK_FILE.Position + 16, SeekOrigin.Current);

            // copy sounds data
            for (int sound_index = 0; sound_index < num_sounds; sound_index++)
            {
                if (IN_data_offsets[0, sound_index] == 0)
                {
                    continue;
                }
                uint out_data_offset = (uint)(OUT_ABK_FILE.Position - ABK_sfxbankoffset);
                if (sound_index == IN_sound_num)
                {
                    Process.Start("sx.exe", "-raw -" + sx_codec + " " + args[1] + " -=" + raw_file_name).WaitForExit();
                    FileStream RAW_FILE = new FileStream(raw_file_name, FileMode.Open);
                    int file_size_aligned = (int)Align((uint)RAW_FILE.Length, 4);
                    byte[] RAW_FILE_Data = new byte[file_size_aligned];
                    RAW_FILE.Read(RAW_FILE_Data, 0, (int)RAW_FILE.Length);
                    RAW_FILE.Close();
                    File.Delete(raw_file_name);
                    OUT_ABK_FILE.Write(RAW_FILE_Data, 0, file_size_aligned);

                    long curr_pos = OUT_ABK_FILE.Position;
                    OUT_ABK_FILE.Seek(OUT_data_offset_offsets[0, sound_index], SeekOrigin.Begin);
                    OUT_ABK_Writer.Write(byte_swap(out_data_offset));
                    OUT_ABK_FILE.Seek(curr_pos, SeekOrigin.Begin);
                }
                else
                {
                    for (int channel_index = 0; channel_index < num_channels_supported; channel_index++)
                    {
                        IN_ABK_FILE.Seek(ABK_sfxbankoffset + IN_data_offsets[channel_index, sound_index], SeekOrigin.Begin);
                        uint size = (uint)CountSoundSize(IN_ABK_FILE_Reader, IN_nSamples[sound_index], IN_codecs[sound_index]);

                        IN_ABK_FILE.Seek(ABK_sfxbankoffset + IN_data_offsets[channel_index, sound_index], SeekOrigin.Begin);

                        uint aligned_size = Align(size, 4);

                        byte[] sound_buf = new byte[aligned_size];
                        IN_ABK_FILE.Read(sound_buf, 0, (int)size);

                        OUT_ABK_FILE.Write(sound_buf, 0, (int)aligned_size);

                        long curr_pos = OUT_ABK_FILE.Position;
                        OUT_ABK_FILE.Seek(OUT_data_offset_offsets[channel_index, sound_index], SeekOrigin.Begin);
                        OUT_ABK_Writer.Write(byte_swap(out_data_offset));
                        OUT_ABK_FILE.Seek(curr_pos, SeekOrigin.Begin);
                    }
                }
            }

            int total_size_diff = (int)OUT_ABK_FILE.Position - (ABK_sfxbankoffset + ABK_sfxbanksize);

            // write offsets to PTs
            OUT_ABK_FILE.Seek(ABK_sfxbankoffset + 0x14, SeekOrigin.Begin);
            for (int index = 0; index < num_sounds; ++index)
            {
                if (OUT_PT_offsets[index] == 0)
                    OUT_ABK_Writer.Write(0);
                else
                    OUT_ABK_Writer.Write((int)(OUT_PT_offsets[index] - OUT_ABK_FILE.Position));
            }

            // change bnk size
            OUT_ABK_FILE.Seek(ABK_sfxbankoffset + 8, SeekOrigin.Begin);
            OUT_ABK_Writer.Write(ABK_sfxbanksize + total_size_diff); // sometimes ABK_sfxbanksize != bnk_size in Underground

            return total_size_diff;
        }


        private static byte[] encodeWavToXAS(string wav_file_name)
        {
            FileStream Wav = new FileStream(wav_file_name, FileMode.Open);
            byte[] samples = new byte[num_of_samples * channels * 2];
            Wav.Seek(wav_data_offset, SeekOrigin.Begin);
            Wav.Read(samples, 0, num_of_samples * 2);
            Wav.Close();

            uint encoded_size = EA_ADPCM_codec.GetXASEncodedSize((uint)num_of_samples, (uint)channels);

            byte[] XAS = new byte[encoded_size];

            //unsafe
            {
                // int* FirstResult = Results[0];
                EA_ADPCM_codec.encode_XAS(XAS, samples, (uint)num_of_samples, (uint)channels);
            }


            Wav.Close();

            return XAS;
        }

        private static int InsertInS10A(BinaryReader IN_ABK_FILE_Reader, BinaryWriter OUT_ABK_Writer,
            int IN_sound_num, int ABK_sfxbanksize, string[] args)
        {
            Stream IN_ABK_FILE = IN_ABK_FILE_Reader.BaseStream;
            Stream OUT_ABK_FILE = OUT_ABK_Writer.BaseStream;
            int ABK_sfxbankoffset = (int)IN_ABK_FILE.Position - 4;

            const int max_samplerate = (1 << 18) - 1;

            if (samplerate > max_samplerate)
            {
                throw new Exception("Max sample rate for S10A is " + max_samplerate);
            }

            OUT_ABK_Writer.Write((uint)Signature.S10A);
            OUT_ABK_Writer.Write(0);

            IN_ABK_FILE.Seek(ABK_sfxbankoffset + 8, SeekOrigin.Begin);

            uint num_sounds_be = IN_ABK_FILE_Reader.ReadUInt32();
            OUT_ABK_Writer.Write(num_sounds_be);
            uint num_sounds = byte_swap(num_sounds_be);

            Console.WriteLine("Sounds: " + num_sounds);

            if (num_sounds <= IN_sound_num)
            {
                throw new Exception("num_sounds <= IN_sound_num");
            }

            IN_ABK_FILE.Seek(IN_sound_num * 4, SeekOrigin.Current);
            uint sound_offset = byte_swap(IN_ABK_FILE_Reader.ReadUInt32());

            if (sound_offset == 0xFFFF_FFFF)
            {
                throw new Exception("sound you are trying to replace is dummy");
            }

            byte[] pre_buf = new byte[sound_offset - 0xC];

            IN_ABK_FILE.Seek(ABK_sfxbankoffset + 0xC, SeekOrigin.Begin);
            IN_ABK_FILE.Read(pre_buf, 0, pre_buf.Length);
            OUT_ABK_FILE.Write(pre_buf, 0, pre_buf.Length);


            /* EA SNR/SPH header */
            //uint header1 = byte_swap(IN_ABK_FILE_Reader.ReadUInt32());
            //uint header2 = byte_swap(IN_ABK_FILE_Reader.ReadUInt32());
            //uint version = (header1 >> 28) & 0x0F; /* 4 bits */
            //uint _codec = (header1 >> 24) & 0x0F; /* 4 bits */
            //uint channel_config = (header1 >> 18) & 0x3F; /* 6 bits */
            //uint sample_rate = (header1 >> 0) & 0x03FFFF; /* 18 bits */
            //uint type = (header2 >> 30) & 0x03; /* 2 bits */
            //uint loop_flag = (header2 >> 29) & 0x01; /* 1 bit */
            //uint num_samples = (header2 >> 0) & 0x1FFFFFFF; /* 29 bits */

            // ---------
            uint data = IN_ABK_FILE_Reader.ReadUInt32();

            byte codec = (byte)(data & 0xF);

            if (codec != 4)
            {
                Console.WriteLine("Unknown codec: " + codec);
                Console.WriteLine("Trying to use XAS");
            }

            data = 4 | byte_swap((uint)samplerate) | (uint)((channels - 1) << 10);

            OUT_ABK_Writer.Write(data);

            data = IN_ABK_FILE_Reader.ReadUInt32();
            byte flags = (byte)(data & 0xF0);
            uint orig_samples = byte_swap((uint)(data & (~0xF0)));
            OUT_ABK_Writer.Write(byte_swap((uint)num_of_samples) | flags);


            if ((flags & 0x20) != 0) // loop flag
            {
                IN_ABK_FILE_Reader.ReadUInt32();
                OUT_ABK_Writer.Write(0); // loop start
                if ((flags & 0x40) != 0) // EAAC_TYPE_STREAM
                {
                    IN_ABK_FILE_Reader.ReadUInt32();
                    OUT_ABK_Writer.Write(0); // loop offset
                }
            }
            // ---------

            int orig_size = 0; // including block's headres

            for (uint samples_count = 0; samples_count < orig_samples;)
            {
                int block_size = (int)byte_swap(IN_ABK_FILE_Reader.ReadUInt32());
                orig_size += block_size;
                samples_count += byte_swap(IN_ABK_FILE_Reader.ReadUInt32());
                IN_ABK_FILE.Seek(block_size - 8, SeekOrigin.Current);
            }

            byte[] XAS = encodeWavToXAS(args[1]);

            int new_sound_size = XAS.Length + 8;
            OUT_ABK_Writer.Write(byte_swap((uint)new_sound_size));
            OUT_ABK_Writer.Write(byte_swap((uint)num_of_samples));
            OUT_ABK_FILE.Write(XAS, 0, XAS.Length);

            int new_bank_size = (new_sound_size - orig_size) + ABK_sfxbanksize;
            int new_bank_size_aligned = (int)Align((uint)new_bank_size, 16);
            int unalign_size_diff = new_sound_size - orig_size;
            int total_size_diff = new_bank_size_aligned - ABK_sfxbanksize;

            int align_diff = new_bank_size_aligned - new_bank_size;

            int remain_size = ABK_sfxbanksize - ((int)IN_ABK_FILE.Position - ABK_sfxbankoffset);

            byte[] post_buf = new byte[remain_size + align_diff];
            IN_ABK_FILE.Read(post_buf, 0, remain_size);
            OUT_ABK_FILE.Write(post_buf, 0, post_buf.Length);

            int pos = ABK_sfxbankoffset + 0xC + (IN_sound_num + 1) * 4;
            OUT_ABK_FILE.Seek(pos, SeekOrigin.Begin);
            IN_ABK_FILE.Seek(pos, SeekOrigin.Begin);

            for (int sound_index = IN_sound_num + 1; sound_index < num_sounds; sound_index++)
            {
                uint BE = IN_ABK_FILE_Reader.ReadUInt32();
                if (BE != 0xFFFF_FFFF)
                {
                    int LE = (int)byte_swap(BE) + unalign_size_diff;
                    BE = byte_swap((uint)LE);
                }
                OUT_ABK_Writer.Write(BE);
            }

            return total_size_diff;
        }

        private static void Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("ABK_insert [ABK file] [wav file] [sample N to replace]");
                return;
            }

            int IN_sound_num = Convert.ToInt32(args[2]);
            FileStream IN_ABK_FILE = new FileStream(args[0], FileMode.Open);
            BinaryReader IN_ABK_FILE_Reader = new BinaryReader(IN_ABK_FILE);
            FileStream OUT_ABK_FILE = new FileStream(Path.GetFileNameWithoutExtension(args[0]) + "m.abk", FileMode.Create);
            BinaryWriter OUT_ABK_Writer = new BinaryWriter(OUT_ABK_FILE);
            BinaryReader OUT_ABK_Reader = new BinaryReader(OUT_ABK_FILE);
            Program.read_wav(args[1]);
            if (IN_ABK_FILE_Reader.ReadInt32() != (uint)Signature.ABKC)
            {
                Console.WriteLine("ABKC signature not found.");
                return;
            }

            IN_ABK_FILE.Seek(16, SeekOrigin.Current);
            int IN_ABK_size = IN_ABK_FILE_Reader.ReadInt32();
            IN_ABK_FILE.Seek(8, SeekOrigin.Current);
            int ABK_sfxbankoffset = IN_ABK_FILE_Reader.ReadInt32();
            int ABK_sfxbanksize = IN_ABK_FILE_Reader.ReadInt32();
            IN_ABK_FILE.Seek(8, SeekOrigin.Current);
            int ABK_funcfixupoffset = IN_ABK_FILE_Reader.ReadInt32();
            int ABK_staticdatafixupoffset = IN_ABK_FILE_Reader.ReadInt32();
            int ABK_interfaceOffset = IN_ABK_FILE_Reader.ReadInt32();
            IN_ABK_FILE.Seek(0, SeekOrigin.Begin);
            byte[] headers_buf = new byte[ABK_sfxbankoffset];
            IN_ABK_FILE.Read(headers_buf, 0, ABK_sfxbankoffset);
            OUT_ABK_FILE.Write(headers_buf, 0, ABK_sfxbankoffset);
            Signature bank_signature = (Signature)IN_ABK_FILE_Reader.ReadInt32();
            int total_size_diff;
            switch (bank_signature)
            {
                case Signature.BNKl:
                    {
                        Console.WriteLine("Bank format: BNKl");
                        total_size_diff = InsertInBNKl(IN_ABK_FILE_Reader, OUT_ABK_Writer, IN_sound_num, ABK_sfxbanksize, args);
                        break;
                    }
                case Signature.S10A:
                    {
                        Console.WriteLine("Bank format: S10A");
                        total_size_diff = InsertInS10A(IN_ABK_FILE_Reader, OUT_ABK_Writer, IN_sound_num, ABK_sfxbanksize, args);
                        break;
                    }
                default:
                    {
                        throw new Exception("Uknown bank signature");
                    }
            }

            OUT_ABK_FILE.Seek(0, SeekOrigin.End);
            IN_ABK_FILE.Seek(ABK_sfxbankoffset + ABK_sfxbanksize, SeekOrigin.Begin);
            // abk's tail
            int count3 = (int)IN_ABK_FILE.Length - ABK_funcfixupoffset;
            byte[] buffer3 = new byte[count3];
            IN_ABK_FILE.Read(buffer3, 0, count3);
            OUT_ABK_FILE.Write(buffer3, 0, count3);

            // total size
            OUT_ABK_FILE.Seek(0x14, SeekOrigin.Begin);
            OUT_ABK_Writer.Write(IN_ABK_size + total_size_diff); // 

            // sfxbanksize
            OUT_ABK_FILE.Seek(0x24, SeekOrigin.Begin);
            OUT_ABK_Writer.Write(ABK_sfxbanksize + total_size_diff);

            OUT_ABK_FILE.Seek(0x30, SeekOrigin.Begin);
            OUT_ABK_Writer.Write(ABK_funcfixupoffset + total_size_diff); // funcfixupoffset
            OUT_ABK_Writer.Write(ABK_staticdatafixupoffset + total_size_diff); // staticdatafixupoffset
            OUT_ABK_Writer.Write(ABK_interfaceOffset + total_size_diff); // interfaceOffset


            // offsets in interface
            OUT_ABK_FILE.Seek(ABK_interfaceOffset + total_size_diff, SeekOrigin.Begin);
            int num10 = OUT_ABK_Reader.ReadInt32();
            for (int index = 0; index < num10; ++index)
            {
                OUT_ABK_Reader.ReadInt32();
                int offset = OUT_ABK_Reader.ReadInt32();
                OUT_ABK_FILE.Seek(-4, SeekOrigin.Current);
                OUT_ABK_Writer.Write(offset + total_size_diff);
                OUT_ABK_Reader.ReadInt32();
            }

            IN_ABK_FILE_Reader.Close();
            IN_ABK_FILE.Close();
            OUT_ABK_Writer.Close();
            OUT_ABK_FILE.Close();
        }

    }
}
