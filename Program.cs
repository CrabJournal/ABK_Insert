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
        private static void Main(string[] args)
        {
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
            }
            else
            {
                int IN_sound_num = Convert.ToInt32(args[2]);
                FileStream IN_ABK_FILE = new FileStream(args[0], FileMode.Open);
                BinaryReader IN_ABK_FILE_Reader = new BinaryReader((Stream)IN_ABK_FILE);
                FileStream OUT_ABK_FILE = new FileStream(Path.GetFileNameWithoutExtension(args[0]) + "m.abk", FileMode.Create);
                BinaryWriter OUT_ABK_Writer = new BinaryWriter((Stream)OUT_ABK_FILE);
                BinaryReader OUT_ABK_Reader = new BinaryReader((Stream)OUT_ABK_FILE);
                Program.read_wav(args[1]);
                Process.Start("sx.exe", "-raw -eaxa_blk " + args[1] + " -=xa.raw").WaitForExit();
                if (IN_ABK_FILE_Reader.ReadInt32() != 1129005633)
                {
                    Console.WriteLine("ABKC signature not found.");
                }
                else
                {
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
                    }
                    else
                    {
                        OUT_ABK_Writer.Write(BNKl_signature);
                        short unk = IN_ABK_FILE_Reader.ReadInt16();
                        OUT_ABK_Writer.Write(unk);
                        short num_sounds = IN_ABK_FILE_Reader.ReadInt16();
                        OUT_ABK_Writer.Write(num_sounds);
                        Console.WriteLine(args[0] + " Sounds: " + (object)num_sounds);
                        int num9 = IN_ABK_FILE_Reader.ReadInt32();
                        OUT_ABK_Writer.Write(num9);
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
                        // someting wrong, I can feel it
                        //start_again:
                        for (int sound_index = 0; sound_index < (int)num_sounds; ++sound_index)
                        {
                        
                            int Channels = 1;
                            int split = 0;
                            IN_ABK_FILE.Seek(SoundsOffsets[sound_index], SeekOrigin.Begin);
                            if (IN_ABK_FILE_Reader.ReadInt32() == 21584) // "PT"
                            {
                                OUT_ABK_FILE.Seek(OUT_ABK_FILE.Position + 3L & 0xFFFFFFFCL/*align by 4*/, SeekOrigin.Begin);
                                IN_PT_offsets[sound_index] = OUT_ABK_FILE.Position;
                                OUT_ABK_Writer.Write(21584); // "PT"
                                long[] numArray3 = new long[2];
                                bool flag1 = false;
                                // proc keys
                                while (true)
                                {
                                    KEY key;
                                    int value;
                                    do
                                    {
                                        bool stop_write;
                                        do
                                        {
                                            stop_write = false;
                                            key = (KEY)IN_ABK_FILE_Reader.ReadByte();
                                            if (key == KEY.End)
                                            {
                                                if (sound_index == IN_sound_num && !flag1)
                                                {
                                                    OUT_ABK_Writer.Write((byte)KEY.SampleRate);
                                                    OUT_ABK_Writer.Write((byte)4);
                                                    OUT_ABK_Writer.Write(Program.byte_swap((uint)Program.samplerate));
                                                }
                                                OUT_ABK_Writer.Write((byte)key); // KEY.End
                                                if (numArray3[0] == 0 || Channels <= 1 || split == 2)
                                                    goto label_continue;
                                                else
                                                    goto label_unsupported;
                                            }
                                            else
                                            {
                                                OUT_ABK_Writer.Write((byte)key);
                                                if (key == KEY.DataStart1)
                                                    stop_write = true;
                                                if ((key == KEY.SampleRate || key == KEY.NumSamples || key == KEY.LoopLength) && sound_index == IN_sound_num)
                                                    stop_write = true;
                                            }
                                        } while (key == KEY.Skip1 || key == KEY.Skip2 || key == KEY.Skip3);

                                        byte num_bytes = IN_ABK_FILE_Reader.ReadByte();
                                        if (!stop_write)
                                            OUT_ABK_Writer.Write(num_bytes);
                                        value = 0; // 1 - 4 byte(s)
                                        for (int byte_index = 0; byte_index < (int)num_bytes; ++byte_index)
                                        {
                                            byte byte_of_data = IN_ABK_FILE_Reader.ReadByte();
                                            value = (value << 8) + (int)byte_of_data;
                                            if (!stop_write)
                                                OUT_ABK_Writer.Write(byte_of_data);
                                        }
                                        if (dictionary.ContainsKey(key))
                                        {
                                            string str = " (" + dictionary[key] + ")";
                                        }
                                        if (key == KEY.Split)
                                            split = value;
                                        if (key == KEY.Channels)
                                            Channels = value;
                                        if (stop_write)
                                        {
                                            if (key == KEY.SampleRate)
                                            {
                                                flag1 = true;
                                                OUT_ABK_Writer.Write((byte)4);
                                                OUT_ABK_Writer.Write(Program.byte_swap((uint)Program.samplerate));
                                            }
                                            if (key == KEY.NumSamples)
                                            {
                                                OUT_ABK_Writer.Write((byte)4);
                                                OUT_ABK_Writer.Write(Program.byte_swap((uint)Program.num_of_samples - 64));
                                            }
                                            if (key == KEY.LoopLength)
                                            {
                                                OUT_ABK_Writer.Write((byte)4);
                                                OUT_ABK_Writer.Write(Program.byte_swap((uint)(Program.num_of_samples - 200)));
                                            }
                                        }
                                        if (key == KEY.DataStart1)
                                        {
                                            OUT_ABK_Writer.Write((byte)4);
                                            if (sound_index == IN_sound_num)
                                                // write in bnk's end
                                                OUT_ABK_Writer.Write( Program.byte_swap((uint)(ABK_sfxbanksize + 16)) );
                                            else
                                                OUT_ABK_Writer.Write(Program.byte_swap((uint)(value + 16)));
                                        }
                                    }
                                    while (key != KEY.LoopLength);
                                    numArray3[1] = (long)value;
                                }
                            label_unsupported:
                                Console.WriteLine("Unsupported stereo split mode");
                            }
                        label_continue:
                            Channels = 1;
                        }
                        if (OUT_ABK_FILE.Position - IN_ABK_FILE.Position > 16L)
                        {
                            Console.WriteLine("Something went wrong.");
                        }
                        else
                        {
                            // make diff 16 bytes
                            OUT_ABK_FILE.Seek(IN_ABK_FILE.Position - OUT_ABK_FILE.Position + 16, SeekOrigin.Current);

                            const int PT_size_diff = 16;

                            // copy remain bnk
                            int count2 = ABK_sfxbanksize - ((int)IN_ABK_FILE.Position - ABK_sfxbankoffset);
                            byte[] buffer2 = new byte[count2];
                            IN_ABK_FILE.Read(buffer2, 0, count2);
                            OUT_ABK_FILE.Write(buffer2, 0, count2);

                            // write data in bnk's end
                            FileStream RAW_FILE = new FileStream("xa.raw", FileMode.Open);
                            byte[] RAW_FILE_Data = new byte[RAW_FILE.Length];
                            RAW_FILE.Read(RAW_FILE_Data, 0, (int)RAW_FILE.Length);
                            OUT_ABK_FILE.Write(RAW_FILE_Data, 0, (int)RAW_FILE.Length);
                            int total_size_diff = PT_size_diff + (int)RAW_FILE.Length;

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
                            int old_bnk_size = OUT_ABK_Reader.ReadInt32();
                            OUT_ABK_FILE.Seek(-4, SeekOrigin.Current);
                            OUT_ABK_Writer.Write(old_bnk_size + total_size_diff);

                            IN_ABK_FILE_Reader.Close();
                            IN_ABK_FILE.Close();
                            RAW_FILE.Close();
                            OUT_ABK_Writer.Close();
                            OUT_ABK_FILE.Close();
                        }
                    }
                }
            }
        }
    }
}