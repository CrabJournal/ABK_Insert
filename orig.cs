// Decompiled with JetBrains decompiler
// Type: ABK_insert.Program
// Assembly: ABK_insert, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 7D5810C2-463D-4C08-AEF1-B4B4A9313386

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ABK_insert
{
    internal class Program
    {
        private static int samplerate;
        private static int channels;
        private static int num_of_samples;

        private static uint swap(uint le)
        {
            return (uint)((int)(le & (uint)byte.MaxValue) * 16777216 + (int)(le >> 8 & (uint)byte.MaxValue) * 65536 + (int)(le >> 16 & (uint)byte.MaxValue) * 256) + (le >> 24 & (uint)byte.MaxValue);
        }

        private static void read_wav(string name)
        {
            BinaryReader binaryReader = new BinaryReader((Stream)new FileStream(name, FileMode.Open));
            if (binaryReader.ReadUInt32() != 1179011410U)
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
                if (binaryReader.ReadUInt32() != 1635017060U)
                    Console.WriteLine("Data not found.");
                else
                    Program.num_of_samples = binaryReader.ReadInt32() / Program.channels / 2;
            }
        }

        private static void Main(string[] args)
        {
            Dictionary<int, string> dictionary = new Dictionary<int, string>();
            dictionary.Add(11, "SplitB");
            dictionary.Add(128, "Split");
            dictionary.Add(130, "Channels");
            dictionary.Add(131, "Compression");
            dictionary.Add(132, "SampleRate");
            dictionary.Add(133, "NumSamples");
            dictionary.Add(134, "LoopOffset");
            dictionary.Add(135, "LoopLength");
            dictionary.Add(136, "DataStart1");
            dictionary.Add(137, "DataStart2");
            dictionary.Add(146, "BytesPerSample");
            dictionary.Add(160, "SplitCompression");
            if (args.Length != 3)
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("ABK_insert [ABK file] [wav file] [sample N to replace]");
            }
            else
            {
                int int32 = Convert.ToInt32(args[2]);
                FileStream fileStream1 = new FileStream(args[0], FileMode.Open);
                BinaryReader binaryReader1 = new BinaryReader((Stream)fileStream1);
                FileStream fileStream2 = new FileStream(Path.GetFileNameWithoutExtension(args[0]) + "m.abk", FileMode.Create);
                BinaryWriter binaryWriter = new BinaryWriter((Stream)fileStream2);
                BinaryReader binaryReader2 = new BinaryReader((Stream)fileStream2);
                Program.read_wav(args[1]);
                Process.Start("sx.exe", "-raw -eaxa_blk " + args[1] + " -=xa.raw").WaitForExit();
                if (binaryReader1.ReadInt32() != 1129005633)
                {
                    Console.WriteLine("ABKC signature not found.");
                }
                else
                {
                    fileStream1.Seek(16L, SeekOrigin.Current);
                    int num1 = binaryReader1.ReadInt32();
                    fileStream1.Seek(8L, SeekOrigin.Current);
                    int count1 = binaryReader1.ReadInt32();
                    int num2 = binaryReader1.ReadInt32();
                    fileStream1.Seek(8L, SeekOrigin.Current);
                    int num3 = binaryReader1.ReadInt32();
                    int num4 = binaryReader1.ReadInt32();
                    int num5 = binaryReader1.ReadInt32();
                    fileStream1.Seek(0L, SeekOrigin.Begin);
                    byte[] buffer1 = new byte[count1];
                    fileStream1.Read(buffer1, 0, count1);
                    fileStream2.Write(buffer1, 0, count1);
                    long position = fileStream1.Position;
                    int num6 = binaryReader1.ReadInt32();
                    if (num6 != 1816874562)
                    {
                        Console.WriteLine("BNKl signature not found.");
                    }
                    else
                    {
                        binaryWriter.Write(num6);
                        short num7 = binaryReader1.ReadInt16();
                        binaryWriter.Write(num7);
                        short num8 = binaryReader1.ReadInt16();
                        binaryWriter.Write(num8);
                        Console.WriteLine(args[0] + " Sounds: " + (object)num8);
                        int num9 = binaryReader1.ReadInt32();
                        binaryWriter.Write(num9);
                        binaryWriter.Write(0);
                        binaryWriter.Write(0);
                        fileStream1.Seek(8L, SeekOrigin.Current);
                        long[] numArray1 = new long[(int)num8];
                        long[] numArray2 = new long[(int)num8];
                        for (int index = 0; index < (int)num8; ++index)
                        {
                            numArray1[index] = fileStream1.Position + (long)binaryReader1.ReadInt32();
                            binaryWriter.Write(0);
                        }
                    label_49:
                        for (int index1 = 0; index1 < (int)num8; ++index1)
                        {
                            int num10 = 1;
                            int num11 = 0;
                            fileStream1.Seek(numArray1[index1], SeekOrigin.Begin);
                            if (binaryReader1.ReadInt32() == 21584)
                            {
                                fileStream2.Seek(fileStream2.Position + 3L & 4294967292L, SeekOrigin.Begin);
                                numArray2[index1] = fileStream2.Position;
                                binaryWriter.Write(21584);
                                long[] numArray3 = new long[2];
                                bool flag1 = false;
                                while (true)
                                {
                                    byte num12;
                                    int num13;
                                    do
                                    {
                                        bool flag2;
                                        do
                                        {
                                            flag2 = false;
                                            num12 = binaryReader1.ReadByte();
                                            if (num12 == byte.MaxValue)
                                            {
                                                if (index1 == int32 && !flag1)
                                                {
                                                    binaryWriter.Write((byte)132);
                                                    binaryWriter.Write((byte)4);
                                                    binaryWriter.Write(Program.swap((uint)Program.samplerate));
                                                }
                                                binaryWriter.Write(num12);
                                                if (numArray3[0] == 0L || num10 <= 1 || num11 == 2)
                                                    goto label_49;
                                                else
                                                    goto label_47;
                                            }
                                            else
                                            {
                                                binaryWriter.Write(num12);
                                                if (num12 == (byte)136)
                                                    flag2 = true;
                                                if ((num12 == (byte)132 || num12 == (byte)133 || num12 == (byte)135) && index1 == int32)
                                                    flag2 = true;
                                            }
                                        }
                                        while (num12 == (byte)252 || num12 == (byte)253 || num12 == (byte)254);
                                        byte num14 = binaryReader1.ReadByte();
                                        if (!flag2)
                                            binaryWriter.Write(num14);
                                        num13 = 0;
                                        for (int index2 = 0; index2 < (int)num14; ++index2)
                                        {
                                            byte num15 = binaryReader1.ReadByte();
                                            num13 = (num13 << 8) + (int)num15;
                                            if (!flag2)
                                                binaryWriter.Write(num15);
                                        }
                                        if (dictionary.ContainsKey((int)num12))
                                        {
                                            string str = " (" + dictionary[(int)num12] + ")";
                                        }
                                        if (num12 == (byte)128)
                                            num11 = num13;
                                        if (num12 == (byte)130)
                                            num10 = num13;
                                        if (flag2)
                                        {
                                            if (num12 == (byte)132)
                                            {
                                                flag1 = true;
                                                binaryWriter.Write((byte)4);
                                                binaryWriter.Write(Program.swap((uint)Program.samplerate));
                                            }
                                            if (num12 == (byte)133)
                                            {
                                                binaryWriter.Write((byte)4);
                                                binaryWriter.Write(Program.swap((uint)Program.num_of_samples));
                                            }
                                            if (num12 == (byte)135)
                                            {
                                                binaryWriter.Write((byte)4);
                                                binaryWriter.Write(Program.swap((uint)(Program.num_of_samples - 1)));
                                            }
                                        }
                                        if (num12 == (byte)136)
                                        {
                                            binaryWriter.Write((byte)4);
                                            if (index1 == int32)
                                                binaryWriter.Write(Program.swap((uint)((ulong)fileStream1.Length - (ulong)count1 + 16UL)));
                                            else
                                                binaryWriter.Write(Program.swap((uint)(num13 + 16)));
                                        }
                                    }
                                    while (num12 != (byte)137);
                                    numArray3[1] = (long)num13;
                                }
                            label_47:
                                Console.WriteLine("Unsupported stereo split mode");
                            }
                        }
                        if (fileStream2.Position - fileStream1.Position > 16L)
                        {
                            Console.WriteLine("Something went wrong.");
                        }
                        else
                        {
                            fileStream2.Seek(16L - fileStream2.Position + fileStream1.Position, SeekOrigin.Current);
                            int count2 = num2 - (int)fileStream1.Position + count1;
                            byte[] buffer2 = new byte[count2];
                            fileStream1.Read(buffer2, 0, count2);
                            fileStream2.Write(buffer2, 0, count2);
                            int count3 = (int)fileStream1.Length - num3;
                            byte[] buffer3 = new byte[count3];
                            fileStream1.Read(buffer3, 0, count3);
                            fileStream2.Write(buffer3, 0, count3);
                            FileStream fileStream3 = new FileStream("xa.raw", FileMode.Open);
                            byte[] buffer4 = new byte[fileStream3.Length];
                            fileStream3.Read(buffer4, 0, (int)fileStream3.Length);
                            fileStream2.Write(buffer4, 0, (int)fileStream3.Length);
                            fileStream2.Seek(20L, SeekOrigin.Begin);
                            binaryWriter.Write(num1 + 16);
                            fileStream2.Seek(36L, SeekOrigin.Begin);
                            binaryWriter.Write(num2 + 16);
                            fileStream2.Seek(48L, SeekOrigin.Begin);
                            binaryWriter.Write(num3 + 16);
                            binaryWriter.Write(num4 + 16);
                            binaryWriter.Write(num5 + 16);
                            fileStream2.Seek(position + 20L, SeekOrigin.Begin);
                            for (int index = 0; index < (int)num8; ++index)
                            {
                                if (numArray2[index] == 0L)
                                    binaryWriter.Write(0);
                                else
                                    binaryWriter.Write((int)(numArray2[index] - fileStream2.Position));
                            }
                            fileStream2.Seek((long)(num5 + 16), SeekOrigin.Begin);
                            int num10 = binaryReader2.ReadInt32();
                            for (int index = 0; index < num10; ++index)
                            {
                                binaryReader2.ReadInt32();
                                int num11 = binaryReader2.ReadInt32();
                                fileStream2.Seek(-4L, SeekOrigin.Current);
                                binaryWriter.Write(num11 + 16);
                                binaryReader2.ReadInt32();
                            }
                            binaryReader1.Close();
                            fileStream1.Close();
                            fileStream3.Close();
                            binaryWriter.Close();
                            fileStream2.Close();
                        }
                    }
                }
            }
        }
    }
}
