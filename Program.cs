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
                if(sign == Signature.smpl)
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
        enum KEY : byte
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

        private static int InsertInBNKl(BinaryReader IN_ABK_FILE_Reader, BinaryWriter OUT_ABK_Writer,
            int IN_sound_num, int ABK_sfxbanksize, string[] args)
        {
            const string tmp_file_name = "xa.raw";
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
                OUT_ABK_Writer.Write(0x5450); // "PT"
                bool is_sample_rate_written = false;
                // proc keys
                while (true)
                {
                    KEY key;
                    int value;

                    bool custom_value;
                    custom_value = false;
                    key = (KEY)IN_ABK_FILE_Reader.ReadByte();
                    if (key == KEY.End)
                    {
                        if (sound_index == IN_sound_num && !is_sample_rate_written)
                        {
                            OUT_ABK_Writer.Write((byte)KEY.SampleRate);
                            OUT_ABK_Writer.Write((byte)4);
                            OUT_ABK_Writer.Write(Program.byte_swap((uint)Program.samplerate));
                        }
                        OUT_ABK_Writer.Write((byte)key); // KEY.End
                        if (Channels <= 1 || split == 2)
                            break;
                        else
                            Console.WriteLine("Unsupported stereo split mode");
                    }
                    else
                    {
                        OUT_ABK_Writer.Write((byte)key);
                        if (key == KEY.DataStart1)
                            custom_value = true;
                        else if ((key == KEY.SampleRate || key == KEY.NumSamples || key == KEY.LoopLength) && sound_index == IN_sound_num)
                            custom_value = true;
                    }
                    if (key == KEY.Skip1 || key == KEY.Skip2 || key == KEY.Skip3)
                        continue;

                    byte num_bytes = IN_ABK_FILE_Reader.ReadByte();
                    if (!custom_value)
                        OUT_ABK_Writer.Write(num_bytes);
                    value = 0; // 1 - 4 byte(s)
                    for (int byte_index = 0; byte_index < num_bytes; ++byte_index)
                    {
                        byte byte_of_data = IN_ABK_FILE_Reader.ReadByte();
                        value = (value << 8) + byte_of_data;
                        if (!custom_value)
                            OUT_ABK_Writer.Write(byte_of_data);
                    }
                    // if (dictionary.ContainsKey(key)) { string str = " (" + dictionary[key] + ")"; }
                    switch (key)
                    {
                        case KEY.Split:
                            split = value;
                            break;
                        case KEY.Channels:
                            Channels = value;
                            break;
                        case KEY.SplitCompression:
                            if (sound_index == IN_sound_num)
                            {
                                // value = 0xA = default 
                                if (value == 8)
                                {
                                    sx_codec = "s16l_blk";
                                    Console.WriteLine("Using uncompressed PCM");
                                }
                                else
                                {
                                    Console.WriteLine("Unknown Split Compression: " + value);
                                }
                            }
                            break;
                        case KEY.DataStart1:

                            OUT_ABK_Writer.Write((byte)4);
                            //DataOffsets[sound_index] = OUT_ABK_FILE.Position;
                            //OUT_ABK_Writer.Write((uint)0);
                            if (sound_index == IN_sound_num)
                                // write in bnk's end
                                OUT_ABK_Writer.Write(Program.byte_swap((uint)(ABK_sfxbanksize + 16)));
                            else
                                OUT_ABK_Writer.Write(Program.byte_swap((uint)(value + 16)));
                            break;
                        case KEY.DataStart2:
                            Console.WriteLine("Unsupported stereo split mode");
                            break;
                        default:
                            if (custom_value)
                            {
                                const int num_samples_drop = 0;
                                switch (key)
                                {
                                    case KEY.SampleRate:
                                        {
                                            is_sample_rate_written = true;
                                            OUT_ABK_Writer.Write((byte)4);
                                            OUT_ABK_Writer.Write(Program.byte_swap((uint)Program.samplerate));
                                            break;
                                        }
                                    case KEY.NumSamples:
                                        {
                                            OUT_ABK_Writer.Write((byte)4);
                                            OUT_ABK_Writer.Write(Program.byte_swap((uint)Program.num_of_samples - num_samples_drop));
                                            break;
                                        }
                                    case KEY.LoopLength:
                                        {
                                            OUT_ABK_Writer.Write((byte)4);
                                            OUT_ABK_Writer.Write(Program.byte_swap((uint)(Program.num_of_samples - num_samples_drop - 1)));
                                            break;
                                        }
                                    default:
                                        break;
                                }
                            }
                            break;
                    }

                }
            }
            if (OUT_ABK_FILE.Position - IN_ABK_FILE.Position > 16)
            {
                throw new Exception("Large files size difference");
            }

            // make diff 16 bytes
            OUT_ABK_FILE.Seek(IN_ABK_FILE.Position - OUT_ABK_FILE.Position + 16, SeekOrigin.Current);

            const int PT_size_diff = 16;

            // copy remain bnk
            int count2 = ABK_sfxbanksize - ((int)IN_ABK_FILE.Position - ABK_sfxbankoffset);
            byte[] buffer2 = new byte[count2];
            IN_ABK_FILE.Read(buffer2, 0, count2);
            OUT_ABK_FILE.Write(buffer2, 0, count2);

            // write data in bnk's end
            Process.Start("sx.exe", "-raw -" + sx_codec + " " + args[1] + " -=" + tmp_file_name).WaitForExit();
            FileStream RAW_FILE = new FileStream(tmp_file_name, FileMode.Open);
            int file_size_aligned = (int)Align((uint)RAW_FILE.Length, 16);
            byte[] RAW_FILE_Data = new byte[file_size_aligned];
            RAW_FILE.Read(RAW_FILE_Data, 0, (int)RAW_FILE.Length);
            RAW_FILE.Close();
            File.Delete(tmp_file_name);
            OUT_ABK_FILE.Write(RAW_FILE_Data, 0, file_size_aligned);
            int total_size_diff = PT_size_diff + file_size_aligned;

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

            IN_ABK_FILE.Seek(IN_sound_num*4, SeekOrigin.Current);
            uint sound_offset = byte_swap(IN_ABK_FILE_Reader.ReadUInt32());

            if (sound_offset == 0xFFFF_FFFF)
            {
                throw new Exception("sound you are trying to replace is empty");
            }

            byte[] pre_buf = new byte[sound_offset - 0xC];

            IN_ABK_FILE.Seek(ABK_sfxbankoffset + 0xC, SeekOrigin.Begin);
            IN_ABK_FILE.Read(pre_buf, 0, pre_buf.Length);
            OUT_ABK_FILE.Write(pre_buf, 0, pre_buf.Length);

            uint data = IN_ABK_FILE_Reader.ReadUInt32();

            byte codec = (byte)data;

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

            if ((flags & 0x20) != 0)
            {
                OUT_ABK_Writer.Write(IN_ABK_FILE_Reader.ReadUInt32());
                if ((flags & 0x40) != 0)
                {
                    OUT_ABK_Writer.Write(IN_ABK_FILE_Reader.ReadUInt32());

                }
            }

            int orig_size = 0; // including block's headres

            for (uint samples_count = 0; samples_count < orig_samples; )
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

            IN_ABK_FILE.Seek(16L, SeekOrigin.Current);
            int IN_ABK_size = IN_ABK_FILE_Reader.ReadInt32();
            IN_ABK_FILE.Seek(8L, SeekOrigin.Current);
            int ABK_sfxbankoffset = IN_ABK_FILE_Reader.ReadInt32();
            int ABK_sfxbanksize = IN_ABK_FILE_Reader.ReadInt32();
            IN_ABK_FILE.Seek(8L, SeekOrigin.Current);
            int ABK_funcfixupoffset = IN_ABK_FILE_Reader.ReadInt32();
            int ABK_staticdatafixupoffset = IN_ABK_FILE_Reader.ReadInt32();
            int ABK_interfaceOffset = IN_ABK_FILE_Reader.ReadInt32();
            IN_ABK_FILE.Seek(0L, SeekOrigin.Begin);
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
