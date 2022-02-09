using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ABK_insert
{
    class Program
    {
        private static int samplerate;
        private static int channels;
        private static int num_of_samples;

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
            if (binaryReader.ReadUInt32() != 1179011410U) // "RIFF"
            {
                Console.WriteLine("I need WAV file.");
            }
            else
            {
                int num1 = (int)binaryReader.ReadUInt32();
                int num2 = (int)binaryReader.ReadUInt32();
                int num3 = (int)binaryReader.ReadUInt32();
                switch (binaryReader.ReadUInt32())
                {
                    case 16:
                        if (binaryReader.ReadUInt16() != (ushort)1)
                        {
                            Console.WriteLine("Unknown WAV format.");
                            return;
                        }
                        Program.channels = (int)binaryReader.ReadUInt16();
                        Program.samplerate = binaryReader.ReadInt32();
                        int num4 = (int)binaryReader.ReadUInt32();
                        int num5 = (int)binaryReader.ReadUInt16();
                        if (binaryReader.ReadUInt16() != (ushort)16)
                        {
                            Console.WriteLine("Only 16 bit WAV supported.");
                            return;
                        }
                        break;
                    case 40:
                        if (binaryReader.ReadUInt16() != (ushort)65534)
                        {
                            Console.WriteLine("Unknown WAV format.");
                            return;
                        }
                        Program.channels = (int)binaryReader.ReadUInt16();
                        Program.samplerate = binaryReader.ReadInt32();
                        int num6 = (int)binaryReader.ReadUInt32();
                        int num7 = (int)binaryReader.ReadUInt16();
                        if (binaryReader.ReadUInt16() != (ushort)16)
                        {
                            Console.WriteLine("Only 16 bit WAV supported.");
                            return;
                        }
                        int num8 = (int)binaryReader.ReadUInt32();
                        int num9 = (int)binaryReader.ReadUInt32();
                        int num10 = (int)binaryReader.ReadUInt32();
                        int num11 = (int)binaryReader.ReadUInt32();
                        int num12 = (int)binaryReader.ReadUInt32();
                        int num13 = (int)binaryReader.ReadUInt32();
                        break;
                    default:
                        Console.WriteLine("Unknown WAV format.");
                        return;
                }
                uint data = binaryReader.ReadUInt32();
                if (data != 0x61746164U)
                    Console.WriteLine("Data not found.");
                else
                    Program.num_of_samples = binaryReader.ReadInt32() / Program.channels / 2;
            }
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
        private readonly string[] codec_names = { "ADPCM EA-XA R2", "ADPCM EA-XAS", "PCM S16LE" };
        private static void Main(string[] args)
        {
            const string tmp_file_name = "xa.raw";
            Dictionary<KEY, string> dictionary = new Dictionary<KEY, string>();
            dictionary.Add(KEY.SplitB, "SplitB");
            dictionary.Add(KEY.Split, "Split");
            dictionary.Add(KEY.Channels, "Channels");
            dictionary.Add(KEY.Compression, "Compression");
            dictionary.Add(KEY.SampleRate, "SampleRate");
            dictionary.Add(KEY.NumSamples, "NumSamples");
            dictionary.Add(KEY.LoopOffset, "LoopOffset");
            dictionary.Add(KEY.LoopLength, "LoopLength");
            dictionary.Add(KEY.DataStart1, "DataStart1");
            dictionary.Add(KEY.DataStart2, "DataStart2");
            dictionary.Add(KEY.BytesPerSample, "BytesPerSample");
            dictionary.Add(KEY.SplitCompression, "SplitCompression");

            if (args.Length != 3)
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("ABK_insert [ABK file] [wav file] [sample N to replace]");
                return;
            }

            int IN_sound_num = Convert.ToInt32(args[2]);
            FileStream IN_ABK_FILE = new FileStream(args[0], FileMode.Open);
            BinaryReader IN_ABK_FILE_Reader = new BinaryReader((Stream)IN_ABK_FILE);
            FileStream OUT_ABK_FILE = new FileStream(Path.GetFileNameWithoutExtension(args[0]) + "m.abk", FileMode.Create);
            BinaryWriter OUT_ABK_Writer = new BinaryWriter((Stream)OUT_ABK_FILE);
            BinaryReader OUT_ABK_Reader = new BinaryReader((Stream)OUT_ABK_FILE);
            Program.read_wav(args[1]);
            if (IN_ABK_FILE_Reader.ReadInt32() != 0x434B4241)
            {
                Console.WriteLine("ABKC signature not found.");
                return;
            }

            string sx_codec = "eaxa_blk"; // also known as ADPCM EA-XA R2(3)

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
            long BNKl_offset = IN_ABK_FILE.Position;
            int BNKl_signature = IN_ABK_FILE_Reader.ReadInt32();
            if (BNKl_signature != 0x6C4B4E42)
            {
                Console.WriteLine("BNKl signature not found.");
                return;
            }

            OUT_ABK_Writer.Write(BNKl_signature);
            short unk = IN_ABK_FILE_Reader.ReadInt16();
            OUT_ABK_Writer.Write(unk);
            short num_sounds = IN_ABK_FILE_Reader.ReadInt16();
            OUT_ABK_Writer.Write(num_sounds);
            Console.WriteLine(args[0] + " Sounds: " + (object)num_sounds);
            int bnk_size = IN_ABK_FILE_Reader.ReadInt32(); // recalc later
            OUT_ABK_Writer.Write(bnk_size);
            OUT_ABK_Writer.Write(0);
            OUT_ABK_Writer.Write(0);
            IN_ABK_FILE.Seek(8L, SeekOrigin.Current);
            long[] SoundsOffsets = new long[(int)num_sounds];
            long[] IN_PT_offsets = new long[(int)num_sounds];
            for (int index = 0; index < (int)num_sounds; ++index)
            {
                SoundsOffsets[index] = IN_ABK_FILE.Position + (long)IN_ABK_FILE_Reader.ReadInt32();
                OUT_ABK_Writer.Write(0);
            }

            // write metadata
            for (int sound_index = 0; sound_index < (int)num_sounds; ++sound_index)
            {

                int Channels = 1;
                int split = 0;
                IN_ABK_FILE.Seek(SoundsOffsets[sound_index], SeekOrigin.Begin);
                if (IN_ABK_FILE_Reader.ReadInt32() != 0x5450) // "PT"
                    continue;

                OUT_ABK_FILE.Seek(Align((uint)OUT_ABK_FILE.Position, 4), SeekOrigin.Begin);
                IN_PT_offsets[sound_index] = OUT_ABK_FILE.Position;
                OUT_ABK_Writer.Write(0x5450); // "PT"
                bool is_sample_rate_written = false;
                // proc keys
                while (true)
                {
                    KEY key;
                    int value;

                    bool custom_value;
                repeat:
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
                        goto repeat;

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
                    /*if (dictionary.ContainsKey(key))
                    {
                        string str = " (" + dictionary[key] + ")";
                    }*/
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
                                const int num_samples_drop = 64;
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
            if (OUT_ABK_FILE.Position - IN_ABK_FILE.Position > 16L)
            {
                Console.WriteLine("Something went wrong.");
                return;
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


            // write offsets to PTs
            OUT_ABK_FILE.Seek(BNKl_offset + 0x14, SeekOrigin.Begin);
            for (int index = 0; index < num_sounds; ++index)
            {
                if (IN_PT_offsets[index] == 0)
                    OUT_ABK_Writer.Write(0);
                else
                    OUT_ABK_Writer.Write((int)(IN_PT_offsets[index] - OUT_ABK_FILE.Position));
            }

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

            // change bnk size
            OUT_ABK_FILE.Seek(ABK_sfxbankoffset + 8, SeekOrigin.Begin);
            OUT_ABK_Writer.Write(bnk_size + total_size_diff);

            IN_ABK_FILE_Reader.Close();
            IN_ABK_FILE.Close();
            RAW_FILE.Close();
            OUT_ABK_Writer.Close();
            OUT_ABK_FILE.Close();
        }
    }
}